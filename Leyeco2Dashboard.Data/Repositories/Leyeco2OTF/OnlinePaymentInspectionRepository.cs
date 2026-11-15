using System.Data;
using Leyeco2Dashboard.Data.Interfaces.Leyeco2OTF;
using Leyeco2Dashboard.Data.Reports;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OnlinePaymentsContext = Leyeco2Dashboard.Data.Entities.Leyeco2OTF.DatabaseContext;

namespace Leyeco2Dashboard.Data.Repositories.Leyeco2OTF
{
    /// <summary>
    /// The Transaction Inspector's reads of the facility, on the dashboard's terms (<c>OnlinePaymentReportRepository</c>):
    /// one range of <c>CreatedOn</c> compared bare against <c>datetime2</c> parameters of the column's own type,
    /// <c>NOLOCK</c>, <c>OPTION (RECOMPILE)</c>, a context of its own per call, no writes.
    /// <para>
    /// Unlike the dashboard it reads bills, not sums: a row per bill, with the columns an inspection shows. The queue is
    /// read by the payments the period holds (their <c>TransactionId</c>), not by when it was written, so a bill queued
    /// again days later is still found. <c>Scripts/online-payments-performance.sql</c> gives the queue the index that
    /// makes that a seek.
    /// </para>
    /// </summary>
    public class OnlinePaymentInspectionRepository : IOnlinePaymentInspectionRepository
    {
        private const string Range = "x.CreatedOn >= @from AND x.CreatedOn < @until";

        // The amounts are rounded to the centavo here, as the facility's own summary adds them, so every figure the
        // inspector shows or compares is one the facility would show.
        private static readonly string BillsSql = $"""
            SELECT x.Id AS Id,
                   x.TransactionId AS TransactionId,
                   x.AccountNumber AS AccountNumber,
                   x.BillNumber AS BillNumber,
                   x.ServicePeriodEnd AS ServicePeriodEnd,
                   x.PaymentStatus AS PaymentStatus,
                   x.PaymentProviderId AS PaymentProviderId,
                   ROUND(x.Amount, 2) AS Amount,
                   ROUND(x.Surcharge, 2) AS Surcharge,
                   x.CreatedOn AS CreatedOn,
                   x.LastModifiedOn AS LastModifiedOn,
                   x.ReferenceNumber AS ReferenceNumber,
                   x.Remarks AS Remarks
            FROM dbo.Transactions AS x WITH (NOLOCK)
            WHERE {Range}
            OPTION (RECOMPILE)
            """;

        private static readonly string QueueSql = $"""
            SELECT q.Id AS Id,
                   q.TransactionId AS TransactionId,
                   q.AccountNumber AS AccountNumber,
                   q.BillNumber AS BillNumber,
                   q.ServicePeriodEnd AS ServicePeriodEnd,
                   q.ORNo AS OrNumber,
                   q.ORDate AS OrDate,
                   q.Username AS Username,
                   ROUND(q.Amount, 2) AS Amount,
                   ROUND(q.Surcharge, 2) AS Surcharge,
                   q.[Timestamp] AS QueuedOn,
                   q.Status AS Status,
                   q.Remarks AS Remarks
            FROM dbo.PostedPaymentsQueue AS q WITH (NOLOCK)
            WHERE q.TransactionId IN (SELECT x.TransactionId
                                      FROM dbo.Transactions AS x WITH (NOLOCK)
                                      WHERE {Range} AND x.TransactionId IS NOT NULL)
            OPTION (RECOMPILE)
            """;

        private readonly IDbContextFactory<OnlinePaymentsContext> _contextFactory;

        public OnlinePaymentInspectionRepository(IDbContextFactory<OnlinePaymentsContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<OnlinePaymentBillRow>> GetBillsAsync(DateTime from, DateTime until, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);
            return await context.Database.SqlQueryRaw<OnlinePaymentBillRow>(BillsSql, RangeParameters(from, until)).ToListAsync(ct);
        }

        public async Task<List<PostedPaymentQueueRow>> GetQueueEntriesAsync(DateTime from, DateTime until, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);
            return await context.Database.SqlQueryRaw<PostedPaymentQueueRow>(QueueSql, RangeParameters(from, until)).ToListAsync(ct);
        }

        private static object[] RangeParameters(DateTime from, DateTime until) =>
        [
            new SqlParameter("@from", SqlDbType.DateTime2) { Value = from },
            new SqlParameter("@until", SqlDbType.DateTime2) { Value = until },
        ];
    }
}
