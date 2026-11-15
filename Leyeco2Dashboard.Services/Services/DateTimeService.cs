using Leyeco2Dashboard.Services.Interfaces;

namespace Leyeco2Dashboard.Services.Services
{
    /// <summary>
    /// Local-time clock. Registered as a singleton in the API composition root.
    /// </summary>
    public class DateTimeService : IDateTimeService
    {
        public DateTime Now() => DateTime.Now;
    }
}
