namespace Leyeco2Dashboard.Services.Exceptions
{
    public enum SignInFailureKind
    {
        /// <summary>No such account, or the wrong password: the caller is never told which.</summary>
        InvalidCredentials = 1,

        Locked = 2,

        Deactivated = 3,

        /// <summary>The password is right, but the account holds no grant.</summary>
        NoAccess = 4,

        /// <summary>The password is right, but an administrator set it: a new one comes first.</summary>
        PasswordChangeRequired = 5,

        /// <summary>The session token is unknown, expired or revoked, or its account can no longer sign in.</summary>
        SessionEnded = 6,
    }

    /// <summary>A sign-in or a session the credentials refuse. <see cref="Exception.Message"/> is written for the user.</summary>
    public class SignInException : Exception
    {
        public SignInFailureKind Kind { get; }

        public SignInException(SignInFailureKind kind, string message) : base(message)
        {
            Kind = kind;
        }
    }
}
