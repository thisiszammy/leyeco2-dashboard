namespace Leyeco2Dashboard.Data.Enums
{
    /// <summary>
    /// The town of an account whose bill the legacy collections ledger (<c>Collection_Transactions_History</c>)
    /// records as paid. The ledger has no column for it: the town is the code an account number begins with
    /// (<c>01-0095-0003</c> is Tacloban's), read by <see cref="CollectionTowns.SqlCase"/>. A named town's number is
    /// its code; the numbers are also the order the dashboard draws the towns in, Others last.
    /// </summary>
    public enum CollectionTownEnum
    {
        Tacloban = 1,
        Palo = 2,
        Babatngon = 3,
        Others = 4
    }

    public static class CollectionTowns
    {
        /// <summary>The towns in their drawing order.</summary>
        public static readonly IReadOnlyList<CollectionTownEnum> All =
        [
            CollectionTownEnum.Tacloban,
            CollectionTownEnum.Palo,
            CollectionTownEnum.Babatngon,
            CollectionTownEnum.Others,
        ];

        /// <summary>
        /// The one place an account number becomes a town, as T-SQL over the ledger's <c>account_no</c> column
        /// aliased <c>c</c>: its first two characters are the town's code, "01" Tacloban, "02" Palo, "03" Babatngon,
        /// and any other code is Others. <see cref="Code"/> names the same codes. <c>account_no</c> is half the
        /// table's clustered key, so every index of the table carries it and reading it costs no lookup.
        /// </summary>
        public const string SqlCase =
            "CASE LEFT(c.account_no, 2) " +
            "WHEN '01' THEN 1 " +
            "WHEN '02' THEN 2 " +
            "WHEN '03' THEN 3 " +
            "ELSE 4 END";

        public static string Key(this CollectionTownEnum town) => town switch
        {
            CollectionTownEnum.Tacloban  => "tacloban",
            CollectionTownEnum.Palo      => "palo",
            CollectionTownEnum.Babatngon => "babatngon",
            CollectionTownEnum.Others    => "others",
            _                            => town.ToString().ToLowerInvariant(),
        };

        public static string Label(this CollectionTownEnum town) => town switch
        {
            CollectionTownEnum.Tacloban  => "Tacloban",
            CollectionTownEnum.Palo      => "Palo",
            CollectionTownEnum.Babatngon => "Babatngon",
            CollectionTownEnum.Others    => "Others",
            _                            => town.ToString(),
        };

        /// <summary>The two digits the town's account numbers begin with; null for Others, which takes the rest.</summary>
        public static string? Code(this CollectionTownEnum town) => town switch
        {
            CollectionTownEnum.Tacloban  => "01",
            CollectionTownEnum.Palo      => "02",
            CollectionTownEnum.Babatngon => "03",
            _                            => null,
        };
    }
}
