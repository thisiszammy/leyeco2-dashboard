namespace Leyeco2Dashboard.Services.DTOs
{
    /// <summary>
    /// The overall dashboard: every bill the collections ledger records as paid in the period, whichever channel
    /// took it. The ledger keeps one row per bill and no trace of which bills were paid together, so its figures
    /// count bills and accounts, never payments.
    /// </summary>
    public class CollectionDashboardDTO
    {
        public DateTime From { get; set; }

        public DateTime To { get; set; }

        public int Days { get; set; }

        /// <summary>The period of as many days just before this one, which <see cref="Previous"/> sums.</summary>
        public DateTime PreviousFrom { get; set; }

        public DateTime PreviousTo { get; set; }

        public bool IncludesToday { get; set; }

        /// <summary>When the ledger was read: the figures are kept a minute (a past period, a quarter hour).</summary>
        public DateTime GeneratedOn { get; set; }

        public CollectionFiguresDTO Totals { get; set; } = new();

        public CollectionFiguresDTO Previous { get; set; } = new();

        /// <summary>Teller, SBA, ECPay, Bayad Center, SM Payment and Bizlink, always all six and in that order
        /// (<c>CollectionChannels.All</c>), which every <see cref="CollectionPointDTO"/>'s arrays and every
        /// <see cref="CollectionTownDTO"/>'s follow.</summary>
        public List<CollectionChannelDTO> Channels { get; set; } = new();

        /// <summary>Tacloban, Palo, Babatngon and Others, always all four and in that order
        /// (<c>CollectionTowns.All</c>): the town is the code each account number begins with.</summary>
        public List<CollectionTownDTO> Towns { get; set; } = new();

        /// <summary>Every day of the period, those with no collections included.</summary>
        public List<CollectionPointDTO> Daily { get; set; } = new();

        /// <summary>The 24 hours of the day, summed over the period, by the time each payment was posted.</summary>
        public List<CollectionPointDTO> Hourly { get; set; } = new();

        /// <summary>Bills the ledger holds no posting time for, left out of <see cref="Hourly"/>.</summary>
        public int UntimedBills { get; set; }

        /// <summary>Every account that posted payments in the period, the largest amount first.</summary>
        public List<CollectorDTO> Collectors { get; set; } = new();
    }

    public class CollectionFiguresDTO
    {
        public int Bills { get; set; }

        /// <summary>Accounts with at least one bill paid, each counted once.</summary>
        public int Accounts { get; set; }

        public decimal Amount { get; set; }

        public decimal Surcharge { get; set; }

        /// <summary>Bills paid late, with a surcharge.</summary>
        public int SurchargedBills { get; set; }
    }

    public class CollectionChannelDTO
    {
        /// <summary>"teller", "sba", "ecpay", "bayadcenter", "smpayment" or "bizlink".</summary>
        public string Key { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public int Bills { get; set; }

        public decimal Amount { get; set; }

        public decimal Surcharge { get; set; }

        /// <summary>The channel's latest posting time in the period; null when it posted nothing.</summary>
        public DateTime? LastPostedOn { get; set; }
    }

    /// <summary>The bills of one town's accounts: its figures, the previous period's, and how they were paid.</summary>
    public class CollectionTownDTO
    {
        /// <summary>"tacloban", "palo", "babatngon" or "others".</summary>
        public string Key { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        /// <summary>The two digits its account numbers begin with ("01"); null for Others, which takes the rest.</summary>
        public string? Code { get; set; }

        public CollectionFiguresDTO Totals { get; set; } = new();

        /// <summary>The previous period's bills, accounts and amount (<see cref="CollectionDashboardDTO.PreviousFrom"/>).</summary>
        public CollectionFiguresDTO Previous { get; set; } = new();

        /// <summary>The amount paid through each channel, in <see cref="CollectionDashboardDTO.Channels"/>' order.</summary>
        public decimal[] Amounts { get; set; } = [];

        /// <summary>The bills paid through each channel, in the same order.</summary>
        public int[] Bills { get; set; } = [];
    }

    /// <summary>One day (<see cref="Date"/>) or one hour of the day (<see cref="Hour"/>), split by channel.</summary>
    public class CollectionPointDTO
    {
        public DateTime? Date { get; set; }

        public int? Hour { get; set; }

        /// <summary>Per channel, in <see cref="CollectionDashboardDTO.Channels"/>' order.</summary>
        public decimal[] Amounts { get; set; } = [];

        public int[] Bills { get; set; } = [];
    }

    public class CollectorDTO
    {
        public string Username { get; set; } = string.Empty;

        /// <summary>The ledger's two-character collector code, when the account has one.</summary>
        public string? CollectorId { get; set; }

        /// <summary>The channel's key (<see cref="CollectionChannelDTO.Key"/>).</summary>
        public string Channel { get; set; } = string.Empty;

        public int Bills { get; set; }

        public decimal Amount { get; set; }

        public DateTime? LastPostedOn { get; set; }
    }
}
