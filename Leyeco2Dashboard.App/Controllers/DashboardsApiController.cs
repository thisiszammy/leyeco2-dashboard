using Leyeco2Dashboard.App.Authorization;
using Leyeco2Dashboard.App.Models;
using Leyeco2Dashboard.Services.Exceptions;
using Leyeco2Dashboard.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leyeco2Dashboard.App.Controllers
{
    /// <summary>
    /// The dashboards' figures: the overall dashboard's from the legacy collections ledger (View the dashboard), the
    /// Online Payments dashboard's from the online payment facility (View online payments). Each takes a period of
    /// whole days, <c>from</c> and <c>to</c> both included (<c>yyyy-MM-dd</c>); neither reads today. Read-only.
    /// </summary>
    [Route("api/dashboards")]
    [ApiController]
    public class DashboardsApiController : ControllerBase
    {
        private readonly ICollectionDashboardService _collections;
        private readonly IOnlinePaymentDashboardService _onlinePayments;
        private readonly ILogger<DashboardsApiController> _logger;

        public DashboardsApiController(
            ICollectionDashboardService collections,
            IOnlinePaymentDashboardService onlinePayments,
            ILogger<DashboardsApiController> logger)
        {
            _collections = collections;
            _onlinePayments = onlinePayments;
            _logger = logger;
        }

        [HttpGet("collections")]
        [Authorize(Policy = MonitorPolicies.ViewDashboard)]
        public async Task<IActionResult> GetCollections([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        {
            var logId = Guid.NewGuid();
            try
            {
                _logger.LogInformation("{logId}: Retrieving the collections dashboard for {from:yyyy-MM-dd} to {to:yyyy-MM-dd}", logId, from, to);

                var result = await _collections.GetAsync(from, to, ct);

                return Ok(new ApiResponse { Code = StatusCodes.Status200OK, Message = "Successfully retrieved the collections dashboard", Data = result });
            }
            catch (Exception ex)
            {
                return Failed(ex, logId, "retrieving the collections dashboard", ct);
            }
        }

        [HttpGet("online-payments")]
        [Authorize(Policy = MonitorPolicies.ViewOnlinePayments)]
        public async Task<IActionResult> GetOnlinePayments([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        {
            var logId = Guid.NewGuid();
            try
            {
                _logger.LogInformation("{logId}: Retrieving the online payments dashboard for {from:yyyy-MM-dd} to {to:yyyy-MM-dd}", logId, from, to);

                var result = await _onlinePayments.GetAsync(from, to, ct);

                return Ok(new ApiResponse { Code = StatusCodes.Status200OK, Message = "Successfully retrieved the online payments dashboard", Data = result });
            }
            catch (Exception ex)
            {
                return Failed(ex, logId, "retrieving the online payments dashboard", ct);
            }
        }

        /// <summary>
        /// A period the dashboard can't read answers 400, a database it can't reach or that answers too slowly 503,
        /// each with its own words; a viewer who left (a new period picked, the page closed) 499; anything else 500.
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
