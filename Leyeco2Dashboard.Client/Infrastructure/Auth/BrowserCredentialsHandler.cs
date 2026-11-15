using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Leyeco2Dashboard.Client.Infrastructure.Auth
{
    /// <summary>
    /// Opts every request into fetch's <c>credentials: include</c>. The session lives in HttpOnly
    /// cookies the Transaction Monitor API stamps on its own origin, which is not the one serving this client unless
    /// the API served it (<c>Api:BaseUrl</c>): under the browser's default credentials mode those cookies would be
    /// left off every API call, so a sign-in would succeed and each request after it would arrive
    /// anonymous. The API's CORS policy allows credentials for its listed origins only.
    /// </summary>
    public class BrowserCredentialsHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
