namespace Leyeco2Dashboard.App.Models
{
    /// <summary>
    /// Body of <c>POST /api/auth/authenticate</c>: the account's username and password. Nullable so the controller returns a consistent <see cref="ApiResponse"/> for
    /// missing fields rather than the framework's default 400 payload.
    /// </summary>
    public class LoginRequest
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
