namespace Leyeco2Dashboard.Services.Reports
{
    /// <summary>
    /// What heads the Transaction Monitor's printouts, the PDFs and the workbooks alike: the cooperative's name and its
    /// seal. The seal is the console's own 192px cut (<c>Leyeco2Dashboard.Client/wwwroot/icon-192.png</c>), which this
    /// project embeds by link rather than by copy, so a new cut of the seal replaces it here too; at 62 KB it is sharp at
    /// the header's size in print and light enough for a workbook to carry.
    /// </summary>
    internal static class Letterhead
    {
        public const string Cooperative = "Leyte II Electric Cooperative, Inc.";

        public const string System = "Transaction Monitor";

        /// <summary>The seal's width over its height: a square, centred on the gear.</summary>
        public const float LogoAspect = 1f;

        private const string LogoResource = "Leyeco2Dashboard.Services.Logo.png";

        private static readonly Lazy<byte[]> LogoBytes = new(ReadLogo);

        /// <summary>The seal, a PNG on a transparent ground, read once for the process.</summary>
        public static byte[] Logo => LogoBytes.Value;

        private static byte[] ReadLogo()
        {
            using var stream = typeof(Letterhead).Assembly.GetManifestResourceStream(LogoResource)
                ?? throw new InvalidOperationException($"The embedded seal {LogoResource} is missing.");
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }
    }
}
