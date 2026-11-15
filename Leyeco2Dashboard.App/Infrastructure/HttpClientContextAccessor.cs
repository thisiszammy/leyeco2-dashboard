using Leyeco2Dashboard.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Leyeco2Dashboard.App.Infrastructure
{
    /// <summary>
    /// <see cref="IClientContextAccessor"/> backed by the current <see cref="HttpContext"/>. Lives in
    /// the API host because <see cref="IHttpContextAccessor"/> is an ASP.NET Core type and the Services
    /// classlib does not reference the web framework. Registered transient in <c>Program.cs</c>
    /// alongside <c>AddHttpContextAccessor()</c>.
    /// </summary>
    public class HttpClientContextAccessor : IClientContextAccessor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpClientContextAccessor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string? IpAddress
            => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

        public string? UserAgent
            => _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString();
    }
}
