using System.Security.Claims;
using Leyeco2Dashboard.App.Authorization;

namespace Leyeco2Dashboard.Client.Infrastructure.Auth
{
    /// <summary>
    /// What the signed-in account may open and do, read off the grants its session lists, for markup that shows,
    /// hides or disables what one grant decides (a sidebar link, an Add button, a row's edit icon).
    /// <c>MainLayout</c> cascades a new one whenever the session changes, so whatever reads it renders again.
    /// The same <see cref="MonitorPolicies"/> back the pages' <c>[Authorize]</c> (<see cref="AuthorizationPolicies"/>)
    /// and the API's, which is what enforces them: hiding a button only spares the user a refusal.
    /// </summary>
    public sealed class MonitorAccess
    {
        public static readonly MonitorAccess None = new(new HashSet<string>());

        private readonly IReadOnlySet<string> _grants;

        private MonitorAccess(IReadOnlySet<string> grants)
        {
            _grants = grants;
        }

        public static MonitorAccess From(ClaimsPrincipal user)
            => new(user.FindAll(AuthorizationPolicies.PermissionClaimType)
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.Ordinal));

        public static MonitorAccess From(IEnumerable<string> grants)
            => new(grants.ToHashSet(StringComparer.Ordinal));

        public bool Can(string policy) => MonitorPolicies.IsMetBy(policy, _grants);

        /// <summary>The screen this account lands on: the dashboard, or the first screen it may open.</summary>
        public string HomePath => MonitorPolicies.HomeFor(_grants);

        /// <summary>A disabled control's title: what it would take, "Needs “Manage users”".</summary>
        public static string Needs(string policy) => "Needs " + MonitorPolicies.Needs(policy);
    }
}
