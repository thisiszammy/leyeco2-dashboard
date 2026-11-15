using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using Leyeco2Dashboard.Services.DTOs;
using Leyeco2Dashboard.Services.Inspection;
using Leyeco2Dashboard.Services.Reports;

namespace Leyeco2Dashboard.Services.Excel
{
    /// <summary>
    /// An inspection as an Excel workbook (ClosedXML), after the HRIS's printouts (its <c>PlantillaWorkbooks</c>):
    /// <list type="bullet">
    /// <item><b>Summary</b>, on the cooperative's letterhead: each book's lines (the facility's by status, the queue's and
    /// the ledger's for the paid bills) with its total, then the verdicts, footed.</item>
    /// <item><b>Bills</b>: every bill of the view as an Excel table, one row a bill and every field each book keeps for it,
    /// amounts as numbers and times as dates, to sort, filter and sum; its totals row sums the amounts of the rows a
    /// filter leaves.</item>
    /// </list>
    /// Each sheet prints on its own: landscape, fitted to the page's width, its column heads on every page.
    /// </summary>
    internal static class TransactionInspectionWorkbook
    {
        private const string FontName = "Calibri";
        private const double FontSize = 10;

        /// <summary>The title block's four rows, in points: 66 in all, which the seal fills less its margins.</summary>
        private static readonly double[] TitleRows = [14, 22, 16, 14];

        /// <summary>The seal's height and its margin from the sheet's corner, in pixels (66pt = 88px).</summary>
        private const int LogoHeight = 78;
        private const int LogoMargin = 4;

        /// <summary>The title lines' indent past the seal: Excel indents a level about 9px at this size.</summary>
        private const int TitleIndent = 9;

        private const string AmountFormat = "#,##0.00;-#,##0.00;-";
        private const string CountFormat = "#,##0";
        private const string TimeFormat = "mmm d, yyyy h:mm AM/PM";
        private const string DateFormat = "mmm d, yyyy";
        private const string PeriodFormat = "mmm yyyy";

        private static readonly XLColor BrandNavy = XLColor.FromHtml("#1E3D6E");
        private static readonly XLColor Ink = XLColor.FromHtml("#2A3439");
        private static readonly XLColor Slate600 = XLColor.FromHtml("#566166");
        private static readonly XLColor Slate500 = XLColor.FromHtml("#64748B");
        private static readonly XLColor Red = XLColor.FromHtml("#B22B1D");
        private static readonly XLColor AmberInk = XLColor.FromHtml("#92400E");
        private static readonly XLColor Green = XLColor.FromHtml("#15803D");
        private static readonly XLColor Rule = XLColor.FromHtml("#8D9AA2");
        private static readonly XLColor RuleStrong = XLColor.FromHtml("#4B565C");
        private static readonly XLColor Hairline = XLColor.FromHtml("#D9E4EA");
        private static readonly XLColor Band = XLColor.FromHtml("#EEF2F5");

        public static byte[] Build(InspectionReport report)
        {
            using var workbook = new XLWorkbook();
            workbook.Style.Font.FontName = FontName;
            workbook.Style.Font.FontSize = FontSize;
            workbook.Style.Font.FontColor = Ink;
            workbook.Properties.Title = $"{InspectionReport.Title}, {report.Period}";
            workbook.Properties.Subject = InspectionReport.Title;
            workbook.Properties.Author = "Leyeco II Transaction Monitor";
            workbook.Properties.Created = report.PrintedOn;

            SummarySheet(workbook, report);
            BillsSheet(workbook, report);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        // ── Summary ─────────────────────────────────────────────────────────────────────────────────────────────

        private static void SummarySheet(XLWorkbook workbook, InspectionReport report)
        {
            const int lastColumn = 4;
            var sheet = workbook.Worksheets.Add("Summary");
            var summary = report.Summary;

            TitleBlock(sheet, report, lastColumn);

            sheet.Column(1).Width = 40;
            sheet.Column(2).Width = 11;
            sheet.Column(3).Width = 18;
            sheet.Column(4).Width = 18;

            var row = 6;
            var filters = sheet.Cell(row, 1);
            filters.Value = report.Filters;
            filters.Style.Font.FontColor = Slate600;
            row += 2;

            row = Book(sheet, row, "Online facility", "Every bill begun, by the facility's status",
                summary.Facility, "Paid online (successful)", summary.Paid.Bills, summary.Paid.Amount, closing: null);

            row = Book(sheet, row, "Posting queue", "The paid bills, by where the queue has them",
                summary.Queue, "Handed to the poster", summary.Queued.Bills, summary.Queued.Amount, closing: null);

            (string Label, decimal Amount, XLColor Color)? shortfall = summary.Paid.Bills == 0
                ? null
                : Math.Abs(summary.Shortfall) < 0.005m
                    ? ("Level with the facility", 0m, Green)
                    : summary.Shortfall > 0
                        ? ("Short of the facility", summary.Shortfall, Red)
                        : ("Over the facility", -summary.Shortfall, Red);

            row = Book(sheet, row, "Collections ledger", "The paid bills, by what the ledger holds for them",
                summary.Ledger, "Posted from these payments", summary.Posted.Bills, summary.Posted.Amount,
                closing: shortfall);

            // The verdicts, footed.
            Heading(sheet, row, "Verdicts", "Each bill's total as the facility and the ledger hold it");
            row++;
            string[] heads = ["Verdict", "Bills", "Facility", "Ledger"];
            for (var c = 0; c < heads.Length; c++)
                sheet.Cell(row, c + 1).Value = heads[c];
            HeadStyle(sheet.Range(row, 1, row, lastColumn));
            var headRow = row;
            row++;

            foreach (var verdict in summary.Verdicts)
            {
                var label = sheet.Cell(row, 1);
                label.Value = verdict.Label;
                label.Style.Font.FontColor = VerdictColor(verdict.Key);
                label.Style.Font.Bold = true;
                Count(sheet.Cell(row, 2), verdict.Bills);
                Amount(sheet.Cell(row, 3), verdict.FacilityAmount);
                Amount(sheet.Cell(row, 4), verdict.LedgerAmount);
                row++;
            }

            var all = summary.Views.FirstOrDefault(view => view.Key == InspectionViews.All);
            sheet.Cell(row, 1).Value = "All bills";
            Count(sheet.Cell(row, 2), all?.Bills ?? 0);
            Amount(sheet.Cell(row, 3), all?.FacilityAmount ?? 0m);
            Amount(sheet.Cell(row, 4), all?.LedgerAmount ?? 0m);
            Footing(sheet.Range(row, 1, row, lastColumn));
            Frame(sheet.Range(headRow, 1, row, lastColumn));

            var note = sheet.Cell(row + 2, 1);
            note.Value = "Amounts in pesos: each bill's amount and surcharge together, as each book keeps them.";
            note.Style.Font.FontColor = Slate500;
            note.Style.Font.FontSize = 9;

            PrintSetup(sheet, report, "Summary", landscape: false, firstHeadRow: 0, lastRow: row + 2, lastColumn);
        }

        /// <summary>A book's lines under its name, then its total; returns the row after the gap that follows it.</summary>
        private static int Book(
            IXLWorksheet sheet, int row, string name, string sub, IReadOnlyList<InspectionLineDTO> lines,
            string totalLabel, int totalBills, decimal totalAmount, (string Label, decimal Amount, XLColor Color)? closing)
        {
            Heading(sheet, row, name, sub);
            row++;

            sheet.Cell(row, 1).Value = "Status";
            sheet.Cell(row, 2).Value = "Bills";
            sheet.Cell(row, 3).Value = "Amount";
            HeadStyle(sheet.Range(row, 1, row, 3));
            var headRow = row;
            row++;

            if (lines.Count == 0)
            {
                var none = sheet.Cell(row, 1);
                none.Value = "No paid bill to follow.";
                none.Style.Font.FontColor = Slate500;
                row++;
            }

            foreach (var line in lines)
            {
                var label = sheet.Cell(row, 1);
                label.Value = line.Label;
                label.Style.Alignment.Indent = 1;
                Count(sheet.Cell(row, 2), line.Bills);
                Amount(sheet.Cell(row, 3), line.Amount);
                row++;
            }

            sheet.Cell(row, 1).Value = totalLabel;
            Count(sheet.Cell(row, 2), totalBills);
            Amount(sheet.Cell(row, 3), totalAmount);
            Footing(sheet.Range(row, 1, row, 3));
            Frame(sheet.Range(headRow, 1, row, 3));
            row++;

            if (closing is { } close)
            {
                var label = sheet.Cell(row, 1);
                label.Value = close.Label;
                label.Style.Font.Bold = true;
                label.Style.Font.FontColor = close.Color;
                var amount = sheet.Cell(row, 3);
                Amount(amount, close.Amount);
                amount.Style.Font.Bold = true;
                amount.Style.Font.FontColor = close.Color;
                row++;
            }

            return row + 1;
        }

        private static void Heading(IXLWorksheet sheet, int row, string name, string sub)
        {
            var heading = sheet.Cell(row, 1);
            heading.Value = name;
            heading.Style.Font.Bold = true;
            heading.Style.Font.FontSize = 12;
            heading.Style.Font.FontColor = BrandNavy;

            var note = sheet.Cell(row, 2);
            note.Value = sub;
            note.Style.Font.FontColor = Slate500;
            note.Style.Font.FontSize = 9;
            note.Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;
            sheet.Row(row).Height = 20;
        }

        // ── Bills ───────────────────────────────────────────────────────────────────────────────────────────────

        private static readonly (string Head, double Width)[] BillColumns =
        [
            ("#", 6),
            ("Paid on", 19),
            ("Payment", 12),
            ("Provider", 13),
            ("Provider reference", 18),
            ("Account no.", 13),
            ("Consumer", 30),
            ("Consumer standing", 12),
            ("Bill no.", 12),
            ("Bill period", 10),
            ("Online status", 12),
            ("Online amount", 13),
            ("Online surcharge", 13),
            ("Online total", 13),
            ("Queue status", 13),
            ("Queue OR no.", 12),
            ("Queued on", 19),
            ("Queue total", 13),
            ("Ledger status", 18),
            ("Ledger OR no.", 13),
            ("Ledger date paid", 13),
            ("Ledger posted on", 19),
            ("Ledger posted by", 13),
            ("Ledger total", 13),
            ("Difference", 12),
            ("Verdict", 18),
            ("Online remarks", 30),
            ("Queue remarks", 30),
            ("Ledger remarks", 30),
        ];

        private static void BillsSheet(XLWorkbook workbook, InspectionReport report)
        {
            var sheet = workbook.Worksheets.Add("Bills");
            var bills = report.Bills;

            for (var c = 0; c < BillColumns.Length; c++)
            {
                sheet.Cell(1, c + 1).Value = BillColumns[c].Head;
                sheet.Column(c + 1).Width = BillColumns[c].Width;
            }

            var row = 2;
            foreach (var bill in bills)
            {
                var c = 1;
                Count(sheet.Cell(row, c++), row - 1);
                Time(sheet.Cell(row, c++), bill.CreatedOn);
                sheet.Cell(row, c++).Value = bill.TransactionId;
                sheet.Cell(row, c++).Value = bill.ProviderLabel;
                Text(sheet.Cell(row, c++), bill.ProviderReference);
                sheet.Cell(row, c++).Value = bill.AccountNumber;
                Text(sheet.Cell(row, c++), bill.ConsumerName);
                Text(sheet.Cell(row, c++), bill.ConsumerName is null ? null : bill.IsDisconnected ? "Disconnected" : "Active");
                Text(sheet.Cell(row, c++), bill.BillNumber);
                var period = sheet.Cell(row, c++);
                period.Value = bill.ServicePeriodEnd;
                period.Style.NumberFormat.Format = PeriodFormat;

                sheet.Cell(row, c++).Value = bill.Facility.StatusLabel;
                Amount(sheet.Cell(row, c++), bill.Facility.Amount);
                Amount(sheet.Cell(row, c++), bill.Facility.Surcharge);
                Amount(sheet.Cell(row, c++), bill.Facility.Total);

                sheet.Cell(row, c++).Value = bill.Queue?.StatusLabel ?? "Not queued";
                Text(sheet.Cell(row, c++), bill.Queue?.OrNumber);
                Time(sheet.Cell(row, c++), bill.Queue?.QueuedOn);
                Amount(sheet.Cell(row, c++), bill.Queue?.Total);

                sheet.Cell(row, c++).Value = bill.Ledger?.StatusLabel ?? "Not in the ledger";
                Text(sheet.Cell(row, c++), bill.Ledger?.OrNumber);
                var paid = sheet.Cell(row, c++);
                if (bill.Ledger is { } ledger)
                {
                    paid.Value = ledger.DatePaid;
                    paid.Style.NumberFormat.Format = DateFormat;
                }

                Time(sheet.Cell(row, c++), bill.Ledger?.PostedOn);
                Text(sheet.Cell(row, c++), bill.Ledger?.PostedBy);
                Amount(sheet.Cell(row, c++), bill.Ledger?.Total);
                Amount(sheet.Cell(row, c++), bill.Difference is decimal difference && difference != 0 ? difference : null);

                var verdict = sheet.Cell(row, c++);
                verdict.Value = bill.VerdictLabel;
                verdict.Style.Font.FontColor = VerdictColor(bill.Verdict);
                verdict.Style.Font.Bold = InspectionRules.NeedsAttention(bill.Verdict);

                Text(sheet.Cell(row, c++), bill.Facility.Remarks);
                Text(sheet.Cell(row, c++), bill.Queue?.Remarks);
                Text(sheet.Cell(row, c), bill.Ledger?.Remarks);
                row++;
            }

            // A table needs a row under its heads: an empty view gets one, saying so.
            if (bills.Count == 0)
            {
                sheet.Cell(2, 3).Value = "No bill is in this view for the period and filters chosen.";
                sheet.Cell(2, 3).Style.Font.FontColor = Slate500;
                row = 3;
            }

            var table = sheet.Range(1, 1, row - 1, BillColumns.Length).CreateTable("Bills");
            table.Theme = XLTableTheme.TableStyleLight1;
            table.ShowAutoFilter = true;
            if (bills.Count > 0)
            {
                table.ShowTotalsRow = true;
                table.Field("#").TotalsRowLabel = "Total";
                foreach (var head in new[] { "Online amount", "Online surcharge", "Online total", "Queue total", "Ledger total", "Difference" })
                    table.Field(head).TotalsRowFunction = XLTotalsRowFunction.Sum;
                table.Field("Payment").TotalsRowFunction = XLTotalsRowFunction.Count;

                var totals = sheet.Row(row);
                totals.Style.Font.Bold = true;
                foreach (var column in new[] { 12, 13, 14, 18, 24, 25 })
                    sheet.Cell(row, column).Style.NumberFormat.Format = AmountFormat;
            }

            var heads = sheet.Range(1, 1, 1, BillColumns.Length);
            heads.Style.Alignment.WrapText = true;
            heads.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            sheet.Row(1).Height = 28;
            sheet.SheetView.Freeze(1, 2);

            var lastRow = bills.Count > 0 ? row : row - 1;
            PrintSetup(sheet, report, "Bills", landscape: true, firstHeadRow: 1, lastRow, BillColumns.Length);
        }

        // ── Building blocks ─────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The letterhead over the sheet: the seal in its corner and, indented past it, the cooperative, the document,
        /// the period, and when the sources were read, the navy rule under them.
        /// </summary>
        private static void TitleBlock(IXLWorksheet sheet, InspectionReport report, int lastColumn)
        {
            for (var r = 0; r < TitleRows.Length; r++)
            {
                sheet.Row(r + 1).Height = TitleRows[r];
                sheet.Cell(r + 1, 1).Style.Alignment.Indent = TitleIndent;
            }

            sheet.AddPicture(new MemoryStream(Letterhead.Logo), XLPictureFormat.Png, "Seal")
                .MoveTo(sheet.Cell(1, 1), LogoMargin, LogoMargin)
                .WithSize((int)Math.Round(LogoHeight * Letterhead.LogoAspect), LogoHeight);

            var cooperative = sheet.Cell(1, 1);
            cooperative.Value = Letterhead.Cooperative;
            cooperative.Style.Font.FontColor = BrandNavy;
            cooperative.Style.Font.FontSize = 9;
            cooperative.Style.Font.Bold = true;

            var heading = sheet.Cell(2, 1);
            heading.Value = InspectionReport.Title;
            heading.Style.Font.FontSize = 16;
            heading.Style.Font.Bold = true;

            var period = sheet.Cell(3, 1);
            period.Value = report.Period;
            period.Style.Font.FontSize = 11;
            period.Style.Font.FontColor = Slate600;

            var read = sheet.Cell(4, 1);
            read.Value = $"Read from the facility and the ledger at {InspectionReport.Stamp(report.ReadOn)}.";
            read.Style.Font.FontSize = 9;
            read.Style.Font.FontColor = Slate500;

            sheet.Range(4, 1, 4, lastColumn).Style.Border.BottomBorder = XLBorderStyleValues.Medium;
            sheet.Range(4, 1, 4, lastColumn).Style.Border.BottomBorderColor = BrandNavy;
            sheet.Row(5).Height = 6;
        }

        private static void HeadStyle(IXLRange range)
        {
            range.Style.Font.Bold = true;
            range.Style.Font.FontColor = Slate600;
            range.Style.Fill.BackgroundColor = Band;
            range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            range.Style.Border.BottomBorderColor = Rule;
            range.LastCell().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            if (range.ColumnCount() > 2)
                range.Cell(1, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            if (range.ColumnCount() > 3)
                range.Cell(1, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }

        /// <summary>A total, as a ledger foots it: a single rule over it and a double rule under it, in ink.</summary>
        private static void Footing(IXLRange range)
        {
            range.Style.Font.Bold = true;
            range.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            range.Style.Border.TopBorderColor = RuleStrong;
            range.Style.Border.BottomBorder = XLBorderStyleValues.Double;
            range.Style.Border.BottomBorderColor = RuleStrong;
        }

        private static void Frame(IXLRange range)
        {
            range.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
            range.Style.Border.InsideBorderColor = Hairline;
        }

        private static XLColor VerdictColor(string verdict) => verdict switch
        {
            InspectionRules.Tallied => Green,
            InspectionRules.Waiting => AmberInk,
            InspectionRules.Unpaid  => Slate500,
            _                       => Red,
        };

        private static void Amount(IXLCell cell, decimal? amount)
        {
            if (amount is decimal value)
                cell.Value = value;
            cell.Style.NumberFormat.Format = AmountFormat;
        }

        private static void Count(IXLCell cell, int count)
        {
            cell.Value = count;
            cell.Style.NumberFormat.Format = CountFormat;
        }

        private static void Time(IXLCell cell, DateTime? time)
        {
            if (time is not DateTime value)
                return;
            cell.Value = value;
            cell.Style.NumberFormat.Format = TimeFormat;
        }

        private static void Text(IXLCell cell, string? text)
        {
            if (!string.IsNullOrEmpty(text))
                cell.Value = text;
        }

        private static void PrintSetup(IXLWorksheet sheet, InspectionReport report, string sheetName, bool landscape, int firstHeadRow, int lastRow, int lastColumn)
        {
            var page = sheet.PageSetup;
            page.PaperSize = XLPaperSize.A4Paper;
            page.PageOrientation = landscape ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
            page.FitToPages(1, 0);
            page.CenterHorizontally = true;
            page.Margins.Left = 0.4;
            page.Margins.Right = 0.4;
            page.Margins.Top = 0.5;
            page.Margins.Bottom = 0.5;
            page.Margins.Header = 0.25;
            page.Margins.Footer = 0.25;
            page.PrintAreas.Add(1, 1, Math.Max(1, lastRow), lastColumn);
            if (firstHeadRow > 0)
                page.SetRowsToRepeatAtTop(firstHeadRow, firstHeadRow);

            // Two ends, no centre: a portrait page has no room for three, and the sheet's own tab names it.
            page.Footer.Left.AddText($"{InspectionReport.Title}, {report.Period}, {sheetName}");
            page.Footer.Right.AddText($"Printed {InspectionReport.Stamp(report.PrintedOn)}. Page ");
            page.Footer.Right.AddText(XLHFPredefinedText.PageNumber);
            page.Footer.Right.AddText(" of ");
            page.Footer.Right.AddText(XLHFPredefinedText.NumberOfPages);
        }
    }
}
