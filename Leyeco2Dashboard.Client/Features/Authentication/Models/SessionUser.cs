namespace Leyeco2Dashboard.Client.Features.Authentication.Models
{
    /// <summary>
    /// The signed-in account, as <c>POST /api/auth/authenticate</c> and <c>GET /api/auth/session</c>
    /// return it. The account and its access are kept on the Transaction Monitor's own Users screen.
    /// </summary>
    public class SessionUser
    {
        public Guid UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// The account's grants by name (<c>TXM_ViewDashboard</c>, …; <c>TXM_Access</c> is All access);
        /// the server lists no other system's. <c>MonitorAccess</c> reads them against <c>MonitorPolicies</c>.
        /// </summary>
        public List<string> Permissions { get; set; } = new();

        /// <summary>The server's own rule for the Transaction Monitor: an account holding any of its grants may use it.</summary>
        public bool HasMonitorAccess => Permissions.Count > 0;
    }
}
