namespace Leyeco2Dashboard.Services.Enums
{
    /// <summary>
    /// What an account can do right now, read off its credentials: never stored. The desktop
    /// mirrors the numbers.
    /// </summary>
    public enum UserAccountStatusEnum
    {
        /// <summary>Signs in.</summary>
        Active = 1,

        /// <summary>Signs in only to set a new password: an administrator set the one it has.</summary>
        PasswordChangeRequired = 2,

        /// <summary>Too many wrong passwords: refused until the lock lapses or an administrator lifts it.</summary>
        Locked = 3,

        /// <summary>Turned off by an administrator: refused, and its sessions ended.</summary>
        Deactivated = 4,

        /// <summary>No password on file: a row from before the Transaction Monitor kept its own accounts.</summary>
        NoPassword = 5,
    }
}
