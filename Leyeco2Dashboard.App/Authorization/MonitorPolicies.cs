namespace Leyeco2Dashboard.App.Authorization
{
    /// <summary>
    /// The Transaction Monitor's grants, and the policies its endpoints and screens are gated on: one file this API
    /// and the <c>Leyeco2Dashboard.Client</c> console both compile (the console links it, as it links
    /// <c>MonitorVersion</c>), so the console's sidebar, pages and buttons open exactly what this API's
    /// <c>[Authorize]</c> attributes let through. A grant is a <c>PermissionEnum</c> member of the Transaction
    /// Monitor's range (<c>Leyeco2Dashboard.Data/Enums</c>), written here by its name because the WebAssembly console
    /// cannot load the data layer; this API refuses to start while a name here is not one
    /// (<c>MonitorAuthorization.Validate</c>).
    /// </summary>
    /// <remarks>
    /// Internal for the reason <c>MonitorVersion</c> is: Debug builds of this API reference the console, where a
    /// public copy would collide with this one.
    /// </remarks>
    internal static class MonitorPolicies
    {
        // Meets every policy, the grants added later included. The Users screen stores it as one grant and ticks
        // the others in its dialog without storing them.
        public const string AllAccess = "TXM_Access";

        // A screen each, in the sidebar's order. A grant's own policy is named after it.
        public const string ViewDashboard = "TXM_ViewDashboard";
        public const string ViewOnlinePayments = "TXM_ViewOnlinePayments";
        public const string InspectTransactions = "TXM_InspectTransactions";
        public const string ManageUsers = "TXM_ManageUsers";

        public static readonly IReadOnlyList<string> Grants =
        [
            AllAccess,
            ViewDashboard,
            ViewOnlinePayments,
            InspectTransactions,
            ManageUsers,
        ];

        // The policies of endpoints more than one screen calls, each met by any of its grants. None yet: the
        // transaction registers will share their lookups through here.
        public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Shared = new Dictionary<string, IReadOnlyList<string>>();

        /// <summary>The grants that meet a policy, All access aside.</summary>
        public static IReadOnlyList<string> GrantsFor(string policy)
            => Shared.TryGetValue(policy, out var grants) ? grants : [policy];

        public static bool IsMetBy(string policy, IReadOnlySet<string> heldGrants)
            => heldGrants.Contains(AllAccess) || GrantsFor(policy).Any(heldGrants.Contains);

        // Each grant in words (PermissionEnumExtensions.GetLabel's), for the console's "needs …" lines and the
        // Users screen.
        public static string Label(string grant) => grant switch
        {
            AllAccess          => "All access",
            ViewDashboard      => "View the dashboard",
            ViewOnlinePayments  => "View online payments",
            InspectTransactions => "Inspect transactions",
            ManageUsers         => "Manage users",
            _                   => grant,
        };

        /// <summary>The claim a policy takes, in words: "“Manage users”".</summary>
        public static string Needs(string policy)
            => string.Join(" or ", GrantsFor(policy).Select(grant => "“" + Label(grant) + "”"));

        /// <summary>A part of the Transaction Monitor and the grants that open its screens, as the Users screen's access editor lists them.</summary>
        public sealed record GrantGroup(string Name, string Summary, IReadOnlyList<string> Grants);

        // The sidebar's groups, in its order. Every grant but All access sits in exactly one
        // (MonitorAuthorization.Validate holds the API to that), so the editor offers each once.
        public static readonly IReadOnlyList<GrantGroup> Groups =
        [
            new("Monitoring",
                "The dashboards, what the collections ledger records as paid by channel and the payments made online, and the inspector that follows each online payment into the ledger.",
                [ViewDashboard, ViewOnlinePayments, InspectTransactions]),
            new("Administration",
                "The accounts that sign in to the Transaction Monitor.",
                [ManageUsers]),
        ];

        /// <summary>What holding a grant lets an account do, for the access editor.</summary>
        public static string Describe(string grant) => grant switch
        {
            AllAccess          => "Open every screen of the Transaction Monitor and do everything in it, including whatever a later version adds.",
            ViewDashboard      => "Open the overall dashboard: the bills paid at the tellers, SBA, ECPay, Bayad Center, SM Payment and Bizlink, as the collections ledger records them.",
            ViewOnlinePayments => "Open the Online Payments dashboard: what was paid through the online payment facility, by provider and outcome.",
            InspectTransactions => "Open the Transaction Inspector: each bill paid online, as the facility, its posting queue and the collections ledger hold it, with what needs attention, and export it as PDF or Excel.",
            ManageUsers       => "Add accounts, set their passwords, choose what each may open, and deactivate them. Whoever holds it can give any account any access, their own included.",
            _                  => string.Empty,
        };

        /// <summary>
        /// Where a signed-in account lands: the dashboard when it may open it, else the first screen it can. One
        /// place for the sign-in page and the sidebar's home link to agree on.
        /// </summary>
        public static string HomeFor(IReadOnlySet<string> heldGrants)
            => IsMetBy(ViewDashboard, heldGrants) ? "/"
             : IsMetBy(ViewOnlinePayments, heldGrants) ? "/online-payments"
             : IsMetBy(InspectTransactions, heldGrants) ? "/transaction-inspector"
             : IsMetBy(ManageUsers, heldGrants) ? "/administration/users"
             : "/";
    }
}
