namespace Leyeco2Dashboard.Data.Reports
{
    // The rows the collections ledger's report queries answer (CollectionReportRepository). They are not entities:
    // EF reads them off hand-written SQL by column name, so a property renamed here is a column alias to rename there.

    /// <summary>
    /// The ledger summed by the day a bill was paid, the hour it was posted, the channel that posted it and the
    /// town of its account: the grain every figure of the overall dashboard but the accounts and the collectors is
    /// summed from.
    /// </summary>
    public class CollectionSliceRow
    {
        public DateTime Day { get; set; }

        /// <summary>The hour of the posting time (<c>timestamp</c>); null for the rows posted without one.</summary>
        public int? Hour { get; set; }

        /// <summary>A <see cref="Enums.CollectionChannelEnum"/> value.</summary>
        public int Channel { get; set; }

        /// <summary>A <see cref="Enums.CollectionTownEnum"/> value.</summary>
        public int Town { get; set; }

        public int Bills { get; set; }

        public decimal Amount { get; set; }

        public decimal Surcharge { get; set; }

        /// <summary>The bills that carried a surcharge: paid after their due date.</summary>
        public int SurchargedBills { get; set; }
    }

    /// <summary>One account that posted payments in the period (a teller, an SBA account, a partner's feed).</summary>
    public class CollectorRow
    {
        public string Username { get; set; } = string.Empty;

        public string? CollectorId { get; set; }

        public int Channel { get; set; }

        public int Bills { get; set; }

        public decimal Amount { get; set; }

        public DateTime? LastPostedOn { get; set; }
    }

    /// <summary>
    /// A period's totals for one town, the accounts counted once however many of their bills were paid. An account
    /// belongs to one town (its number's code), so the towns' accounts add up to the period's.
    /// </summary>
    public class CollectionTotalsRow
    {
        /// <summary>A <see cref="Enums.CollectionTownEnum"/> value.</summary>
        public int Town { get; set; }

        public int Bills { get; set; }

        public int Accounts { get; set; }

        public decimal Amount { get; set; }
    }
}
