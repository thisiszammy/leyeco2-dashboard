using Leyeco2Dashboard.Data.Reports;

namespace Leyeco2Dashboard.Data.Interfaces.Legacy
{
    /// <summary>
    /// Read-only figures from the legacy collections ledger (<c>Collection_Transactions_History</c>), one bill a row.
    /// Every query reads the bills paid from <paramref name="from"/> up to, not including, <paramref name="until"/>
    /// (dates, midnight to midnight).
    /// </summary>
    public interface ICollectionReportRepository
    {
        Task<List<CollectionSliceRow>> GetSlicesAsync(DateTime from, DateTime until, CancellationToken ct = default);

        Task<List<CollectorRow>> GetCollectorsAsync(DateTime from, DateTime until, CancellationToken ct = default);

        /// <summary>A row per town that had a bill paid in the period; a town with none has no row.</summary>
        Task<List<CollectionTotalsRow>> GetTotalsByTownAsync(DateTime from, DateTime until, CancellationToken ct = default);
    }
}
