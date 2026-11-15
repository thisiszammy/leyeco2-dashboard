namespace Leyeco2Dashboard.App.Models
{
    /// <summary>Body of <c>POST api/users/add</c>. <see cref="Grants"/> are <c>PermissionEnum</c> member names.</summary>
    public class AddUserRequest
    {
        public string? Username { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }

        /// <summary>The account sets a password of its own at its first sign-in. On unless turned off.</summary>
        public bool MustChangePassword { get; set; } = true;

        public List<string> Grants { get; set; } = new();
    }

    /// <summary>Body of <c>PUT api/users/{userId}</c>.</summary>
    public class UpdateUserRequest
    {
        public string? Username { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
    }

    /// <summary>Body of <c>PUT api/users/{userId}/access</c>: the whole set the account is to hold.</summary>
    public class SetUserAccessRequest
    {
        public List<string> Grants { get; set; } = new();
    }

    /// <summary>Body of <c>PUT api/users/{userId}/status</c>.</summary>
    public class SetUserStatusRequest
    {
        public bool IsActive { get; set; }
    }

    /// <summary>Body of <c>POST api/users/{userId}/reset-password</c>.</summary>
    public class ResetUserPasswordRequest
    {
        public string? NewPassword { get; set; }
        public bool MustChangePassword { get; set; } = true;
    }
}
