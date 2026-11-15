using Leyeco2Dashboard.Data.Reports;

namespace Leyeco2Dashboard.Data.Interfaces.Leyeco2OTF
{
    /// <summary>
    /// Read-only figures from the online payment facility (Leyeco2OTF), whose <c>Transactions</c> table holds a row
    /// per bill: the bills one payment settled share its <c>TransactionId</c>. Every query reads the payments made
    /// from <paramref name="from"/> up to, not including, <paramref name="until"/> (<c>CreatedOn</c>).
    /// </summary>
    public interface IOnlinePaymentReportRepository
    {
        Task<List<OnlinePaymentSliceRow>> GetSlicesAsync(DateTime from, DateTime until, CancellationToken ct = default);

        Task<OnlinePaymentTotalsRow> GetSuccessfulTotalsAsync(DateTime from, DateTime until, CancellationToken ct = default);

        Task<List<PaymentProviderRow>> GetProvidersAsync(CancellationToken ct = default);
    }
}
