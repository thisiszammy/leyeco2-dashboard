using Leyeco2Dashboard.App.Authorization;
using Leyeco2Dashboard.App.Extensions;
using Leyeco2Dashboard.App.Models;
using Leyeco2Dashboard.Services.Exceptions;
using Leyeco2Dashboard.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Leyeco2Dashboard.App.Controllers
{
    /// <summary>
    /// The Users screen's endpoints: the Transaction Monitor's own accounts, their passwords and what each may
    /// open. Every one takes the Manage users grant, reads included: the register shows who holds
    /// what. A password is never logged, so no request body is.
    /// </summary>
    [Route("api/users")]
    [ApiController]
    [Authorize(Policy = MonitorPolicies.ManageUsers)]
    public class UsersApiController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UsersApiController> _logger;

        public UsersApiController(IUserService userService, ILogger<UsersApiController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetUsersPaged(
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            var logId = Guid.NewGuid();
            try
            {
                _logger.LogInformation("{logId}: Retrieving users page `{search}` ({status}, page {page}, size {pageSize})", logId, search, status, page, pageSize);

                var result = await _userService.GetUsersPagedAsync(search, status, page, pageSize, ct);

                return Ok(new ApiResponse { Code = StatusCodes.Status200OK, Message = "Successfully retrieved users page", Data = result });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Routine for the debounced search box: the client abandoned this request for a newer one.
                _logger.LogInformation("{logId}: Users page request cancelled by the client", logId);
                return StatusCode(StatusCodes.Status499ClientClosedRequest,
                    new ApiResponse { Code = StatusCodes.Status499ClientClosedRequest, Message = "Request cancelled" });
            }
            catch (Exception ex)
            {
                return Failed(ex, logId, "retrieving users page");
            }
        }

        [HttpGet("{userId:guid}")]
        public async Task<IActionResult> GetUser(Guid userId, CancellationToken ct)
        {
            var logId = Guid.NewGuid();
            try
            {
                _logger.LogInformation("{logId}: Retrieving user `{userId}`", logId, userId);

                var user = await _userService.GetUserAsync(userId, ct);

                return Ok(new ApiResponse { Code = StatusCodes.Status200OK, Message = "Successfully retrieved user", Data = user });
            }
            catch (Exception ex)
            {
                return Failed(ex, logId, "retrieving user");
            }
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddUser([FromBody] AddUserRequest request, CancellationToken ct)
        {
            var logId = Guid.NewGuid();
            try
            {
                _logger.LogInformation("{logId}: Creating user `{username}` with {grants} grant(s)", logId, request.Username, request.Grants.Count);

                var userId = await _userService.CreateUserAsync(
                    request.Username ?? string.Empty,
                    request.FirstName ?? string.Empty,
                    request.LastName ?? string.Empty,
                    request.Email,
                    request.Password ?? string.Empty,
                    request.MustChangePassword,
                    request.Grants,
                    this.GetRequiredUserGuid(),
                    ct);

                return Ok(new ApiResponse { Code = StatusCodes.Status200OK, Message = "Successfully created user", Data = userId });
            }
            catch (Exception ex)
            {
                return Failed(ex, logId, "creating user");
            }
        }

        [HttpPut("{userId:guid}")]
        public async Task<IActionResult> UpdateUser(Guid userId, [FromBody] UpdateUserRequest request, CancellationToken ct)
        {
            var logId = Guid.NewGuid();
            try
            {
                _logger.LogInformation("{logId}: Updating user `{userId}` as `{username}`", logId, userId, request.Username);

                await _userService.UpdateUserAsync(
                    userId,
                    request.Username ?? string.Empty,
                    request.FirstName ?? string.Empty,
                    request.LastName ?? string.Empty,
                    request.Email,
                    this.GetRequiredUserGuid(),
                    ct);

                return Ok(new ApiResponse { Code = StatusCodes.Status200OK, Message = "Successfully updated user" });
            }
            catch (Exception ex)
            {
                return Failed(ex, logId, "updating user");
            }
        }

        [HttpPut("{userId:guid}/access")]
        public async Task<IActionResult> SetUserAccess(Guid userId, [FromBody] SetUserAccessRequest request, CancellationToken ct)
        {
            var logId = Guid.NewGuid();
            try
            {
                _logger.LogInformation("{logId}: Setting the access of user `{userId}` to `{@grants}`", logId, userId, request.Grants);

                await _userService.SetUserAccessAsync(userId, request.Grants, this.GetRequiredUserGuid(), ct);

                return Ok(new ApiResponse { Code = StatusCodes.Status200OK, Message = "Successfully changed the user's access" });
            }
            catch (Exception ex)
            {
                return Failed(ex, logId, "setting user access");
            }
        }

        [HttpPut("{userId:guid}/status")]
        public async Task<IActionResult> SetUserStatus(Guid userId, [FromBody] SetUserStatusRequest request, CancellationToken ct)
        {
            var logId = Guid.NewGuid();
            try
            {
                _logger.LogInformation("{logId}: Setting user `{userId}` active = {isActive}", logId, userId, request.IsActive);

                await _userService.SetUserActiveAsync(userId, request.IsActive, this.GetRequiredUserGuid(), ct);

                return Ok(new ApiResponse
                {
                    Code = StatusCodes.Status200OK,
                    Message = request.IsActive ? "Successfully reactivated user" : "Successfully deactivated user"
                });
            }
            catch (Exception ex)
            {
                return Failed(ex, logId, "setting user status");
            }
        }

        [HttpPost("{userId:guid}/reset-password")]
        public async Task<IActionResult> ResetUserPassword(Guid userId, [FromBody] ResetUserPasswordRequest request, CancellationToken ct)
        {
            var logId = Guid.NewGuid();
            try
            {
                _logger.LogInformation("{logId}: Resetting the password of user `{userId}`", logId, userId);

                await _userService.ResetUserPasswordAsync(
                    userId, request.NewPassword ?? string.Empty, request.MustChangePassword, this.GetRequiredUserGuid(), ct);

                return Ok(new ApiResponse { Code = StatusCodes.Status200OK, Message = "Successfully reset the user's password" });
            }
            catch (Exception ex)
            {
                return Failed(ex, logId, "resetting user password");
            }
        }

        [HttpPost("{userId:guid}/unlock")]
        public async Task<IActionResult> UnlockUser(Guid userId, CancellationToken ct)
        {
            var logId = Guid.NewGuid();
            try
            {
                _logger.LogInformation("{logId}: Unlocking user `{userId}`", logId, userId);

                await _userService.UnlockUserAsync(userId, this.GetRequiredUserGuid(), ct);

                return Ok(new ApiResponse { Code = StatusCodes.Status200OK, Message = "Successfully unlocked user" });
            }
            catch (Exception ex)
            {
                return Failed(ex, logId, "unlocking user");
            }
        }

        /// <summary>A rule the request broke answers 400 with its own words; anything else is a 500 with none of its own.</summary>
        private IActionResult Failed(Exception exception, Guid logId, string action)
        {
            if (exception is UserValidationException)
            {
                _logger.LogWarning("{logId}: Error {action} - {message}", logId, action, exception.Message);
                return BadRequest(new ApiResponse { Code = StatusCodes.Status400BadRequest, Message = exception.Message });
            }

            _logger.LogError(exception, "{logId}: Error {action}", logId, action);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ApiResponse { Code = StatusCodes.Status500InternalServerError, Message = "An unknown server error occurred" });
        }
    }
}
