namespace Leyeco2Dashboard.Client.Infrastructure.Api
{
    /// <summary>
    /// Client-side mirror of the Transaction Monitor API's <c>ApiResponse</c> wrapper — every endpoint returns
    /// <c>{ code, message, data }</c>. <see cref="Data"/> is null on mutation endpoints.
    /// </summary>
    public class ApiEnvelope<T>
    {
        public int Code { get; set; }

        public string? Message { get; set; }

        public T? Data { get; set; }
    }
}
