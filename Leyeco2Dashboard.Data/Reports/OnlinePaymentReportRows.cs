namespace Leyeco2Dashboard.Data.Reports
{
    // The rows the online payment facility's report queries answer (OnlinePaymentReportRepository), read by column
    // name off hand-written SQL as the collections rows are.

    /// <summary>
    /// The facility's payments, one transaction counted once however many bills it settled, summed by the day and
    /// hour it was made, its outcome, its provider and how many bills it carried.
    /// </summary>
    public class OnlinePaymentSliceRow
    {
        public DateTime Day { get; set; }

        public int Hour { get; set; }

        /// <summary>The facility's own word: SUCCESSFUL, PENDING, FAILED, CANCELLED or DROPPED.</summary>
        public string PaymentStatus { get; set; } = string.Empty;

        /// <summary>The facility's <c>PaymentProviders.Id</c>, as the transactions store it (text).</summary>
        public string ProviderId { get; set; } = string.Empty;

        /// <summary>How many bills each transaction of the slice settled: 1, 2, 3, or 4 for four or more.</summary>
        public int BillBand { get; set; }

        public int Transactions { get; set; }

        public int Bills { get; set; }

        /// <summary>Each bill's amount and surcharge, rounded to the centavo as the facility's own summary rounds them.</summary>
        public decimal Amount { get; set; }

        public decimal Surcharge { get; set; }
    }

    /// <summary>A period's successful payments.</summary>
    public class OnlinePaymentTotalsRow
    {
        public int Transactions { get; set; }

        public int Bills { get; set; }

        public decimal Amount { get; set; }
    }

    /// <summary>A payment provider as the facility lists it.</summary>
    public class PaymentProviderRow
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }
}
