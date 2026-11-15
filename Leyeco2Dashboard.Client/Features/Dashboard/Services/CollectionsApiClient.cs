using System.Globalization;
using Leyeco2Dashboard.Client.Features.Dashboard.Models;
using Leyeco2Dashboard.Client.Infrastructure.Api;

namespace Leyeco2Dashboard.Client.Features.Dashboard.Services
{
    /// <summary>The overall dashboard's figures (<c>GET api/dashboards/collections</c>, View the dashboard).</summary>
    public class CollectionsApiClient
    {
        private readonly ApiClient _api;

        public CollectionsApiClient(ApiClient api)
        {
            _api = api;
        }

        /// <summary>The bills paid from <paramref name="from"/> through <paramref name="to"/>, both days included.</summary>
        public Task<CollectionDashboard> GetAsync(DateTime from, DateTime to, CancellationToken ct = default)
            => _api.GetDataAsync<CollectionDashboard>(
                $"api/dashboards/collections?from={Iso(from)}&to={Iso(to)}", ct);

        private static string Iso(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
