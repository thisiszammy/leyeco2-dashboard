using Leyeco2Dashboard.Services.Reports;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Leyeco2Dashboard.Services.Pdf
{
    /// <summary>
    /// What the Transaction Monitor's PDFs share, after the HRIS's printouts (its <c>PlantillaPdfLayout</c>): IBM Plex Sans
    /// Condensed throughout, the console's face for figures and headings, so dense registers fit a page; ink and rules
    /// that hold up on a black-and-white office printer, every status in words so none rests on colour; the letterhead
    /// (the seal beside the cooperative's name over the document's title and period, what it covers on the right, over
    /// a navy rule); and the footer (the document, when it was printed, the page of how many).
    /// </summary>
    internal static class PrintoutLayout
    {
        public const string FontFamily = "IBM Plex Sans Condensed";

        /// <summary>The seal's height in the header: as tall as the three lines of text beside it.</summary>
        private const float LogoHeight = 42;

        private static readonly string[] FontWeights = ["Regular", "Medium", "SemiBold", "Bold"];

        // Brand and neutral tokens from the console's design system (Leyeco2Dashboard.Client/wwwroot/css/app.css).
        public static readonly Color BrandNavy = Color.FromHex("#1E3D6E");
        public static readonly Color Ink = Color.FromHex("#2A3439");
        public static readonly Color Slate600 = Color.FromHex("#566166");
        public static readonly Color Slate500 = Color.FromHex("#64748B");
        public static readonly Color Slate400 = Color.FromHex("#94A3B8");
        public static readonly Color Red = Color.FromHex("#B22B1D");
        public static readonly Color AmberInk = Color.FromHex("#92400E");
        public static readonly Color Green = Color.FromHex("#15803D");
        public static readonly Color GoldDark = Color.FromHex("#7D5A14");

        /// <summary>The grid's hairlines: light on paper, still there in grayscale.</summary>
        public static readonly Color Hairline = Color.FromHex("#D9E4EA");

        /// <summary>The rules that frame a table and foot a total.</summary>
        public static readonly Color Rule = Color.FromHex("#8D9AA2");

        public static readonly Color RuleStrong = Color.FromHex("#4B565C");

        /// <summary>The column heads' ground (slate-100), a light gray on paper.</summary>
        public static readonly Color Band = Color.FromHex("#EEF2F5");

        private static readonly Lazy<bool> Fonts = new(RegisterFonts);

        static PrintoutLayout()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        /// <summary>Registers the embedded Plex faces with QuestPDF, once for the process.</summary>
        public static void EnsureFonts() => _ = Fonts.Value;

        private static bool RegisterFonts()
        {
            var assembly = typeof(PrintoutLayout).Assembly;
            foreach (var weight in FontWeights)
            {
                var name = $"Leyeco2Dashboard.Services.Fonts.IBMPlexSansCondensed-{weight}.ttf";
                using var stream = assembly.GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException($"The embedded font {name} is missing.");
                FontManager.RegisterFont(stream);
            }

            return true;
        }

        /// <summary>The letterhead's seal for one document: made once and shared by every page's header, so the PDF carries it once.</summary>
        public static Image Logo() => Image.FromBinaryData(Letterhead.Logo);

        public static void Header(IContainer container, Image logo, string title, string subtitle, string rightTop, string rightBottom)
        {
            container.BorderBottom(1.5f).BorderColor(BrandNavy).PaddingBottom(6).Row(row =>
            {
                row.Spacing(10);
                row.ConstantItem(LogoHeight * Letterhead.LogoAspect).Height(LogoHeight).Image(logo).FitArea();

                row.RelativeItem().AlignMiddle().Column(col =>
                {
                    col.Item().Text(Letterhead.Cooperative).FontSize(7.5f).Medium().FontColor(BrandNavy);
                    col.Item().PaddingTop(1).Text(title).FontSize(17).SemiBold().FontColor(Ink).LineHeight(1.05f);
                    col.Item().PaddingTop(1).Text(subtitle).FontSize(9).Medium().FontColor(Slate600);
                });

                row.ConstantItem(300).AlignRight().AlignBottom().Column(col =>
                {
                    col.Item().AlignRight().Text(rightTop).FontSize(9).SemiBold().FontColor(Ink);
                    col.Item().AlignRight().Text(rightBottom).FontSize(7.5f).FontColor(Slate500);
                });
            });
        }

        public static void Footer(IContainer container, string document, DateTime printedOn)
        {
            container.PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Text(document).FontSize(7).FontColor(Slate500);
                row.RelativeItem().AlignCenter().Text($"Printed {InspectionReport.Stamp(printedOn)}").FontSize(7).FontColor(Slate500);
                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(7).FontColor(Slate500));
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }
    }
}
