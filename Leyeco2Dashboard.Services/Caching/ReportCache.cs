using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace Leyeco2Dashboard.Services.Caching
{
    /// <summary>
    /// Keeps a dashboard's figures for a short while and lets every viewer asking for the same period share one
    /// read of its database: the collections ledger is the cashiering system's, and ten supervisors with the
    /// dashboard open must cost it what one does. While a read is under way, a second request for the same key
    /// waits on it instead of starting another.
    /// <para>
    /// The read itself runs without the request's cancellation, so a viewer who navigates away does not cancel it
    /// under the others; each caller stops waiting on its own token. It must therefore not touch anything the
    /// request scope owns (the report repositories open contexts of their own). A failed read is not kept.
    /// </para>
    /// Registered as a singleton.
    /// </summary>
    public sealed class ReportCache
    {
        private readonly IMemoryCache _cache;
        private readonly ConcurrentDictionary<string, Lazy<Task<object>>> _reads = new();

        public ReportCache(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task<T> GetOrReadAsync<T>(string key, TimeSpan keepFor, Func<Task<T>> read, CancellationToken ct)
            where T : class
        {
            if (_cache.TryGetValue(key, out T? kept) && kept is not null)
                return kept;

            var shared = _reads.GetOrAdd(key, missing => new Lazy<Task<object>>(async () =>
            {
                try
                {
                    var value = await read();
                    _cache.Set(missing, value, keepFor);
                    return value;
                }
                finally
                {
                    _reads.TryRemove(missing, out var _);
                }
            }));

            return (T)await shared.Value.WaitAsync(ct);
        }
    }
}
