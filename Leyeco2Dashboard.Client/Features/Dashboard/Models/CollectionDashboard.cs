namespace Leyeco2Dashboard.Client.Features.Dashboard.Models
{
    /// <summary>
    /// The overall dashboard as <c>GET api/dashboards/collections</c> answers it (the API's
    /// <c>CollectionDashboardDTO</c>): every bill the collections ledger records as paid in the period. The ledger
    /// keeps a row per bill and nothing of which bills were paid together, so it counts bills and accounts.
    /// </summary>
    public sealed class CollectionDashboard
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int Days { get; set; }
        public DateTime PreviousFrom { get; set; }
        public DateTime PreviousTo { get; set; }
        public bool IncludesToday { get; set; }
        public DateTime GeneratedOn { get; set; }
        public CollectionFigures Totals { get; set; } = new();
        public CollectionFigures Previous { get; set; } = new();

        /// <summary>Teller, SBA, ECPay, Bayad Center, SM Payment, Bizlink, in that order; the points' arrays follow it.</summary>
        public List<CollectionChannel> Channels { get; set; } = new();

        /// <summary>Tacloban, Palo, Babatngon, Others, in that order: the code each account number begins with.</summary>
        public List<CollectionTown> Towns { get; set; } = new();

        public List<CollectionPoint> Daily { get; set; } = new();
        public List<CollectionPoint> Hourly { get; set; } = new();
        public int UntimedBills { get; set; }
        public List<Collector> Collectors { get; set; } = new();
    }

    public sealed class CollectionFigures
    {
        public int Bills { get; set; }
        public int Accounts { get; set; }
        public decimal Amount { get; set; }
        public decimal Surcharge { get; set; }
        public int SurchargedBills { get; set; }
    }

    public sealed class CollectionChannel
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Bills { get; set; }
        public decimal Amount { get; set; }
        public decimal Surcharge { get; set; }
        public DateTime? LastPostedOn { get; set; }
    }

    /// <summary>The bills of one town's accounts; <see cref="Amounts"/> and <see cref="Bills"/> follow the channels.</summary>
    public sealed class CollectionTown
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;

        /// <summary>"01", "02", "03"; null for Others.</summary>
        public string? Code { get; set; }

        public CollectionFigures Totals { get; set; } = new();
        public CollectionFigures Previous { get; set; } = new();
        public decimal[] Amounts { get; set; } = [];
        public int[] Bills { get; set; } = [];
    }

    public sealed class CollectionPoint
    {
        public DateTime? Date { get; set; }
        public int? Hour { get; set; }
        public decimal[] Amounts { get; set; } = [];
        public int[] Bills { get; set; } = [];
    }

    public sealed class Collector
    {
        public string Username { get; set; } = string.Empty;
        public string? CollectorId { get; set; }
        public string Channel { get; set; } = string.Empty;
        public int Bills { get; set; }
        public decimal Amount { get; set; }
        public DateTime? LastPostedOn { get; set; }
    }
}
