using System.Globalization;

namespace Leyeco2Dashboard.Client.Infrastructure.Formatting
{
    /// <summary>
    /// How the dashboards write their figures: pesos to the centavo in the registers and the key figures, compact
    /// ("₱1.2M") only on a chart's axis, counts grouped by thousands. One culture-free style ("1,234.56"), whatever
    /// the browser's language, so a figure reads the same on every desk.
    /// </summary>
    public static class Figures
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary>"₱1,234.56"; a negative amount (a ledger's correction) "−₱424.28", the sign ahead of the currency.</summary>
        public static string Peso(decimal amount)
            => amount < 0 ? "−₱" + (-amount).ToString("N2", Invariant) : "₱" + amount.ToString("N2", Invariant);

        public static string Count(int count) => count.ToString("N0", Invariant);

        public static string Count(decimal count) => count.ToString("N0", Invariant);

        /// <summary>An axis tick: ₱0, ₱250K, ₱1.5M.</summary>
        public static string PesoCompact(decimal amount) => "₱" + Compact(amount);

        /// <summary>An axis tick for counts: 0, 250, 1.5K.</summary>
        public static string Compact(decimal value)
        {
            var abs = Math.Abs(value);
            return abs switch
            {
                >= 1_000_000_000m => (value / 1_000_000_000m).ToString("0.#", Invariant) + "B",
                >= 1_000_000m     => (value / 1_000_000m).ToString("0.#", Invariant) + "M",
                >= 1_000m         => (value / 1_000m).ToString("0.#", Invariant) + "K",
                _                 => value.ToString("0.#", Invariant),
            };
        }

        /// <summary>A share of a whole: "46.2%", "&lt;0.1%" for a sliver, "—" when the whole is nothing.</summary>
        public static string Share(decimal part, decimal whole)
        {
            if (whole <= 0)
                return "—";

            var share = part / whole * 100m;
            return share > 0 && share < 0.1m ? "<0.1%" : share.ToString("0.0", Invariant) + "%";
        }

        public static decimal Ratio(decimal part, decimal whole) => whole <= 0 ? 0 : part / whole;

        /// <summary>
        /// The change from <paramref name="previous"/> in words, "Up 8.2% on yesterday": the words carry the
        /// direction, so no colour has to. Null when there is nothing to compare with.
        /// </summary>
        public static string? Change(decimal current, decimal previous, string against)
        {
            if (previous <= 0)
                return current > 0 ? $"Nothing {against}" : null;

            var change = (current - previous) / previous * 100m;
            var rounded = Math.Round(change, 1);
            return rounded == 0
                ? $"Level with {against}"
                : $"{(rounded > 0 ? "Up" : "Down")} {Math.Abs(rounded).ToString("0.0", Invariant)}% on {against}";
        }

        /// <summary>
        /// <see cref="Change"/> for a register's column, whose header names what it is against: "Up 8.2%",
        /// "Down 3.1%", "Level", "None before" (nothing then, something now), "—" (nothing either time).
        /// </summary>
        public static string ChangeBrief(decimal current, decimal previous)
        {
            if (previous <= 0)
                return current > 0 ? "None before" : "—";

            var rounded = Math.Round((current - previous) / previous * 100m, 1);
            return rounded == 0
                ? "Level"
                : $"{(rounded > 0 ? "Up" : "Down")} {Math.Abs(rounded).ToString("0.0", Invariant)}%";
        }

        public static string Day(DateTime date) => date.ToString("MMM d", Invariant);

        public static string DayLong(DateTime date) => date.ToString("ddd, MMM d, yyyy", Invariant);

        public static string Time(DateTime time) => time.ToString("h:mm tt", Invariant);

        /// <summary>"12 AM", "9 AM", "12 PM", "3 PM".</summary>
        public static string Hour(int hour) => hour switch
        {
            0  => "12 AM",
            12 => "12 PM",
            < 12 => $"{hour} AM",
            _  => $"{hour - 12} PM",
        };

        /// <summary>"9 AM – 10 AM", an hour of the day as a span.</summary>
        public static string HourSpan(int hour) => $"{Hour(hour)} – {Hour((hour + 1) % 24)}";

        /// <summary>The period in words: "Jun 6, 2026", "Jun 1 – 6, 2026", "May 28 – Jun 6, 2026".</summary>
        public static string Period(DateTime from, DateTime to)
        {
            if (from.Date == to.Date)
                return from.ToString("MMM d, yyyy", Invariant);
            if (from.Year != to.Year)
                return $"{from.ToString("MMM d, yyyy", Invariant)} – {to.ToString("MMM d, yyyy", Invariant)}";
            if (from.Month != to.Month)
                return $"{from.ToString("MMM d", Invariant)} – {to.ToString("MMM d, yyyy", Invariant)}";
            return $"{from.ToString("MMM d", Invariant)} – {to.ToString("d, yyyy", Invariant)}";
        }

        /// <summary>How long ago, for a time today: "just now", "12 min ago", "2 h 5 min ago".</summary>
        public static string Ago(TimeSpan elapsed)
        {
            if (elapsed < TimeSpan.FromMinutes(1))
                return "just now";
            if (elapsed < TimeSpan.FromHours(1))
                return $"{(int)elapsed.TotalMinutes} min ago";
            return elapsed.Minutes == 0
                ? $"{(int)elapsed.TotalHours} h ago"
                : $"{(int)elapsed.TotalHours} h {elapsed.Minutes} min ago";
        }

        /// <summary>"1 bill", "3 bills".</summary>
        public static string Plural(int count, string one, string many) => $"{Count(count)} {(count == 1 ? one : many)}";
    }
}
