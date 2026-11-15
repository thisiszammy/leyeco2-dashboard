using Leyeco2Dashboard.App.Authorization;
using Leyeco2Dashboard.App.Models;
using Leyeco2Dashboard.Services.DTOs;
using Leyeco2Dashboard.Services.Exceptions;
using Leyeco2Dashboard.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leyeco2Dashboard.App.Controllers
{
    /// <summary>
    /// The Transaction Inspector (Inspect transactions): every bill of the online payments begun in a period of whole
    /// days (<c>from</c> and <c>to</c> both included, <c>yyyy-MM-dd</c>; neither reads today), followed into the
    /// facility's posting queue and the collections ledger. <c>view</c> (all, attention, waiting, tallied, unpaid),
    /// <c>provider</c> (a provider key) and <c>search</c> narrow it alike on screen and in its printouts. Read-only.
    /// </summary>
    [Route("api/transaction-inspector")]
    [ApiController]
    [Authorize(Policy = MonitorPolicies.InspectTransactions)]
    public class TransactionInspectorApiController : ControllerBase
    {
        private readonly ITransactionInspectionService _inspection;
        private readonly ILogger<TransactionInspectorApiController> _logger;

        public TransactionInspectorApiController(ITransactionInspectionService inspection, ILogger<TransactionInspectorApiController> logger)
        {
            _inspection = inspection;
            _logger = logger;
        }

        [HttpGet("bills")]
        public async Task<IActionResult> GetBills(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string? view,
            [FromQuery] string? provider,
            [FromQuery] string? search,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            CancellationToken ct)
        {
            var logId = Guid.NewGuid();
            try
            {
                _logger.LogInformation("{logId}: Inspecting the online payments of {from:yyyy-MM-dd} to {to:yyyy-MM-dd} ({view}, page {page})",
                    logId, from, to, view ?? "all", page);

                var result = await _inspection.GetAsync(from, to, view, provider, search, page, pageSize, ct);

                return Ok(new ApiResponse { Code = StatusCodes.Status200OK, Message = "Successfully inspected the online payments", Data = result });
            }
            catch (Exception ex)
            {
                return Failed(ex, logId, "inspecting the online payments", ct);
            }
        }

        /// <summary>The summary and every bill of the view as a PDF (a download).</summary>
        [HttpGet("bills/pdf")]
        public Task<IActionResult> GetBillsPdf(
            [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? view,
            [FromQuery] string? provider, [FromQuery] string? search, CancellationToken ct)
            => DocumentAsync("PDF", () => _inspection.GetPdfAsync(from, to, view, provider, search, ct), from, to, ct);

        /// <summary>The summary and every bill of the view as an Excel workbook (a download).</summary>
        [HttpGet("bills/xlsx")]
        public Task<IActionResult> GetBillsWorkbook(
            [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? view,
            [FromQuery] string? provider, [FromQuery] string? search, CancellationToken ct)
            => DocumentAsync("workbook", () => _inspection.GetWorkbookAsync(from, to, view, provider, search, ct), from, to, ct);

        /// <summary>
        /// Streams a printout as a download. It names consumers and their payments, so no copy is kept along the way
        /// (no-store).
        /// </summary>
        private async Task<IActionResult> DocumentAsync(
            string document, Func<Task<InspectionDocumentDTO>> render, DateTime? from, DateTime? to, CancellationToken ct)
        {
            var logId = Guid.NewGuid();
            try
            {
                _logger.LogInformation("{logId}: Rendering the inspection {document} of {from:yyyy-MM-dd} to {to:yyyy-MM-dd}", logId, document, from, to);

                var file = await render();

                Response.Headers.CacheControl = "no-store";
                return File(file.Content, file.ContentType, file.FileName);
            }
            catch (Exception ex)
            {
                return Failed(ex, logId, $"rendering the inspection {document}", ct);
            }
        }

        /// <summary>
        /// A period or printout the inspector can't make answers 400, a source it can't reach or that answers too slowly
        /// 503, each with its own words; a viewer who left 499; anything else 500.
        /// </summary>
        private IActionResult Failed(Exception exception, Guid logId, string action, CancellationToken ct)
        {
            switch (exception)
            {
                case OperationCanceledException when ct.IsCancellationRequested:
                    _logger.LogInformation("{logId}: Request cancelled by the client while {action}", logId, action);
                    return StatusCode(StatusCodes.Status499ClientClosedRequest,
                        new ApiResponse { Code = StatusCodes.Status499ClientClosedRequest, Message = "Request cancelled" });

                case DashboardRequestException:
                    _logger.LogWarning("{logId}: Error {action} - {message}", logId, action, exception.Message);
                    return BadRequest(new ApiResponse { Code = StatusCodes.Status400BadRequest, Message = exception.Message });

                case ReportSourceException:
                    _logger.LogError(exception.InnerException, "{logId}: Error {action} - {message}", logId, action, exception.Message);
                    return StatusCode(StatusCodes.Status503ServiceUnavailable,
                        new ApiResponse { Code = StatusCodes.Status503ServiceUnavailable, Message = exception.Message });

                default:
                    _logger.LogError(exception, "{logId}: Error {action}", logId, action);
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new ApiResponse { Code = StatusCodes.Status500InternalServerError, Message = "An unknown server error occurred" });
            }
        }
    }
}
