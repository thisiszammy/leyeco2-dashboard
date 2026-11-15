namespace Leyeco2Dashboard.Services.Exceptions
{
    /// <summary>A dashboard asked for a period it can't read (the dates reversed, or longer than the limit). The API answers 400 with its message.</summary>
    public class DashboardRequestException : Exception
    {
        public DashboardRequestException(string message) : base(message)
        {
        }
    }
}
