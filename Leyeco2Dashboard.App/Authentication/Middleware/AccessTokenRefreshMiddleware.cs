using Leyeco2Dashboard.Authentication.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Leyeco2Dashboard.Authentication.Middleware;

/// <summary>
/// Silent refresh, run before <c>UseAuthentication</c>. When a request carries the host's refresh
/// cookie and its access token is missing, expired, or within <c>Auth:PreemptiveRefreshSeconds</c>
/// of expiring, the session is renewed through <see cref="IRefreshCoalescer"/>: the new token is
/// handed to JwtBearer for this very request, and the rotated cookies ride back on its response.
/// </summary>
/// <remarks>
/// A session that is over (signed out, expired, its account deactivated) ends here: the cookies
/// are cleared, and an <c>/api</c> request is answered 401. A refresh that fails for another
/// reason (the database cannot be reached) is no verdict on the session: the cookies stay, a
/// token still inside its lifetime carries the request, and an <c>/api</c> request whose token
/// has lapsed is answered 503. Anything else
/// (the client shell, static assets) always continues; the Blazor client owns the redirect to
/// <c>/login</c>.
/// </remarks>
public class AccessTokenRefreshMiddleware
{
    // The routes that open, replace or end a session themselves: a refresh in passing would
    // only renew what they are about to discard.
    private const string AuthenticatePath = "/api/auth/authenticate";
    private const string ChangePasswordPath = "/api/auth/change-password";
    private const string LogoutPath = "/api/auth/logout";

    // Used when Auth:PreemptiveRefreshSeconds is absent or empty (a value that is not a whole
    // number throws instead): ample headroom for a refresh, against access tokens minted for five
    // minutes by default.
    private const int DefaultPreemptiveRefreshSeconds = 60;

    private const string UnavailableMessage =
        "Your session can't be checked right now. Try again in a moment.";

    /// <summary>
    /// Set after a successful refresh on the current request. The JwtBearer <c>OnChallenge</c>
    /// handler reads it to skip its cookie clear: <c>IResponseCookies.Delete</c> strips matching
    /// earlier Appends from the response, so clearing would undo the refresh.
    /// </summary>
    public const string RefreshedItemKey = "Leyeco2Dashboard.Authentication.AccessTokenRefreshed";

    /// <summary>The access token minted on this request, read first by JwtBearer's <c>OnMessageReceived</c>.</summary>
    public const string RefreshedAccessTokenKey = "Leyeco2Dashboard.Authentication.RefreshedAccessToken";

    private enum RefreshNeed
    {
        None,

        // Still inside its lifetime: the request can go on with it if the refresh fails.
        Preemptive,

        // Missing or lapsed: nothing authenticates the request without a refresh.
        Required,
    }

    private readonly RequestDelegate _next;
    private readonly ILogger<AccessTokenRefreshMiddleware> _logger;
    private readonly IOptionsMonitor<JwtBearerOptions> _jwtOptionsMonitor;
    private readonly TimeSpan _preemptiveRefreshWindow;

    public AccessTokenRefreshMiddleware(
        RequestDelegate next,
        ILogger<AccessTokenRefreshMiddleware> logger,
        IOptionsMonitor<JwtBearerOptions> jwtOptionsMonitor,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _jwtOptionsMonitor = jwtOptionsMonitor;

        // 0 or less: refresh only a lapsed token.
        var configured = configuration.GetValue<int?>("Auth:PreemptiveRefreshSeconds")
                         ?? DefaultPreemptiveRefreshSeconds;
        _preemptiveRefreshWindow = TimeSpan.FromSeconds(Math.Max(0, configured));
    }

    public async Task InvokeAsync(
        HttpContext context,
        IAuthGatewayService authGateway,
        IRefreshCoalescer refreshCoalescer)
    {
        if (ShouldSkipPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var refreshTokenId = context.Request.Cookies[AuthenticationCookies.RefreshTokenName];
        if (string.IsNullOrEmpty(refreshTokenId))
        {
            await _next(context);
            return;
        }

        var existingAccessToken = context.Request.Cookies[AuthenticationCookies.AccessTokenName];
        var need = await AssessAsync(existingAccessToken);
        if (need == RefreshNeed.None)
        {
            await _next(context);
            return;
        }

        try
        {
            _logger.LogInformation(
                "Silent refresh decided: key={CoalesceKey} need={Need} path={Path}",
                RefreshCoalescer.BuildCoalesceKey(existingAccessToken, refreshTokenId),
                need,
                context.Request.Path);

            var session = await refreshCoalescer.CoalescedRefreshAsync(
                existingAccessToken,
                refreshTokenId,
                AuthenticationCookies.IsPersistent(context.Request),
                authGateway,
                context.RequestAborted);

            context.Items[RefreshedAccessTokenKey] = session.AccessToken;
            context.Items[RefreshedItemKey] = true;
            AuthenticationCookies.Stamp(context, session);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            return;
        }
        catch (AuthGatewayException ex) when (!ex.IsTransient)
        {
            _logger.LogWarning(ex, "Silent refresh refused; clearing the session cookies.");

            AuthenticationCookies.Clear(context);
            if (await AuthChallengeResolver.TryWriteChallengeAsync(context)) return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Silent refresh failed without a verdict on the session; keeping its cookies (need={Need}).", need);

            if (need == RefreshNeed.Required
                && await AuthChallengeResolver.TryWriteChallengeAsync(context, StatusCodes.Status503ServiceUnavailable, UnavailableMessage))
            {
                return;
            }
        }

        await _next(context);
    }

    private static bool ShouldSkipPath(PathString path)
        => path.StartsWithSegments(AuthenticatePath)
           || path.StartsWithSegments(ChangePasswordPath)
           || path.StartsWithSegments(LogoutPath);

    private async Task<RefreshNeed> AssessAsync(string? accessToken)
    {
        if (string.IsNullOrEmpty(accessToken)) return RefreshNeed.Required;

        var parameters = _jwtOptionsMonitor.Get(AuthenticationSchemes.Cookies).TokenValidationParameters.Clone();
        parameters.ValidateLifetime = false;

        // A token this host would refuse for another reason (signature, issuer, audience) is left
        // to JwtBearer: a refreshed one is signed the same way, so a refresh would change nothing.
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(accessToken, parameters);
        if (!validation.IsValid || validation.SecurityToken is not JsonWebToken jwt)
            return RefreshNeed.None;

        var now = DateTime.UtcNow;
        if (jwt.ValidTo <= now) return RefreshNeed.Required;

        return jwt.ValidTo <= now.Add(_preemptiveRefreshWindow)
            ? RefreshNeed.Preemptive
            : RefreshNeed.None;
    }
}
