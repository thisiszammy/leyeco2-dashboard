namespace Leyeco2Dashboard.Authentication;

/// <summary>
/// The claim type names of the access tokens the host mints (<c>AccessTokenIssuer</c>), and the
/// system it signs its staff in to, with that system's block of grants. JwtBearer runs with
/// <c>MapInboundClaims = false</c>, so inbound claims keep these short names. Authorization is
/// claims-based: every <see cref="PermissionClaimType"/> claim that names a member of the
/// host's block is a grant of its system; there is no role claim.
/// </summary>
public static class AuthConstants
{
    public const string PermissionClaimType = PermissionEnumExtensions.ClaimType;
    public const string SubjectClaimType = "sub";
    public const string UniqueNameClaimType = "unique_name";
    public const string GivenNameClaimType = "given_name";
    public const string FamilyNameClaimType = "family_name";
    public const string EmailClaimType = "email";
    public const string PhoneNumberClaimType = "phone_number";

    private static AccessedSystemEnum? _accessedSystem;
    private static PermissionEnum[] _permissions = [];
    private static PermissionEnum? _allAccess;

    /// <summary>
    /// The system the host signs its staff in to. The token carries every grant an account
    /// holds in every system; only this one's count here. Set once at startup by
    /// <c>AddLeyeco2Authentication(system)</c>: reading it before then throws, so a host that
    /// forgot the call fails on its first request instead of admitting anyone.
    /// </summary>
    public static AccessedSystemEnum AccessedSystem => _accessedSystem
        ?? throw new InvalidOperationException(
            "The accessed system is not configured. Call builder.AddLeyeco2Authentication(system) in Program.cs.");

    /// <summary>The grants the host honours: <see cref="AccessedSystem"/>'s block of <see cref="PermissionEnum"/>.</summary>
    public static IReadOnlyList<PermissionEnum> Permissions
    {
        get
        {
            _ = AccessedSystem;
            return _permissions;
        }
    }

    /// <summary>
    /// <see cref="AccessedSystem"/>'s all-access grant (<c>PermissionEnumExtensions.GetAllAccess</c>),
    /// which meets every policy of the block; null for a system without one.
    /// </summary>
    public static PermissionEnum? AllAccess
    {
        get
        {
            _ = AccessedSystem;
            return _allAccess;
        }
    }

    internal static void Configure(AccessedSystemEnum system)
    {
        var permissions = system.GetPermissions();
        if (permissions.Length == 0)
            throw new InvalidOperationException(
                $"{system} has no range in PermissionEnum (see PermissionEnumExtensions.GetAccessedSystem), so nobody could ever sign in to it.");

        _permissions = permissions;
        _allAccess = system.GetAllAccess();
        _accessedSystem = system;
    }

    /// <summary>The value a token's <see cref="PermissionClaimType"/> claim carries for a grant: its member name.</summary>
    public static string ToClaimValue(PermissionEnum permission) => permission.ToString();

    /// <summary>
    /// Maps one permission claim value onto a grant of <see cref="AccessedSystem"/>'s block.
    /// A token carries a grant by its member name only, so a number, a name this build does not
    /// define (a token minted by a newer build, before a roll-back) and a grant of another
    /// system are all no match.
    /// </summary>
    public static bool TryParsePermission(string? claimValue, out PermissionEnum permission)
    {
        permission = default;
        if (string.IsNullOrEmpty(claimValue)) return false;

        // Enum.TryParse also accepts "1001" and "A, B"; only a member's own name counts.
        if (!Enum.TryParse<PermissionEnum>(claimValue, ignoreCase: false, out var parsed)
            || !string.Equals(parsed.ToString(), claimValue, StringComparison.Ordinal))
        {
            return false;
        }

        if (parsed.GetAccessedSystem() != AccessedSystem) return false;

        permission = parsed;
        return true;
    }
}
