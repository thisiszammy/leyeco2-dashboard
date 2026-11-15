using Leyeco2Dashboard.App.Authorization;

namespace Leyeco2Dashboard.Client.Features.Users.Models
{
    /// <summary>
    /// What a set of grants opens, part by part of the Transaction Monitor (<see cref="MonitorPolicies.Groups"/>):
    /// the one reading the Users register and the access editor share.
    /// </summary>
    public static class UserAccess
    {
        /// <summary>How much of one part of the Transaction Monitor an account holds.</summary>
        public sealed record GroupShare(string Name, int Held, int Of)
        {
            public int Percent => Of == 0 ? 0 : (int)Math.Round(100d * Held / Of);
        }

        /// <summary>Every permission the editor offers, All access aside.</summary>
        public static readonly int Offered = MonitorPolicies.Groups.Sum(group => group.Grants.Count);

        public static bool IsAll(IEnumerable<string> grants) => grants.Contains(MonitorPolicies.AllAccess);

        /// <summary>The offered permissions held; All access holds every one.</summary>
        public static int Count(IReadOnlyCollection<string> grants)
            => IsAll(grants) ? Offered : MonitorPolicies.Groups.Sum(group => group.Grants.Count(grants.Contains));

        public static IReadOnlyList<GroupShare> Footprint(IReadOnlyCollection<string> grants)
        {
            var all = IsAll(grants);
            return MonitorPolicies.Groups
                .Select(group => new GroupShare(
                    group.Name,
                    all ? group.Grants.Count : group.Grants.Count(grants.Contains),
                    group.Grants.Count))
                .ToList();
        }

        /// <summary>"All access", "No access" or "7 of 24".</summary>
        public static string Summary(IReadOnlyCollection<string> grants)
        {
            if (IsAll(grants)) return MonitorPolicies.Label(MonitorPolicies.AllAccess);

            var count = Count(grants);
            return count == 0 ? "No access" : $"{count} of {Offered}";
        }

        /// <summary>The parts an account reaches, for a title: "Workforce 2 of 5, Leave 4 of 4".</summary>
        public static string Detail(IReadOnlyCollection<string> grants)
        {
            if (IsAll(grants)) return MonitorPolicies.Describe(MonitorPolicies.AllAccess);

            var parts = Footprint(grants).Where(share => share.Held > 0).Select(share => $"{share.Name} {share.Held} of {share.Of}").ToList();
            return parts.Count == 0
                ? "This account holds no permission, so it can't sign in."
                : string.Join(", ", parts);
        }
    }
}
