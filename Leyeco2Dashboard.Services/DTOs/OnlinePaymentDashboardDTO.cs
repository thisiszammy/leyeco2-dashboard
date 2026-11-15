namespace Leyeco2Dashboard.Services.DTOs
{
    /// <summary>
    /// The Online Payments dashboard: the payments made through the online payment facility in the period. A
    /// payment (a transaction) may settle several bills at once, so its figures count both: transactions, each
    /// once, and the bills they settled.
    /// </summary>
    public class OnlinePaymentDashboardDTO
    {
        public DateTime From { get; set; }

        public DateTime To { get; set; }

        public int Days { get; set; }

        public DateTime PreviousFrom { get; set; }

        public DateTime PreviousTo { get; set; }

        public bool IncludesToday { get; set; }

        public DateTime GeneratedOn { get; set; }

        /// <summary>The payments that went through.</summary>
        public OnlinePaymentFiguresDTO Successful { get; set; } = new();

        /// <summary>The same, for the period of as many days just before this one.</summary>
        public OnlinePaymentFiguresDTO PreviousSuccessful { get; set; } = new();

        /// <summary>Every transaction begun in the period, however it ended.</summary>
        public int Attempted { get; set; }

        /// <summary>Successful, Pending, Failed or cancelled, Dropped (and Other, only when some status is none of
        /// those), always in that order, which <see cref="OnlinePaymentPointDTO.Transactions"/> follows.</summary>
        public List<OnlinePaymentOutcomeDTO> Outcomes { get; set; } = new();

        /// <summary>Every provider that carried a payment in the period, by its number, which
        /// <see cref="OnlinePaymentPointDTO.Amounts"/> follows.</summary>
        public List<OnlinePaymentProviderDTO> Providers { get; set; } = new();

        /// <summary>Every day of the period, those with no payments included.</summary>
        public List<OnlinePaymentPointDTO> Daily { get; set; } = new();

        /// <summary>The 24 hours of the day, summed over the period.</summary>
        public List<OnlinePaymentPointDTO> Hourly { get; set; } = new();

        /// <summary>The successful payments by how many bills each settled: 1, 2, 3, and 4 or more.</summary>
        public List<OnlinePaymentBillBandDTO> BillsPerTransaction { get; set; } = new();
    }

    public class OnlinePaymentFiguresDTO
    {
        public int Transactions { get; set; }

        public int Bills { get; set; }

        /// <summary>The bills' amounts with their surcharges.</summary>
        public decimal Amount { get; set; }

        public decimal Surcharge { get; set; }
    }

    public class OnlinePaymentOutcomeDTO
    {
        /// <summary>"successful", "pending", "failed", "dropped" or "other".</summary>
        public string Key { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public int Transactions { get; set; }

        public int Bills { get; set; }

        public decimal Amount { get; set; }
    }

    public class OnlinePaymentProviderDTO
    {
        /// <summary>The facility's provider number, as text.</summary>
        public string Key { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        /// <summary>Successful payments only.</summary>
        public int Transactions { get; set; }

        public int Bills { get; set; }

        public decimal Amount { get; set; }

        /// <summary>Every transaction the provider carried, however it ended.</summary>
        public int Attempted { get; set; }
    }

    /// <summary>One day (<see cref="Date"/>) or one hour of the day (<see cref="Hour"/>).</summary>
    public class OnlinePaymentPointDTO
    {
        public DateTime? Date { get; set; }

        public int? Hour { get; set; }

        /// <summary>Successful amounts per provider, in <see cref="OnlinePaymentDashboardDTO.Providers"/>' order.</summary>
        public decimal[] Amounts { get; set; } = [];

        /// <summary>Transactions per outcome, in <see cref="OnlinePaymentDashboardDTO.Outcomes"/>' order.</summary>
        public int[] Transactions { get; set; } = [];
    }

    public class OnlinePaymentBillBandDTO
    {
        /// <summary>1, 2, 3, or 4 for four or more.</summary>
        public int Bills { get; set; }

        public int Transactions { get; set; }

        public decimal Amount { get; set; }
    }
}
