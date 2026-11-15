using System.Globalization;

namespace Leyeco2Dashboard.Client.Components.Shared.Charts
{
    /// <summary>
    /// One series of a chart: its name and its fill, a CSS colour (<c>var(--series-1)</c>). <paramref name="Ink"/>
    /// is the colour of text set on the fill (white on the darker series, <c>var(--slate-900)</c> on gold).
    /// </summary>
    public sealed record ChartSeries(string Label, string Color, string Ink = "#FFFFFF");

    /// <summary>
    /// One column of a <see cref="StackedColumns"/>: a day or an hour. <paramref name="Values"/> follow the chart's
    /// series. <paramref name="AxisLabel"/> is what the axis prints under it (null prints nothing, so a dense axis
    /// can label every few columns); <paramref name="Title"/> heads its tooltip and its table row.
    /// <paramref name="Quiet"/> greys its axis label (a Sunday, an hour the counters are closed).
    /// <paramref name="RowTitle"/> is the shorter name its table row takes, so the figures keep the width.
    /// <paramref name="MinorLabel"/> marks an axis label a phone's narrow axis leaves out (every other one).
    /// </summary>
    public sealed record ChartColumn(string Title, string? AxisLabel, IReadOnlyList<decimal> Values, bool Quiet = false,
        string? RowTitle = null, bool MinorLabel = false)
    {
        public decimal Total => Values.Sum();
    }

    /// <summary>One part of a <see cref="SplitBar"/>.</summary>
    public sealed record SplitPart(string Label, decimal Value, string Color, string Ink = "#FFFFFF");

    internal static class ChartMath
    {
        /// <summary>A CSS number, never in the browser's culture ("12.5", not "12,5").</summary>
        public static string Css(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>
        /// An axis that ends on a round figure at or above <paramref name="max"/>, in at most five steps of 1, 2,
        /// 2.5 or 5 times a power of ten: ticks every ₱250K up to ₱1M, not every ₱237,413.
        /// </summary>
        /// <param name="whole">Counts: no step under 1, so the axis never reads half a bill.</param>
        public static IReadOnlyList<decimal> Ticks(decimal max, bool whole = false)
        {
            if (max <= 0)
                return [0m, 1m];

            var rough = (double)max / 4;
            var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
            var step = new[] { 1, 2, 2.5, 5, 10 }.Select(f => f * magnitude).First(s => s >= rough);
            if (whole)
                step = Math.Max(1, Math.Ceiling(step));
            var stepDecimal = (decimal)step;
            var top = Math.Ceiling(max / stepDecimal) * stepDecimal;

            var ticks = new List<decimal>();
            for (var tick = 0m; tick <= top + stepDecimal / 2; tick += stepDecimal)
                ticks.Add(tick);
            return ticks;
        }
    }
}
