using Leyeco2Dashboard.Data.Reports;

namespace Leyeco2Dashboard.Data.Interfaces.Leyeco2OTF
{
    /// <summary>
    /// The Transaction Inspector's reads of the online payment facility (Leyeco2OTF): every bill of the payments made
    /// from <c>from</c> up to, not including, <c>until</c> (<c>CreatedOn</c>), row by row, and what the facility's
    /// posting queue holds for them.
    /// </summary>
    public interface IOnlinePaymentInspectionRepository
    {
        Task<List<OnlinePaymentBillRow>> GetBillsAsync(DateTime from, DateTime until, CancellationToken ct = default);

        /// <summary>Every queue row of a payment made in the period, however long after it was queued.</summary>
        Task<List<PostedPaymentQueueRow>> GetQueueEntriesAsync(DateTime from, DateTime until, CancellationToken ct = default);
    }
}
