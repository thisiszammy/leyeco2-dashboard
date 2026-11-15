namespace Leyeco2Dashboard.Client.Features.TransactionInspector.Models
{
    /// <summary>
    /// The Transaction Inspector as <c>GET api/transaction-inspector/bills</c> answers it (the API's
    /// <c>TransactionInspectionDTO</c>): every bill of the period's online payments, followed into the posting queue
    /// and the collections ledger, summed by book, counted by view, and one page of the view asked for.
    /// </summary>
    public sealed class TransactionInspection
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int Days { get; set; }
        public bool IncludesToday { get; set; }
        public DateTime GeneratedOn { get; set; }
        public string View { get; set; } = InspectionViews.All;
        public string Provider { get; set; } = string.Empty;
        public string Search { get; set; } = string.Empty;
        public List<InspectionProvider> Providers { get; set; } = new();
        public InspectionSummary Summary { get; set; } = new();
        public InspectionPage Page { get; set; } = new();
    }

    public sealed class InspectionProvider
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Bills { get; set; }
    }

    public sealed class InspectionPage
    {
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; } = 1;
        public List<InspectedBill> Items { get; set; } = new();
    }

    public sealed class InspectionSummary
    {
        /// <summary>The bills the facility recorded as paid, in how many payments.</summary>
        public InspectionFigures Paid { get; set; } = new();
        public int PaidPayments { get; set; }

        /// <summary>The paid bills handed to the poster, at the queue's amounts.</summary>
        public InspectionFigures Queued { get; set; } = new();

        /// <summary>The paid bills the ledger holds as posted from these payments, at its amounts.</summary>
        public InspectionFigures Posted { get; set; } = new();

        /// <summary>The paid amount less the posted: positive when the ledger is short of the facility.</summary>
        public decimal Shortfall { get; set; }

        public List<InspectionLine> Facility { get; set; } = new();
        public List<InspectionLine> Queue { get; set; } = new();
        public List<InspectionLine> Ledger { get; set; } = new();
        public List<InspectionTally> Views { get; set; } = new();
        public List<InspectionTally> Verdicts { get; set; } = new();
    }

    public sealed class InspectionFigures
    {
        public int Bills { get; set; }
        public decimal Amount { get; set; }
    }

    public sealed class InspectionLine
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Bills { get; set; }
        public decimal Amount { get; set; }
    }

    /// <summary>A view's or a verdict's bills, and what the facility and the ledger hold for them.</summary>
    public sealed class InspectionTally
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Bills { get; set; }
        public decimal FacilityAmount { get; set; }
        public decimal LedgerAmount { get; set; }
    }

    /// <summary>One bill as each book records it, and the verdict.</summary>
    public sealed class InspectedBill
    {
        public int Id { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string? ConsumerName { get; set; }
        public bool IsDisconnected { get; set; }
        public string BillNumber { get; set; } = string.Empty;
        public DateTime ServicePeriodEnd { get; set; }
        public string ProviderKey { get; set; } = string.Empty;
        public string ProviderLabel { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public DateTime? ResolvedOn { get; set; }
        public string? ProviderReference { get; set; }
        public FacilityRecord Facility { get; set; } = new();
        public QueueRecord? Queue { get; set; }
        public LedgerRecord? Ledger { get; set; }
        public string Verdict { get; set; } = string.Empty;
        public string VerdictLabel { get; set; } = string.Empty;

        /// <summary>The ledger's total less the facility's, for a bill posted from this payment.</summary>
        public decimal? Difference { get; set; }
    }

    public sealed class FacilityRecord
    {
        /// <summary>successful, pending, failed, cancelled, dropped or other.</summary>
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Surcharge { get; set; }
        public decimal Total { get; set; }
        public string? Remarks { get; set; }
    }

    public sealed class QueueRecord
    {
        /// <summary>queued, posting, posted, failed or other.</summary>
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public string? OrNumber { get; set; }
        public string? OrDate { get; set; }
        public string? PostAs { get; set; }
        public DateTime QueuedOn { get; set; }
        public decimal Amount { get; set; }
        public decimal Surcharge { get; set; }
        public decimal Total { get; set; }
        public string? Remarks { get; set; }
        public int Entries { get; set; } = 1;
    }

    public sealed class LedgerRecord
    {
        /// <summary>posted (this payment's own receipt) or elsewhere (another receipt).</summary>
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string? OrNumber { get; set; }
        public DateTime? OrDate { get; set; }
        public DateTime DatePaid { get; set; }
        public DateTime? PostedOn { get; set; }
        public string? Username { get; set; }
        public string? PostedBy { get; set; }
        public string? CollectorId { get; set; }
        public string BillNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Surcharge { get; set; }
        public decimal Total { get; set; }
        public string? Remarks { get; set; }
    }

    /// <summary>The views' keys, in the tabs' order (the API's <c>InspectionViews</c>).</summary>
    public static class InspectionViews
    {
        public const string All = "all";
        public const string Attention = "attention";
        public const string Waiting = "waiting";
        public const string Tallied = "tallied";
        public const string Unpaid = "unpaid";
    }
}
