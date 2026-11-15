using System;
using System.Collections.Generic;
using System.Linq;

namespace Leyeco2Dashboard.Data.Enums
{
    // The grants an account can hold. Stored by number in UserGrants.Permission and written by
    // name into every access token's permission claims: append new members, never renumber or
    // rename an existing one.
    //
    // Each system takes a range of a hundred, tied to its AccessedSystemEnum member by
    // GetAccessedSystem. The Transaction Monitor's is 1200-1299, clear of the HRIS's 1100-1199,
    // so the two catalogs can share one credential manager later without a renumbering.
    //
    // The grants follow the sidebar, a screen each. TXM_Access is the all-access grant
    // (GetAllAccess).
    public enum PermissionEnum
    {
        TXM_Access = 1201,
        TXM_ViewDashboard = 1202,
        TXM_ManageUsers = 1203,
        TXM_ViewOnlinePayments = 1204,
        TXM_InspectTransactions = 1205
    }

    public static class PermissionEnumExtensions
    {
        public const string ClaimType = "permission";

        public static string GetLabel(this PermissionEnum permission) => permission switch
        {
            PermissionEnum.TXM_Access             => "All access",
            PermissionEnum.TXM_ViewDashboard      => "View the dashboard",
            PermissionEnum.TXM_ManageUsers        => "Manage users",
            PermissionEnum.TXM_ViewOnlinePayments => "View online payments",
            PermissionEnum.TXM_InspectTransactions => "Inspect transactions",
            _                                    => permission.ToString(),
        };

        public static AccessedSystemEnum? GetAccessedSystem(this PermissionEnum permission) => (int)permission switch
        {
            >= 1200 and <= 1299 => AccessedSystemEnum.TransactionMonitor,
            _ => null,
        };

        // A system's grants, in value order.
        public static PermissionEnum[] GetPermissions(this AccessedSystemEnum system)
            => System.Enum.GetValues<PermissionEnum>()
                .Where(permission => permission.GetAccessedSystem() == system)
                .ToArray();

        // The grant that holds every other grant of its system, those added later included: its
        // holder passes each of that system's checks without holding the grant checked. Null for
        // a system without one. It is stored as one row, never as the grants it stands for.
        public static PermissionEnum? GetAllAccess(this AccessedSystemEnum system) => system switch
        {
            AccessedSystemEnum.TransactionMonitor => PermissionEnum.TXM_Access,
            _ => null,
        };

        // Defined so its number is taken, offered and checked nowhere. None yet; kept so the
        // services and the Users screen keep one place to ask.
        public static bool IsReserved(this PermissionEnum permission) => false;
    }
}
