namespace Leyeco2Dashboard.Client.Features.OnlinePayments.Models
{
    /// <summary>
    /// The Online Payments dashboard as <c>GET api/dashboards/online-payments</c> answers it (the API's
    /// <c>OnlinePaymentDashboardDTO</c>). A payment (a transaction) can settle several bills, so it counts both.
    /// </summary>
    public sealed class OnlinePaymentDashboard
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int Days { get; set; }
        public DateTime PreviousFrom { get; set; }
        public DateTime PreviousTo { get; set; }
        public bool IncludesToday { get; set; }
        public DateTime GeneratedOn { get; set; }
        public OnlinePaymentFigures Successful { get; set; } = new();
        public OnlinePaymentFigures PreviousSuccessful { get; set; } = new();
        public int Attempted { get; set; }

        /// <summary>Successful, Pending, Failed or cancelled, Dropped (and Other); the points' transactions follow it.</summary>
        public List<OnlinePaymentOutcome> Outcomes { get; set; } = new();

        /// <summary>By the facility's provider number; the points' amounts follow it.</summary>
        public List<OnlinePaymentProvider> Providers { get; set; } = new();

        public List<OnlinePaymentPoint> Daily { get; set; } = new();
        public List<OnlinePaymentPoint> Hourly { get; set; } = new();
        public List<OnlinePaymentBillBand> BillsPerTransaction { get; set; } = new();
    }

    public sealed class OnlinePaymentFigures
    {
        public int Transactions { get; set; }
        public int Bills { get; set; }
        public decimal Amount { get; set; }
        public decimal Surcharge { get; set; }
    }

    public sealed class OnlinePaymentOutcome
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Transactions { get; set; }
        public int Bills { get; set; }
        public decimal Amount { get; set; }
    }

    public sealed class OnlinePaymentProvider
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Transactions { get; set; }
        public int Bills { get; set; }
        public decimal Amount { get; set; }
        public int Attempted { get; set; }
    }

    public sealed class OnlinePaymentPoint
    {
        public DateTime? Date { get; set; }
        public int? Hour { get; set; }
        public decimal[] Amounts { get; set; } = [];
        public int[] Transactions { get; set; } = [];
    }

    public sealed class OnlinePaymentBillBand
    {
        public int Bills { get; set; }
        public int Transactions { get; set; }
        public decimal Amount { get; set; }
    }
}
