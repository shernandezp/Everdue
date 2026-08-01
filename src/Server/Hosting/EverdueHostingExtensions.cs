using Everdue.Server.Api;
using Everdue.Server.Engine;
using Everdue.Server.Infrastructure;
using Everdue.Server.Infrastructure.Options;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;

namespace Everdue.Server.Hosting;

public static class EverdueHostingExtensions
{
    /// <summary>
    /// Configuration sources, in increasing priority: appsettings, environment-specific appsettings,
    /// user secrets in Development, environment variables (with an <c>EVERDUE_</c> prefix as well as
    /// the plain names) and finally command-line arguments.
    /// </summary>
    public static IConfigurationBuilder AddEverdueConfiguration(this IConfigurationBuilder configuration, string[] args)
    {
        configuration.AddEnvironmentVariables("EVERDUE_");
        configuration.AddCommandLine(args);
        return configuration;
    }

    /// <summary>Windows service and systemd hosting come from the framework; no extra packaging code.</summary>
    public static IHostBuilder UseEverdueServiceHosting(this IHostBuilder host)
    {
        host.UseWindowsService(options => options.ServiceName = "Everdue");
        host.UseSystemd();
        return host;
    }

    /// <summary>
    /// Six background services in one process, one job each. They are kept apart rather than merged
    /// because they run on different cadences and fail in different ways — and because nothing may
    /// ever be able to take the occurrence engine down with it.
    /// </summary>
    public static IServiceCollection AddEverdueEngine(this IServiceCollection services)
    {
        services.AddScoped<OccurrenceEngine>();
        services.AddHostedService<OccurrenceEngineService>();
        services.AddHostedService<DigestService>();

        // Registered as singletons as well so the tests can drive one pass by hand instead of
        // racing a timer, exactly as they already do with the occurrence engine.
        services.AddSingleton<NotificationDispatcherService>();
        services.AddHostedService(sp => sp.GetRequiredService<NotificationDispatcherService>());

        services.AddSingleton<DueTodayReminderService>();
        services.AddHostedService(sp => sp.GetRequiredService<DueTodayReminderService>());

        services.AddSingleton<TelegramUpdatePollingService>();
        services.AddHostedService(sp => sp.GetRequiredService<TelegramUpdatePollingService>());

        services.AddSingleton<WebhookDispatcherService>();
        services.AddHostedService(sp => sp.GetRequiredService<WebhookDispatcherService>());

        return services;
    }

    /// <summary>Migrations, the default tenant and the bootstrap admin — all idempotent, all on every start.</summary>
    public static async Task InitializeEverdueDatabaseAsync(this WebApplication app)
    {
        // "Where is my data?" should never require reading source. It is also the fastest way to spot
        // that a rebuild or a config switch has pointed the app at a different (empty) directory.
        app.Logger.LogInformation(
            "Data directory: {DataDir}",
            InfrastructureServiceCollectionExtensions.ResolveDataDirectory(app.Configuration));

        // Settings that are valid but will not work where it matters. Said once, before the first
        // user meets the consequence.
        foreach (var warning in app.Services.GetServices<IStartupWarning>())
        {
            app.Logger.LogWarning("{Warning}", warning.Message);
        }

        using var scope = app.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync();
    }

    public static WebApplication UseEverduePipeline(this WebApplication app, IConfiguration configuration)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        app.UseEverdueSecurityHeaders(configuration.GetValue($"{SecurityOptions.Section}:RequireHttps", false));

        // Before the static files registered by MapEverdueClient, so the SPA bundle is compressed.
        app.UseResponseCompression();
        app.UseRateLimiter();

        app.UseAuthentication();
        app.UseAuthorization();

        // After authentication and routing, because both refusals need the principal *and* the endpoint's
        // metadata; before anything that could mutate data.
        app.UseMiddleware<ApiKeyGate>();
        app.UseMiddleware<PasswordChangeGate>();

        MapOpenApiDocument(app);

        return app;
    }

    /// <summary>
    /// The OpenAPI document is the client's type source (<c>npm run gen:api</c>), so it has to exist
    /// outside Development too — a contract that is only reachable in one environment is a contract
    /// that drifts. It is anonymous in Development and sign-in-only everywhere else, so a public
    /// instance does not hand out its API surface for free. The interactive UI stays dev-only.
    /// </summary>
    private static void MapOpenApiDocument(WebApplication app)
    {
        var document = app.MapOpenApi();

        if (app.Environment.IsDevelopment())
        {
            document.AllowAnonymous();
            app.MapScalarApiReference(options => options.WithTitle("Everdue API"));
        }
        else
        {
            document.RequireAuthorization();
        }
    }

    /// <summary>
    /// Serves the built SPA from wwwroot on the same port as the API — one process, one port, no
    /// reverse proxy required. When wwwroot is absent (a plain `dotnet run` during development) the
    /// API simply runs on its own and Vite serves the client.
    /// </summary>
    public static WebApplication MapEverdueClient(this WebApplication app)
    {
        var webRoot = app.Environment.WebRootPath;

        if (string.IsNullOrWhiteSpace(webRoot) || !Directory.Exists(webRoot) || !File.Exists(Path.Combine(webRoot, "index.html")))
        {
            app.Logger.LogInformation(
                "No built client found in wwwroot; serving the API only. Run the Vite dev server, or publish with -p:BuildClient=true.");
            return app;
        }

        var files = new PhysicalFileProvider(webRoot);

        var staticFiles = new StaticFileOptions
        {
            FileProvider = files,
            OnPrepareResponse = SecurityHeaders.ApplyStaticFileCaching,
        };

        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        app.UseStaticFiles(staticFiles);

        // Client-side routing: anything that is not an API route falls back to the SPA shell.
        app.MapFallbackToFile("index.html", staticFiles);

        return app;
    }
}
