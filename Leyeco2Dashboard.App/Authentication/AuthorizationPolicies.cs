using Microsoft.AspNetCore.Authorization;

namespace Leyeco2Dashboard.Authentication;

/// <summary>
/// The host's claims-based authorization policies. Access is decided from the token's
/// <c>permission</c> claims alone (the host's block of <see cref="PermissionEnum"/>), with no
/// role and no database read per request, so a grant revoked on the Users screen stops opening
/// anything at the next token refresh, which reads the account's grants again.
/// </summary>
/// <remarks>
/// Two kinds of policy are registered:
/// <list type="bullet">
///   <item><see cref="SystemAccess"/>: the caller holds at least one grant of the host's
///   system. It is also the default policy, so a bare <c>[Authorize]</c> means "staff of this
///   system", never merely "signed in".</item>
///   <item>One policy per grant of the block, named after the member:
///   <c>[Authorize(Policy = nameof(PermissionEnum.TXM_ManageUsers))]</c>. Appending a member to
///   the block's range in <see cref="PermissionEnum"/> is all it takes to get its policy.</item>
/// </list>
/// A system with an all-access grant (<see cref="AuthConstants.AllAccess"/>) has it meet every
/// one of these, and every policy <see cref="AnyGrantOf"/> builds for the host. The Blazor client
/// registers the same names in its own <c>AuthorizationPolicies</c> (it never sees the token; it
/// rebuilds the claims from <c>GET /api/auth/session</c>): keep the two in step.
/// </remarks>
public static class AuthorizationPolicies
{
    /// <summary>
    /// At least one grant of the host's system. An account left without one mid-session (its
    /// last grant revoked on the Users screen) is still signed in until its session ends, and
    /// gets 403.
    /// </summary>
    public const string SystemAccess = "SystemAccess";

    /// <summary>
    /// Signed in, no grant required: <c>GET /api/auth/session</c> uses it so an account whose
    /// grants were revoked mid-session still learns it holds no access and can sign out.
    /// </summary>
    public const string Authenticated = "Authenticated";

    /// <summary>The policy name for one grant: its member name.</summary>
    public static string For(PermissionEnum permission) => permission.ToString();

    /// <summary>
    /// A policy met by any one of <paramref name="grants"/>, or by the system's all-access grant,
    /// for a host to register under a name of its own when one action serves several screens:
    /// <c>options.AddPolicy("Conformes", AuthorizationPolicies.AnyGrantOf(a, b))</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">A grant is not one of the host's system.</exception>
    public static AuthorizationPolicy AnyGrantOf(params PermissionEnum[] grants)
    {
        if (grants.Length == 0)
            throw new ArgumentException("A policy needs at least one grant.", nameof(grants));

        var foreign = grants.Where(grant => !AuthConstants.Permissions.Contains(grant)).ToArray();
        if (foreign.Length > 0)
            throw new InvalidOperationException(
                $"Not grants of {AuthConstants.AccessedSystem}, so no token could meet them here: {string.Join(", ", foreign)}.");

        var metBy = AuthConstants.AllAccess is { } allAccess ? grants.Append(allAccess) : grants;

        // RequireClaim(type, values) is met by ANY of the values.
        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim(AuthConstants.PermissionClaimType, metBy.Distinct().Select(AuthConstants.ToClaimValue))
            .Build();
    }

    public static void Configure(AuthorizationOptions options)
    {
        // RequireClaim(type, values) is met by ANY of the values: "holds at least one grant" of
        // this system's block. Another system's grants, which the token carries too, are not
        // listed, so they open nothing here.
        var systemAccess = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim(AuthConstants.PermissionClaimType, AuthConstants.Permissions.Select(AuthConstants.ToClaimValue))
            .Build();

        options.AddPolicy(SystemAccess, systemAccess);
        options.DefaultPolicy = systemAccess;

        options.AddPolicy(Authenticated, policy => policy
            .RequireAuthenticatedUser());

        foreach (var permission in AuthConstants.Permissions)
        {
            options.AddPolicy(For(permission), AnyGrantOf(permission));
        }
    }
}
