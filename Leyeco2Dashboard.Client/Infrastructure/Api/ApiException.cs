namespace Leyeco2Dashboard.Client.Infrastructure.Api
{
    /// <summary>
    /// Normalized failure raised by <see cref="ApiClient"/>. <see cref="Message"/> is always safe
    /// to show the user: for HTTP 400 it carries the server's validation reason verbatim, for
    /// everything else (unreachable host, timeout, 5xx, unreadable body) it carries generic copy
    /// that says what happened and what to do next.
    /// </summary>
    public class ApiException : Exception
    {
        public int? StatusCode { get; }

        /// <summary>
        /// True when the server rejected the request as invalid (HTTP 400) — surface the message
        /// inline next to the form that produced it rather than as a generic failure.
        /// </summary>
        public bool IsValidation => StatusCode == 400;

        public ApiException(string message, int? statusCode = null, Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
        }
    }
}
