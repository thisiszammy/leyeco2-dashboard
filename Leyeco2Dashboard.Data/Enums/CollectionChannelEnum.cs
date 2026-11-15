namespace Leyeco2Dashboard.Data.Enums
{
    /// <summary>
    /// Where a bill in the legacy collections ledger (<c>Collection_Transactions_History</c>) was paid. The ledger
    /// has no column for it: the channel is read off the account that posted the payment (its <c>username</c>),
    /// by <see cref="CollectionChannels.SqlCase"/>. The numbers are the order the dashboard draws the channels in.
    /// </summary>
    public enum CollectionChannelEnum
    {
        Teller = 1,
        Sba = 2,
        ECPay = 3,
        BayadCenter = 4,
        SmPayment = 5,
        Bizlink = 6
    }

    public static class CollectionChannels
    {
        /// <summary>The channels in their drawing order.</summary>
        public static readonly IReadOnlyList<CollectionChannelEnum> All =
        [
            CollectionChannelEnum.Teller,
            CollectionChannelEnum.Sba,
            CollectionChannelEnum.ECPay,
            CollectionChannelEnum.BayadCenter,
            CollectionChannelEnum.SmPayment,
            CollectionChannelEnum.Bizlink,
        ];

        /// <summary>
        /// The one place a username becomes a channel, as T-SQL over the ledger's <c>username</c> column aliased
        /// <c>c</c>: an SBA account's name starts with "sba", ECPay posts as "ecpay", Bayad Center as "bayadctr" or
        /// "bayadcntr", SM Payment as "smpayment" or "smpayments", Bizlink as "bizlink" or "linkbiz", and every other
        /// account is a teller's. A partner posts under either of its names (the live ledger and its copies differ),
        /// and the ledger's collation is case-insensitive, so "SBA…" is SBA too.
        /// </summary>
        public const string SqlCase =
            "CASE WHEN c.username LIKE 'sba%' THEN 2 " +
            "WHEN c.username = 'ecpay' THEN 3 " +
            "WHEN c.username IN ('bayadctr', 'bayadcntr') THEN 4 " +
            "WHEN c.username IN ('smpayment', 'smpayments') THEN 5 " +
            "WHEN c.username IN ('bizlink', 'linkbiz') THEN 6 " +
            "ELSE 1 END";

        public static string Key(this CollectionChannelEnum channel) => channel switch
        {
            CollectionChannelEnum.Teller      => "teller",
            CollectionChannelEnum.Sba         => "sba",
            CollectionChannelEnum.ECPay       => "ecpay",
            CollectionChannelEnum.BayadCenter => "bayadcenter",
            CollectionChannelEnum.SmPayment   => "smpayment",
            CollectionChannelEnum.Bizlink     => "bizlink",
            _                                 => channel.ToString().ToLowerInvariant(),
        };

        public static string Label(this CollectionChannelEnum channel) => channel switch
        {
            CollectionChannelEnum.Teller      => "Teller",
            CollectionChannelEnum.Sba         => "SBA",
            CollectionChannelEnum.ECPay       => "ECPay",
            CollectionChannelEnum.BayadCenter => "Bayad Center",
            CollectionChannelEnum.SmPayment   => "SM Payment",
            CollectionChannelEnum.Bizlink     => "Bizlink",
            _                                 => channel.ToString(),
        };
    }
}
