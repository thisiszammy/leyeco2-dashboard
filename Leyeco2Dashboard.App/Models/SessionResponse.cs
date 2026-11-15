namespace Leyeco2Dashboard.App.Models
{
    /// <summary>
    /// The signed-in account, as <c>POST /api/auth/authenticate</c> and <c>GET /api/auth/session</c>
    /// return it. The console builds its authentication state from this, since its tokens ride
    /// only in HttpOnly cookies it cannot read.
    /// </summary>
    public class SessionResponse
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// The account's grants of the Transaction Monitor's block, by <c>PermissionEnum</c> member name
        /// (<c>TXM_Access</c>). Empty when it holds none: signed in, but with no access here.
        /// </summary>
        public List<string> Permissions { get; set; } = new();
    }
}
