using Leyeco2Dashboard.Services.Enums;

namespace Leyeco2Dashboard.Services.DTOs
{
    /// <summary>An account as the Users screen lists it. It never carries the password hash.</summary>
    public class UserDTO
    {
        public Guid UserId { get; set; }

        public string Username { get; set; } = null!;

        public string FirstName { get; set; } = null!;

        public string LastName { get; set; } = null!;

        public string Email { get; set; } = null!;

        public UserAccountStatusEnum Status { get; set; }

        /// <summary>When the lock lapses by itself; set only while <see cref="Status"/> is Locked.</summary>
        public DateTime? LockedUntil { get; set; }

        /// <summary>The account's grants by <c>PermissionEnum</c> member name, in the enum's order.</summary>
        public List<string> Grants { get; set; } = new();

        public DateTime? LastSignInOn { get; set; }

        public DateTime? PasswordChangedOn { get; set; }

        public DateTime? CreatedOn { get; set; }
    }
}
