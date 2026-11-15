using Leyeco2Dashboard.Data.Reports;

namespace Leyeco2Dashboard.Data.Interfaces.Legacy
{
    /// <summary>
    /// The Transaction Inspector's reads of the legacy cashiering database: the collections ledger's row for each bill
    /// asked about, found by its key, and the consumers' names. Read-only.
    /// </summary>
    public interface ILedgerInspectionRepository
    {
        /// <summary>The ledger's row for each bill that has one; a bill the ledger doesn't hold has none.</summary>
        Task<List<LedgerEntryRow>> GetEntriesAsync(IReadOnlyCollection<LedgerBillKey> bills, CancellationToken ct = default);

        /// <summary>The consumer of each account the cashiering system knows; an unknown account has no row.</summary>
        Task<List<ConsumerNameRow>> GetConsumerNamesAsync(IReadOnlyCollection<string> accountNumbers, CancellationToken ct = default);
    }
}
