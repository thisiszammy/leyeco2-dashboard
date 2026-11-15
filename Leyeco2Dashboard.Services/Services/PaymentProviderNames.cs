using System.Data.Common;
using Leyeco2Dashboard.Data.Interfaces.Leyeco2OTF;
using Microsoft.Extensions.Logging;

namespace Leyeco2Dashboard.Services.Services
{
    /// <summary>
    /// The online payment facility's providers by name, for the screens that show its payments. The names are a nicety:
    /// when its <c>PaymentProviders</c> can't be read, the two its own summary names by number still have theirs and the
    /// rest show by number, and the figures stand.
    /// </summary>
    internal static class PaymentProviderNames
    {
        // The providers the facility's own summary names by number (its ManagementV2ApiController).
        private static readonly Dictionary<string, string> Known = new()
        {
            ["2"] = "ECPay",
            ["3"] = "Bayad Center",
        };

        public static async Task<Dictionary<string, string>> ReadAsync(IOnlinePaymentReportRepository repository, ILogger logger)
        {
            var names = new Dictionary<string, string>(Known, StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var provider in await repository.GetProvidersAsync())
                {
                    if (!string.IsNullOrWhiteSpace(provider.Name))
                        names[provider.Id.ToString()] = provider.Name.Trim();
                }
            }
            catch (DbException ex)
            {
                logger.LogWarning(ex, "The online payment facility's PaymentProviders could not be read; its providers show by number");
            }

            return names;
        }

        /// <summary>A provider key's name: "No provider" for a bill without one, "Provider 7" for one the facility doesn't name.</summary>
        public static string Label(string key, IReadOnlyDictionary<string, string> names)
            => key.Length == 0 ? "No provider"
             : names.TryGetValue(key, out var name) ? name
             : $"Provider {key}";

        /// <summary>The facility's numbering first, then any key that isn't a number.</summary>
        public static int Order(string key) => int.TryParse(key, out var number) ? number : int.MaxValue;
    }
}
