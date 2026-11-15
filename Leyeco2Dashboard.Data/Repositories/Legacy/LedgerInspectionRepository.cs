using System.Data;
using System.Globalization;
using System.Security;
using System.Text;
using Leyeco2Dashboard.Data.Enums;
using Leyeco2Dashboard.Data.Interfaces.Legacy;
using Leyeco2Dashboard.Data.Reports;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using LegacyContext = Leyeco2Dashboard.Data.Entities.Legacy.DatabaseContext;

namespace Leyeco2Dashboard.Data.Repositories.Legacy
{
    /// <summary>
    /// The Transaction Inspector's reads of the legacy cashiering database, on the dashboard's terms
    /// (<c>CollectionReportRepository</c>): <c>NOLOCK</c> so a teller's posting never waits on it, <c>OPTION (RECOMPILE)</c>,
    /// a context of its own per call, no writes.
    /// <para>
    /// A bill is looked up by the ledger's own clustered key (<c>ServicePeriodEnd</c>, <c>account_no</c>), which is also
    /// how the facility's own reconciliation tool matches a payment to its posting. The bills asked about travel as one
    /// XML parameter (the ledger runs at compatibility level 90: no table-valued JSON, no STRING_SPLIT), are shredded into
    /// a keyed temporary table, and drive a nested-loop join into the ledger, one clustered seek a bill and nothing
    /// scanned: on the local copy a thousand bills read 6,620 pages in 32 ms. The join hint keeps that plan however the
    /// estimate turns out, since a scan of the ledger is the one plan it must never take. The temporary table takes the
    /// database's collation, so its key compares with <c>account_no</c> as the column itself does.
    /// </para>
    /// <para>
    /// Names come from the two tables the facility's <c>Consumers2</c> view unions, active consumers
    /// (<c>Lib_Consumers</c>, keyed on the account) and disconnected ones (<c>Disconnected</c>, indexed on it), read by
    /// key: the view's DISTINCT over both and its subquery per row would cost a scan of every consumer to name a few.
    /// </para>
    /// </summary>
    public class LedgerInspectionRepository : ILedgerInspectionRepository
    {
        /// <summary>The ledger's account numbers are varchar(12): a longer one can't be among them.</summary>
        public const int AccountNumberLength = 12;

        /// <summary>Bills per query: a month's are a few batches, each statement short and its parameter small.</summary>
        private const int BatchSize = 4000;

        private static readonly string EntriesSql = $"""
            SET NOCOUNT ON;
            IF OBJECT_ID(N'tempdb..#bills') IS NOT NULL DROP TABLE #bills;
            CREATE TABLE #bills (
                ServicePeriodEnd datetime NOT NULL,
                account_no varchar(12) COLLATE DATABASE_DEFAULT NOT NULL,
                PRIMARY KEY (ServicePeriodEnd, account_no));
            INSERT INTO #bills (ServicePeriodEnd, account_no)
            SELECT DISTINCT CONVERT(datetime, b.value('@s', 'char(8)'), 112), b.value('@a', 'varchar(12)')
            FROM @bills.nodes('/b') AS t(b);
            SELECT c.account_no AS AccountNumber,
                   c.ServicePeriodEnd AS ServicePeriodEnd,
                   c.bill_no AS BillNumber,
                   c.or_no AS OrNumber,
                   c.or_date AS OrDate,
                   c.date_paid AS DatePaid,
                   c.amount_paid AS AmountPaid,
                   c.surcharge AS Surcharge,
                   c.username AS Username,
                   c.posted_by AS PostedBy,
                   c.collector_id AS CollectorId,
                   c.[timestamp] AS PostedOn,
                   c.remarks AS Remarks,
                   {CollectionChannels.SqlCase} AS Channel
            FROM #bills AS k
            INNER LOOP JOIN dbo.Collection_Transactions_History AS c WITH (NOLOCK)
                ON c.ServicePeriodEnd = k.ServicePeriodEnd AND c.account_no = k.account_no
            OPTION (RECOMPILE);
            """;

        private const string NamesSql = """
            SET NOCOUNT ON;
            IF OBJECT_ID(N'tempdb..#accounts') IS NOT NULL DROP TABLE #accounts;
            CREATE TABLE #accounts (account_no varchar(12) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY);
            INSERT INTO #accounts (account_no)
            SELECT DISTINCT a.value('@a', 'varchar(12)')
            FROM @accounts.nodes('/b') AS t(a);
            SELECT a.account_no AS AccountNumber,
                   COALESCE(l.consumer_name, d.consumer_name) AS Name,
                   CAST(CASE WHEN l.account_no IS NULL AND d.account_no IS NOT NULL THEN 1 ELSE 0 END AS bit) AS IsDisconnected
            FROM #accounts AS a
            LEFT JOIN dbo.Lib_Consumers AS l WITH (NOLOCK) ON l.account_no = a.account_no
            OUTER APPLY (SELECT TOP (1) x.account_no, x.consumer_name
                         FROM dbo.Disconnected AS x WITH (NOLOCK)
                         WHERE x.account_no = a.account_no) AS d
            WHERE l.account_no IS NOT NULL OR d.account_no IS NOT NULL
            OPTION (RECOMPILE);
            """;

        private readonly IDbContextFactory<LegacyContext> _contextFactory;

        public LedgerInspectionRepository(IDbContextFactory<LegacyContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<LedgerEntryRow>> GetEntriesAsync(IReadOnlyCollection<LedgerBillKey> bills, CancellationToken ct = default)
        {
            var keys = bills
                .Where(bill => IsLedgerAccount(bill.AccountNumber))
                .Distinct()
                .ToList();

            var entries = new List<LedgerEntryRow>();
            if (keys.Count == 0)
                return entries;

            await using var context = await _contextFactory.CreateDbContextAsync(ct);
            foreach (var batch in keys.Chunk(BatchSize))
            {
                var xml = Xml(batch.Select(key => (key.AccountNumber, (DateTime?)key.ServicePeriodEnd)));
                var parameter = new SqlParameter("@bills", SqlDbType.Xml) { Value = xml };
                entries.AddRange(await context.Database.SqlQueryRaw<LedgerEntryRow>(EntriesSql, parameter).ToListAsync(ct));
            }

            return entries;
        }

        public async Task<List<ConsumerNameRow>> GetConsumerNamesAsync(IReadOnlyCollection<string> accountNumbers, CancellationToken ct = default)
        {
            var accounts = accountNumbers
                .Where(IsLedgerAccount)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var names = new List<ConsumerNameRow>();
            if (accounts.Count == 0)
                return names;

            await using var context = await _contextFactory.CreateDbContextAsync(ct);
            foreach (var batch in accounts.Chunk(BatchSize))
            {
                var xml = Xml(batch.Select(account => (account, (DateTime?)null)));
                var parameter = new SqlParameter("@accounts", SqlDbType.Xml) { Value = xml };
                names.AddRange(await context.Database.SqlQueryRaw<ConsumerNameRow>(NamesSql, parameter).ToListAsync(ct));
            }

            return names;
        }

        private static bool IsLedgerAccount(string account)
            => !string.IsNullOrWhiteSpace(account) && account.Length <= AccountNumberLength;

        /// <summary>
        /// <c>&lt;b a="01-0095-0003" s="20260401"/&gt;…</c>: a fragment, one element a bill, its period as yyyyMMdd so
        /// the server reads it with a fixed style whatever its language.
        /// </summary>
        private static string Xml(IEnumerable<(string Account, DateTime? Period)> bills)
        {
            var xml = new StringBuilder();
            foreach (var (account, period) in bills)
            {
                xml.Append("<b a=\"").Append(SecurityElement.Escape(account)).Append('"');
                if (period is DateTime date)
                    xml.Append(" s=\"").Append(date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)).Append('"');
                xml.Append("/>");
            }

            return xml.ToString();
        }
    }
}
