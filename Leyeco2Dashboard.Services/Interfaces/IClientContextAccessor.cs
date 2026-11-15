namespace Leyeco2Dashboard.Services.Interfaces
{
    /// <summary>
    /// Exposes the caller's transport metadata (IP address, user agent) to the Services layer for
    /// audit logging. This is NOT authentication — it carries no identity, claims, or token. The
    /// concrete implementation lives in the host (<c>Leyeco2Dashboard.App</c>) because it reads
    /// <c>HttpContext</c>; the Services project stays framework-agnostic behind this interface.
    /// </summary>
    public interface IClientContextAccessor
    {
        string? IpAddress { get; }
        string? UserAgent { get; }
    }
}
