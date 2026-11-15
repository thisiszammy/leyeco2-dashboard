using System.Globalization;
using Leyeco2Dashboard.Client.Infrastructure.Formatting;

namespace Leyeco2Dashboard.Client.Features.TransactionInspector.Models
{
    /// <summary>
    /// How the inspector draws a book's status and a verdict. Ink for what a book records as it should
    /// (Successful, Posted), amber for what waits (Pending, Queued, Posting), red only for a failure (Posting failed),
    /// slate for a payment that never went through, and pencil, a dashed place left empty, for a record a book doesn't
    /// hold. The verdict is the row's one coloured word: green tallied, amber waiting, red what needs someone, slate
    /// not paid; the margin carries the same as a mark, and the words carry it alone for anyone who can't tell the
    /// colours apart.
    /// </summary>
    public static class InspectionTones
    {
        public const string Tallied = "tallied";
        public const string AmountDiffers = "amount-differs";
        public const string PaidElsewhere = "paid-elsewhere";
        public const string NotPosted = "not-posted";
        public const string Waiting = "waiting";
        public const string CreditedUnpaid = "credited-unpaid";
        public const string Unpaid = "unpaid";

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary>The tone of a facility status: <c>is-ink</c>, <c>is-wait</c> or <c>is-mute</c>.</summary>
        public static string Facility(string status) => status switch
        {
            "successful" => "is-ink",
            "pending"    => "is-wait",
            _            => "is-mute",
        };

        public static string Queue(string status) => status switch
        {
            "posted"  => "is-ink",
            "queued"  => "is-wait",
            "posting" => "is-wait",
            "failed"  => "is-fail",
            _         => "is-mute",
        };

        /// <summary>The ledger's row is ink either way: a posting under another receipt is a fact; the verdict judges it.</summary>
        public static string Ledger(string status) => "is-ink";

        public static string Verdict(string verdict) => verdict switch
        {
            Tallied => "is-tallied",
            Waiting => "is-waiting",
            Unpaid  => "is-unpaid",
            _       => "is-attention",
        };

        public static bool NeedsAttention(string verdict) => Verdict(verdict) == "is-attention";

        /// <summary>The margin's mark, for screen readers too (it is drawn <c>aria-hidden</c>; this goes in the row's words).</summary>
        public static string? MarginMark(string verdict) => verdict switch
        {
            Tallied => "tallied",
            Waiting => "waiting",
            Unpaid  => null,
            _       => "attention",
        };

        /// <summary>The verdict in a sentence: what each book holds and what that means, for the bill's dialog.</summary>
        public static string Statement(InspectedBill bill)
        {
            var paid = Figures.Peso(bill.Facility.Total);
            var ledger = bill.Ledger;
            var queue = bill.Queue;

            switch (bill.Verdict)
            {
                case Tallied:
                    return $"The ledger holds this payment's own posting, receipt {ledger?.OrNumber}, for the {paid} the facility recorded as paid.";

                case AmountDiffers:
                {
                    var difference = bill.Difference ?? 0m;
                    var way = difference < 0 ? "less" : "more";
                    return $"The ledger holds this payment's own posting, receipt {ledger?.OrNumber}, for {Figures.Peso(ledger?.Total ?? 0m)}: "
                         + $"{Figures.Peso(Math.Abs(difference))} {way} than the {paid} the facility recorded as paid.";
                }

                case PaidElsewhere:
                    return $"The facility recorded {paid} as paid, but the ledger has this bill paid via {ledger?.Channel} under receipt "
                         + $"{ledger?.OrNumber ?? "(none)"} on {Day(ledger?.DatePaid)}, not under this payment. The bill may have been paid twice: "
                         + "one of the two is to be refunded or reversed.";

                case NotPosted:
                    return queue?.Status switch
                    {
                        null => $"The facility recorded {paid} as paid, but the bill was never queued for posting and the ledger holds nothing for it.",
                        "failed" => $"The facility recorded {paid} as paid. The poster could not post it"
                                    + (queue.Remarks is { } reason ? $" ({reason.TrimEnd('.')})" : string.Empty)
                                    + ", and the ledger holds nothing for it.",
                        "posted" => $"The facility recorded {paid} as paid and the queue says the bill was posted, but the ledger holds nothing for it.",
                        _ => $"The facility recorded {paid} as paid. The queue holds the bill at status {queue.StatusCode}, and the ledger holds nothing for it.",
                    };

                case Waiting:
                    return queue?.Status == "posting"
                        ? $"The facility recorded {paid} as paid and queued the bill at {Stamp(queue.QueuedOn)}. The poster is posting it; the ledger holds nothing for it yet."
                        : $"The facility recorded {paid} as paid and queued the bill at {Stamp(queue?.QueuedOn)}. The poster hasn't reached it; the ledger holds nothing for it yet.";

                case CreditedUnpaid:
                    var word = bill.Facility.StatusLabel.ToLowerInvariant();
                    return ledger?.Status == "posted"
                        ? $"The facility recorded this payment as {word}, yet the ledger holds its posting, receipt {ledger.OrNumber}, for "
                          + $"{Figures.Peso(ledger.Total)}. The bill is credited without a payment."
                        : $"The facility recorded this payment as {word}, yet the posting queue still means to post it ({queue?.StatusLabel.ToLowerInvariant()}).";

                default:
                    var status = bill.Facility.StatusLabel.ToLowerInvariant();
                    var tail = ledger is null
                        ? " Nothing was owed a posting, and the ledger holds none for this bill."
                        : $" Nothing was owed a posting. The ledger has the bill paid via {ledger.Channel}, receipt {ledger.OrNumber ?? "(none)"}.";
                    return $"The facility recorded this payment as {status}.{tail}";
            }
        }

        /// <summary>"Apr 2026": a bill as its consumer knows it.</summary>
        public static string BillPeriod(DateTime servicePeriodEnd) => servicePeriodEnd.ToString("MMM yyyy", Invariant);

        /// <summary>"Jun 6, 9:41 AM", or the time alone when the period is a single day.</summary>
        public static string Paid(DateTime createdOn, bool singleDay)
            => singleDay ? createdOn.ToString("h:mm tt", Invariant) : createdOn.ToString("MMM d, h:mm tt", Invariant);

        public static string Stamp(DateTime? time) => time?.ToString("MMM d, yyyy h:mm tt", Invariant) ?? "—";

        public static string Day(DateTime? date) => date?.ToString("MMM d, yyyy", Invariant) ?? "—";

        /// <summary>A signed peso difference: "+₱12.00", "−₱3.50".</summary>
        public static string Signed(decimal amount)
            => amount > 0 ? "+" + Figures.Peso(amount) : amount < 0 ? "−" + Figures.Peso(-amount) : Figures.Peso(0);
    }
}
