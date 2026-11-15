using Leyeco2Dashboard.Services.DTOs;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Leyeco2Dashboard.Authentication.Services;

/// <summary>
/// Mints the access token of a session: a short-lived JWT naming the account and the grants it
/// holds at that moment, signed with this host's own key. JwtBearer validates it off the access
/// cookie on every request; nothing looks the account up again until the token is renewed, so
/// its lifetime (<c>Jwt:AccessExpiryMinutes</c>) is how long a revoked grant or a deactivation
/// can still be honoured.
/// </summary>
public sealed class AccessTokenIssuer
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly SigningCredentials _signingCredentials;
    private readonly TimeSpan _lifetime;

    public AccessTokenIssuer(string issuer, string audience, SecurityKey signingKey, TimeSpan lifetime)
    {
        _issuer = issuer;
        _audience = audience;
        _signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        _lifetime = lifetime;
    }

    public (string Token, DateTime ExpiresOn) Issue(SessionDTO account)
    {
        // A token's times are UTC by the JWT standard, unlike the local time the Transaction Monitor's rows keep.
        var issuedOn = DateTime.UtcNow;
        var expiresOn = issuedOn.Add(_lifetime);

        var claims = new Dictionary<string, object>
        {
            [AuthConstants.SubjectClaimType] = account.UserId.ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            [AuthConstants.UniqueNameClaimType] = account.Username,
            [AuthConstants.GivenNameClaimType] = account.FirstName,
            [AuthConstants.FamilyNameClaimType] = account.LastName,
        };

        if (!string.IsNullOrEmpty(account.Email))
            claims[AuthConstants.EmailClaimType] = account.Email;

        // An account whose grants were all revoked mid-session keeps its session but carries none,
        // which is how its console learns to sign it out.
        if (account.Permissions.Count > 0)
            claims[AuthConstants.PermissionClaimType] = account.Permissions.Select(AuthConstants.ToClaimValue).ToArray();

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _issuer,
            Audience = _audience,
            Claims = claims,
            IssuedAt = issuedOn,
            NotBefore = issuedOn,
            Expires = expiresOn,
            SigningCredentials = _signingCredentials,
        });

        return (token, expiresOn);
    }
}
