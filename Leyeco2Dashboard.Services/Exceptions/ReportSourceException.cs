namespace Leyeco2Dashboard.Services.Exceptions
{
    /// <summary>
    /// A database a dashboard reads (the collections ledger, the online payment facility) is not configured, out of
    /// reach, or too slow to answer. The API answers 503 with its message, which says which one and what to do.
    /// </summary>
    public class ReportSourceException : Exception
    {
        public ReportSourceException(string message, Exception? innerException = null) : base(message, innerException)
        {
        }
    }
}
