namespace Leyeco2Dashboard.Authentication;

public static class AuthenticationSchemes
{
    // The one JwtBearer scheme. Its token comes from the host's own access cookie (or a Bearer
    // header for programmatic callers), and a failure is a bare 401: the Blazor client owns
    // the redirect to /login.
    public const string Cookies = "Cookies";
}
