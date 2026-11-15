using Leyeco2Dashboard.Client.Components.Shared.Charts;

namespace Leyeco2Dashboard.Client.Features.Dashboard.Models
{
    /// <summary>
    /// Each collection channel's place in the chart series (app.css, <c>--series-*</c>): Teller, SBA, ECPay,
    /// Bayad Center, SM Payment and Bizlink take slots 1 to 6 in order, everywhere on the overall dashboard.
    /// </summary>
    public static class Channels
    {
        public static string Color(string key) => key switch
        {
            "teller"      => "var(--series-1)",
            "sba"         => "var(--series-2)",
            "ecpay"       => "var(--series-3)",
            "bayadcenter" => "var(--series-4)",
            "smpayment"   => "var(--series-5)",
            "bizlink"     => "var(--series-6)",
            _             => "var(--slate-400)",
        };

        public static string Ink(string key) => key == "sba" ? "var(--slate-900)" : "#FFFFFF";

        public static ChartSeries Series(CollectionChannel channel) => new(channel.Label, Color(channel.Key), Ink(channel.Key));
    }
}
