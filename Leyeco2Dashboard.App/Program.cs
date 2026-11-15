using Leyeco2Dashboard.App.Authorization;
using Leyeco2Dashboard.App.Controllers;
using Leyeco2Dashboard.App.Infrastructure;
using Leyeco2Dashboard.App.Models;
using Leyeco2Dashboard.Authentication.Extensions;
using Leyeco2Dashboard.Authentication.Middleware;
using Leyeco2Dashboard.Data;
using Leyeco2Dashboard.Data.Entities.Database;
using Leyeco2Dashboard.Data.Interfaces;
using Leyeco2Dashboard.Data.Interfaces.Legacy;
using Leyeco2Dashboard.Data.Interfaces.Leyeco2OTF;
using Leyeco2Dashboard.Data.Repositories;
using Leyeco2Dashboard.Data.Repositories.Legacy;
using Leyeco2Dashboard.Data.Repositories.Leyeco2OTF;
using Leyeco2Dashboard.Services.Caching;
using Leyeco2Dashboard.Services.Configuration;
using Leyeco2Dashboard.Services.Enums;
using Leyeco2Dashboard.Services.Interfaces;
using Leyeco2Dashboard.Services.Services;
using Leyeco2Dashboard.Services.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using System.IO.Compression;
using System.Threading.RateLimiting;
using LegacyContext = Leyeco2Dashboard.Data.Entities.Legacy.DatabaseContext;
using OnlinePaymentsContext = Leyeco2Dashboard.Data.Entities.Leyeco2OTF.DatabaseContext;

namespace Leyeco2Dashboard.App
{
    public class Program
    {
        public static void Main(string[] args)
        {
            DotNetEnv.Env.Load();

            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog((ctx, lc) =>
            {
                lc.MinimumLevel.Information()
                  .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                  .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                  .MinimumLevel.Override("System", LogEventLevel.Warning)
                  .ReadFrom.Configuration(ctx.Configuration)
                  .Enrich.FromLogContext()
                  .Enrich.WithProperty("Application", "[L2TXM]");

                lc.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

                // Seq is where the cooperative reads its logs; a development machine without one still logs to the console.
                if (!string.IsNullOrWhiteSpace(ctx.Configuration["Seq:Url"]))
                    lc.WriteTo.Seq(ctx.Configuration["Seq:Url"]!);

#if !DEBUG
                lc.WriteTo.File(Path.Combine(AppContext.BaseDirectory, "logs", "leyeco2-dashboard-.log"),
                    rollingInterval: RollingInterval.Day, retainedFileCountLimit: 31);
#endif
            });

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("WebAppPolicy", policy =>
                {
                    policy.WithOrigins(
                            (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
                                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          // The console sends the session cookies with every call; valid because the origins are explicit.
                          .AllowCredentials();
                });
            });

            // Staff sign in with the Transaction Monitor's own accounts, kept on its Users screen. The auth layer
            // (Authentication/) mints an access token per session, validates it off this API's own HttpOnly cookies,
            // renews it silently, and makes a bare [Authorize] mean "holds a grant of the Transaction Monitor's block"
            // (PermissionEnum 1200-1299). Each screen's endpoints take its grant's policy (MonitorPolicies), which
            // All access meets too; a refusal names the grant it takes.
            builder.AddLeyeco2Authentication(AccessedSystemEnum.TransactionMonitor);
            MonitorAuthorization.Validate();
            builder.Services.AddAuthorization(MonitorAuthorization.AddSharedPolicies);
            builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, MissingGrantResultHandler>();

            // Sign-in sessions and lock-out; the defaults stand for whatever .env leaves out.
            var credentialOptions = new CredentialOptions();
            builder.Configuration.GetSection("Auth").Bind(credentialOptions);
            builder.Services.AddSingleton(credentialOptions);

            // The routes that take a password answer a caller address this often and no more. The per-account
            // lock is what stops a guesser; this keeps one address from trying account after account. Behind a
            // proxy every caller shares its address: size the limit, or add forwarded-headers handling, first.
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = async (context, cancellationToken) =>
                {
                    await context.HttpContext.Response.WriteAsJsonAsync(new ApiResponse
                    {
                        Code = StatusCodes.Status429TooManyRequests,
                        Message = "Too many attempts from this computer. Wait a minute, then try again."
                    }, cancellationToken);
                };
                options.AddPolicy(AuthController.PasswordRateLimit, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
            });

            builder.Services.AddDbContext<DatabaseContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DatabaseConnection")));

            // The dashboards read two databases this API does not own: the legacy collections ledger (the cashiering
            // system's) and the online payment facility's. Read-only, so they stay out of the unit of work: their
            // repositories open a context per query from these factories, and a report shared between viewers
            // (ReportCache) never holds a request's. The API starts without either connection string; the dashboard
            // that needs a missing one says so (ReportSourceOptions). A query is given a minute before it is given up.
            var legacyConnection = builder.Configuration.GetConnectionString("LegacyConnection");
            var onlinePaymentsConnection = builder.Configuration.GetConnectionString("OnlinePaymentsConnection");
            builder.Services.AddSingleton(new ReportSourceOptions
            {
                LegacyConfigured = !string.IsNullOrWhiteSpace(legacyConnection),
                OnlinePaymentsConfigured = !string.IsNullOrWhiteSpace(onlinePaymentsConnection),
            });
            builder.Services.AddDbContextFactory<LegacyContext>(options => UseReportSource(options, legacyConnection));
            builder.Services.AddDbContextFactory<OnlinePaymentsContext>(options => UseReportSource(options, onlinePaymentsConnection));
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<ReportCache>();

            // One scoped UnitOfWork behind both registrations: repositories take the concrete type, services the
            // interface, and a transaction only wraps the repositories' writes when they share the instance.
            builder.Services.AddScoped<UnitOfWork>();
            builder.Services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<UnitOfWork>());

            // Repositories
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IUserCredentialRepository, UserCredentialRepository>();
            builder.Services.AddScoped<IUserGrantRepository, UserGrantRepository>();
            builder.Services.AddScoped<IUserSessionRepository, UserSessionRepository>();
            builder.Services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
            builder.Services.AddScoped<ICollectionReportRepository, CollectionReportRepository>();
            builder.Services.AddScoped<IOnlinePaymentReportRepository, OnlinePaymentReportRepository>();
            builder.Services.AddScoped<IOnlinePaymentInspectionRepository, OnlinePaymentInspectionRepository>();
            builder.Services.AddScoped<ILedgerInspectionRepository, LedgerInspectionRepository>();

            // Services
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<ISignInService, SignInService>();
            builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
            builder.Services.AddScoped<ICollectionDashboardService, CollectionDashboardService>();
            builder.Services.AddScoped<IOnlinePaymentDashboardService, OnlinePaymentDashboardService>();
            builder.Services.AddScoped<ITransactionInspectionService, TransactionInspectionService>();

            // Validators (stateless, registered singleton)
            builder.Services.AddSingleton<UserValidator>();

            // Cross-cutting infrastructure (audit context, clock)
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddTransient<IClientContextAccessor, HttpClientContextAccessor>();
            builder.Services.AddSingleton<IDateTimeService, DateTimeService>();

            builder.Services.AddControllers();

            // The registers answer with repetitive JSON, which compresses well. Over HTTPS too: the bodies carry no
            // secret (the session lives in cookies, which are headers), so BREACH has nothing to probe for. Gzip
            // first: the serializer flushes every few kilobytes, which costs Brotli's fastest level most of its ratio.
            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<GzipCompressionProvider>();
                options.Providers.Add<BrotliCompressionProvider>();
            });
            builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
            builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

            var app = builder.Build();

            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Leyeco II - Transaction Monitor API {Version} starting at {Time}", MonitorVersion.Current, DateTime.Now);

            EnsureAdministrator(app, logger);

            app.UseHttpsRedirection();
            app.UseResponseCompression();

#if DEBUG
            // Debug builds serve the console (Leyeco2Dashboard.Client, which the csproj references in Debug only) from
            // this origin: launching the API opens the console too, and its SameSite=Strict session cookies are
            // first-party. _framework/* comes from the referenced project, the rest from its wwwroot, and every
            // non-API path the console routes falls back to its index.html (below).
            if (app.Environment.IsDevelopment())
            {
                app.UseWebAssemblyDebugging();
            }

            app.UseBlazorFrameworkFiles();

            // index.html and the plain-named assets it links carry no fingerprint, so a browser given only an ETag
            // would keep a stale copy across rebuilds. no-cache keeps the cache but revalidates.
            var consoleAssetOptions = new StaticFileOptions
            {
                OnPrepareResponse = context =>
                {
                    context.Context.Response.Headers.CacheControl = "no-cache";
                }
            };
            app.UseStaticFiles(consoleAssetOptions);
#endif

            app.UseRouting();

            // Before the auth pipeline, so its 401s and 403s carry the CORS headers too: a console served from
            // another origin reads them as an ended session or a lost grant, which a blocked response would hide.
            app.UseCors("WebAppPolicy");

            // Before UseAuthentication: a silently renewed token must be in place when JwtBearer reads it.
            app.UseMiddleware<AccessTokenRefreshMiddleware>();
            app.UseAuthentication();
            app.UseRateLimiter();
            app.UseAuthorization();

            app.MapControllers();

#if DEBUG
            // The fingerprinted names the console's import map points at exist only as static-asset endpoints until
            // a publish writes them out: UseStaticFiles alone would 404 them on a dev run.
            app.MapStaticAssets();

            // An unknown API route stays a 404 instead of falling through to the console.
            app.MapFallback("/api/{**slug}", () => Results.NotFound());
            app.MapFallbackToFile("index.html", consoleAssetOptions);
#endif

            app.Run();
        }

        // A dashboard's source: never tracked (nothing is written), and without a connection string until .env names
        // one, which the services check before any query.
        private static void UseReportSource(DbContextOptionsBuilder options, string? connectionString)
        {
            void Configure(Microsoft.EntityFrameworkCore.Infrastructure.SqlServerDbContextOptionsBuilder sql) => sql.CommandTimeout(60);

            if (string.IsNullOrWhiteSpace(connectionString))
                options.UseSqlServer(Configure);
            else
                options.UseSqlServer(connectionString, Configure);

            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }

        /// <summary>
        /// Someone has to be able to open the Users screen before anyone else can be given an account. While no
        /// active account can, the one .env names (Bootstrap__AdminUsername, Bootstrap__AdminPassword) is created
        /// with All access, to set a password of its own at its first sign-in. Once an administrator exists the
        /// two settings do nothing, and are best removed. A failure here is logged and the API still starts:
        /// nobody can sign in until it is put right, which the log says how to do.
        /// </summary>
        private static void EnsureAdministrator(WebApplication app, Microsoft.Extensions.Logging.ILogger logger)
        {
            try
            {
                using var scope = app.Services.CreateScope();
                var users = scope.ServiceProvider.GetRequiredService<IUserService>();

                var outcome = users.EnsureAdministratorAsync(
                    app.Configuration["Bootstrap:AdminUsername"],
                    app.Configuration["Bootstrap:AdminPassword"]).GetAwaiter().GetResult();

                switch (outcome)
                {
                    case AdministratorBootstrapEnum.Created:
                        logger.LogWarning(
                            "No account could manage users, so '{Username}' was given All access with the password in Bootstrap__AdminPassword. It must set a new one at its first sign-in. Remove both Bootstrap settings from .env afterwards.",
                            app.Configuration["Bootstrap:AdminUsername"]);
                        break;
                    case AdministratorBootstrapEnum.NotConfigured:
                        logger.LogError(
                            "No active account can manage users, so nobody can be given an account. Set Bootstrap__AdminUsername and Bootstrap__AdminPassword in .env and restart the API to create the first administrator.");
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "The first administrator could not be checked or created. If the database has no tables yet, run Leyeco2Dashboard.Data/Scripts/create-schema.sql on it; if Bootstrap__AdminPassword breaks the password rule, the message above says which.");
            }
        }
    }
}
