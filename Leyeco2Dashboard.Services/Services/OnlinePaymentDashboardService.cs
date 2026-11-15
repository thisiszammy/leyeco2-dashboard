using System.Data.Common;
using Leyeco2Dashboard.Data.Interfaces.Leyeco2OTF;
using Leyeco2Dashboard.Data.Reports;
using Leyeco2Dashboard.Services.Caching;
using Leyeco2Dashboard.Services.Configuration;
using Leyeco2Dashboard.Services.DTOs;
using Leyeco2Dashboard.Services.Exceptions;
using Leyeco2Dashboard.Services.Interfaces;
using Leyeco2Dashboard.Services.Validators;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Leyeco2Dashboard.Services.Services
{
    /// <summary>
    /// The Online Payments dashboard, summed from the online payment facility's transactions: what was paid
    /// online, through which provider, how each attempt ended, and how many bills each payment settled. Read like
    /// the overall dashboard (<see cref="CollectionDashboardService"/>): a few short reads per period, kept and
    /// shared by <see cref="ReportCache"/>, nothing written.
    /// </summary>
    public class OnlinePaymentDashboardService : IOnlinePaymentDashboardService
    {
        private const string NotConfiguredMessage =
            "The online payment facility's database isn't connected yet. Set ConnectionStrings__OnlinePaymentsConnection in the API's .env, then restart the API.";

        private const string UnreachableMessage =
            "The online payment facility's database can't be reached right now. Try again in a minute; if it keeps failing, check the server ConnectionStrings__OnlinePaymentsConnection names.";

        private const string TimeoutMessage =
            "The online payment facility's database took too long to answer. Try a shorter period, or try again in a minute.";

        // The facility's statuses (its PaymentStatusEnum), folded into what a supervisor acts on: a failure and a
        // cancellation both mean the bill is still unpaid.
        private static readonly (string Key, string Label, string[] Statuses)[] OutcomeOrder =
        [
            ("successful", "Successful", ["SUCCESSFUL"]),
            ("pending", "Pending", ["PENDING"]),
            ("failed", "Failed or cancelled", ["FAILED", "CANCELLED"]),
            ("dropped", "Dropped", ["DROPPED"]),
        ];

        private const string OtherOutcome = "other";

        private readonly IOnlinePaymentReportRepository _repository;
        private readonly ReportCache _cache;
        private readonly ReportSourceOptions _sources;
        private readonly IDateTimeService _dateTime;
        private readonly ILogger<OnlinePaymentDashboardService> _logger;

        public OnlinePaymentDashboardService(
            IOnlinePaymentReportRepository repository,
            ReportCache cache,
            ReportSourceOptions sources,
            IDateTimeService dateTime,
            ILogger<OnlinePaymentDashboardService> logger)
        {
            _repository = repository;
            _cache = cache;
            _sources = sources;
            _dateTime = dateTime;
            _logger = logger;
        }

        public Task<OnlinePaymentDashboardDTO> GetAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
        {
            var period = DashboardPeriod.Of(from, to, _dateTime.Now());

            if (!_sources.OnlinePaymentsConfigured)
                throw new ReportSourceException(NotConfiguredMessage);

            return _cache.GetOrReadAsync("online-payments:" + period.Key, period.KeepFor, () => ReadAsync(period), cancellationToken);
        }

        private async Task<OnlinePaymentDashboardDTO> ReadAsync(DashboardPeriod period)
        {
            try
            {
                var slices = await _repository.GetSlicesAsync(period.From, period.Until);
                var previous = await _repository.GetSuccessfulTotalsAsync(period.PreviousFrom, period.PreviousUntil);
                var names = await PaymentProviderNames.ReadAsync(_repository, _logger);

                return Build(period, slices, previous, names);
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

        private OnlinePaymentDashboardDTO Build(
            DashboardPeriod period,
            List<OnlinePaymentSliceRow> slices,
            OnlinePaymentTotalsRow previous,
            Dictionary<string, string> names)
        {
            var outcomeKeys = OutcomeOrder.Select(outcome => outcome.Key).ToList();
            if (slices.Any(slice => OutcomeOf(slice.PaymentStatus) == OtherOutcome))
                outcomeKeys.Add(OtherOutcome);

            var providerKeys = slices
                .Select(slice => slice.ProviderId.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(PaymentProviderNames.Order)
                .ThenBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var daily = Enumerable.Range(0, period.Days)
                .Select(offset => NewPoint(period.From.AddDays(offset), null, providerKeys.Count, outcomeKeys.Count))
                .ToList();
            var hourly = Enumerable.Range(0, 24)
                .Select(hour => NewPoint(null, hour, providerKeys.Count, outcomeKeys.Count))
                .ToList();

            var outcomes = outcomeKeys.Select(key => new OnlinePaymentOutcomeDTO
            {
                Key = key,
                Label = key == OtherOutcome ? "Other" : OutcomeOrder.First(outcome => outcome.Key == key).Label,
            }).ToList();

            var providers = providerKeys.Select(key => new OnlinePaymentProviderDTO
            {
                Key = key,
                Label = PaymentProviderNames.Label(key, names),
            }).ToList();

            var bands = Enumerable.Range(1, 4).Select(bills => new OnlinePaymentBillBandDTO { Bills = bills }).ToList();
            var successful = new OnlinePaymentFiguresDTO();
            var attempted = 0;

            foreach (var slice in slices)
            {
                var outcomeIndex = outcomeKeys.IndexOf(OutcomeOf(slice.PaymentStatus));
                var providerIndex = providerKeys.FindIndex(key => string.Equals(key, slice.ProviderId.Trim(), StringComparison.OrdinalIgnoreCase));
                var isSuccessful = outcomeIndex == 0;
                var dayIndex = (int)(slice.Day.Date - period.From).TotalDays;
                var day = dayIndex >= 0 && dayIndex < daily.Count ? daily[dayIndex] : null;
                var hour = slice.Hour is >= 0 and < 24 ? hourly[slice.Hour] : null;

                attempted += slice.Transactions;

                var outcome = outcomes[outcomeIndex];
                outcome.Transactions += slice.Transactions;
                outcome.Bills += slice.Bills;
                outcome.Amount += slice.Amount;

                providers[providerIndex].Attempted += slice.Transactions;

                if (day is not null) day.Transactions[outcomeIndex] += slice.Transactions;
                if (hour is not null) hour.Transactions[outcomeIndex] += slice.Transactions;

                if (!isSuccessful)
                    continue;

                successful.Transactions += slice.Transactions;
                successful.Bills += slice.Bills;
                successful.Amount += slice.Amount;
                successful.Surcharge += slice.Surcharge;

                var provider = providers[providerIndex];
                provider.Transactions += slice.Transactions;
                provider.Bills += slice.Bills;
                provider.Amount += slice.Amount;

                if (day is not null) day.Amounts[providerIndex] += slice.Amount;
                if (hour is not null) hour.Amounts[providerIndex] += slice.Amount;

                var band = bands[Math.Clamp(slice.BillBand, 1, 4) - 1];
                band.Transactions += slice.Transactions;
                band.Amount += slice.Amount;
            }

            return new OnlinePaymentDashboardDTO
            {
                From = period.From,
                To = period.To,
                Days = period.Days,
                PreviousFrom = period.PreviousFrom,
                PreviousTo = period.PreviousTo,
                IncludesToday = period.IncludesToday,
                GeneratedOn = _dateTime.Now(),
                Successful = successful,
                PreviousSuccessful = new OnlinePaymentFiguresDTO
                {
                    Transactions = previous.Transactions,
                    Bills = previous.Bills,
                    Amount = previous.Amount,
                },
                Attempted = attempted,
                Outcomes = outcomes,
                Providers = providers,
                Daily = daily,
                Hourly = hourly,
                BillsPerTransaction = bands,
            };
        }

        private static OnlinePaymentPointDTO NewPoint(DateTime? date, int? hour, int providers, int outcomes) => new()
        {
            Date = date,
            Hour = hour,
            Amounts = new decimal[providers],
            Transactions = new int[outcomes],
        };

        private static string OutcomeOf(string status)
        {
            var trimmed = status.Trim();
            foreach (var (key, _, statuses) in OutcomeOrder)
            {
                if (statuses.Any(candidate => string.Equals(candidate, trimmed, StringComparison.OrdinalIgnoreCase)))
                    return key;
            }

            return OtherOutcome;
        }
    }
}
