using Leyeco2Dashboard.Services.DTOs;

namespace Leyeco2Dashboard.Services.Interfaces
{
    /// <summary>
    /// The overall dashboard's figures, read from the legacy collections ledger, the source of truth for what was
    /// collected. Neither date given reads today; one reads that day.
    /// </summary>
    public interface ICollectionDashboardService
    {
        /// <exception cref="Exceptions.DashboardRequestException">The period is reversed or too long.</exception>
        /// <exception cref="Exceptions.ReportSourceException">The ledger is not configured, out of reach or too slow.</exception>
        Task<CollectionDashboardDTO> GetAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// The Transaction Inspector: every bill of the online payments begun in a period, followed into the facility's
    /// posting queue and the collections ledger. A period is whole days, at most
    /// <see cref="Services.TransactionInspectionService.MaxDays"/>; neither date given reads today. The filters
    /// (<paramref name="provider"/> a provider key or empty for all; <paramref name="search"/> an account number, a
    /// name, a payment or bill number or a receipt) narrow the summary and the views alike; <paramref name="view"/> picks
    /// the bills listed (all, attention, waiting, tallied, unpaid).
    /// </summary>
    public interface ITransactionInspectionService
    {
        /// <exception cref="Exceptions.DashboardRequestException">The period is reversed or too long.</exception>
        /// <exception cref="Exceptions.ReportSourceException">A source is not configured, out of reach or too slow.</exception>
        Task<TransactionInspectionDTO> GetAsync(
            DateTime? from, DateTime? to, string? view, string? provider, string? search, int page, int pageSize,
            CancellationToken cancellationToken = default);

        /// <summary>The summary and every bill of the view, as a PDF (A4 landscape).</summary>
        /// <exception cref="Exceptions.DashboardRequestException">The period is reversed or too long, or the view too large to print.</exception>
        /// <exception cref="Exceptions.ReportSourceException">A source is not configured, out of reach or too slow.</exception>
        Task<InspectionDocumentDTO> GetPdfAsync(
            DateTime? from, DateTime? to, string? view, string? provider, string? search,
            CancellationToken cancellationToken = default);

        /// <summary>The summary and every bill of the view, as an Excel workbook.</summary>
        /// <exception cref="Exceptions.DashboardRequestException">The period is reversed or too long.</exception>
        /// <exception cref="Exceptions.ReportSourceException">A source is not configured, out of reach or too slow.</exception>
        Task<InspectionDocumentDTO> GetWorkbookAsync(
            DateTime? from, DateTime? to, string? view, string? provider, string? search,
            CancellationToken cancellationToken = default);
    }

    /// <summary>The Online Payments dashboard's figures, read from the online payment facility.</summary>
    public interface IOnlinePaymentDashboardService
    {
        /// <exception cref="Exceptions.DashboardRequestException">The period is reversed or too long.</exception>
        /// <exception cref="Exceptions.ReportSourceException">The facility's database is not configured, out of reach or too slow.</exception>
        Task<OnlinePaymentDashboardDTO> GetAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    }
}
