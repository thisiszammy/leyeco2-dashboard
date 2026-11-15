namespace Leyeco2Dashboard.Authentication.Services;

/// <summary>
/// De-duplicates concurrent refreshes. Callers carrying the same access token (or the same
/// refresh token id, when no access token is present) share one
/// <see cref="IAuthGatewayService.RefreshAsync"/> call and all receive the same rotated session.
/// </summary>
/// <remarks>
/// The silent-refresh middleware goes through it. A page that opens fires a burst of requests
/// under one lapsed token: coalesced, the burst costs one read of the account and one minted
/// token instead of one each.
/// </remarks>
public interface IRefreshCoalescer
{
    /// <summary>
    /// The shared refresh for the caller's session. <paramref name="cancellationToken"/> cancels
    /// only this caller's wait: the refresh runs on for the others awaiting it.
    /// </summary>
    Task<AuthGatewayResult> CoalescedRefreshAsync(
        string? accessToken,
        string refreshTokenId,
        bool persistent,
        IAuthGatewayService authGateway,
        CancellationToken cancellationToken);
}
