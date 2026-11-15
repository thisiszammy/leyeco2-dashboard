using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Leyeco2Dashboard.Client.Features.Authentication.Models;
using Leyeco2Dashboard.Client.Infrastructure.Api;
using Microsoft.AspNetCore.Components.Authorization;

namespace Leyeco2Dashboard.Client.Infrastructure.Auth
{
    /// <summary>
    /// Drives Blazor's authentication state from the Transaction Monitor API's session. The session's tokens ride
    /// in HttpOnly cookies on the API's origin (sent with every request by
    /// <see cref="BrowserCredentialsHandler"/>), which this client never reads: it asks
    /// <c>GET /api/auth/session</c> who the cookies authenticate and builds the
    /// <see cref="ClaimsPrincipal"/> from the answer, with the claim names the token itself carries.
    /// The API renews the session silently, so the answer stands until a request comes back 401
    /// (the session ended) or 403 (a grant the account lacks), when it is read again
    /// (<see cref="RevalidateAsync"/>).
    /// </summary>
    public class SessionAuthenticationStateProvider : AuthenticationStateProvider
    {
        private const string SessionEndedNotice = "Your session has ended. Sign in again to continue.";

        private const string NoAccessNotice =
            "This account no longer has access to the Transaction Monitor. Ask an administrator to give it access on the Users screen.";

        private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

        private readonly HttpClient _http;

        private Task<ClaimsPrincipal>? _initialLoad;
        private Task? _revalidation;
        private ClaimsPrincipal? _principal;
        private string? _notice;

        public SessionAuthenticationStateProvider(HttpClient http)
        {
            _http = http;
        }

        /// <summary>The signed-in account, or null when there is none.</summary>
        public SessionUser? CurrentUser { get; private set; }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            if (_principal is null)
            {
                // Every component asking at start-up shares the one session read; a sign-in or
                // sign-out made while it was under way is newer, and stands.
                var loaded = await (_initialLoad ??= LoadAsync());
                _principal ??= loaded;
            }

            return new AuthenticationState(_principal);
        }

        /// <summary>Adopts the session a sign-in just opened (the API has stamped its cookies).</summary>
        public void MarkSignedIn(SessionUser user)
        {
            CurrentUser = user;
            _notice = null;
            Publish(BuildPrincipal(user));
        }

        /// <summary>Forgets the session here; the cookies are the API's to clear.</summary>
        public void MarkSignedOut() => Publish(Anonymous, signedOut: true);

        /// <summary>
        /// The HTTP layer proved the session gone (401), or a fresh read of it found the account
        /// without any grant of the Transaction Monitor. Only a session that was signed in leaves a notice: a sign-in
        /// refused on the login page answers 401 too, and must not read as an ended session.
        /// </summary>
        public void EndSession(bool noAccess = false)
        {
            if (CurrentUser is not null)
                _notice = noAccess ? NoAccessNotice : SessionEndedNotice;

            Publish(Anonymous, signedOut: true);
        }

        /// <summary>
        /// Reads the session again after the API refused a request (403): the account may have lost a grant
        /// since the shell last read it, and then the sidebar and the screens drop what it opened; an account
        /// left with no grant of the Transaction Monitor is signed out with the no-access notice. The refusals of one burst share
        /// one read.
        /// </summary>
        public Task RevalidateAsync()
        {
            if (_revalidation is { IsCompleted: false })
                return _revalidation;

            return _revalidation = RevalidateCoreAsync();
        }

        /// <summary>Why the last session ended, once; the login page shows it above the form.</summary>
        public string? ConsumeNotice()
        {
            var notice = _notice;
            _notice = null;
            return notice;
        }

        private async Task<ClaimsPrincipal> LoadAsync()
        {
            try
            {
                using var response = await _http.GetAsync("api/auth/session");
                if (!response.IsSuccessStatusCode)
                    return Anonymous;

                var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<SessionUser>>();
                if (envelope?.Data is not { } user)
                    return Anonymous;

                if (!user.HasMonitorAccess)
                {
                    // Still signed in, but its last grant was revoked since.
                    _notice = NoAccessNotice;
                    await EndServerSessionAsync();
                    return Anonymous;
                }

                CurrentUser = user;
                return BuildPrincipal(user);
            }
            catch
            {
                // Unreachable API: nothing could be authorized anyway, so present as signed out.
                return Anonymous;
            }
        }

        private async Task RevalidateCoreAsync()
        {
            try
            {
                if (CurrentUser is null)
                    return;

                using var response = await _http.GetAsync("api/auth/session");
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    EndSession();
                    return;
                }

                // The API could not answer: nothing says the grants changed.
                if (!response.IsSuccessStatusCode)
                    return;

                var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<SessionUser>>();
                if (envelope?.Data is not { } user || CurrentUser is null)
                    return;

                if (!user.HasMonitorAccess)
                {
                    await EndServerSessionAsync();
                    EndSession(noAccess: true);
                    return;
                }

                if (user.Permissions.ToHashSet(StringComparer.Ordinal).SetEquals(CurrentUser.Permissions))
                    return;

                CurrentUser = user;
                Publish(BuildPrincipal(user));
            }
            catch
            {
                // Best effort: the refusal that asked for this read has reached the user already.
            }
        }

        private async Task EndServerSessionAsync()
        {
            try
            {
                using var response = await _http.PostAsync("api/auth/logout", content: null);
            }
            catch
            {
                // Best effort: the cookies of a session without access open nothing here.
            }
        }

        private void Publish(ClaimsPrincipal principal, bool signedOut = false)
        {
            if (signedOut)
                CurrentUser = null;

            _principal = principal;
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(principal)));
        }

        private static ClaimsPrincipal BuildPrincipal(SessionUser user)
        {
            var claims = new List<Claim>
            {
                new("sub", user.UserId.ToString()),
                new("unique_name", user.Username),
                new("given_name", user.FirstName),
                new("family_name", user.LastName)
            };

            if (!string.IsNullOrEmpty(user.Email))
                claims.Add(new Claim("email", user.Email));

            foreach (var permission in user.Permissions)
                claims.Add(new Claim("permission", permission));

            return new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies", "unique_name", roleType: null));
        }
    }
}
