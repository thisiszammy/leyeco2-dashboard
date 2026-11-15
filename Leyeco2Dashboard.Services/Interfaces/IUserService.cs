using Leyeco2Dashboard.Services.DTOs;
using Leyeco2Dashboard.Services.Enums;

namespace Leyeco2Dashboard.Services.Interfaces
{
    /// <summary>
    /// The Transaction Monitor's staff accounts, as the Users screen keeps them: who may sign in, with what
    /// password, and what each may open. <c>Users</c> is the account (the row every audit column
    /// names); <c>UserCredentials</c> is what signs it in, <c>UserGrants</c> what it may open.
    /// Signing in and out is <see cref="ISignInService"/>'s.
    /// </summary>
    public interface IUserService
    {
        /// <param name="status">"active", "attention" or "deactivated"; anything else lists every account.</param>
        Task<UserPagedListDTO> GetUsersPagedAsync(string? search, string? status, int page, int pageSize, CancellationToken cancellationToken = default);

        Task<UserDTO> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <param name="grants"><c>PermissionEnum</c> member names of the Transaction Monitor's range.</param>
        Task<Guid> CreateUserAsync(string username, string firstName, string lastName, string? email, string password,
            bool mustChangePassword, IEnumerable<string> grants, Guid createdBy, CancellationToken cancellationToken = default);

        Task UpdateUserAsync(Guid userId, string username, string firstName, string lastName, string? email,
            Guid updatedBy, CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces what the account may open. It reaches the account's open sessions at their next
        /// silent refresh, within one access-token lifetime.
        /// </summary>
        Task SetUserAccessAsync(Guid userId, IEnumerable<string> grants, Guid updatedBy, CancellationToken cancellationToken = default);

        /// <summary>Deactivating ends the account's open sessions.</summary>
        Task SetUserActiveAsync(Guid userId, bool isActive, Guid updatedBy, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets a password an administrator chose, for an account that forgot its own or never had
        /// one. Ends the account's open sessions and lifts a lock.
        /// </summary>
        Task ResetUserPasswordAsync(Guid userId, string newPassword, bool mustChangePassword, Guid updatedBy, CancellationToken cancellationToken = default);

        Task UnlockUserAsync(Guid userId, Guid updatedBy, CancellationToken cancellationToken = default);

        /// <summary>
        /// Makes sure someone can open the Users screen. While no active account can, it creates
        /// the named one (or gives an existing account of that name the password) with All Transaction Monitor
        /// access, to change its password at its first sign-in. Otherwise it changes nothing, so it
        /// cannot be used to take over a system that has an administrator.
        /// </summary>
        Task<AdministratorBootstrapEnum> EnsureAdministratorAsync(string? username, string? password, CancellationToken cancellationToken = default);
    }
}
