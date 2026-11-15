using Leyeco2Dashboard.Client.Components.Shared.Charts;

namespace Leyeco2Dashboard.Client.Features.OnlinePayments.Models
{
    /// <summary>
    /// The Online Payments dashboard's colours. A provider keeps the slot its channel has on the overall dashboard
    /// (ECPay 3, Bayad Center 4), so the two pages agree; any other provider takes the slots left, navy then gold,
    /// in the facility's numbering, and past those a neutral. Outcomes wear the status tones, never a series:
    /// green went through, amber waits, red failed, slate was dropped.
    /// </summary>
    public static class OnlinePaymentTones
    {
        private static readonly (string Color, string Ink)[] OtherSlots =
        [
            ("var(--series-1)", "#FFFFFF"),
            ("var(--series-2)", "var(--slate-900)"),
            ("var(--slate-400)", "var(--slate-900)"),
            ("var(--slate-300)", "var(--slate-900)"),
        ];

        public static IReadOnlyDictionary<string, ChartSeries> Providers(IEnumerable<OnlinePaymentProvider> providers)
        {
            var tones = new Dictionary<string, ChartSeries>();
            var next = 0;
            bool ecpayTaken = false, bayadTaken = false;
            foreach (var provider in providers)
            {
                // A second provider of the same name (a staging entry beside the live one) takes a slot of its own.
                var name = provider.Label;
                if (!ecpayTaken && name.Contains("ecpay", StringComparison.OrdinalIgnoreCase))
                {
                    tones[provider.Key] = new ChartSeries(name, "var(--series-3)");
                    ecpayTaken = true;
                }
                else if (!bayadTaken && name.Contains("bayad", StringComparison.OrdinalIgnoreCase))
                {
                    tones[provider.Key] = new ChartSeries(name, "var(--series-4)");
                    bayadTaken = true;
                }
                else
                {
                    var (color, ink) = OtherSlots[Math.Min(next++, OtherSlots.Length - 1)];
                    tones[provider.Key] = new ChartSeries(name, color, ink);
                }
            }

            return tones;
        }

        public static ChartSeries Outcome(OnlinePaymentOutcome outcome) => outcome.Key switch
        {
            "successful" => new(outcome.Label, "var(--green-600)"),
            "pending"    => new(outcome.Label, "var(--amber-600)"),
            "failed"     => new(outcome.Label, "var(--red-primary)"),
            "dropped"    => new(outcome.Label, "var(--slate-400)", "var(--slate-900)"),
            _            => new(outcome.Label, "var(--slate-300)", "var(--slate-900)"),
        };
    }
}
