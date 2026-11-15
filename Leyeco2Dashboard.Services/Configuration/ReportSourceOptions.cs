namespace Leyeco2Dashboard.Services.Configuration
{
    /// <summary>
    /// Which of the dashboards' databases the API host was given a connection string for
    /// (<c>ConnectionStrings__LegacyConnection</c>, <c>ConnectionStrings__OnlinePaymentsConnection</c>). The API starts
    /// without them; a dashboard whose database is missing says so instead of failing on its first query.
    /// </summary>
    public class ReportSourceOptions
    {
        public bool LegacyConfigured { get; init; }

        public bool OnlinePaymentsConfigured { get; init; }
    }
}
