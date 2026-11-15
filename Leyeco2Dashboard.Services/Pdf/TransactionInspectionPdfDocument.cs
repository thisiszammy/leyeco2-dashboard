using Leyeco2Dashboard.Services.DTOs;
using Leyeco2Dashboard.Services.Inspection;
using Leyeco2Dashboard.Services.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static Leyeco2Dashboard.Services.Pdf.PrintoutLayout;

namespace Leyeco2Dashboard.Services.Pdf
{
    /// <summary>
    /// An inspection as a PDF, A4 landscape: the letterhead; the three books side by side as the console's passage
    /// draws them (what the facility recorded as paid, what it handed to the poster, what the ledger posted from it,
    /// each by status) beside the verdicts; then every bill of the view, its three books in three column groups, its
    /// verdict in words, footed. The column heads repeat on every page.
    /// </summary>
    internal sealed class TransactionInspectionPdfDocument : IDocument
    {
        private readonly InspectionReport _report;
        private readonly Image _logo;

        public TransactionInspectionPdfDocument(InspectionReport report)
        {
            EnsureFonts();
            _report = report;
            _logo = Logo();
        }

        public DocumentMetadata GetMetadata() => new()
        {
            Title = $"{InspectionReport.Title}, {_report.Period}",
            Subject = InspectionReport.Title,
            Author = "Leyeco II Transaction Monitor",
            CreationDate = _report.PrintedOn,
            ModifiedDate = _report.PrintedOn,
        };

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(style => style.FontFamily(FontFamily).FontSize(7.5f).FontColor(Ink).LineHeight(1.2f));

                page.Header().Element(header => PrintoutLayout.Header(
                    header,
                    _logo,
                    InspectionReport.Title,
                    _report.Period,
                    _report.Filters,
                    $"Read from the facility and the ledger at {InspectionReport.Stamp(_report.ReadOn)}"));

                page.Content().PaddingTop(10).Column(column =>
                {
                    column.Spacing(12);
                    column.Item().ShowEntire().Element(Summary);
                    column.Item().Element(Register);
                });

                page.Footer().Element(footer => Footer(footer, $"{InspectionReport.Title}, {_report.Period}", _report.PrintedOn));
            });
        }

        // ── The books ───────────────────────────────────────────────────────────────────────────────────────────

        private void Summary(IContainer container)
        {
            var summary = _report.Summary;

            container.Row(row =>
            {
                row.Spacing(14);

                row.RelativeItem().Element(book => Book(book,
                    "Online facility",
                    summary.Paid.Amount,
                    $"{InspectionReport.Plural(summary.Paid.Bills, "bill", "bills")} paid in {InspectionReport.Plural(summary.PaidPayments, "payment", "payments")}",
                    summary.Facility,
                    closing: null));

                row.RelativeItem().Element(book => Book(book,
                    "Posting queue",
                    summary.Queued.Amount,
                    $"{InspectionReport.Plural(summary.Queued.Bills, "paid bill", "paid bills")} handed to the poster",
                    summary.Queue,
                    closing: null));

                row.RelativeItem().Element(book => Book(book,
                    "Collections ledger",
                    summary.Posted.Amount,
                    $"{InspectionReport.Plural(summary.Posted.Bills, "bill", "bills")} posted from these payments",
                    summary.Ledger,
                    closing: Shortfall(summary)));

                row.RelativeItem(1.15f).Element(Verdicts);
            });
        }

        private static void Book(IContainer container, string name, decimal headline, string sub, IReadOnlyList<InspectionLineDTO> lines, (string Text, Color Color)? closing)
        {
            container.BorderTop(1.5f).BorderColor(RuleStrong).PaddingTop(4).Column(column =>
            {
                column.Item().Text(name).FontSize(8.5f).SemiBold().FontColor(BrandNavy);
                column.Item().PaddingTop(1).Text(InspectionReport.Pesos(headline)).FontSize(13).SemiBold().FontColor(Ink);
                column.Item().Text(sub).FontSize(7).FontColor(Slate500);

                column.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.ConstantColumn(34);
                        columns.ConstantColumn(64);
                    });

                    if (lines.Count == 0)
                    {
                        table.Cell().ColumnSpan(3).BorderTop(0.5f).BorderColor(Hairline).PaddingVertical(2.5f)
                            .Text("No paid bill to follow.").FontColor(Slate500);
                    }

                    foreach (var line in lines)
                    {
                        Line(table.Cell()).Text(line.Label);
                        Line(table.Cell()).AlignRight().Text(InspectionReport.Count(line.Bills));
                        Line(table.Cell()).AlignRight().Text(InspectionReport.Amount(line.Amount));
                    }
                });

                if (closing is { } close)
                    column.Item().PaddingTop(3).Text(close.Text).FontSize(7.5f).SemiBold().FontColor(close.Color);
            });
        }

        private static (string, Color) Shortfall(InspectionSummaryDTO summary)
        {
            if (summary.Paid.Bills == 0)
                return ("Nothing paid online to post.", Slate500);

            return summary.Shortfall switch
            {
                > 0.004m => ($"{InspectionReport.Pesos(summary.Shortfall)} short of the facility", Red),
                < -0.004m => ($"{InspectionReport.Pesos(-summary.Shortfall)} over the facility", Red),
                _ => ("Level with the facility", Green),
            };
        }

        private void Verdicts(IContainer container)
        {
            var summary = _report.Summary;
            var all = summary.Views.FirstOrDefault(view => view.Key == InspectionViews.All);

            container.BorderTop(1.5f).BorderColor(RuleStrong).PaddingTop(4).Column(column =>
            {
                column.Item().Text("Verdicts").FontSize(8.5f).SemiBold().FontColor(BrandNavy);
                column.Item().PaddingTop(1).Text(InspectionReport.Plural(all?.Bills ?? 0, "bill", "bills") + " inspected").FontSize(13).SemiBold().FontColor(Ink);
                column.Item().Text("Each bill's facility and ledger totals").FontSize(7).FontColor(Slate500);

                column.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.ConstantColumn(30);
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(60);
                    });

                    table.Header(header =>
                    {
                        HeadLine(header.Cell()).Text("Verdict");
                        HeadLine(header.Cell()).AlignRight().Text("Bills");
                        HeadLine(header.Cell()).AlignRight().Text("Facility");
                        HeadLine(header.Cell()).AlignRight().Text("Ledger");
                    });

                    foreach (var verdict in summary.Verdicts)
                    {
                        Line(table.Cell()).Text(verdict.Label).FontColor(VerdictColor(verdict.Key)).SemiBold();
                        Line(table.Cell()).AlignRight().Text(InspectionReport.Count(verdict.Bills));
                        Line(table.Cell()).AlignRight().Text(InspectionReport.Amount(verdict.FacilityAmount));
                        Line(table.Cell()).AlignRight().Text(InspectionReport.Amount(verdict.LedgerAmount));
                    }

                    if (all is not null)
                    {
                        FootLine(table.Cell()).Text("All bills");
                        FootLine(table.Cell()).AlignRight().Text(InspectionReport.Count(all.Bills));
                        FootLine(table.Cell()).AlignRight().Text(InspectionReport.Amount(all.FacilityAmount));
                        FootLine(table.Cell()).AlignRight().Text(InspectionReport.Amount(all.LedgerAmount));
                    }
                });
            });
        }

        // ── The register ────────────────────────────────────────────────────────────────────────────────────────

        private void Register(IContainer container)
        {
            var bills = _report.Bills;

            container.Column(column =>
            {
                column.Item().PaddingBottom(4).Row(row =>
                {
                    row.RelativeItem().Text(_report.ViewLabel).FontSize(9.5f).SemiBold().FontColor(Ink);
                    row.RelativeItem().AlignRight().AlignBottom()
                        .Text($"{InspectionReport.Plural(bills.Count, "bill", "bills")}. Amounts in pesos, each bill's amount and surcharge together.")
                        .FontSize(7).FontColor(Slate500);
                });

                if (bills.Count == 0)
                {
                    column.Item().Border(0.75f).BorderColor(Rule).Padding(10).AlignCenter()
                        .Text("No bill is in this view for the period and filters above.").FontColor(Slate500);
                    return;
                }

                column.Item().Border(0.75f).BorderColor(Rule).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(24);   // #
                        columns.ConstantColumn(_report.From.Date == _report.To.Date ? 36 : 54); // Paid
                        columns.ConstantColumn(44);   // Payment
                        columns.ConstantColumn(54);   // Provider: "Bayad Center" whole
                        columns.ConstantColumn(52);   // Account
                        columns.RelativeColumn();     // Consumer
                        columns.ConstantColumn(36);   // Bill
                        columns.ConstantColumn(44);   // Online status
                        columns.ConstantColumn(46);   // Online amount
                        columns.ConstantColumn(46);   // Queue status
                        columns.ConstantColumn(46);   // Queue amount
                        columns.ConstantColumn(60);   // Ledger status
                        columns.ConstantColumn(46);   // Ledger amount
                        columns.ConstantColumn(56);   // OR no.: a ten-digit receipt on one line
                        columns.ConstantColumn(62);   // Verdict
                    });

                    table.Header(header =>
                    {
                        Head(header.Cell().RowSpan(2)).AlignRight().Text("#");
                        Head(header.Cell().RowSpan(2)).Text("Paid");
                        Head(header.Cell().RowSpan(2)).Text("Payment");
                        Head(header.Cell().RowSpan(2)).Text("Provider");
                        Head(header.Cell().RowSpan(2)).Text("Account");
                        Head(header.Cell().RowSpan(2)).Text("Consumer");
                        Head(header.Cell().RowSpan(2)).Text("Bill");
                        Group(header.Cell().ColumnSpan(2)).Text("Online facility");
                        Group(header.Cell().ColumnSpan(2)).Text("Posting queue");
                        Group(header.Cell().ColumnSpan(3)).Text("Collections ledger");
                        Head(header.Cell().RowSpan(2)).Text("Verdict");

                        Head(header.Cell()).Text("Status");
                        Head(header.Cell()).AlignRight().Text("Amount");
                        Head(header.Cell()).Text("Status");
                        Head(header.Cell()).AlignRight().Text("Amount");
                        Head(header.Cell()).Text("Status");
                        Head(header.Cell()).AlignRight().Text("Amount");
                        Head(header.Cell()).Text("OR no.");
                    });

                    var number = 0;
                    foreach (var bill in bills)
                    {
                        number++;
                        Cell(table.Cell()).AlignRight().Text(InspectionReport.Count(number)).FontColor(Slate500);
                        Cell(table.Cell()).Text(_report.Paid(bill.CreatedOn));
                        Cell(table.Cell()).Text(bill.TransactionId);
                        Cell(table.Cell()).Text(bill.ProviderLabel).ClampLines(1);
                        Cell(table.Cell()).Text(bill.AccountNumber);
                        Cell(table.Cell()).Text(bill.ConsumerName ?? "Not on file").FontColor(bill.ConsumerName is null ? Slate500 : Ink).ClampLines(2);
                        Cell(table.Cell()).Text(InspectionReport.BillPeriod(bill.ServicePeriodEnd));

                        Cell(table.Cell()).Text(bill.Facility.StatusLabel).FontColor(bill.Facility.Status == InspectionRules.Successful ? Ink : Slate500);
                        Cell(table.Cell()).AlignRight().Text(InspectionReport.Amount(bill.Facility.Total));

                        if (bill.Queue is { } queue)
                        {
                            Cell(table.Cell()).Text(queue.StatusLabel).FontColor(queue.Status == InspectionRules.Failed ? Red : Ink);
                            Cell(table.Cell()).AlignRight().Text(InspectionReport.Amount(queue.Total));
                        }
                        else
                        {
                            Cell(table.Cell()).Text("Not queued").FontColor(Slate400);
                            Cell(table.Cell()).AlignRight().Text("—").FontColor(Slate400);
                        }

                        if (bill.Ledger is { } ledger)
                        {
                            Cell(table.Cell()).Text(ledger.StatusLabel).FontColor(ledger.Status == InspectionRules.Posted ? Ink : Red).ClampLines(1);
                            Cell(table.Cell()).AlignRight().Text(InspectionReport.Amount(ledger.Total))
                                .FontColor(bill.Verdict == InspectionRules.AmountDiffers ? Red : Ink);
                            Cell(table.Cell()).Text(ledger.OrNumber ?? "—").FontColor(ledger.OrNumber is null ? Slate400 : Ink);
                        }
                        else
                        {
                            Cell(table.Cell()).Text("Not in the ledger").FontColor(Slate400);
                            Cell(table.Cell()).AlignRight().Text("—").FontColor(Slate400);
                            Cell(table.Cell()).Text("—").FontColor(Slate400);
                        }

                        Cell(table.Cell()).Text(text =>
                        {
                            text.Span(bill.VerdictLabel).SemiBold().FontColor(VerdictColor(bill.Verdict));
                            if (bill.Verdict == InspectionRules.AmountDiffers && bill.Difference is decimal difference)
                                text.Span(" " + InspectionReport.Signed(difference)).FontColor(Red);
                        });
                    }

                    Foot(table.Cell().ColumnSpan(7)).Text($"Total, {InspectionReport.Plural(bills.Count, "bill", "bills")}");
                    Foot(table.Cell());
                    Foot(table.Cell()).AlignRight().Text(InspectionReport.Amount(bills.Sum(bill => bill.Facility.Total)));
                    Foot(table.Cell());
                    Foot(table.Cell()).AlignRight().Text(InspectionReport.Amount(bills.Sum(bill => bill.Queue?.Total ?? 0m)));
                    Foot(table.Cell());
                    Foot(table.Cell()).AlignRight().Text(InspectionReport.Amount(bills.Sum(bill => bill.Ledger?.Total ?? 0m)));
                    Foot(table.Cell().ColumnSpan(2));
                });
            });
        }

        // ── Building blocks ─────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A verdict's colour, second to its words: green tallied, amber waiting, red what needs someone, slate not paid.
        /// On a grayscale printer the words alone carry it.
        /// </summary>
        private static Color VerdictColor(string verdict) => verdict switch
        {
            InspectionRules.Tallied => Green,
            InspectionRules.Waiting => AmberInk,
            InspectionRules.Unpaid  => Slate500,
            _                       => Red,
        };

        private static IContainer Head(IContainer cell)
            => cell.Background(Band).BorderBottom(0.75f).BorderColor(Rule).PaddingVertical(3).PaddingHorizontal(3)
                .DefaultTextStyle(style => style.FontSize(6.8f).SemiBold().FontColor(Slate600));

        private static IContainer Group(IContainer cell)
            => cell.Background(Band).BorderBottom(0.5f).BorderColor(Hairline).BorderLeft(0.5f).PaddingVertical(2.5f).PaddingHorizontal(3)
                .DefaultTextStyle(style => style.FontSize(7).SemiBold().FontColor(BrandNavy));

        private static IContainer Cell(IContainer cell)
            => cell.BorderBottom(0.5f).BorderColor(Hairline).PaddingVertical(2.2f).PaddingHorizontal(3)
                .DefaultTextStyle(style => style.FontSize(7));

        private static IContainer HeadLine(IContainer cell)
            => cell.BorderBottom(0.75f).BorderColor(Rule).PaddingVertical(2)
                .DefaultTextStyle(style => style.FontSize(6.8f).SemiBold().FontColor(Slate600));

        private static IContainer Line(IContainer cell)
            => cell.BorderBottom(0.5f).BorderColor(Hairline).PaddingVertical(2.2f);

        /// <summary>A footing, as a ledger foots a total: a rule over it in ink.</summary>
        private static IContainer Foot(IContainer cell)
            => FootLine(cell).PaddingHorizontal(3);

        /// <summary>A summary table's footing, flush with its lines.</summary>
        private static IContainer FootLine(IContainer cell)
            => cell.BorderTop(1).BorderColor(RuleStrong).PaddingVertical(3)
                .DefaultTextStyle(style => style.SemiBold().FontColor(Ink));
    }
}
