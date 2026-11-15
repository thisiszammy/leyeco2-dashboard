using Leyeco2Dashboard.Services.DTOs;
using Leyeco2Dashboard.Services.Exceptions;
using Leyeco2Dashboard.Services.Interfaces;

namespace Leyeco2Dashboard.Authentication.Services;

/// <inheritdoc/>
/// <remarks>
/// A singleton that opens a DI scope per call, and so a <c>DatabaseContext</c> of its own. The
/// refresh coalescer hands one caller's refresh to every request waiting on it and lets it outlive
/// that caller, so it must not run on a request's scope; and a failed write here must leave
/// nothing staged on the context the request's own save uses.
/// </remarks>
public class AuthGatewayService : IAuthGatewayService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AccessTokenIssuer _tokenIssuer;
    private readonly ILogger<AuthGatewayService> _logger;

    public AuthGatewayService(
        IServiceScopeFactory scopeFactory,
        AccessTokenIssuer tokenIssuer,
        ILogger<AuthGatewayService> logger)
    {
        _scopeFactory = scopeFactory;
        _tokenIssuer = tokenIssuer;
        _logger = logger;
    }

    public async Task<AuthGatewayResult> AuthenticateAsync(
        string username,
        string password,
        bool rememberMe,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var signIn = scope.ServiceProvider.GetRequiredService<ISignInService>();

        return Issue(await signIn.SignInAsync(username, password, rememberMe, cancellationToken));
    }

    public async Task<AuthGatewayResult> RefreshAsync(
        string refreshTokenId,
        bool persistent,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var signIn = scope.ServiceProvider.GetRequiredService<ISignInService>();

        try
        {
            return Issue(await signIn.RefreshAsync(refreshTokenId, cancellationToken));
        }
        catch (SignInException ex)
        {
            // The one failure that is a verdict: the middleware clears the cookies on it.
            throw new AuthGatewayException(AuthGatewayErrorKind.Unauthorized, ex.Message, inner: ex);
        }
    }

    public async Task LogoutAsync(
        string? refreshTokenId,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var signIn = scope.ServiceProvider.GetRequiredService<ISignInService>();

            await signIn.SignOutAsync(refreshTokenId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sign-out could not end session {Session}; it lapses by itself.", RefreshCoalescer.HashTokenId(refreshTokenId));
        }
    }

    public async Task<AuthGatewayResult> ChangePasswordAsync(
        string username,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var signIn = scope.ServiceProvider.GetRequiredService<ISignInService>();

        return Issue(await signIn.ChangePasswordAsync(username, currentPassword, newPassword, persistent: false, cancellationToken));
    }

    private AuthGatewayResult Issue(SessionDTO session)
    {
        var (token, tokenExpiresOn) = _tokenIssuer.Issue(session);

        return new AuthGatewayResult
        {
            Account = session,
            AccessToken = token,
            AccessTokenCookieValue = token,
            RefreshTokenId = session.SessionToken,

            // Only a kept session's cookies carry an expiry; the others go when the browser closes.
            AccessTokenCookieExpiresOn = session.IsPersistent ? tokenExpiresOn : null,
            RefreshTokenExpiresOn = session.IsPersistent ? session.ExpiresOn : null,
            Persistent = session.IsPersistent,
        };
    }
}
