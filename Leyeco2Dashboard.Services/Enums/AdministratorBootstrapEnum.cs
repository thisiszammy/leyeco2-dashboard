namespace Leyeco2Dashboard.Services.Enums
{
    /// <summary>What <c>IUserService.EnsureAdministratorAsync</c> found at start-up.</summary>
    public enum AdministratorBootstrapEnum
    {
        /// <summary>An active account can already manage users: nothing was changed.</summary>
        AlreadyPresent = 1,

        /// <summary>No account could, and the configured one now can.</summary>
        Created = 2,

        /// <summary>No account can, and no username and password were configured to make one.</summary>
        NotConfigured = 3,
    }
}
