using Leyeco2Dashboard.App.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Leyeco2Dashboard.Client.Infrastructure.Auth
{
    /// <summary>
    /// The console's copy of the API's policies, under the same names over the same <c>permission</c> claims, so a
    /// page's <c>[Authorize(Policy = …)]</c> opens exactly when the API behind it answers. The console never sees
    /// the token (<see cref="SessionAuthenticationStateProvider"/> rebuilds the claims from
    /// <c>GET api/auth/session</c>), so this only routes and hides; the API enforces.
    /// </summary>
    public static class AuthorizationPolicies
    {
        public const string PermissionClaimType = "permission";

        /// <summary>Any grant of the Transaction Monitor: the session lists no other system's, so any permission claim is one.</summary>
        public const string SystemAccess = "SystemAccess";

        public const string Authenticated = "Authenticated";

        public static void Configure(AuthorizationOptions options)
        {
            var systemAccess = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(PermissionClaimType)
                .Build();

            options.AddPolicy(SystemAccess, systemAccess);
            options.DefaultPolicy = systemAccess;

            options.AddPolicy(Authenticated, policy => policy.RequireAuthenticatedUser());

            foreach (var name in MonitorPolicies.Grants.Concat(MonitorPolicies.Shared.Keys))
            {
                options.AddPolicy(name, policy => policy
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context => MonitorAccess.From(context.User).Can(name)));
            }
        }
    }
}
