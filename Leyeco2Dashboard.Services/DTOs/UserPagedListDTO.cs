namespace Leyeco2Dashboard.Services.DTOs
{
    /// <summary>
    /// One page of the Users register plus its key figures, in a single round trip.
    /// <see cref="Page"/> echoes the page actually served: the service clamps an out-of-range
    /// request rather than failing. The figures count every account, whatever the search or filter.
    /// </summary>
    public class UserPagedListDTO
    {
        public List<UserDTO> Items { get; set; } = new();

        public int Page { get; set; }

        public int PageSize { get; set; }

        /// <summary>Rows matching the search and the status filter, across all pages.</summary>
        public int TotalCount { get; set; }

        public int TotalPages { get; set; }

        public int TotalUsers { get; set; }

        /// <summary>Accounts that are switched on, whether or not something keeps them from signing in today.</summary>
        public int ActiveUsers { get; set; }

        /// <summary>Accounts someone is waiting on: locked, due a new password, without one, or active without any access.</summary>
        public int AttentionUsers { get; set; }

        public int DeactivatedUsers { get; set; }
    }
}
