using System.Globalization;
using Leyeco2Dashboard.Client.Features.OnlinePayments.Models;
using Leyeco2Dashboard.Client.Infrastructure.Api;

namespace Leyeco2Dashboard.Client.Features.OnlinePayments.Services
{
    /// <summary>The Online Payments dashboard's figures (<c>GET api/dashboards/online-payments</c>, View online payments).</summary>
    public class OnlinePaymentsApiClient
    {
        private readonly ApiClient _api;

        public OnlinePaymentsApiClient(ApiClient api)
        {
            _api = api;
        }

        /// <summary>The payments made from <paramref name="from"/> through <paramref name="to"/>, both days included.</summary>
        public Task<OnlinePaymentDashboard> GetAsync(DateTime from, DateTime to, CancellationToken ct = default)
            => _api.GetDataAsync<OnlinePaymentDashboard>(
                $"api/dashboards/online-payments?from={Iso(from)}&to={Iso(to)}", ct);

        private static string Iso(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
