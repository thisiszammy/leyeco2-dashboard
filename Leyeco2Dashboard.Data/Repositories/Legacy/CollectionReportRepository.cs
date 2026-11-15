using System.Data;
using Leyeco2Dashboard.Data.Enums;
using Leyeco2Dashboard.Data.Interfaces.Legacy;
using Leyeco2Dashboard.Data.Reports;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using LegacyContext = Leyeco2Dashboard.Data.Entities.Legacy.DatabaseContext;

namespace Leyeco2Dashboard.Data.Repositories.Legacy
{
    /// <summary>
    /// The overall dashboard's reads of the legacy collections ledger, written as T-SQL because the table is the
    /// cashiering system's and has to be read on its terms:
    /// <list type="bullet">
    /// <item>It holds millions of wide rows clustered on the bill (<c>ServicePeriodEnd</c>, <c>account_no</c>), not
    /// on the day paid, so every query is one range of <c>date_paid</c>, compared bare (no function over the column)
    /// against <c>datetime</c> parameters of the column's own type, and reads only the columns
    /// <c>IX_CTH_Dashboard_DatePaid</c> carries (<c>Scripts/legacy-collections-performance.sql</c>): a seek over
    /// that index and nothing else, where the table alone would be scanned whole. <c>account_no</c>, which the
    /// town is read off, is one of them: the clustered key rides in every index.</item>
    /// <item><c>NOLOCK</c>: the tellers post into this table all day, and a dashboard must never hold a lock they
    /// wait on, nor wait on theirs. The price is that a payment still being written can be counted a moment
    /// early, which a figure refreshed every minute can bear.</item>
    /// <item><c>OPTION (RECOMPILE)</c>: one day and three months want different plans, and the compile costs
    /// less than a plan sized for the wrong one.</item>
    /// </list>
    /// Each call opens a context of its own (the factory's), so a report shared between viewers
    /// (<c>ReportCache</c>) outlives the request that started it. Nothing here writes.
    /// </summary>
    public class CollectionReportRepository : ICollectionReportRepository
    {
        private const string Range = "c.date_paid >= @from AND c.date_paid < @until";

        // The town doubles a quarter's rows (about 4,800 to 9,500 on the local copy) and nothing else: the same
        // pages are read, in the same time.
        private static readonly string SlicesSql = $"""
            SELECT k.Day AS Day,
                   DATEPART(hour, c.[timestamp]) AS Hour,
                   k.Channel AS Channel,
                   k.Town AS Town,
                   COUNT(*) AS Bills,
                   ISNULL(SUM(c.amount_paid), 0) AS Amount,
                   ISNULL(SUM(c.surcharge), 0) AS Surcharge,
                   SUM(CASE WHEN c.surcharge > 0 THEN 1 ELSE 0 END) AS SurchargedBills
            FROM dbo.Collection_Transactions_History AS c WITH (NOLOCK)
            CROSS APPLY (SELECT DATEADD(day, DATEDIFF(day, 0, c.date_paid), 0) AS Day,
                                {CollectionChannels.SqlCase} AS Channel,
                                {CollectionTowns.SqlCase} AS Town) AS k
            WHERE {Range}
            GROUP BY k.Day, DATEPART(hour, c.[timestamp]), k.Channel, k.Town
            OPTION (RECOMPILE)
            """;

        private static readonly string CollectorsSql = $"""
            SELECT ISNULL(c.username, '') AS Username,
                   MAX(c.collector_id) AS CollectorId,
                   k.Channel AS Channel,
                   COUNT(*) AS Bills,
                   ISNULL(SUM(c.amount_paid), 0) AS Amount,
                   MAX(c.[timestamp]) AS LastPostedOn
            FROM dbo.Collection_Transactions_History AS c WITH (NOLOCK)
            CROSS APPLY (SELECT {CollectionChannels.SqlCase} AS Channel) AS k
            WHERE {Range}
            GROUP BY c.username, k.Channel
            OPTION (RECOMPILE)
            """;

        // The accounts are counted by grouping on them first, not with COUNT(DISTINCT) beside the other sums, which
        // makes the server read the range twice and sort it: a quarter took 0.8 s that way and 22 ms this way. The
        // town is a function of the account number, so each account's sums fall in exactly one town's row.
        private static readonly string TotalsByTownSql = $"""
            SELECT a.Town AS Town,
                   COUNT(*) AS Accounts,
                   ISNULL(SUM(a.Bills), 0) AS Bills,
                   ISNULL(SUM(a.Amount), 0) AS Amount
            FROM (SELECT {CollectionTowns.SqlCase} AS Town, COUNT(*) AS Bills, SUM(c.amount_paid) AS Amount
                  FROM dbo.Collection_Transactions_History AS c WITH (NOLOCK)
                  WHERE {Range}
                  GROUP BY c.account_no) AS a
            GROUP BY a.Town
            OPTION (RECOMPILE)
            """;

        private readonly IDbContextFactory<LegacyContext> _contextFactory;

        public CollectionReportRepository(IDbContextFactory<LegacyContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<CollectionSliceRow>> GetSlicesAsync(DateTime from, DateTime until, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);
            return await context.Database.SqlQueryRaw<CollectionSliceRow>(SlicesSql, RangeParameters(from, until)).ToListAsync(ct);
        }

        public async Task<List<CollectorRow>> GetCollectorsAsync(DateTime from, DateTime until, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);
            return await context.Database.SqlQueryRaw<CollectorRow>(CollectorsSql, RangeParameters(from, until)).ToListAsync(ct);
        }

        public async Task<List<CollectionTotalsRow>> GetTotalsByTownAsync(DateTime from, DateTime until, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);
            return await context.Database.SqlQueryRaw<CollectionTotalsRow>(TotalsByTownSql, RangeParameters(from, until)).ToListAsync(ct);
        }

        // datetime, the column's type: a datetime2 parameter (EF's default for DateTime) would have the server convert
        // every date_paid it compares, which can cost the seek.
        private static object[] RangeParameters(DateTime from, DateTime until) =>
        [
            new SqlParameter("@from", SqlDbType.DateTime) { Value = from },
            new SqlParameter("@until", SqlDbType.DateTime) { Value = until },
        ];
    }
}
