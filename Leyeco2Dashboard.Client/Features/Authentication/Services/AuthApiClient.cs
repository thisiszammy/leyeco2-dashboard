using Leyeco2Dashboard.Client.Features.Authentication.Models;
using Leyeco2Dashboard.Client.Infrastructure.Api;

namespace Leyeco2Dashboard.Client.Features.Authentication.Services
{
    /// <summary>
    /// Typed client for the Transaction Monitor API's sign-in endpoints. The API checks the account against the
    /// Transaction Monitor's own users and keeps the session in HttpOnly cookies of its own; this client only
    /// ever sees who signed in.
    /// </summary>
    public class AuthApiClient
    {
        /// <summary>The status <see cref="LoginAsync"/> fails with when the account must set a password of its own first.</summary>
        public const int PasswordChangeRequired = 428;

        private readonly ApiClient _api;

        public AuthApiClient(ApiClient api)
        {
            _api = api;
        }

        /// <summary>
        /// Signs in with the account's username and password. Throws <see cref="ApiException"/> with
        /// a message safe to show the user: refused credentials (401), an account that is
        /// deactivated or holds no access (403), a locked one (423), too many attempts (429), or
        /// one whose password an administrator set (<see cref="PasswordChangeRequired"/>), which
        /// goes on to <see cref="ChangePasswordAsync"/>.
        /// </summary>
        public Task<SessionUser> LoginAsync(string username, string password, CancellationToken ct = default)
            => _api.PostDataAsync<SessionUser>(
                "api/auth/authenticate",
                new { Username = username, Password = password },
                ct);

        /// <summary>
        /// Replaces the account's password on proof of the current one, and signs it in under the
        /// new one: its other sessions end. <paramref name="username"/> counts only while nobody is
        /// signed in (the change a first sign-in asks for); a signed-in account changes its own.
        /// A wrong current password or a new one that breaks the rule fails with the API's words (400).
        /// </summary>
        public Task<SessionUser> ChangePasswordAsync(string? username, string currentPassword, string newPassword, CancellationToken ct = default)
            => _api.PostDataAsync<SessionUser>(
                "api/auth/change-password",
                new { Username = username, CurrentPassword = currentPassword, NewPassword = newPassword },
                ct);

        /// <summary>Ends the session and clears the API's cookies.</summary>
        public Task LogoutAsync(CancellationToken ct = default)
            => _api.PostAsync("api/auth/logout", new { }, ct);
    }
}
