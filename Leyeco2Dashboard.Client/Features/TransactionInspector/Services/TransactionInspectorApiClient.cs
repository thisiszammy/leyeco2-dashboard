using System.Globalization;
using Leyeco2Dashboard.Client.Features.TransactionInspector.Models;
using Leyeco2Dashboard.Client.Infrastructure.Api;

namespace Leyeco2Dashboard.Client.Features.TransactionInspector.Services
{
    /// <summary>The Transaction Inspector (<c>api/transaction-inspector</c>, Inspect transactions).</summary>
    public class TransactionInspectorApiClient
    {
        private readonly ApiClient _api;

        public TransactionInspectorApiClient(ApiClient api)
        {
            _api = api;
        }

        /// <summary>The period's bills, summed, and one page of <paramref name="view"/>.</summary>
        public Task<TransactionInspection> GetAsync(
            DateTime from, DateTime to, string view, string provider, string search, int page, int pageSize, CancellationToken ct = default)
            => _api.GetDataAsync<TransactionInspection>(
                $"api/transaction-inspector/bills?{Query(from, to, view, provider, search)}&page={page}&pageSize={pageSize}", ct);

        /// <summary>The summary and every bill of the view as a PDF.</summary>
        public Task<byte[]> GetPdfAsync(DateTime from, DateTime to, string view, string provider, string search, CancellationToken ct = default)
            => _api.GetBytesAsync($"api/transaction-inspector/bills/pdf?{Query(from, to, view, provider, search)}", ct);

        /// <summary>The summary and every bill of the view as an Excel workbook.</summary>
        public Task<byte[]> GetWorkbookAsync(DateTime from, DateTime to, string view, string provider, string search, CancellationToken ct = default)
            => _api.GetBytesAsync($"api/transaction-inspector/bills/xlsx?{Query(from, to, view, provider, search)}", ct);

        /// <summary>"Transaction-Inspection-2026-06-01-to-2026-06-30-Needs-attention", as the API names the same file.</summary>
        public static string FileStem(DateTime from, DateTime to, string viewLabel, bool allBills)
        {
            var stem = $"Transaction-Inspection-{Iso(from)}";
            if (to.Date != from.Date)
                stem += $"-to-{Iso(to)}";
            if (!allBills)
                stem += "-" + viewLabel.Replace(' ', '-');
            return stem;
        }

        private static string Query(DateTime from, DateTime to, string view, string provider, string search)
            => $"from={Iso(from)}&to={Iso(to)}&view={Uri.EscapeDataString(view)}"
             + $"&provider={Uri.EscapeDataString(provider)}&search={Uri.EscapeDataString(search)}";

        private static string Iso(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
