using Leyeco2Dashboard.Data.Enums;

namespace Leyeco2Dashboard.Services.DTOs
{
    /// <summary>
    /// An open session: who it signs in, what they may open, and the token that keeps it. The host
    /// mints the access token from this and sends the session token back as an HttpOnly cookie; it
    /// never reaches a response body.
    /// </summary>
    public class SessionDTO
    {
        public Guid UserId { get; set; }

        public string Username { get; set; } = null!;

        public string FirstName { get; set; } = null!;

        public string LastName { get; set; } = null!;

        public string Email { get; set; } = null!;

        /// <summary>The account's grants as they stand now: read again at every refresh.</summary>
        public List<PermissionEnum> Permissions { get; set; } = new();

        /// <summary>The secret the refresh cookie carries. Only its hash is stored.</summary>
        public string SessionToken { get; set; } = null!;

        public DateTime ExpiresOn { get; set; }

        /// <summary>The sign-in asked for the session to outlive the browser.</summary>
        public bool IsPersistent { get; set; }
    }
}
