using Leyeco2Dashboard.Services.DTOs;
using Leyeco2Dashboard.Services.Exceptions;

namespace Leyeco2Dashboard.Services.Interfaces
{
    /// <summary>
    /// Signing an account in and keeping its session, against the Transaction Monitor's own accounts
    /// (<see cref="IUserService"/> keeps them). A refusal is a <see cref="SignInException"/> whose
    /// message is written for the user.
    /// </summary>
    public interface ISignInService
    {
        /// <summary>
        /// Checks the password and opens a session. Refuses an unknown account or a wrong password
        /// alike, a locked or deactivated account, one whose password an administrator set
        /// (<see cref="SignInFailureKind.PasswordChangeRequired"/>: it goes through
        /// <see cref="ChangePasswordAsync"/>), and one that holds no grant.
        /// </summary>
        Task<SessionDTO> SignInAsync(string username, string password, bool persistent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Keeps a session going: reads the account and its grants again and pushes the session's
        /// end back. Refuses (<see cref="SignInFailureKind.SessionEnded"/>) a token that is unknown,
        /// expired or revoked, and an account that could no longer sign in.
        /// </summary>
        Task<SessionDTO> RefreshAsync(string sessionToken, CancellationToken cancellationToken = default);

        /// <summary>Ends the one session the token keeps. An unknown token is nothing to end.</summary>
        Task SignOutAsync(string? sessionToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces the account's password with one of its own choosing, on proof of the current
        /// one, ends its other sessions and opens a new one. Serves both the forced change at a
        /// first sign-in and a signed-in account changing its password.
        /// </summary>
        Task<SessionDTO> ChangePasswordAsync(string username, string currentPassword, string newPassword, bool persistent, CancellationToken cancellationToken = default);
    }
}
