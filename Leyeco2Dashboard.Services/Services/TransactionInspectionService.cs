using System.Data.Common;
using Leyeco2Dashboard.Data.Interfaces.Legacy;
using Leyeco2Dashboard.Data.Interfaces.Leyeco2OTF;
using Leyeco2Dashboard.Data.Reports;
using Leyeco2Dashboard.Services.Caching;
using Leyeco2Dashboard.Services.Configuration;
using Leyeco2Dashboard.Services.DTOs;
using Leyeco2Dashboard.Services.Exceptions;
using Leyeco2Dashboard.Services.Inspection;
using Leyeco2Dashboard.Services.Interfaces;
using Leyeco2Dashboard.Services.Validators;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Leyeco2Dashboard.Services.Services
{
    /// <summary>
    /// The Transaction Inspector. A period is read once from both sources and kept (<see cref="ReportCache"/>, as the
    /// dashboards keep theirs): the facility's bills and its queue rows (two short reads of Leyeco2OTF), then the
    /// ledger's row for each of those bills by its key and the consumers' names (a few batches of seeks into the
    /// cashiering database). Every filter, view, page and printout of the period is then cut from that one read in
    /// memory, so paging through a month, or exporting it, costs the tellers' server nothing more. Nothing is written.
    /// </summary>
    public partial class TransactionInspectionService : ITransactionInspectionService
    {
        /// <summary>A month at most: the inspector reads bills, not sums, and the ledger's are read one seek a bill.</summary>
        public const int MaxDays = 31;

        public const int DefaultPageSize = 20;

        private const string FacilityNotConfigured =
            "The online payment facility's database isn't connected yet. Set ConnectionStrings__OnlinePaymentsConnection in the API's .env, then restart the API.";

        private const string LedgerNotConfigured =
            "The collections ledger isn't connected yet, and the inspector follows every payment into it. Set ConnectionStrings__LegacyConnection in the API's .env, then restart the API.";

        private const string FacilityUnreachable =
            "The online payment facility's database can't be reached right now. Try again in a minute; if it keeps failing, check the server ConnectionStrings__OnlinePaymentsConnection names.";

        private const string LedgerUnreachable =
            "The collections ledger can't be reached right now. Try again in a minute; if it keeps failing, check the server ConnectionStrings__LegacyConnection names.";

        private const string FacilityTimeout =
            "The online payment facility's database took too long to answer. Try a shorter period, or try again in a minute.";

        private const string LedgerTimeout =
            "The collections ledger took too long to answer. Try a shorter period, or try again in a minute.";

        private readonly IOnlinePaymentInspectionRepository _facility;
        private readonly IOnlinePaymentReportRepository _providers;
        private readonly ILedgerInspectionRepository _ledger;
        private readonly ReportCache _cache;
        private readonly ReportSourceOptions _sources;
        private readonly IDateTimeService _dateTime;
        private readonly ILogger<TransactionInspectionService> _logger;

        public TransactionInspectionService(
            IOnlinePaymentInspectionRepository facility,
            IOnlinePaymentReportRepository providers,
            ILedgerInspectionRepository ledger,
            ReportCache cache,
            ReportSourceOptions sources,
            IDateTimeService dateTime,
            ILogger<TransactionInspectionService> logger)
        {
            _facility = facility;
            _providers = providers;
            _ledger = ledger;
            _cache = cache;
            _sources = sources;
            _dateTime = dateTime;
            _logger = logger;
        }

        public async Task<TransactionInspectionDTO> GetAsync(
            DateTime? from, DateTime? to, string? view, string? provider, string? search, int page, int pageSize,
            CancellationToken cancellationToken = default)
        {
            var period = Period(from, to);
            var inspection = await InspectionAsync(period, cancellationToken);
            var cut = Cut(inspection, view, provider, search);

            var size = pageSize <= 0 ? DefaultPageSize : Math.Clamp(pageSize, 1, 100);
            var totalPages = Math.Max(1, (int)Math.Ceiling(cut.Listed.Count / (double)size));
            var current = Math.Clamp(page <= 0 ? 1 : page, 1, totalPages);

            return new TransactionInspectionDTO
            {
                From = period.From,
                To = period.To,
                Days = period.Days,
                IncludesToday = period.IncludesToday,
                GeneratedOn = inspection.ReadOn,
                View = cut.View,
                Provider = cut.Provider,
                Search = cut.Search,
                Providers = inspection.Providers,
                Summary = cut.Summary,
                Page = new InspectionPageDTO
                {
                    CurrentPage = current,
                    PageSize = size,
                    TotalItems = cut.Listed.Count,
                    TotalPages = totalPages,
                    Items = cut.Listed.Skip((current - 1) * size).Take(size).ToList(),
                },
            };
        }

        private DashboardPeriod Period(DateTime? from, DateTime? to)
        {
            var period = DashboardPeriod.Of(from, to, _dateTime.Now(), MaxDays, "The inspector");

            if (!_sources.OnlinePaymentsConfigured)
                throw new ReportSourceException(FacilityNotConfigured);
            if (!_sources.LegacyConfigured)
                throw new ReportSourceException(LedgerNotConfigured);

            return period;
        }

        private Task<Inspection> InspectionAsync(DashboardPeriod period, CancellationToken cancellationToken)
            => _cache.GetOrReadAsync("inspection:" + period.Key, period.KeepFor, () => ReadAsync(period), cancellationToken);

        /// <summary>The bills the filters leave, summed, and those of the view, in the register's order.</summary>
        private static InspectionCut Cut(Inspection inspection, string? view, string? provider, string? search)
        {
            var viewKey = InspectionRules.NormalizeView(view);
            var providerKey = provider?.Trim() ?? string.Empty;
            var term = search?.Trim() ?? string.Empty;
            var needle = term.ToLowerInvariant();
            var needleDigits = Compact(term);

            var filtered = inspection.Entries
                .Where(entry => providerKey.Length == 0 || string.Equals(entry.Bill.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase))
                .Where(entry => needle.Length == 0
                                || entry.Haystack.Contains(needle, StringComparison.Ordinal)
                                || (needleDigits.Length >= 4 && entry.Compact.Contains(needleDigits, StringComparison.Ordinal)))
                .Select(entry => entry.Bill)
                .ToList();

            var listed = viewKey == InspectionViews.All
                ? filtered
                : filtered.Where(bill => InspectionRules.ViewOf(bill.Verdict) == viewKey).ToList();

            return new InspectionCut(viewKey, providerKey, term, InspectionRules.Summarize(filtered), listed);
        }

        private async Task<Inspection> ReadAsync(DashboardPeriod period)
        {
            List<OnlinePaymentBillRow> bills;
            List<PostedPaymentQueueRow> queue;
            try
            {
                bills = await _facility.GetBillsAsync(period.From, period.Until);
                queue = bills.Count == 0 ? [] : await _facility.GetQueueEntriesAsync(period.From, period.Until);
            }
            catch (SqlException ex) when (ex.Number == -2)
            {
                throw new ReportSourceException(FacilityTimeout, ex);
            }
            catch (DbException ex)
            {
                throw new ReportSourceException(FacilityUnreachable, ex);
            }

            var keys = bills
                .Where(bill => !string.IsNullOrWhiteSpace(bill.AccountNumber))
                .Select(bill => new LedgerBillKey(bill.AccountNumber!.Trim(), bill.ServicePeriodEnd.Date))
                .ToList();

            List<LedgerEntryRow> ledger;
            try
            {
                ledger = keys.Count == 0 ? [] : await _ledger.GetEntriesAsync(keys);
            }
            catch (SqlException ex) when (ex.Number == -2)
            {
                throw new ReportSourceException(LedgerTimeout, ex);
            }
            catch (DbException ex)
            {
                throw new ReportSourceException(LedgerUnreachable, ex);
            }

            var names = await ReadNamesAsync(keys.Select(key => key.AccountNumber).ToList());
            var providerNames = bills.Count == 0 ? new Dictionary<string, string>() : await PaymentProviderNames.ReadAsync(_providers, _logger);

            return Build(bills, queue, ledger, names, providerNames, _dateTime.Now());
        }

        // The names are a nicety too: without them an account shows by its number, and every figure stands.
        private async Task<List<ConsumerNameRow>> ReadNamesAsync(List<string> accounts)
        {
            if (accounts.Count == 0)
                return [];

            try
            {
                return await _ledger.GetConsumerNamesAsync(accounts);
            }
            catch (DbException ex)
            {
                _logger.LogWarning(ex, "The consumers' names could not be read from the cashiering database; accounts show by number");
                return [];
            }
        }

        private static Inspection Build(
            List<OnlinePaymentBillRow> bills,
            List<PostedPaymentQueueRow> queue,
            List<LedgerEntryRow> ledger,
            List<ConsumerNameRow> names,
            Dictionary<string, string> providerNames,
            DateTime readOn)
        {
            // A bill's queue rows by its payment and bill number, and, for a row without a bill number, by its period.
            var queueByBill = queue
                .GroupBy(row => QueueKey(row.TransactionId, row.BillNumber, row.ServicePeriodEnd))
                .ToDictionary(group => group.Key, group => group.OrderByDescending(row => row.QueuedOn).ToList());

            var ledgerByBill = new Dictionary<(string, DateTime), LedgerEntryRow>();
            foreach (var row in ledger)
                ledgerByBill.TryAdd((row.AccountNumber.Trim().ToUpperInvariant(), row.ServicePeriodEnd.Date), row);

            var nameByAccount = new Dictionary<string, ConsumerNameRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in names)
                nameByAccount.TryAdd(row.AccountNumber.Trim(), row);

            var entries = new List<InspectionEntry>(bills.Count);
            foreach (var bill in bills)
            {
                queueByBill.TryGetValue(QueueKey(bill.TransactionId, bill.BillNumber, bill.ServicePeriodEnd), out var queued);
                var account = bill.AccountNumber?.Trim() ?? string.Empty;
                ledgerByBill.TryGetValue((account.ToUpperInvariant(), bill.ServicePeriodEnd.Date), out var posted);
                nameByAccount.TryGetValue(account, out var consumer);
                var providerKey = bill.PaymentProviderId?.Trim() ?? string.Empty;

                var inspected = InspectionRules.Inspect(
                    bill,
                    queued?.FirstOrDefault(),
                    queued?.Count ?? 0,
                    posted,
                    consumer,
                    providerKey,
                    PaymentProviderNames.Label(providerKey, providerNames));

                entries.Add(new InspectionEntry(inspected));
            }

            // Newest payment first; a payment's bills together, the oldest bill first, as they would be paid. A payment
            // is placed by when it was begun (its earliest bill): the facility stamps its bills alike, but a payment
            // whose bills were written a moment apart still stays whole.
            var begun = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                var key = PaymentKey(entry.Bill);
                if (!begun.TryGetValue(key, out var first) || entry.Bill.CreatedOn < first)
                    begun[key] = entry.Bill.CreatedOn;
            }

            entries.Sort((a, b) =>
            {
                var byTime = begun[PaymentKey(b.Bill)].CompareTo(begun[PaymentKey(a.Bill)]);
                if (byTime != 0) return byTime;
                var byPayment = string.Compare(a.Bill.TransactionId, b.Bill.TransactionId, StringComparison.OrdinalIgnoreCase);
                if (byPayment != 0) return byPayment;
                var byPeriod = a.Bill.ServicePeriodEnd.CompareTo(b.Bill.ServicePeriodEnd);
                return byPeriod != 0 ? byPeriod : a.Bill.Id.CompareTo(b.Bill.Id);
            });

            var providers = entries
                .GroupBy(entry => entry.Bill.ProviderKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => new InspectionProviderDTO
                {
                    Key = group.Key,
                    Label = group.First().Bill.ProviderLabel,
                    Bills = group.Count(),
                })
                .OrderBy(provider => PaymentProviderNames.Order(provider.Key))
                .ThenBy(provider => provider.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new Inspection(entries, providers, readOn);
        }

        /// <summary>A bill's payment; a bill without a TransactionId stands alone, as the dashboard counts it.</summary>
        private static string PaymentKey(InspectedBillDTO bill)
            => bill.TransactionId.Length > 0 ? bill.TransactionId : "#" + bill.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static string QueueKey(string? transactionId, string? billNumber, DateTime servicePeriodEnd)
        {
            var payment = transactionId?.Trim().ToUpperInvariant() ?? string.Empty;
            var bill = billNumber?.Trim().ToUpperInvariant();
            return string.IsNullOrEmpty(bill)
                ? $"{payment}|@{servicePeriodEnd:yyyyMMdd}"
                : $"{payment}|{bill}";
        }

        /// <summary>Letters and digits only, lower-cased: "01-0095-0003" and "0100950003" both read "0100950003".</summary>
        private static string Compact(string text)
            => new(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

        /// <summary>A period's bills as read, kept for every viewer of the period.</summary>
        private sealed record Inspection(List<InspectionEntry> Entries, List<InspectionProviderDTO> Providers, DateTime ReadOn);

        private sealed record InspectionCut(string View, string Provider, string Search, InspectionSummaryDTO Summary, List<InspectedBillDTO> Listed);

        /// <summary>A bill and what a search looks through: its numbers, its consumer and its receipts.</summary>
        private sealed class InspectionEntry
        {
            public InspectionEntry(InspectedBillDTO bill)
            {
                Bill = bill;
                var fields = new[]
                {
                    bill.AccountNumber, bill.ConsumerName, bill.TransactionId, bill.BillNumber, bill.ProviderReference,
                    bill.Ledger?.OrNumber, bill.Queue?.OrNumber,
                };
                Haystack = string.Join('\n', fields.Where(field => !string.IsNullOrEmpty(field))).ToLowerInvariant();
                Compact = TransactionInspectionService.Compact(bill.AccountNumber) + "|" + TransactionInspectionService.Compact(bill.BillNumber);
            }

            public InspectedBillDTO Bill { get; }

            public string Haystack { get; }

            public string Compact { get; }
        }
    }
}
