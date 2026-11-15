namespace Leyeco2Dashboard.Client.Features.Users.Models
{
    /// <summary>
    /// Wire model for <c>GET api/users/paged</c>: one page of rows plus the register's key figures,
    /// which count every account whatever the search or the tab. <see cref="Page"/> is the page the
    /// server actually served (out-of-range requests are clamped server-side).
    /// </summary>
    public class UserPage
    {
        public List<UserDto> Items { get; set; } = new();

        public int Page { get; set; }

        public int PageSize { get; set; }

        /// <summary>Rows matching the search and the tab, across all pages.</summary>
        public int TotalCount { get; set; }

        public int TotalPages { get; set; }

        public int TotalUsers { get; set; }

        public int ActiveUsers { get; set; }

        /// <summary>Accounts someone is waiting on: locked, due a new password, without one, or without any access.</summary>
        public int AttentionUsers { get; set; }

        public int DeactivatedUsers { get; set; }
    }
}
