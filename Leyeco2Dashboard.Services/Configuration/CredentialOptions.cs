namespace Leyeco2Dashboard.Services.Configuration
{
    /// <summary>
    /// Host-supplied settings for sign-in sessions and lock-out. The API host fills it from the
    /// optional <c>Auth:*</c> keys (<c>Auth__SessionHours</c> … in its <c>.env</c>) and registers it as
    /// a singleton; the defaults stand for whatever is left out.
    /// </summary>
    public class CredentialOptions
    {
        /// <summary>How long a session lasts without use. Every silent refresh starts the count again.</summary>
        public int SessionHours { get; set; } = 12;

        /// <summary>The same, for a session whose sign-in asked to be kept past the browser.</summary>
        public int KeptSessionDays { get; set; } = 14;

        /// <summary>Wrong passwords in a row before the account is locked.</summary>
        public int MaxFailedSignIns { get; set; } = 5;

        public int LockoutMinutes { get; set; } = 15;
    }
}
