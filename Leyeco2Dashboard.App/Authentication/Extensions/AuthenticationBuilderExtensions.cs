using System.Text;
using Leyeco2Dashboard.Authentication.Middleware;
using Leyeco2Dashboard.Authentication.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Leyeco2Dashboard.Authentication.Extensions;

/// <summary>
/// The one place the host wires up sign-in: the minting of its access tokens and their JWT
/// validation, off the host's own HttpOnly cookies; the claims policies of the host's system; the
/// door to the Transaction Monitor's own accounts; and the refresh de-duplicator.
/// </summary>
/// <remarks>
/// The host keeps its pipeline in this order, so the silently refreshed token is in place when
/// JwtBearer validates: <c>app.UseMiddleware&lt;AccessTokenRefreshMiddleware&gt;()</c>, then
/// <c>app.UseAuthentication()</c>, the rate limiter, <c>app.UseAuthorization()</c>. Its own
/// <c>AuthController</c> signs in and out, changes a password, and answers the session.
/// </remarks>
public static class AuthenticationBuilderExtensions
{
    public const string JwtBearerLogCategory = "Leyeco2Dashboard.Authentication.JwtBearer";

    // The key id every token is signed with; any other Jwt:Key:{kid} is still accepted, which
    // serves a key rotation.
    private const string SigningKeyId = "l2auth";

    // HS256 takes a key of at least 256 bits.
    private const int MinimumKeyBytes = 32;

    private const int DefaultAccessExpiryMinutes = 5;

    public static WebApplicationBuilder AddLeyeco2Authentication(
        this WebApplicationBuilder builder,
        AccessedSystemEnum system)
    {
        var configuration = builder.Configuration;
        var services = builder.Services;

        AuthConstants.Configure(system);
        AuthenticationCookies.Configure(system, configuration["Auth:CookieDomain"]);

        // The host mints its own tokens and accepts no others: the first entry of Jwt:Issuers and
        // of Jwt:Audience goes into every token, and each entry is accepted on the way back, so a
        // name can be changed without ending the sessions open under the old one.
        var issuers = SplitList(configuration["Jwt:Issuers"]);
        if (issuers.Length == 0)
            throw new InvalidOperationException("Jwt:Issuers is not configured. Set Jwt__Issuers in .env.");

        var audiences = SplitList(configuration["Jwt:Audience"]);
        if (audiences.Length == 0)
            throw new InvalidOperationException("Jwt:Audience is not configured. Set Jwt__Audience in .env.");

        var signingKeys = configuration.GetSection("Jwt:Key").GetChildren()
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
            .ToDictionary(
                entry => entry.Key,
                entry => (SecurityKey)new SymmetricSecurityKey(Encoding.UTF8.GetBytes(entry.Value!)) { KeyId = entry.Key },
                StringComparer.Ordinal);

        if (!signingKeys.TryGetValue(SigningKeyId, out var signingKey))
            throw new InvalidOperationException(
                $"Jwt:Key:{SigningKeyId} is not configured. Set Jwt__Key__{SigningKeyId} in .env.");

        if (Encoding.UTF8.GetByteCount(configuration[$"Jwt:Key:{SigningKeyId}"]!) < MinimumKeyBytes)
            throw new InvalidOperationException(
                $"Jwt:Key:{SigningKeyId} is too short to sign with: set Jwt__Key__{SigningKeyId} in .env to at least {MinimumKeyBytes} characters.");

        var accessExpiryMinutes = configuration.GetValue<int?>("Jwt:AccessExpiryMinutes") ?? DefaultAccessExpiryMinutes;
        if (accessExpiryMinutes <= 0)
            throw new InvalidOperationException("Jwt:AccessExpiryMinutes must be a whole number of minutes above zero. Set Jwt__AccessExpiryMinutes in .env, or leave it out for 5.");

        // A token naming no known kid is tried against every configured key.
        var allSigningKeys = signingKeys.Values.ToArray();

        // ClockSkew: a small tolerance, not zero. nbf is the minting moment truncated to whole
        // seconds, so behind a load balancer a sub-second lag of another instance's clock would
        // reject a token issued moments earlier, and OnChallenge would then wipe the cookies a
        // refresh just stamped.
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidIssuers = issuers,
            ValidAudiences = audiences,
            IssuerSigningKeyResolver = (_, _, kid, _) =>
                kid is not null && signingKeys.TryGetValue(kid, out var key) ? [key] : allSigningKeys,
        };

        services
            .AddAuthentication(AuthenticationSchemes.Cookies)
            .AddJwtBearer(AuthenticationSchemes.Cookies, options =>
            {
                // Keep the claim types as minted ("sub", "unique_name", "permission"), not the
                // long WS-* URIs, so AuthConstants and the policies can read them.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = tokenValidationParameters;

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // 1. The token the silent refresh minted on this very request.
                        if (context.HttpContext.Items.TryGetValue(
                                AccessTokenRefreshMiddleware.RefreshedAccessTokenKey,
                                out var freshObj)
                            && freshObj is string freshToken
                            && !string.IsNullOrEmpty(freshToken))
                        {
                            context.Token = freshToken;
                            return Task.CompletedTask;
                        }

                        // 2. The browser's: the host's access cookie.
                        if (context.Request.Cookies.TryGetValue(AuthenticationCookies.AccessTokenName, out var cookieToken)
                            && !string.IsNullOrEmpty(cookieToken))
                        {
                            context.Token = cookieToken;
                            return Task.CompletedTask;
                        }

                        // 3. A programmatic caller's Bearer header, which the handler reads itself.
                        return Task.CompletedTask;
                    },

                    // The reason validation failed (expired, signature, audience, issuer), so a
                    // "signed in, then every request 401s" mismatch (a key or an issuer changed
                    // under live sessions) shows in the log rather than only as the client's bare 401.
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger(JwtBearerLogCategory);

                        logger.LogWarning(context.Exception,
                            "JWT validation failed on {Path}: {ExceptionType} - {Message} (middlewareRefreshed={MiddlewareRefreshed})",
                            context.Request.Path,
                            context.Exception.GetType().Name,
                            context.Exception.Message,
                            WasRefreshed(context.HttpContext));

                        return Task.CompletedTask;
                    },

                    OnChallenge = context =>
                    {
                        // Not when the middleware rotated the cookies on this request: Delete strips
                        // the matching Appends from the response, which would undo the refresh.
                        if (!WasRefreshed(context.HttpContext))
                            AuthenticationCookies.Clear(context.HttpContext);

                        // No redirect: the protected endpoints are all /api, called by the Blazor
                        // client, whose handler answers a bare 401 with one refresh and a replay.
                        // A redirect would come back to fetch as a 200 with the client shell.
                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorization(AuthorizationPolicies.Configure);

        services.AddSingleton(new AccessTokenIssuer(
            issuers[0], audiences[0], signingKey, TimeSpan.FromMinutes(accessExpiryMinutes)));

        // A singleton that opens a scope of its own per call (see AuthGatewayService).
        services.AddSingleton<IAuthGatewayService, AuthGatewayService>();

        // A singleton, so its in-flight map spans requests.
        services.AddSingleton<IRefreshCoalescer, RefreshCoalescer>();


        return builder;
    }

    private static bool WasRefreshed(HttpContext context)
        => context.Items.TryGetValue(AccessTokenRefreshMiddleware.RefreshedItemKey, out var refreshed)
           && refreshed is true;

    private static string[] SplitList(string? value)
        => (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
