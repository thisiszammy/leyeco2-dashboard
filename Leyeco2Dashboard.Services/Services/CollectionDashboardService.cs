using System.Data.Common;
using Leyeco2Dashboard.Data.Enums;
using Leyeco2Dashboard.Data.Interfaces.Legacy;
using Leyeco2Dashboard.Data.Reports;
using Leyeco2Dashboard.Services.Caching;
using Leyeco2Dashboard.Services.Configuration;
using Leyeco2Dashboard.Services.DTOs;
using Leyeco2Dashboard.Services.Exceptions;
using Leyeco2Dashboard.Services.Interfaces;
using Leyeco2Dashboard.Services.Validators;
using Microsoft.Data.SqlClient;

namespace Leyeco2Dashboard.Services.Services
{
    /// <summary>
    /// The overall dashboard, summed from the legacy collections ledger, the source of truth for what was collected
    /// whichever channel took it. Four short reads per period (the slices, the collectors, the period's totals by town
    /// and the previous period's), kept and shared between viewers by <see cref="ReportCache"/>; everything else is
    /// summed here from the slices. Read-only: no audit row, no transaction.
    /// </summary>
    public class CollectionDashboardService : ICollectionDashboardService
    {
        private const string NotConfiguredMessage =
            "The collections ledger isn't connected yet. Set ConnectionStrings__LegacyConnection in the API's .env, then restart the API.";

        private const string UnreachableMessage =
            "The collections ledger can't be reached right now. Try again in a minute; if it keeps failing, check the server ConnectionStrings__LegacyConnection names.";

        private const string TimeoutMessage =
            "The collections ledger took too long to answer. Try a shorter period, or try again in a minute.";

        private readonly ICollectionReportRepository _repository;
        private readonly ReportCache _cache;
        private readonly ReportSourceOptions _sources;
        private readonly IDateTimeService _dateTime;

        public CollectionDashboardService(
            ICollectionReportRepository repository,
            ReportCache cache,
            ReportSourceOptions sources,
            IDateTimeService dateTime)
        {
            _repository = repository;
            _cache = cache;
            _sources = sources;
            _dateTime = dateTime;
        }

        public Task<CollectionDashboardDTO> GetAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
        {
            var period = DashboardPeriod.Of(from, to, _dateTime.Now());

            if (!_sources.LegacyConfigured)
                throw new ReportSourceException(NotConfiguredMessage);

            return _cache.GetOrReadAsync("collections:" + period.Key, period.KeepFor, () => ReadAsync(period), cancellationToken);
        }

        private async Task<CollectionDashboardDTO> ReadAsync(DashboardPeriod period)
        {
            try
            {
                // One after another, not side by side: each is quick on the dashboard's index, and the ledger's
                // server is the tellers' first.
                var slices = await _repository.GetSlicesAsync(period.From, period.Until);
                var collectors = await _repository.GetCollectorsAsync(period.From, period.Until);
                var totals = await _repository.GetTotalsByTownAsync(period.From, period.Until);
                var previous = await _repository.GetTotalsByTownAsync(period.PreviousFrom, period.PreviousUntil);

                return Build(period, slices, collectors, totals, previous);
            }
            catch (SqlException ex) when (ex.Number == -2)
            {
                throw new ReportSourceException(TimeoutMessage, ex);
            }
            catch (DbException ex)
            {
                throw new ReportSourceException(UnreachableMessage, ex);
            }
        }

        private CollectionDashboardDTO Build(
            DashboardPeriod period,
            List<CollectionSliceRow> slices,
            List<CollectorRow> collectors,
            List<CollectionTotalsRow> totals,
            List<CollectionTotalsRow> previous)
        {
            var channels = CollectionChannels.All;
            var slot = channels.Select((channel, index) => (channel, index)).ToDictionary(x => (int)x.channel, x => x.index);
            int SlotOf(int channel) => slot.TryGetValue(channel, out var index) ? index : 0;

            var daily = Enumerable.Range(0, period.Days)
                .Select(offset => new CollectionPointDTO
                {
                    Date = period.From.AddDays(offset),
                    Amounts = new decimal[channels.Count],
                    Bills = new int[channels.Count],
                })
                .ToList();

            var hourly = Enumerable.Range(0, 24)
                .Select(hour => new CollectionPointDTO
                {
                    Hour = hour,
                    Amounts = new decimal[channels.Count],
                    Bills = new int[channels.Count],
                })
                .ToList();

            var untimed = 0;
            foreach (var slice in slices)
            {
                var index = SlotOf(slice.Channel);

                var dayIndex = (int)(slice.Day.Date - period.From).TotalDays;
                if (dayIndex >= 0 && dayIndex < daily.Count)
                {
                    daily[dayIndex].Amounts[index] += slice.Amount;
                    daily[dayIndex].Bills[index] += slice.Bills;
                }

                if (slice.Hour is int hour and >= 0 and < 24)
                {
                    hourly[hour].Amounts[index] += slice.Amount;
                    hourly[hour].Bills[index] += slice.Bills;
                }
                else
                {
                    untimed += slice.Bills;
                }
            }

            var collectorRows = collectors
                .Select(row => new CollectorDTO
                {
                    Username = row.Username.Trim(),
                    CollectorId = string.IsNullOrWhiteSpace(row.CollectorId) ? null : row.CollectorId.Trim(),
                    Channel = ((CollectionChannelEnum)row.Channel).Key(),
                    Bills = row.Bills,
                    Amount = row.Amount,
                    LastPostedOn = row.LastPostedOn,
                })
                .OrderByDescending(row => row.Amount)
                .ThenBy(row => row.Username, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var channelRows = channels
                .Select(channel =>
                {
                    var own = slices.Where(slice => slice.Channel == (int)channel).ToList();
                    return new CollectionChannelDTO
                    {
                        Key = channel.Key(),
                        Label = channel.Label(),
                        Bills = own.Sum(slice => slice.Bills),
                        Amount = own.Sum(slice => slice.Amount),
                        Surcharge = own.Sum(slice => slice.Surcharge),
                        LastPostedOn = collectors.Where(row => row.Channel == (int)channel).Max(row => row.LastPostedOn),
                    };
                })
                .ToList();

            var townRows = CollectionTowns.All
                .Select(town =>
                {
                    var own = slices.Where(slice => slice.Town == (int)town).ToList();
                    var amounts = new decimal[channels.Count];
                    var bills = new int[channels.Count];
                    foreach (var slice in own)
                    {
                        amounts[SlotOf(slice.Channel)] += slice.Amount;
                        bills[SlotOf(slice.Channel)] += slice.Bills;
                    }

                    var current = totals.Where(row => row.Town == (int)town).ToList();
                    var before = previous.Where(row => row.Town == (int)town).ToList();
                    return new CollectionTownDTO
                    {
                        Key = town.Key(),
                        Label = town.Label(),
                        Code = town.Code(),
                        Totals = new CollectionFiguresDTO
                        {
                            Bills = current.Sum(row => row.Bills),
                            Accounts = current.Sum(row => row.Accounts),
                            Amount = current.Sum(row => row.Amount),
                            Surcharge = own.Sum(slice => slice.Surcharge),
                            SurchargedBills = own.Sum(slice => slice.SurchargedBills),
                        },
                        Previous = new CollectionFiguresDTO
                        {
                            Bills = before.Sum(row => row.Bills),
                            Accounts = before.Sum(row => row.Accounts),
                            Amount = before.Sum(row => row.Amount),
                        },
                        Amounts = amounts,
                        Bills = bills,
                    };
                })
                .ToList();

            return new CollectionDashboardDTO
            {
                From = period.From,
                To = period.To,
                Days = period.Days,
                PreviousFrom = period.PreviousFrom,
                PreviousTo = period.PreviousTo,
                IncludesToday = period.IncludesToday,
                GeneratedOn = _dateTime.Now(),
                Totals = new CollectionFiguresDTO
                {
                    Bills = totals.Sum(row => row.Bills),
                    Accounts = totals.Sum(row => row.Accounts),
                    Amount = totals.Sum(row => row.Amount),
                    Surcharge = slices.Sum(slice => slice.Surcharge),
                    SurchargedBills = slices.Sum(slice => slice.SurchargedBills),
                },
                Previous = new CollectionFiguresDTO
                {
                    Bills = previous.Sum(row => row.Bills),
                    Accounts = previous.Sum(row => row.Accounts),
                    Amount = previous.Sum(row => row.Amount),
                },
                Channels = channelRows,
                Towns = townRows,
                Daily = daily,
                Hourly = hourly,
                UntimedBills = untimed,
                Collectors = collectorRows,
            };
        }
    }
}
