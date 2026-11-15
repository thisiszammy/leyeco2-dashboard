using System.Data;
using Leyeco2Dashboard.Data.Interfaces.Leyeco2OTF;
using Leyeco2Dashboard.Data.Reports;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OnlinePaymentsContext = Leyeco2Dashboard.Data.Entities.Leyeco2OTF.DatabaseContext;

namespace Leyeco2Dashboard.Data.Repositories.Leyeco2OTF
{
    /// <summary>
    /// The Online Payments dashboard's reads of the facility's <c>Transactions</c>, as T-SQL on the collections
    /// ledger's terms (<c>CollectionReportRepository</c>): one range of <c>CreatedOn</c> compared bare, against
    /// <c>datetime2</c> parameters of the column's own type; <c>NOLOCK</c>, so the facility posting a provider's
    /// answer never waits on a dashboard; <c>OPTION (RECOMPILE)</c>; a context of its own per call; no writes.
    /// <para>
    /// A payment is its <c>TransactionId</c>, not its rows: the inner query folds each transaction's bills into one
    /// row (when it was made, how it ended, who carried it, how many bills, how much) before anything is counted, so
    /// a payment of three bills counts once among the transactions and three times among the bills. A row with no
    /// <c>TransactionId</c> stands alone. Amounts are each bill's amount plus its surcharge, each rounded to the
    /// centavo, as the facility's own summary adds them.
    /// </para>
    /// </summary>
    public class OnlinePaymentReportRepository : IOnlinePaymentReportRepository
    {
        private const string TransactionKey = "ISNULL(x.TransactionId, N'#' + CAST(x.Id AS nvarchar(12)))";

        private const string Range = "x.CreatedOn >= @from AND x.CreatedOn < @until";

        private const string BillBand = "CASE WHEN t.Bills > 4 THEN 4 ELSE t.Bills END";

        private const string Day = "DATEADD(day, DATEDIFF(day, 0, t.CreatedOn), 0)";

        private static readonly string SlicesSql = $"""
            WITH t AS (
                SELECT {TransactionKey} AS TransactionKey,
                       MIN(x.CreatedOn) AS CreatedOn,
                       MAX(x.PaymentStatus) AS PaymentStatus,
                       MAX(x.PaymentProviderId) AS ProviderId,
                       COUNT(*) AS Bills,
                       SUM(ROUND(x.Amount, 2) + ROUND(x.Surcharge, 2)) AS Amount,
                       SUM(ROUND(x.Surcharge, 2)) AS Surcharge
                FROM dbo.Transactions AS x WITH (NOLOCK)
                WHERE {Range}
                GROUP BY {TransactionKey}
            )
            SELECT {Day} AS Day,
                   DATEPART(hour, t.CreatedOn) AS Hour,
                   ISNULL(t.PaymentStatus, N'') AS PaymentStatus,
                   ISNULL(t.ProviderId, N'') AS ProviderId,
                   {BillBand} AS BillBand,
                   COUNT(*) AS Transactions,
                   SUM(t.Bills) AS Bills,
                   SUM(t.Amount) AS Amount,
                   SUM(t.Surcharge) AS Surcharge
            FROM t
            GROUP BY {Day}, DATEPART(hour, t.CreatedOn), ISNULL(t.PaymentStatus, N''), ISNULL(t.ProviderId, N''), {BillBand}
            OPTION (RECOMPILE)
            """;

        private static readonly string SuccessfulTotalsSql = $"""
            SELECT COUNT(DISTINCT {TransactionKey}) AS Transactions,
                   COUNT(*) AS Bills,
                   ISNULL(SUM(ROUND(x.Amount, 2) + ROUND(x.Surcharge, 2)), 0) AS Amount
            FROM dbo.Transactions AS x WITH (NOLOCK)
            WHERE {Range} AND x.PaymentStatus = N'SUCCESSFUL'
            OPTION (RECOMPILE)
            """;

        // The names only: the table also keeps each provider's secret key, which nothing here has any use for.
        private const string ProvidersSql = "SELECT p.Id AS Id, p.Name AS Name FROM dbo.PaymentProviders AS p WITH (NOLOCK)";

        private readonly IDbContextFactory<OnlinePaymentsContext> _contextFactory;

        public OnlinePaymentReportRepository(IDbContextFactory<OnlinePaymentsContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<OnlinePaymentSliceRow>> GetSlicesAsync(DateTime from, DateTime until, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);
            return await context.Database.SqlQueryRaw<OnlinePaymentSliceRow>(SlicesSql, RangeParameters(from, until)).ToListAsync(ct);
        }

        public async Task<OnlinePaymentTotalsRow> GetSuccessfulTotalsAsync(DateTime from, DateTime until, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);
            var rows = await context.Database.SqlQueryRaw<OnlinePaymentTotalsRow>(SuccessfulTotalsSql, RangeParameters(from, until)).ToListAsync(ct);
            return rows.FirstOrDefault() ?? new OnlinePaymentTotalsRow();
        }

        public async Task<List<PaymentProviderRow>> GetProvidersAsync(CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);
            return await context.Database.SqlQueryRaw<PaymentProviderRow>(ProvidersSql).ToListAsync(ct);
        }

        private static object[] RangeParameters(DateTime from, DateTime until) =>
        [
            new SqlParameter("@from", SqlDbType.DateTime2) { Value = from },
            new SqlParameter("@until", SqlDbType.DateTime2) { Value = until },
        ];
    }
}
