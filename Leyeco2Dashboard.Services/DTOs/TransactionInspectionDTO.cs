namespace Leyeco2Dashboard.Services.DTOs
{
    /// <summary>
    /// The Transaction Inspector's answer for a period: every bill paid (or begun) through the online payment facility,
    /// each followed into the facility's posting queue and the collections ledger, summed by stage, counted by view,
    /// and one page of the view asked for. Statuses and verdicts travel as keys with their words beside them, so the
    /// console, the PDF and the workbook word them alike.
    /// </summary>
    public class TransactionInspectionDTO
    {
        public DateTime From { get; set; }

        public DateTime To { get; set; }

        public int Days { get; set; }

        public bool IncludesToday { get; set; }

        /// <summary>When the sources were read: the figures are as of then.</summary>
        public DateTime GeneratedOn { get; set; }

        /// <summary>The view listed: all, attention, waiting, tallied or unpaid.</summary>
        public string View { get; set; } = InspectionViews.All;

        /// <summary>The provider the bills are narrowed to; empty for all.</summary>
        public string Provider { get; set; } = string.Empty;

        public string Search { get; set; } = string.Empty;

        /// <summary>The providers that carried a bill in the period, whatever the filters.</summary>
        public List<InspectionProviderDTO> Providers { get; set; } = [];

        /// <summary>The bills the provider and search leave, summed by stage; the views slice the same bills.</summary>
        public InspectionSummaryDTO Summary { get; set; } = new();

        public InspectionPageDTO Page { get; set; } = new();
    }

    public class InspectionProviderDTO
    {
        public string Key { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public int Bills { get; set; }
    }

    public class InspectionPageDTO
    {
        public int CurrentPage { get; set; } = 1;

        public int PageSize { get; set; }

        public int TotalItems { get; set; }

        public int TotalPages { get; set; } = 1;

        public List<InspectedBillDTO> Items { get; set; } = [];
    }

    /// <summary>
    /// What each book holds for the bills inspected. The facility's lines cover every bill begun; the queue's and the
    /// ledger's cover the bills the facility recorded as paid, which are the ones owed a posting, with a closing line
    /// for any bill credited although its payment did not go through.
    /// </summary>
    public class InspectionSummaryDTO
    {
        /// <summary>Paid bills: the facility's successful ones, in how many payments, and their amount.</summary>
        public InspectionFiguresDTO Paid { get; set; } = new();

        public int PaidPayments { get; set; }

        /// <summary>Paid bills handed to the poster, at the queue's own amounts.</summary>
        public InspectionFiguresDTO Queued { get; set; } = new();

        /// <summary>Bills the ledger holds as posted from these payments (its receipt is the payment's), at the ledger's amounts.</summary>
        public InspectionFiguresDTO Posted { get; set; } = new();

        /// <summary>The facility's paid amount less what the ledger posted from it: positive when the ledger is short.</summary>
        public decimal Shortfall { get; set; }

        /// <summary>Every bill begun, by the facility's status.</summary>
        public List<InspectionLineDTO> Facility { get; set; } = [];

        /// <summary>The paid bills, by where the queue has them (Not queued last).</summary>
        public List<InspectionLineDTO> Queue { get; set; } = [];

        /// <summary>The paid bills, by what the ledger holds for them; then any unpaid bill it credits anyway.</summary>
        public List<InspectionLineDTO> Ledger { get; set; } = [];

        /// <summary>The views, in the tabs' order, with what each holds.</summary>
        public List<InspectionViewDTO> Views { get; set; } = [];

        /// <summary>Every verdict that holds a bill, in the order a reconciliation works through them.</summary>
        public List<InspectionViewDTO> Verdicts { get; set; } = [];
    }

    public class InspectionFiguresDTO
    {
        public int Bills { get; set; }

        public decimal Amount { get; set; }
    }

    /// <summary>A status within a stage and the bills at it. <see cref="Key"/> is the status's key in that stage.</summary>
    public class InspectionLineDTO
    {
        public string Key { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public int Bills { get; set; }

        public decimal Amount { get; set; }
    }

    public class InspectionViewDTO
    {
        public string Key { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public int Bills { get; set; }

        /// <summary>What the facility recorded for the view's bills.</summary>
        public decimal FacilityAmount { get; set; }

        /// <summary>What the ledger holds for them, whoever posted it.</summary>
        public decimal LedgerAmount { get; set; }
    }

    /// <summary>One bill as each book records it, and the verdict.</summary>
    public class InspectedBillDTO
    {
        /// <summary>The facility's row id: a bill's identity across reads.</summary>
        public int Id { get; set; }

        public string TransactionId { get; set; } = string.Empty;

        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>The consumer as the cashiering system names them; null for an account it doesn't know.</summary>
        public string? ConsumerName { get; set; }

        /// <summary>The account is among the disconnected consumers.</summary>
        public bool IsDisconnected { get; set; }

        public string BillNumber { get; set; } = string.Empty;

        public DateTime ServicePeriodEnd { get; set; }

        public string ProviderKey { get; set; } = string.Empty;

        public string ProviderLabel { get; set; } = string.Empty;

        /// <summary>When the payment was begun.</summary>
        public DateTime CreatedOn { get; set; }

        /// <summary>When the facility last changed it: a provider's answer, an expiry, a cancellation.</summary>
        public DateTime? ResolvedOn { get; set; }

        /// <summary>The provider's own reference for the payment.</summary>
        public string? ProviderReference { get; set; }

        public FacilityRecordDTO Facility { get; set; } = new();

        /// <summary>The queue's row for the bill (the latest, when there are several); null when it was never queued.</summary>
        public QueueRecordDTO? Queue { get; set; }

        /// <summary>The ledger's row for the bill; null when the ledger holds none.</summary>
        public LedgerRecordDTO? Ledger { get; set; }

        /// <summary>tallied, amount-differs, paid-elsewhere, not-posted, waiting, credited-unpaid or unpaid (<c>InspectionRules</c>).</summary>
        public string Verdict { get; set; } = string.Empty;

        public string VerdictLabel { get; set; } = string.Empty;

        /// <summary>The ledger's total less the facility's, for a bill posted from this payment; null otherwise.</summary>
        public decimal? Difference { get; set; }
    }

    public class FacilityRecordDTO
    {
        /// <summary>successful, pending, failed, cancelled, dropped or other.</summary>
        public string Status { get; set; } = string.Empty;

        public string StatusLabel { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public decimal Surcharge { get; set; }

        public decimal Total { get; set; }

        public string? Remarks { get; set; }
    }

    public class QueueRecordDTO
    {
        /// <summary>queued, posting, posted, failed or other.</summary>
        public string Status { get; set; } = string.Empty;

        public string StatusLabel { get; set; } = string.Empty;

        /// <summary>The queue's number, as stored.</summary>
        public int StatusCode { get; set; }

        public string? OrNumber { get; set; }

        public string? OrDate { get; set; }

        /// <summary>The ledger account the posting is to be made under.</summary>
        public string? PostAs { get; set; }

        public DateTime QueuedOn { get; set; }

        public decimal Amount { get; set; }

        public decimal Surcharge { get; set; }

        public decimal Total { get; set; }

        public string? Remarks { get; set; }

        /// <summary>How many rows the queue holds for the bill: more than one when it was queued again.</summary>
        public int Entries { get; set; } = 1;
    }

    public class LedgerRecordDTO
    {
        /// <summary>posted (this payment's own receipt) or elsewhere (another receipt: paid through another channel).</summary>
        public string Status { get; set; } = string.Empty;

        public string StatusLabel { get; set; } = string.Empty;

        /// <summary>The channel the posting account belongs to: Teller, SBA, ECPay, Bayad Center, SM Payment, Bizlink.</summary>
        public string Channel { get; set; } = string.Empty;

        public string? OrNumber { get; set; }

        public DateTime? OrDate { get; set; }

        public DateTime DatePaid { get; set; }

        public DateTime? PostedOn { get; set; }

        public string? Username { get; set; }

        public string? PostedBy { get; set; }

        public string? CollectorId { get; set; }

        public string BillNumber { get; set; } = string.Empty;

        /// <summary>The bill's amount as posted, the surcharge not included.</summary>
        public decimal Amount { get; set; }

        public decimal Surcharge { get; set; }

        public decimal Total { get; set; }

        public string? Remarks { get; set; }
    }

    /// <summary>A printout of an inspection: its name, its type and its bytes.</summary>
    public class InspectionDocumentDTO
    {
        public string FileName { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public byte[] Content { get; set; } = [];
    }

    /// <summary>The views' keys, in the tabs' order.</summary>
    public static class InspectionViews
    {
        public const string All = "all";
        public const string Attention = "attention";
        public const string Waiting = "waiting";
        public const string Tallied = "tallied";
        public const string Unpaid = "unpaid";

        public static readonly IReadOnlyList<string> Order = [All, Attention, Waiting, Tallied, Unpaid];
    }
}
