using Leyeco2Dashboard.Client;
using Leyeco2Dashboard.Client.Components.Shared.Toasts;
using Leyeco2Dashboard.Client.Features.Authentication.Services;
using Leyeco2Dashboard.Client.Features.Dashboard.Services;
using Leyeco2Dashboard.Client.Features.OnlinePayments.Services;
using Leyeco2Dashboard.Client.Features.TransactionInspector.Services;
using Leyeco2Dashboard.Client.Features.Users.Services;
using Leyeco2Dashboard.Client.Infrastructure.Api;
using Leyeco2Dashboard.Client.Infrastructure.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The API is not necessarily the origin serving this app. Debug builds of the API serve it themselves, and then
// its own origin is the API; served from elsewhere (its own dev server, or a site of its own:
// monitor.leyeco2.example calling monitor-api.leyeco2.example) it takes the API's URL from Api:BaseUrl in
// wwwroot/appsettings*.json, and the API must list this app's origin in Cors:AllowedOrigins. Every request
// carries the API's session cookies, which a call to another origin would go without.
builder.Services.AddScoped(sp => new HttpClient(new BrowserCredentialsHandler { InnerHandler = new HttpClientHandler() })
{
    BaseAddress = ResolveApiBaseAddress(builder)
});
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<AuthApiClient>();
builder.Services.AddScoped<UsersApiClient>();
builder.Services.AddScoped<CollectionsApiClient>();
builder.Services.AddScoped<OnlinePaymentsApiClient>();
builder.Services.AddScoped<TransactionInspectorApiClient>();
builder.Services.AddScoped<ToastService>();

// Authentication: the session lives in the API's HttpOnly cookies, so the client's state comes from
// GET api/auth/session. The concrete provider is also resolvable directly so components can sign the
// user in/out. The policies are the API's (MonitorPolicies): each page takes its screen's grant.
builder.Services.AddAuthorizationCore(AuthorizationPolicies.Configure);
builder.Services.AddScoped<SessionAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<SessionAuthenticationStateProvider>());

await builder.Build().RunAsync();

// Api:BaseUrl may list the API's URLs semicolon-separated, like launchSettings' applicationUrl; the one whose
// scheme matches this app's own origin wins. The session cookies are SameSite=Strict and an http-to-https hop
// counts as cross-site, so a mixed pairing would sign in and then lose the cookies on every call after. No
// value means the API itself served this app.
static Uri ResolveApiBaseAddress(WebAssemblyHostBuilder builder)
{
    var hostBase = new Uri(builder.HostEnvironment.BaseAddress);

    var candidates = (builder.Configuration["Api:BaseUrl"] ?? string.Empty)
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(url => new Uri(url.EndsWith('/') ? url : url + "/", UriKind.Absolute))
        .ToArray();

    if (candidates.Length == 0)
        return hostBase;

    var api = candidates.FirstOrDefault(u => u.Scheme == hostBase.Scheme) ?? candidates[0];

    // The API listens on every address of its machine, so the console it serves may be opened under another
    // name (the machine's LAN address, from another PC): a loopback entry on this app's own scheme and port is
    // that same API, reached the way this app was.
    return api.IsLoopback && api.Scheme == hostBase.Scheme && api.Port == hostBase.Port ? hostBase : api;
}
