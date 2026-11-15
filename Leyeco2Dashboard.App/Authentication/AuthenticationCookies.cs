using Leyeco2Dashboard.Authentication.Services;

namespace Leyeco2Dashboard.Authentication;

/// <summary>
/// The session's cookies on the host's own origin, which the browser carries back on every call:
/// the access token, the session token that renews it, and the marker of a kept session.
/// </summary>
/// <remarks>
/// The names carry the system (<c>l2auth_{segment}_…</c>, see <see cref="SystemCookieSegment"/>).
/// A browser scopes a cookie by host, never by port, so every LEYECO II app served from one host
/// shares one cookie jar: under bare names, a sign-in to one app would overwrite another's
/// session.
/// </remarks>
public static class AuthenticationCookies
{
    private const string CookiePath = "/";

    private static string? _domain;
    private static string? _systemSegment;

    public static string RefreshTokenName => Name("rtk");
    public static string AccessTokenName => Name("atk");

    /// <summary>"1" while the session is a kept one; absent otherwise. Not a secret, HttpOnly all the same.</summary>
    public static string RememberMeName => Name("rm");

    /// <summary>
    /// The system segment of the cookie names. These names are live in browsers: changing one
    /// signs every user of that host out, so a segment never changes once shipped, and a new
    /// system gets a segment of its own here.
    /// </summary>
    public static string SystemCookieSegment(AccessedSystemEnum system) => system switch
    {
        AccessedSystemEnum.TransactionMonitor => "txm",
        _ => throw new NotSupportedException(
            $"{system} has no auth cookie names yet: add its segment to AuthenticationCookies.SystemCookieSegment."),
    };

    internal static void Configure(AccessedSystemEnum system, string? domain)
    {
        _systemSegment = SystemCookieSegment(system);
        _domain = string.IsNullOrWhiteSpace(domain) ? null : domain;
    }

    private static string Name(string suffix) => _systemSegment is null
        ? throw new InvalidOperationException(
            "The auth cookies are not configured. Call builder.AddLeyeco2Authentication(system) in Program.cs.")
        : $"l2auth_{_systemSegment}_{suffix}";

    /// <summary>True while the request carries the marker of a kept session.</summary>
    public static bool IsPersistent(HttpRequest request)
        => request.Cookies.TryGetValue(RememberMeName, out var marker) && marker == "1";

    /// <summary>
    /// Writes a session onto the cookies, each with the session's own expiry: none (a cookie the
    /// browser drops when it closes) unless the sign-in asked to be kept.
    /// </summary>
    public static void Stamp(HttpContext context, AuthGatewayResult session)
    {
        var secure = context.Request.IsHttps;
        var cookies = context.Response.Cookies;

        if (!string.IsNullOrEmpty(session.RefreshTokenId))
            cookies.Append(RefreshTokenName, session.RefreshTokenId, Options(session.RefreshTokenExpiresOn, secure));

        if (!string.IsNullOrEmpty(session.AccessTokenCookieValue))
            cookies.Append(AccessTokenName, session.AccessTokenCookieValue, Options(session.AccessTokenCookieExpiresOn, secure));

        if (session.Persistent)
            cookies.Append(RememberMeName, "1", Options(session.RefreshTokenExpiresOn, secure));
        else
            cookies.Delete(RememberMeName, ClearOptions(secure));
    }

    /// <summary>Deletes the session's cookies. A Delete must carry the options of the Append it undoes.</summary>
    public static void Clear(HttpContext context)
    {
        var options = ClearOptions(context.Request.IsHttps);
        context.Response.Cookies.Delete(RefreshTokenName, options);
        context.Response.Cookies.Delete(AccessTokenName, options);
        context.Response.Cookies.Delete(RememberMeName, options);
    }

    // Secure follows the request's scheme (CookieSecurePolicy.SameAsRequest): a hard-coded
    // Secure makes every browser but localhost drop the cookies on a plain-HTTP origin (a LAN
    // address, a machine name), so a sign-in answers 200 and the next request is anonymous.
    // Strict: the host's client is served from the host's own site
    // (its own origin, or a sibling one under the same domain), so no cross-site request ever needs
    // to carry the session.
    private static CookieOptions Options(DateTime? expiresOn, bool secure) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = SameSiteMode.Strict,
        Expires = expiresOn,
        Path = CookiePath,
        Domain = _domain,
        IsEssential = true,
    };

    private static CookieOptions ClearOptions(bool secure) => Options(expiresOn: null, secure);
}
