namespace Leyeco2Dashboard.Data.Reports
{
    // The rows the Transaction Inspector's queries answer, read by column name off hand-written SQL as the dashboards'
    // rows are: a property renamed here is a column alias to rename in its query. Everything the sources may leave
    // empty is nullable, so a row with a gap reads rather than fails.

    /// <summary>
    /// One bill paid (or begun) through the online payment facility, a row of its <c>Transactions</c>. The bills one
    /// payment settled share its <see cref="TransactionId"/> and its account.
    /// </summary>
    public class OnlinePaymentBillRow
    {
        public int Id { get; set; }

        public string? TransactionId { get; set; }

        public string? AccountNumber { get; set; }

        public string? BillNumber { get; set; }

        /// <summary>The bill's period, the half of the ledger's key the account number doesn't hold.</summary>
        public DateTime ServicePeriodEnd { get; set; }

        /// <summary>The facility's own word: SUCCESSFUL, PENDING, FAILED, CANCELLED or DROPPED.</summary>
        public string? PaymentStatus { get; set; }

        public string? PaymentProviderId { get; set; }

        /// <summary>The bill's amount, rounded to the centavo as the facility's own summary rounds it.</summary>
        public decimal Amount { get; set; }

        public decimal Surcharge { get; set; }

        public DateTime CreatedOn { get; set; }

        /// <summary>When the provider's answer (or an expiry, or a cancellation) last changed the row.</summary>
        public DateTime? LastModifiedOn { get; set; }

        /// <summary>The provider's own reference for the payment.</summary>
        public string? ReferenceNumber { get; set; }

        public string? Remarks { get; set; }
    }

    /// <summary>A row of the facility's <c>PostedPaymentsQueue</c>: one bill handed to the poster.</summary>
    public class PostedPaymentQueueRow
    {
        public Guid Id { get; set; }

        public string? TransactionId { get; set; }

        public string? AccountNumber { get; set; }

        public string? BillNumber { get; set; }

        public DateTime ServicePeriodEnd { get; set; }

        /// <summary>The official receipt the posting is to carry: the facility uses the payment's TransactionId.</summary>
        public string? OrNumber { get; set; }

        /// <summary>The receipt's date, as the queue keeps it (text).</summary>
        public string? OrDate { get; set; }

        /// <summary>The account the posting is to be made under: the provider's gateway ("ecpay").</summary>
        public string? Username { get; set; }

        public decimal Amount { get; set; }

        public decimal Surcharge { get; set; }

        public DateTime QueuedOn { get; set; }

        /// <summary>A <see cref="Enums.PostedPaymentQueueStatusEnum"/> value, as stored.</summary>
        public int Status { get; set; }

        public string? Remarks { get; set; }
    }

    /// <summary>
    /// The collections ledger's row for a bill (<c>Collection_Transactions_History</c>), found by its key: the account
    /// and the bill's period. The ledger keeps one row per bill, whoever posted it.
    /// </summary>
    public class LedgerEntryRow
    {
        public string AccountNumber { get; set; } = string.Empty;

        public DateTime ServicePeriodEnd { get; set; }

        public decimal BillNumber { get; set; }

        public string? OrNumber { get; set; }

        public DateTime? OrDate { get; set; }

        public DateTime DatePaid { get; set; }

        /// <summary>The bill's amount, the surcharge not included (the ledger keeps it apart).</summary>
        public decimal? AmountPaid { get; set; }

        public decimal? Surcharge { get; set; }

        public string? Username { get; set; }

        public string? PostedBy { get; set; }

        public string? CollectorId { get; set; }

        /// <summary>When the posting was written (<c>timestamp</c>): <see cref="DatePaid"/> is a date only.</summary>
        public DateTime? PostedOn { get; set; }

        public string? Remarks { get; set; }

        /// <summary>A <see cref="Enums.CollectionChannelEnum"/> value, read off <see cref="Username"/>.</summary>
        public int Channel { get; set; }
    }

    /// <summary>An account's consumer, as the cashiering system names it.</summary>
    public class ConsumerNameRow
    {
        public string AccountNumber { get; set; } = string.Empty;

        public string? Name { get; set; }

        /// <summary>The account is among the disconnected consumers, not the active ones.</summary>
        public bool IsDisconnected { get; set; }
    }

    /// <summary>A bill as the collections ledger keys it: its account and its period.</summary>
    public readonly record struct LedgerBillKey(string AccountNumber, DateTime ServicePeriodEnd);
}
