namespace Leyeco2Dashboard.Data.Enums
{
    /// <summary>
    /// Controlled vocabulary of auditable write operations. Every database write records an
    /// <see cref="Entities.Database.ActivityLog"/> row whose <c>Activity</c> column stores one of
    /// these values. Group new activities by domain range and never repurpose an existing value
    /// for a different action.
    /// </summary>
    public enum ActivityEnum
    {
        // User (100–199). The accounts are the Transaction Monitor's own, kept on its Users screen.
        CreatedUser = 100,
        UpdatedUser = 101,
        ResetUserPassword = 102,
        SignedIn = 103,
        ChangedUserAccess = 104,
        DeactivatedUser = 105,
        ReactivatedUser = 106,
        UnlockedUser = 107,
        ChangedOwnPassword = 108,
        LockedUser = 109,

        // Transactions (200–299) and reconciliation (300–399) take the next ranges as those screens arrive.
    }
}
