namespace Leyeco2Dashboard.Services.Interfaces
{
    /// <summary>
    /// Single source of "now" for the Services layer. Services depend on this instead of calling
    /// <see cref="System.DateTime.Now"/> directly so the clock can be substituted in tests and so
    /// the local-time rule (never <c>DateTime.UtcNow</c>) is enforced in one place.
    /// </summary>
    public interface IDateTimeService
    {
        DateTime Now();
    }
}
