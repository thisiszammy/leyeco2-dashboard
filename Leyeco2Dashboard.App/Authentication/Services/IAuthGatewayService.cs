using Leyeco2Dashboard.Services.DTOs;
using Leyeco2Dashboard.Services.Exceptions;

namespace Leyeco2Dashboard.Authentication.Services;

/// <summary>
/// The door between this host's sign-in pipeline (its <c>AuthController</c>, the silent-refresh
/// middleware) and the Transaction Monitor's own accounts. Each call checks the account in a DI scope of its
/// own and comes back with the session and a freshly minted access token.
/// </summary>
public interface IAuthGatewayService
{
    /// <exception cref="SignInException">The sign-in is refused; its message is for the user.</exception>
    Task<AuthGatewayResult> AuthenticateAsync(
        string username,
        string password,
        bool rememberMe,
        CancellationToken cancellationToken);

    /// <param name="persistent">Unused: the session's own row knows whether it is a kept one.</param>
    /// <exception cref="AuthGatewayException">
    /// <see cref="AuthGatewayErrorKind.Unauthorized"/> when the session is over; anything else
    /// that goes wrong (the database is down) is not a verdict on the session.
    /// </exception>
    Task<AuthGatewayResult> RefreshAsync(
        string refreshTokenId,
        bool persistent,
        CancellationToken cancellationToken);

    /// <summary>
    /// Ends the session, best effort: a failure is logged, never thrown, since the host clears
    /// its own cookies regardless.
    /// </summary>
    Task LogoutAsync(
        string? refreshTokenId,
        string? accessToken,
        CancellationToken cancellationToken);

    /// <summary>Sets a password of the account's own choosing, on proof of the current one, and signs it in.</summary>
    /// <exception cref="SignInException">The current password is refused.</exception>
    /// <exception cref="UserValidationException">The new password breaks a rule.</exception>
    Task<AuthGatewayResult> ChangePasswordAsync(
        string username,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken);
}

/// <summary>A session, as the cookies carry it. Neither token ever appears in a response body.</summary>
public class AuthGatewayResult
{
    /// <summary>Who the session signs in, and what they may open as of this moment.</summary>
    public SessionDTO Account { get; set; } = null!;

    public string AccessToken { get; set; } = string.Empty;

    /// <summary>The session token: the refresh cookie's value.</summary>
    public string? RefreshTokenId { get; set; }

    /// <summary>Null for a session cookie: a session that was not kept ends with the browser.</summary>
    public DateTime? RefreshTokenExpiresOn { get; set; }

    public string? AccessTokenCookieValue { get; set; }
    public DateTime? AccessTokenCookieExpiresOn { get; set; }

    /// <summary>The sign-in asked for the session to be kept ("remember me").</summary>
    public bool Persistent { get; set; }
}
