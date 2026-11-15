using Leyeco2Dashboard.App.Authorization;

namespace Leyeco2Dashboard.Client.Features.Users.Models
{
    /// <summary>
    /// What an account can do right now. Mirrors the API's <c>UserAccountStatusEnum</c> number for
    /// number.
    /// </summary>
    public enum UserAccountStatus
    {
        Active = 1,
        PasswordChangeRequired = 2,
        Locked = 3,
        Deactivated = 4,
        NoPassword = 5,
    }

    /// <summary>Wire model for the Transaction Monitor API's user endpoints: one account, as the Users screen lists it.</summary>
    public class UserDto
    {
        public Guid UserId { get; set; }

        public string Username { get; set; } = "";

        public string FirstName { get; set; } = "";

        public string LastName { get; set; } = "";

        public string Email { get; set; } = "";

        public UserAccountStatus Status { get; set; }

        /// <summary>When a lock lapses by itself; set only while the account is locked.</summary>
        public DateTime? LockedUntil { get; set; }

        /// <summary>The account's grants by name (<c>TXM_ViewDashboard</c>, …), read against <see cref="MonitorPolicies"/>.</summary>
        public List<string> Grants { get; set; } = new();

        public DateTime? LastSignInOn { get; set; }

        public DateTime? PasswordChangedOn { get; set; }

        public DateTime? CreatedOn { get; set; }

        public string FullName => $"{FirstName} {LastName}".Trim();

        public bool HasAllAccess => Grants.Contains(MonitorPolicies.AllAccess);
    }
}
