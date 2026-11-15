using System.Security.Claims;
using Leyeco2Dashboard.Authentication;
using Leyeco2Dashboard.Authentication.Services;
using Leyeco2Dashboard.App.Models;
using Leyeco2Dashboard.Services.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Leyeco2Dashboard.App.Controllers
{
    /// <summary>
    /// Sign-in against the Transaction Monitor's own accounts (the Users screen keeps them). The session lives in
    /// this API's HttpOnly cookies (<c>l2auth_txm_*</c>): an access token it mints and a session
    /// token that renews it, so no token ever reaches a response body or the browser's script.
    /// The console signs in here.
    /// </summary>
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        /// <summary>The rate limit on the routes that take a password (<c>Program.cs</c>).</summary>
        public const string PasswordRateLimit = "auth-password";

        private readonly IAuthGatewayService _authGateway;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthGatewayService authGateway, ILogger<AuthController> logger)
        {
            _authGateway = authGateway;
            _logger = logger;
        }

        /// <summary>
        /// Signs an account in and stamps the session cookies. Refusals: a wrong username or
        /// password (401), a locked account (423), a deactivated one or one that holds no grant
        /// (403), and one whose password an administrator set (428): that account goes through
        /// <see cref="ChangePassword"/>, which signs it in.
        /// </summary>
        [AllowAnonymous]
        [EnableRateLimiting(PasswordRateLimit)]
        [HttpPost("authenticate")]
        public async Task<IActionResult> Authenticate([FromBody] LoginRequest request, CancellationToken ct)
        {
            var logId = Guid.NewGuid();
            try
            {
                // Log the username only — never the password.
                _logger.LogInformation("{logId}: Sign-in attempt for `{username}`", logId, request.Username);

                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
                {
                    return BadRequest(new ApiResponse
                    {
                        Code = StatusCodes.Status400BadRequest,
                        Message = "Username and password are required.",
                        Data = null
                    });
                }

                var session = await _authGateway.AuthenticateAsync(
                    request.Username.Trim(), request.Password, rememberMe: false, ct);

                AuthenticationCookies.Stamp(HttpContext, session);

                _logger.LogInformation("{logId}: Signed in `{username}` ({userId})", logId, session.Account.Username, session.Account.UserId);

                return Ok(new ApiResponse
                {
                    Code = StatusCodes.Status200OK,
                    Message = "Signed in",
                    Data = SessionOf(session)
                });
            }
            catch (SignInException ex)
            {
                _logger.LogWarning("{logId}: Sign-in refused for `{username}` ({kind})", logId, request.Username, ex.Kind);

                var status = ex.Kind switch
                {
                    SignInFailureKind.Locked => StatusCodes.Status423Locked,
                    SignInFailureKind.Deactivated or SignInFailureKind.NoAccess => StatusCodes.Status403Forbidden,
                    SignInFailureKind.PasswordChangeRequired => StatusCodes.Status428PreconditionRequired,
                    _ => StatusCodes.Status401Unauthorized
                };

                // The cookies of any earlier session in this browser go too, as on every refused sign-in.
                AuthenticationCookies.Clear(HttpContext);
                return StatusCode(status, new ApiResponse
                {
                    Code = status,
                    Message = ex.Message,
                    Data = null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{logId}: Error during sign-in for `{username}`", logId, request.Username);
                AuthenticationCookies.Clear(HttpContext);
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse
                {
                    Code = StatusCodes.Status500InternalServerError,
                    Message = "An unknown server error occurred",
                    Data = null
                });
            }
        }

        /// <summary>
        /// Sets a password of the account's own choosing, on proof of the current one, and signs
        /// it in under the new one; its other sessions end. Anonymous, because an account whose
        /// password an administrator set has no session yet when it gets here. A signed-in caller
        /// changes its own account's password, whatever username the body names.
        /// </summary>
        /// <remarks>
        /// A wrong current password answers 400, not 401: the console reads any 401 as the end of
        /// its session.
        /// </remarks>
        [AllowAnonymous]
        [EnableRateLimiting(PasswordRateLimit)]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
        {
            var logId = Guid.NewGuid();
            var username = User.Identity?.IsAuthenticated == true
                ? User.FindFirstValue(AuthConstants.UniqueNameClaimType)
                : request.Username?.Trim();

            try
            {
                _logger.LogInformation("{logId}: Password change for `{username}`", logId, username);

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword))
                {
                    return BadRequest(new ApiResponse
                    {
                        Code = StatusCodes.Status400BadRequest,
                        Message = "Your current password and a new password are required.",
                        Data = null
                    });
                }

                var session = await _authGateway.ChangePasswordAsync(username, request.CurrentPassword, request.NewPassword, ct);

                AuthenticationCookies.Stamp(HttpContext, session);

                _logger.LogInformation("{logId}: Password changed for `{username}` ({userId})", logId, session.Account.Username, session.Account.UserId);

                return Ok(new ApiResponse
                {
                    Code = StatusCodes.Status200OK,
                    Message = "Password changed",
                    Data = SessionOf(session)
                });
            }
            catch (UserValidationException ex)
            {
                _logger.LogWarning("{logId}: Password change refused for `{username}` - {message}", logId, username, ex.Message);
                return BadRequest(new ApiResponse
                {
                    Code = StatusCodes.Status400BadRequest,
                    Message = ex.Message,
                    Data = null
                });
            }
            catch (SignInException ex)
            {
                _logger.LogWarning("{logId}: Password change refused for `{username}` ({kind})", logId, username, ex.Kind);

                var (status, message) = ex.Kind switch
                {
                    SignInFailureKind.InvalidCredentials => (StatusCodes.Status400BadRequest, "Your current password is incorrect."),
                    SignInFailureKind.Locked => (StatusCodes.Status423Locked, ex.Message),
                    _ => (StatusCodes.Status403Forbidden, ex.Message)
                };

                return StatusCode(status, new ApiResponse
                {
                    Code = status,
                    Message = message,
                    Data = null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{logId}: Error changing the password of `{username}`", logId, username);
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse
                {
                    Code = StatusCodes.Status500InternalServerError,
                    Message = "An unknown server error occurred",
                    Data = null
                });
            }
        }

        /// <summary>Ends the session (best effort) and clears the session cookies.</summary>
        [AllowAnonymous]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            var logId = Guid.NewGuid();
            try
            {
                _logger.LogInformation("{logId}: Sign-out `{username}`", logId, User.FindFirstValue(AuthConstants.UniqueNameClaimType));

                var refreshTokenId = Request.Cookies[AuthenticationCookies.RefreshTokenName];
                var accessToken = Request.Cookies[AuthenticationCookies.AccessTokenName];

                if (!string.IsNullOrEmpty(refreshTokenId))
                    await _authGateway.LogoutAsync(refreshTokenId, accessToken, ct);

                AuthenticationCookies.Clear(HttpContext);

                return Ok(new ApiResponse
                {
                    Code = StatusCodes.Status200OK,
                    Message = "Signed out",
                    Data = null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{logId}: Error during sign-out", logId);
                AuthenticationCookies.Clear(HttpContext);

                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse
                {
                    Code = StatusCodes.Status500InternalServerError,
                    Message = "An unknown server error occurred",
                    Data = null
                });
            }
        }

        /// <summary>
        /// Who the session cookies authenticate. Open to any signed-in account, not only one with
        /// a Transaction Monitor grant, so an account whose grants were all revoked mid-session learns it holds
        /// no access (empty <see cref="SessionResponse.Permissions"/>) and can still sign out.
        /// </summary>
        [Authorize(Policy = AuthorizationPolicies.Authenticated)]
        [HttpGet("session")]
        public IActionResult Session()
        {
            var logId = Guid.NewGuid();
            try
            {
                var permissions = User.FindAll(AuthConstants.PermissionClaimType)
                    .Select(c => AuthConstants.TryParsePermission(c.Value, out var permission)
                        ? AuthConstants.ToClaimValue(permission)
                        : null)
                    .OfType<string>()
                    .Distinct()
                    .ToList();

                return Ok(new ApiResponse
                {
                    Code = StatusCodes.Status200OK,
                    Message = "Successfully retrieved the session",
                    Data = new SessionResponse
                    {
                        UserId = Guid.TryParse(User.FindFirstValue(AuthConstants.SubjectClaimType), out var userId) ? userId : Guid.Empty,
                        Username = User.FindFirstValue(AuthConstants.UniqueNameClaimType) ?? string.Empty,
                        FirstName = User.FindFirstValue(AuthConstants.GivenNameClaimType) ?? string.Empty,
                        LastName = User.FindFirstValue(AuthConstants.FamilyNameClaimType) ?? string.Empty,
                        Email = User.FindFirstValue(AuthConstants.EmailClaimType) ?? string.Empty,
                        Permissions = permissions
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{logId}: Error reading the session", logId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse
                {
                    Code = StatusCodes.Status500InternalServerError,
                    Message = "An unknown server error occurred",
                    Data = null
                });
            }
        }

        private static SessionResponse SessionOf(AuthGatewayResult session) => new()
        {
            UserId = session.Account.UserId,
            Username = session.Account.Username,
            FirstName = session.Account.FirstName,
            LastName = session.Account.LastName,
            Email = session.Account.Email,
            Permissions = session.Account.Permissions.Select(AuthConstants.ToClaimValue).ToList()
        };
    }
}
