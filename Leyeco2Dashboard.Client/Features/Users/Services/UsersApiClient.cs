using Leyeco2Dashboard.Client.Features.Users.Models;
using Leyeco2Dashboard.Client.Infrastructure.Api;

namespace Leyeco2Dashboard.Client.Features.Users.Services
{
    /// <summary>
    /// Typed client for the Transaction Monitor API's user endpoints: the accounts that sign in, their passwords
    /// and what each may open. Every call takes the Manage users grant.
    /// </summary>
    public class UsersApiClient
    {
        private readonly ApiClient _api;

        public UsersApiClient(ApiClient api)
        {
            _api = api;
        }

        /// <param name="status">"active", "attention" or "deactivated"; null lists every account.</param>
        public Task<UserPage> GetPageAsync(string? search, string? status, int page, int pageSize, CancellationToken ct = default)
        {
            var uri = $"api/users/paged?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(search))
                uri += $"&search={Uri.EscapeDataString(search.Trim())}";
            if (!string.IsNullOrWhiteSpace(status))
                uri += $"&status={Uri.EscapeDataString(status)}";

            return _api.GetDataAsync<UserPage>(uri, ct);
        }

        public Task<Guid> AddAsync(
            string username, string firstName, string lastName, string email, string password,
            bool mustChangePassword, IEnumerable<string> grants, CancellationToken ct = default)
            => _api.PostDataAsync<Guid>("api/users/add", new
            {
                Username = username,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Password = password,
                MustChangePassword = mustChangePassword,
                Grants = grants.ToList()
            }, ct);

        public Task UpdateAsync(Guid userId, string username, string firstName, string lastName, string email, CancellationToken ct = default)
            => _api.PutAsync($"api/users/{userId}", new { Username = username, FirstName = firstName, LastName = lastName, Email = email }, ct);

        /// <summary>Replaces what the account may open with <paramref name="grants"/>, the whole set.</summary>
        public Task SetAccessAsync(Guid userId, IEnumerable<string> grants, CancellationToken ct = default)
            => _api.PutAsync($"api/users/{userId}/access", new { Grants = grants.ToList() }, ct);

        public Task SetActiveAsync(Guid userId, bool isActive, CancellationToken ct = default)
            => _api.PutAsync($"api/users/{userId}/status", new { IsActive = isActive }, ct);

        public Task ResetPasswordAsync(Guid userId, string newPassword, bool mustChangePassword, CancellationToken ct = default)
            => _api.PostAsync($"api/users/{userId}/reset-password", new { NewPassword = newPassword, MustChangePassword = mustChangePassword }, ct);

        public Task UnlockAsync(Guid userId, CancellationToken ct = default)
            => _api.PostAsync($"api/users/{userId}/unlock", new { }, ct);
    }
}
