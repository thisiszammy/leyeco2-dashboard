using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Leyeco2Dashboard.Authentication.Services;

/// <inheritdoc/>
public class RefreshCoalescer : IRefreshCoalescer
{
    // One Lazy per session key ("atk:<hash>", or "rtk:<hash>" when no access cookie came):
    // GetOrAdd hands every concurrent caller the same Lazy, and ExecutionAndPublication runs its
    // refresh once. An instance field of a singleton, so its lifetime is the container's.
    private readonly ConcurrentDictionary<string, Lazy<Task<AuthGatewayResult>>> _inFlight = new();

    // How long a finished refresh is still handed out: requests that left the browser before
    // the new cookies arrived still carry the lapsed token, and take the one just minted
    // rather than each minting another.
    private static readonly TimeSpan InFlightCacheDuration = TimeSpan.FromSeconds(5);

    private readonly ILogger<RefreshCoalescer> _logger;

    public RefreshCoalescer(ILogger<RefreshCoalescer> logger)
    {
        _logger = logger;
    }

    public Task<AuthGatewayResult> CoalescedRefreshAsync(
        string? accessToken,
        string refreshTokenId,
        bool persistent,
        IAuthGatewayService authGateway,
        CancellationToken cancellationToken)
    {
        var key = BuildCoalesceKey(accessToken, refreshTokenId);

        var lazy = _inFlight.GetOrAdd(
            key,
            k => new Lazy<Task<AuthGatewayResult>>(
                () => RefreshAndScheduleCleanupAsync(k, refreshTokenId, persistent, authGateway),
                LazyThreadSafetyMode.ExecutionAndPublication));

        _logger.LogDebug("Refresh coalesce: key={Key}", key);

        return lazy.Value.WaitAsync(cancellationToken);
    }

    private async Task<AuthGatewayResult> RefreshAndScheduleCleanupAsync(
        string coalesceKey,
        string refreshTokenId,
        bool persistent,
        IAuthGatewayService authGateway)
    {
        AuthGatewayResult result;
        try
        {
            // Never tied to one caller's RequestAborted: if the first caller goes away, the
            // refresh has to go on for the others awaiting it.
            result = await authGateway.RefreshAsync(refreshTokenId, persistent, CancellationToken.None);
        }
        catch
        {
            // Evicted at once, so the next request retries instead of inheriting this failure.
            _inFlight.TryRemove(coalesceKey, out _);
            throw;
        }

        _ = Task.Delay(InFlightCacheDuration)
            .ContinueWith(delay => _inFlight.TryRemove(coalesceKey, out _), TaskScheduler.Default);

        return result;
    }

    /// <summary>Eight hex characters of SHA-256: correlates log lines without writing a secret.</summary>
    public static string HashTokenId(string? tokenId)
    {
        if (string.IsNullOrEmpty(tokenId)) return "<empty>";
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(tokenId), hash);
        return Convert.ToHexString(hash[..4]);
    }

    public static string BuildCoalesceKey(string? accessToken, string refreshTokenId)
        => string.IsNullOrEmpty(accessToken)
            ? "rtk:" + HashTokenId(refreshTokenId)
            : "atk:" + HashTokenId(accessToken);
}
