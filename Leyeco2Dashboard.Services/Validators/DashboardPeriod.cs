using Leyeco2Dashboard.Services.Exceptions;

namespace Leyeco2Dashboard.Services.Validators
{
    /// <summary>
    /// The days a dashboard reads, from <see cref="From"/> through <see cref="To"/> (whole days, both included), and
    /// the period of as many days just before it, which its figures are compared with.
    /// </summary>
    public sealed class DashboardPeriod
    {
        /// <summary>
        /// The longest period a dashboard reads at once: a quarter. The ledger's index makes a quarter quick, but a
        /// year would still be a year of rows read on the cashiering system's server, for a chart too dense to read.
        /// </summary>
        public const int MaxDays = 92;

        private DashboardPeriod(DateTime from, DateTime to, DateTime today)
        {
            From = from;
            To = to;
            Days = (int)(to - from).TotalDays + 1;
            PreviousFrom = from.AddDays(-Days);
            PreviousTo = from.AddDays(-1);
            IncludesToday = from <= today && today <= to;
        }

        public DateTime From { get; }

        public DateTime To { get; }

        /// <summary>The day after <see cref="To"/>: the queries read up to it, not including it.</summary>
        public DateTime Until => To.AddDays(1);

        public int Days { get; }

        public DateTime PreviousFrom { get; }

        public DateTime PreviousTo { get; }

        public DateTime PreviousUntil => PreviousTo.AddDays(1);

        /// <summary>Today's figures still move, so they are kept a minute rather than a quarter of an hour.</summary>
        public bool IncludesToday { get; }

        public TimeSpan KeepFor => IncludesToday ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(15);

        public string Key => $"{From:yyyyMMdd}-{To:yyyyMMdd}";

        /// <summary>
        /// The period asked for: today when neither date is given, one day when only one is. Only the dates count;
        /// a time of day is dropped.
        /// </summary>
        public static DashboardPeriod Of(DateTime? from, DateTime? to, DateTime now)
            => Of(from, to, now, MaxDays, "A dashboard");

        /// <summary>
        /// <see cref="Of(DateTime?, DateTime?, DateTime)"/> under a limit of its own: a screen that reads records, not
        /// sums, reads fewer days at a time. <paramref name="reader"/> names it in the refusal ("The inspector").
        /// </summary>
        public static DashboardPeriod Of(DateTime? from, DateTime? to, DateTime now, int maxDays, string reader)
        {
            var today = now.Date;
            var start = (from ?? to ?? today).Date;
            var end = (to ?? from ?? today).Date;

            if (end < start)
                throw new DashboardRequestException("The period ends before it starts. Pick the earlier date first.");

            var days = (int)(end - start).TotalDays + 1;
            if (days > maxDays)
                throw new DashboardRequestException(
                    $"That period is {days:N0} days long. {reader} reads up to {maxDays} days at a time: pick a shorter one.");

            return new DashboardPeriod(start, end, today);
        }
    }
}
