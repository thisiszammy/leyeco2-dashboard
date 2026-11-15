namespace Leyeco2Dashboard.Authentication.Services;

public enum AuthGatewayErrorKind
{
    /// <summary>No verdict on the session: what checks it failed.</summary>
    Unknown = 0,

    /// <summary>The session is over: its token is unknown, expired or revoked, or its account can no longer sign in.</summary>
    Unauthorized = 1,
}

/// <summary>
/// A session could not be renewed. Only <see cref="AuthGatewayErrorKind.Unauthorized"/> is a
/// verdict on it; a sign-in's own refusals are <c>SignInException</c>s, which the sign-in routes
/// answer themselves.
/// </summary>
public class AuthGatewayException : Exception
{
    public AuthGatewayErrorKind Kind { get; }

    /// <summary>
    /// Nothing judged the session: what checks it could not be reached or failed. A transient
    /// failure must not end a session the next attempt may well renew.
    /// </summary>
    public bool IsTransient => Kind != AuthGatewayErrorKind.Unauthorized;

    public AuthGatewayException(AuthGatewayErrorKind kind, string message, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
    }
}
