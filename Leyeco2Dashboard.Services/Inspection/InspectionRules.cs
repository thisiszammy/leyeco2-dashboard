using System.Globalization;
using Leyeco2Dashboard.Data.Enums;
using Leyeco2Dashboard.Data.Reports;
using Leyeco2Dashboard.Services.DTOs;

namespace Leyeco2Dashboard.Services.Inspection
{
    /// <summary>
    /// How the Transaction Inspector reads a bill across the three books, in one place: what each book's status is in
    /// words, which ledger row is the payment's own posting, and the verdict. The ledger is the judge of what was
    /// collected (the root CLAUDE.md); the facility says what was paid; the queue says where the hand-off between them
    /// stands.
    /// <list type="bullet">
    /// <item><b>The payment's own posting.</b> The ledger keeps one row per bill (account and period), whoever posted it.
    /// The facility posts a bill under the payment's TransactionId as its receipt (the queue's ORNo), so a row with that
    /// receipt is this payment's; a row with any other receipt is the bill paid through another channel. The posting
    /// account doesn't tell: ECPay's outlets post as "ecpay" too.</item>
    /// <item><b>Amounts.</b> A bill's total is its amount plus its surcharge in every book, each rounded to the centavo:
    /// the ledger keeps the surcharge apart from <c>amount_paid</c>, as the facility does from <c>Amount</c>.</item>
    /// </list>
    /// </summary>
    public static class InspectionRules
    {
        public const string Tallied = "tallied";
        public const string AmountDiffers = "amount-differs";
        public const string PaidElsewhere = "paid-elsewhere";
        public const string NotPosted = "not-posted";
        public const string Waiting = "waiting";
        public const string CreditedUnpaid = "credited-unpaid";
        public const string Unpaid = "unpaid";

        // Facility statuses (its PaymentStatusEnum words).
        public const string Successful = "successful";
        public const string Pending = "pending";
        public const string Failed = "failed";
        public const string Cancelled = "cancelled";
        public const string Dropped = "dropped";
        public const string Other = "other";

        // Queue states, and the place of a bill that has none.
        public const string Queued = "queued";
        public const string Posting = "posting";
        public const string Posted = "posted";
        public const string NotQueued = "none";

        // Ledger states.
        public const string Elsewhere = "elsewhere";
        public const string NotInLedger = "none";

        /// <summary>A centavo's half: amounts nearer than this agree.</summary>
        private const decimal Tolerance = 0.005m;

        private static readonly (string Key, string Word, string Label)[] FacilityStatuses =
        [
            (Successful, "SUCCESSFUL", "Successful"),
            (Pending, "PENDING", "Pending"),
            (Failed, "FAILED", "Failed"),
            (Cancelled, "CANCELLED", "Cancelled"),
            (Dropped, "DROPPED", "Dropped"),
        ];

        public static IReadOnlyList<string> FacilityOrder { get; } = [Successful, Pending, Failed, Cancelled, Dropped, Other];

        public static IReadOnlyList<string> QueueOrder { get; } = [Queued, Posting, Posted, Failed, Other, NotQueued];

        /// <summary>The verdicts that need someone first, then the waiting, the tallied and the unpaid.</summary>
        public static IReadOnlyList<string> VerdictOrder { get; } =
            [NotPosted, AmountDiffers, PaidElsewhere, CreditedUnpaid, Waiting, Tallied, Unpaid];

        /// <summary>A verdict as a summary line names it (a bill's own label may say more: "Queued, not paid").</summary>
        public static string VerdictLineLabel(string verdict) => verdict switch
        {
            Tallied        => "Tallied",
            AmountDiffers  => "Amount differs",
            PaidElsewhere  => "Paid elsewhere",
            NotPosted      => "Not posted",
            Waiting        => "Waiting to post",
            CreditedUnpaid => "Credited, not paid",
            Unpaid         => "Not paid",
            _              => verdict,
        };

        /// <summary>The verdicts a reconciliation has to act on.</summary>
        public static bool NeedsAttention(string verdict) => ViewOf(verdict) == InspectionViews.Attention;

        public static (string Key, string Label) FacilityStatus(string? word)
        {
            var trimmed = word?.Trim() ?? string.Empty;
            foreach (var (key, candidate, label) in FacilityStatuses)
            {
                if (string.Equals(candidate, trimmed, StringComparison.OrdinalIgnoreCase))
                    return (key, label);
            }

            return (Other, trimmed.Length == 0 ? "No status" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(trimmed.ToLowerInvariant()));
        }

        public static (string Key, string Label) QueueStatus(int code) => code switch
        {
            (int)PostedPaymentQueueStatusEnum.Queued  => (Queued, "Queued"),
            (int)PostedPaymentQueueStatusEnum.Posting => (Posting, "Posting"),
            (int)PostedPaymentQueueStatusEnum.Posted  => (Posted, "Posted"),
            // "Failed" under the queue's own head; the summary's line says "Posting failed" (QueueLineLabel).
            (int)PostedPaymentQueueStatusEnum.Failed  => (Failed, "Failed"),
            _                                         => (Other, $"Status {code.ToString(CultureInfo.InvariantCulture)}"),
        };

        public static string QueueLineLabel(string key) => key switch
        {
            Queued    => "Queued",
            Posting   => "Posting",
            Posted    => "Posted",
            Failed    => "Posting failed",
            Other     => "Other status",
            NotQueued => "Not queued",
            _         => key,
        };

        public static string FacilityLineLabel(string key) => key switch
        {
            Successful => "Successful",
            Pending    => "Pending",
            Failed     => "Failed",
            Cancelled  => "Cancelled",
            Dropped    => "Dropped",
            _          => "Other status",
        };

        /// <summary>The view a verdict is listed under.</summary>
        public static string ViewOf(string verdict) => verdict switch
        {
            Tallied => InspectionViews.Tallied,
            Waiting => InspectionViews.Waiting,
            Unpaid  => InspectionViews.Unpaid,
            _       => InspectionViews.Attention,
        };

        public static string ViewLabel(string view) => view switch
        {
            InspectionViews.All       => "All bills",
            InspectionViews.Attention => "Needs attention",
            InspectionViews.Waiting   => "Waiting to post",
            InspectionViews.Tallied   => "Tallied",
            InspectionViews.Unpaid    => "Not paid",
            _                         => view,
        };

        /// <summary>A view's key as asked for, or All for one that isn't a view.</summary>
        public static string NormalizeView(string? view)
        {
            var key = view?.Trim().ToLowerInvariant() ?? string.Empty;
            return InspectionViews.Order.Contains(key) ? key : InspectionViews.All;
        }

        /// <summary>
        /// One bill read across the books. <paramref name="queue"/> is the latest of its queue rows and
        /// <paramref name="queueEntries"/> how many there are; <paramref name="ledger"/> the ledger's row for its account
        /// and period, whoever posted it.
        /// </summary>
        public static InspectedBillDTO Inspect(
            OnlinePaymentBillRow bill,
            PostedPaymentQueueRow? queue,
            int queueEntries,
            LedgerEntryRow? ledger,
            ConsumerNameRow? consumer,
            string providerKey,
            string providerLabel)
        {
            var transactionId = bill.TransactionId?.Trim() ?? string.Empty;
            var (facilityKey, facilityLabel) = FacilityStatus(bill.PaymentStatus);
            var facility = new FacilityRecordDTO
            {
                Status = facilityKey,
                StatusLabel = facilityLabel,
                Amount = bill.Amount,
                Surcharge = bill.Surcharge,
                Total = bill.Amount + bill.Surcharge,
                Remarks = Clean(bill.Remarks),
            };

            QueueRecordDTO? queueRecord = null;
            if (queue is not null)
            {
                var (queueKey, queueLabel) = QueueStatus(queue.Status);
                queueRecord = new QueueRecordDTO
                {
                    Status = queueKey,
                    StatusLabel = queueLabel,
                    StatusCode = queue.Status,
                    OrNumber = Clean(queue.OrNumber),
                    OrDate = Clean(queue.OrDate),
                    PostAs = Clean(queue.Username),
                    QueuedOn = queue.QueuedOn,
                    Amount = queue.Amount,
                    Surcharge = queue.Surcharge,
                    Total = queue.Amount + queue.Surcharge,
                    Remarks = Clean(queue.Remarks),
                    Entries = Math.Max(1, queueEntries),
                };
            }

            LedgerRecordDTO? ledgerRecord = null;
            var own = false;
            if (ledger is not null)
            {
                var receipt = ledger.OrNumber?.Trim() ?? string.Empty;
                own = receipt.Length > 0
                      && (SameText(receipt, transactionId) || SameText(receipt, queueRecord?.OrNumber));
                var channel = Enum.IsDefined(typeof(CollectionChannelEnum), ledger.Channel)
                    ? ((CollectionChannelEnum)ledger.Channel).Label()
                    : CollectionChannelEnum.Teller.Label();
                var amount = Math.Round(ledger.AmountPaid ?? 0m, 2);
                var surcharge = Math.Round(ledger.Surcharge ?? 0m, 2);

                ledgerRecord = new LedgerRecordDTO
                {
                    Status = own ? Posted : Elsewhere,
                    // Another receipt: the channel that posted it ("Via Teller"); the verdict says what that means.
                    StatusLabel = own ? "Posted" : $"Via {channel}",
                    Channel = channel,
                    OrNumber = receipt.Length == 0 ? null : receipt,
                    OrDate = ledger.OrDate,
                    DatePaid = ledger.DatePaid,
                    PostedOn = ledger.PostedOn,
                    Username = Clean(ledger.Username),
                    PostedBy = Clean(ledger.PostedBy),
                    CollectorId = Clean(ledger.CollectorId),
                    BillNumber = ledger.BillNumber.ToString("0", CultureInfo.InvariantCulture),
                    Amount = amount,
                    Surcharge = surcharge,
                    Total = amount + surcharge,
                    Remarks = Clean(ledger.Remarks),
                };
            }

            var (verdict, label, difference) = Judge(facilityKey, queueRecord, ledgerRecord, own, facility.Total);

            return new InspectedBillDTO
            {
                Id = bill.Id,
                TransactionId = transactionId,
                AccountNumber = bill.AccountNumber?.Trim() ?? string.Empty,
                ConsumerName = Clean(consumer?.Name),
                IsDisconnected = consumer?.IsDisconnected ?? false,
                BillNumber = bill.BillNumber?.Trim() ?? string.Empty,
                ServicePeriodEnd = bill.ServicePeriodEnd,
                ProviderKey = providerKey,
                ProviderLabel = providerLabel,
                CreatedOn = bill.CreatedOn,
                ResolvedOn = bill.LastModifiedOn,
                ProviderReference = Clean(bill.ReferenceNumber),
                Facility = facility,
                Queue = queueRecord,
                Ledger = ledgerRecord,
                Verdict = verdict,
                VerdictLabel = label,
                Difference = difference,
            };
        }

        /// <summary>
        /// The verdict on a bill. Paid: tallied when the ledger holds this payment's posting for the same total, amount
        /// differs when for another; paid elsewhere when it holds the bill under another receipt (the bill was settled
        /// twice, or posted by hand under another); waiting while the queue still has it to post; not posted otherwise
        /// (never queued, the poster failed, or the queue says posted and the ledger has nothing). Not paid: credited
        /// when the ledger holds this payment's posting or the queue still means to post it, else simply not paid.
        /// </summary>
        private static (string Verdict, string Label, decimal? Difference) Judge(
            string facilityStatus, QueueRecordDTO? queue, LedgerRecordDTO? ledger, bool own, decimal facilityTotal)
        {
            if (facilityStatus == Successful)
            {
                if (ledger is not null && own)
                {
                    var difference = ledger.Total - facilityTotal;
                    return Math.Abs(difference) < Tolerance
                        ? (Tallied, "Tallied", 0m)
                        : (AmountDiffers, "Amount differs", difference);
                }

                if (ledger is not null)
                    return (PaidElsewhere, "Paid elsewhere", null);

                if (queue?.Status is Queued or Posting)
                    return (Waiting, "Waiting to post", null);

                return (NotPosted, "Not posted", null);
            }

            if (ledger is not null && own)
                return (CreditedUnpaid, "Posted, not paid", ledger.Total - facilityTotal);

            if (queue?.Status is Queued or Posting or Posted)
                return (CreditedUnpaid, "Queued, not paid", null);

            return (Unpaid, "Not paid", null);
        }

        /// <summary>Every book's summary for a set of bills, and the views' counts.</summary>
        public static InspectionSummaryDTO Summarize(IReadOnlyCollection<InspectedBillDTO> bills)
        {
            var summary = new InspectionSummaryDTO();
            var paid = bills.Where(bill => bill.Facility.Status == Successful).ToList();

            summary.Paid = Figures(paid, bill => bill.Facility.Total);
            summary.PaidPayments = paid.Select(bill => bill.TransactionId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            summary.Queued = Figures(paid.Where(bill => bill.Queue is not null), bill => bill.Queue!.Total);
            summary.Posted = Figures(paid.Where(bill => bill.Ledger?.Status == Posted), bill => bill.Ledger!.Total);
            summary.Shortfall = summary.Paid.Amount - summary.Posted.Amount;

            // The facility's lines: Successful always, the rest when they hold a bill.
            foreach (var key in FacilityOrder)
            {
                var at = bills.Where(bill => bill.Facility.Status == key).ToList();
                if (at.Count > 0 || key == Successful)
                    summary.Facility.Add(Line(key, FacilityLineLabel(key), at, bill => bill.Facility.Total));
            }

            foreach (var key in QueueOrder)
            {
                var at = paid.Where(bill => (bill.Queue?.Status ?? NotQueued) == key).ToList();
                if (at.Count > 0)
                    summary.Queue.Add(Line(key, QueueLineLabel(key), at, bill => bill.Queue?.Total ?? bill.Facility.Total));
            }

            AddLedgerLine(summary, Tallied, "Posted, amounts agree", paid.Where(bill => bill.Verdict == Tallied), bill => bill.Ledger!.Total);
            AddLedgerLine(summary, AmountDiffers, "Posted, amount differs", paid.Where(bill => bill.Verdict == AmountDiffers), bill => bill.Ledger!.Total);
            AddLedgerLine(summary, PaidElsewhere, "Paid through another channel", paid.Where(bill => bill.Verdict == PaidElsewhere), bill => bill.Ledger!.Total);
            AddLedgerLine(summary, NotInLedger, "Not in the ledger", paid.Where(bill => bill.Ledger is null), bill => bill.Facility.Total);
            AddLedgerLine(summary, CreditedUnpaid, "Credited, not paid", bills.Where(bill => bill.Verdict == CreditedUnpaid), bill => bill.Ledger?.Total ?? bill.Queue?.Total ?? 0m);

            foreach (var verdict in VerdictOrder)
            {
                var at = bills.Where(bill => bill.Verdict == verdict).ToList();
                if (at.Count == 0)
                    continue;

                summary.Verdicts.Add(new InspectionViewDTO
                {
                    Key = verdict,
                    Label = VerdictLineLabel(verdict),
                    Bills = at.Count,
                    FacilityAmount = at.Sum(bill => bill.Facility.Total),
                    LedgerAmount = at.Sum(bill => bill.Ledger?.Total ?? 0m),
                });
            }

            foreach (var view in InspectionViews.Order)
            {
                var at = view == InspectionViews.All ? bills : bills.Where(bill => ViewOf(bill.Verdict) == view).ToList();
                summary.Views.Add(new InspectionViewDTO
                {
                    Key = view,
                    Label = ViewLabel(view),
                    Bills = at.Count,
                    FacilityAmount = at.Sum(bill => bill.Facility.Total),
                    LedgerAmount = at.Sum(bill => bill.Ledger?.Total ?? 0m),
                });
            }

            return summary;
        }

        private static void AddLedgerLine(InspectionSummaryDTO summary, string key, string label, IEnumerable<InspectedBillDTO> bills, Func<InspectedBillDTO, decimal> amount)
        {
            var at = bills.ToList();
            if (at.Count > 0)
                summary.Ledger.Add(Line(key, label, at, amount));
        }

        private static InspectionLineDTO Line(string key, string label, IReadOnlyCollection<InspectedBillDTO> bills, Func<InspectedBillDTO, decimal> amount)
            => new() { Key = key, Label = label, Bills = bills.Count, Amount = bills.Sum(amount) };

        private static InspectionFiguresDTO Figures(IEnumerable<InspectedBillDTO> bills, Func<InspectedBillDTO, decimal> amount)
        {
            var list = bills.ToList();
            return new InspectionFiguresDTO { Bills = list.Count, Amount = list.Sum(amount) };
        }

        private static bool SameText(string? a, string? b)
            => !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
               && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        private static string? Clean(string? text)
        {
            var trimmed = text?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }
    }
}
