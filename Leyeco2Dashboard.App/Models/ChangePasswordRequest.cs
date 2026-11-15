namespace Leyeco2Dashboard.App.Models
{
    /// <summary>
    /// Body of <c>POST /api/auth/change-password</c>. <see cref="Username"/> counts only for a
    /// caller with no session (the forced change at a first sign-in); a signed-in caller changes
    /// its own password. Nullable so the controller answers a missing field with an
    /// <see cref="ApiResponse"/>.
    /// </summary>
    public class ChangePasswordRequest
    {
        public string? Username { get; set; }
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
    }
}
