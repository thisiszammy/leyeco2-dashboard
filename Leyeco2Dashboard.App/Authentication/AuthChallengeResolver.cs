namespace Leyeco2Dashboard.Authentication;

/// <summary>
/// How a failed silent refresh surfaces: the answer the middleware writes, kept beside the
/// JwtBearer <c>OnChallenge</c> rule it matches (a bare status for <c>/api</c>, nothing else).
/// </summary>
/// <remarks>
/// The host serves a Blazor WebAssembly client: every browser navigation resolves to the
/// anonymous <c>index.html</c> shell, whose router owns the redirect to <c>/login</c>. So
/// nothing here ever redirects: <c>/api</c> callers get a status the client's handlers act on,
/// and everything else continues down the pipeline unauthenticated.
/// </remarks>
public static class AuthChallengeResolver
{
    public static bool IsApiPath(PathString path) =>
        path.StartsWithSegments("/api");

    /// <summary>
    /// For an <c>/api</c> request, writes <paramref name="statusCode"/> (with
    /// <paramref name="message"/> as plain text, when given) and returns true: the caller
    /// short-circuits. For anything else returns false: the caller continues.
    /// </summary>
    public static async Task<bool> TryWriteChallengeAsync(
        HttpContext context,
        int statusCode = StatusCodes.Status401Unauthorized,
        string? message = null)
    {
        if (!IsApiPath(context.Request.Path)) return false;

        context.Response.StatusCode = statusCode;
        if (message is not null)
        {
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(message, context.RequestAborted);
        }

        return true;
    }
}
