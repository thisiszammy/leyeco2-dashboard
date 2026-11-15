using Leyeco2Dashboard.Services.DTOs;
using Leyeco2Dashboard.Services.Excel;
using Leyeco2Dashboard.Services.Exceptions;
using Leyeco2Dashboard.Services.Pdf;
using Leyeco2Dashboard.Services.Reports;
using QuestPDF.Fluent;

namespace Leyeco2Dashboard.Services.Services
{
    /// <summary>
    /// The inspection's printouts: the summary and every bill of the view, as a PDF (QuestPDF) or an Excel workbook
    /// (ClosedXML), both cut from the period's one read as the register is, so a printout says what the screen says.
    /// They write nothing, so they record no audit.
    /// </summary>
    public partial class TransactionInspectionService
    {
        /// <summary>
        /// The most bills a PDF lists: about 250 pages. A longer register is for the workbook, which sorts and filters
        /// what no one would page through.
        /// </summary>
        public const int MaxPdfBills = 10_000;

        private const string PdfContentType = "application/pdf";
        private const string WorkbookContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public async Task<InspectionDocumentDTO> GetPdfAsync(
            DateTime? from, DateTime? to, string? view, string? provider, string? search,
            CancellationToken cancellationToken = default)
        {
            var report = await ReportAsync(from, to, view, provider, search, cancellationToken);
            if (report.Bills.Count > MaxPdfBills)
                throw new DashboardRequestException(
                    $"This view lists {report.Bills.Count:N0} bills, more than a PDF holds ({MaxPdfBills:N0}). Export it to Excel instead, or narrow the period, the view or the provider.");

            var content = new TransactionInspectionPdfDocument(report).GeneratePdf();
            return Document(report, ".pdf", PdfContentType, content);
        }

        public async Task<InspectionDocumentDTO> GetWorkbookAsync(
            DateTime? from, DateTime? to, string? view, string? provider, string? search,
            CancellationToken cancellationToken = default)
        {
            var report = await ReportAsync(from, to, view, provider, search, cancellationToken);
            return Document(report, ".xlsx", WorkbookContentType, TransactionInspectionWorkbook.Build(report));
        }

        private async Task<InspectionReport> ReportAsync(
            DateTime? from, DateTime? to, string? view, string? provider, string? search, CancellationToken cancellationToken)
        {
            var period = Period(from, to);
            var inspection = await InspectionAsync(period, cancellationToken);
            var cut = Cut(inspection, view, provider, search);

            return new InspectionReport
            {
                From = period.From,
                To = period.To,
                ReadOn = inspection.ReadOn,
                PrintedOn = _dateTime.Now(),
                View = cut.View,
                Provider = cut.Provider.Length == 0
                    ? null
                    : inspection.Providers.FirstOrDefault(p => string.Equals(p.Key, cut.Provider, StringComparison.OrdinalIgnoreCase))?.Label
                      ?? $"Provider {cut.Provider}",
                Search = cut.Search,
                Summary = cut.Summary,
                Bills = cut.Listed,
            };
        }

        private static InspectionDocumentDTO Document(InspectionReport report, string extension, string contentType, byte[] content)
            => new()
            {
                FileName = report.FileStem() + extension,
                ContentType = contentType,
                Content = content,
            };
    }
}
