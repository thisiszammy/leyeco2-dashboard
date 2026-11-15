using System.Globalization;
using Leyeco2Dashboard.Services.DTOs;
using Leyeco2Dashboard.Services.Inspection;

namespace Leyeco2Dashboard.Services.Reports
{
    /// <summary>
    /// An inspection as its printouts read it, the PDF and the workbook alike: the period, what it was narrowed to, the
    /// books' summary of the bills the filters leave, and every bill of the view, in the register's order. The words a
    /// printout prints about itself (its period, its filters, its file name) are made here once.
    /// </summary>
    public sealed class InspectionReport
    {
        public const string Title = "Transaction Inspection";

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public required DateTime From { get; init; }

        public required DateTime To { get; init; }

        /// <summary>When the sources were read.</summary>
        public required DateTime ReadOn { get; init; }

        public required DateTime PrintedOn { get; init; }

        public required string View { get; init; }

        /// <summary>The provider's name, or null for all of them.</summary>
        public string? Provider { get; init; }

        public string Search { get; init; } = string.Empty;

        public required InspectionSummaryDTO Summary { get; init; }

        public required IReadOnlyList<InspectedBillDTO> Bills { get; init; }

        public string ViewLabel => InspectionRules.ViewLabel(View);

        /// <summary>"Jun 6, 2026", "Jun 1 – 30, 2026", "May 28 – Jun 6, 2026", as the console writes a period.</summary>
        public string Period
        {
            get
            {
                if (From.Date == To.Date)
                    return From.ToString("MMM d, yyyy", Invariant);
                if (From.Year != To.Year)
                    return $"{From.ToString("MMM d, yyyy", Invariant)} – {To.ToString("MMM d, yyyy", Invariant)}";
                if (From.Month != To.Month)
                    return $"{From.ToString("MMM d", Invariant)} – {To.ToString("MMM d, yyyy", Invariant)}";
                return $"{From.ToString("MMM d", Invariant)} – {To.ToString("d, yyyy", Invariant)}";
            }
        }

        /// <summary>What the bills were narrowed to, in words: "Needs attention. ECPay only. Search “01-0095”."</summary>
        public string Filters
        {
            get
            {
                var parts = new List<string> { View == InspectionViews.All ? "All bills" : ViewLabel };
                parts.Add(Provider is null ? "All providers" : $"{Provider} only");
                if (Search.Length > 0)
                    parts.Add($"Search “{Search}”");
                return string.Join(". ", parts) + ".";
            }
        }

        /// <summary>"Transaction-Inspection-2026-06-01-to-2026-06-30-Needs-attention".</summary>
        public string FileStem()
        {
            var stem = $"Transaction-Inspection-{From.ToString("yyyy-MM-dd", Invariant)}";
            if (To.Date != From.Date)
                stem += $"-to-{To.ToString("yyyy-MM-dd", Invariant)}";
            if (View != InspectionViews.All)
                stem += "-" + ViewLabel.Replace(' ', '-');
            return stem;
        }

        public static string Amount(decimal amount) => amount.ToString("N2", Invariant);

        /// <summary>"PHP 1,234.56": the currency spelled out, so the printout's glyph set stays the font's own.</summary>
        public static string Pesos(decimal amount) => "PHP " + Amount(amount);

        public static string Count(int count) => count.ToString("N0", Invariant);

        public static string Plural(int count, string one, string many) => $"{Count(count)} {(count == 1 ? one : many)}";

        /// <summary>"Jun 6, 2026 9:41 AM".</summary>
        public static string Stamp(DateTime time) => time.ToString("MMM d, yyyy h:mm tt", Invariant);

        /// <summary>A bill's period as its consumer knows it: "Apr 2026".</summary>
        public static string BillPeriod(DateTime servicePeriodEnd) => servicePeriodEnd.ToString("MMM yyyy", Invariant);

        /// <summary>When a payment was begun, as a register writes it: the time alone on a one-day printout.</summary>
        public string Paid(DateTime createdOn)
            => From.Date == To.Date
                ? createdOn.ToString("h:mm tt", Invariant)
                : createdOn.ToString("MMM d, h:mm tt", Invariant);

        /// <summary>A signed difference: "+12.00", "-3.50" (a hyphen-minus: the printouts keep to the font's own glyphs).</summary>
        public static string Signed(decimal amount)
            => amount > 0 ? "+" + Amount(amount) : amount < 0 ? "-" + Amount(-amount) : Amount(0);
    }
}
