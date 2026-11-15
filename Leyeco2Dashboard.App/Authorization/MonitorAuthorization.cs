using Leyeco2Dashboard.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Leyeco2Dashboard.App.Authorization
{
    /// <summary>
    /// The Transaction Monitor's own half of its policies. A grant's policy comes from the shared auth layer, which names it after
    /// the member and lets All access meet it too; the policies more than one grant meets
    /// (<see cref="MonitorPolicies.Shared"/>) are registered here the same way.
    /// </summary>
    public static class MonitorAuthorization
    {
        /// <summary>
        /// Refuses to start while <see cref="MonitorPolicies"/> names a grant that is not a member of
        /// <see cref="PermissionEnum"/>: its policies would match no token, locking every account out of the
        /// screen but holders of All access, and the console would hide the screen from them all.
        /// Call it after <c>AddLeyeco2Authentication</c>.
        /// </summary>
        public static void Validate()
        {
            foreach (var grant in MonitorPolicies.Grants.Concat(MonitorPolicies.Shared.Values.SelectMany(grants => grants)))
                Parse(grant);

            if (AuthConstants.AllAccess is not { } allAccess || AuthConstants.ToClaimValue(allAccess) != MonitorPolicies.AllAccess)
                throw new InvalidOperationException(
                    $"MonitorPolicies.AllAccess is {MonitorPolicies.AllAccess}, but the Transaction Monitor's all-access grant is {AuthConstants.AllAccess?.ToString() ?? "none"} (PermissionEnumExtensions.GetAllAccess).");

            // The Users screen offers exactly what the groups list: a grant left out could never be
            // given, and one listed twice would be drawn twice.
            var grouped = MonitorPolicies.Groups.SelectMany(group => group.Grants).ToList();
            var offered = MonitorPolicies.Grants.Where(grant => grant != MonitorPolicies.AllAccess);
            if (grouped.Count != grouped.Distinct().Count() || !grouped.ToHashSet().SetEquals(offered))
                throw new InvalidOperationException(
                    "MonitorPolicies.Groups must list every grant of MonitorPolicies.Grants but All access, each exactly once.");
        }

        public static void AddSharedPolicies(AuthorizationOptions options)
        {
            foreach (var (policy, grants) in MonitorPolicies.Shared)
                options.AddPolicy(policy, AuthorizationPolicies.AnyGrantOf(grants.Select(Parse).ToArray()));
        }

        private static PermissionEnum Parse(string grant)
            => AuthConstants.TryParsePermission(grant, out var permission)
                ? permission
                : throw new InvalidOperationException(
                    $"MonitorPolicies names {grant}, which is not a grant of the Transaction Monitor (a member of PermissionEnum in 1200-1299).");
    }
}
