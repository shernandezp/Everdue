using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Attachments;
using Everdue.Server.Application.Checklists;
using Everdue.Server.Application.Entities;
using Everdue.Server.Application.Exports;
using Everdue.Server.Application.Imports;
using Everdue.Server.Application.Insights;
using Everdue.Server.Application.Notifications;
using Everdue.Server.Application.Webhooks;
using Everdue.Server.Infrastructure.ApiKeys;
using Everdue.Server.Infrastructure.Channels;
using Everdue.Server.Infrastructure.Demo;
using Everdue.Server.Infrastructure.Webhooks;
using Everdue.Server.Infrastructure.Email;
using Everdue.Server.Infrastructure.Files;
using Everdue.Server.Infrastructure.Identity;
using Everdue.Server.Infrastructure.Options;
using Everdue.Server.Infrastructure.Persistence;
using Everdue.Server.Infrastructure.Tenancy;
using Everdue.Server.Infrastructure.Time;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Everdue.Server.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Options, both EF providers, tenancy, identity, clock and e-mail. Program.cs calls this and nothing else.</summary>
    public static IServiceCollection AddEverdueInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEverdueOptions(configuration);
        services.AddEverduePersistence(configuration);
        services.AddEverdueIdentity();
        services.AddEverdueDataProtection(configuration);

        services.AddScoped<IEverdueDbContext>(sp => sp.GetRequiredService<EverdueDbContext>());
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IUserAdmin, UserAdmin>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ExternalLoginService>();
        services.AddSingleton<ITenantContext, TenantContext>();
        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<DemoDataSeeder>();
        services.AddScoped<TenantWipe>();
        services.AddScoped<IDemoMode, DemoModeService>();

        services.AddEverdueNotifications();
        services.AddEverdueChannels();
        services.AddEverdueApiKeys();
        services.AddEverdueWebhooks();
        services.AddSingleton<IFileStore, LocalDiskFileStore>();

        return services;
    }

    /// <summary>
    /// The store is the only place the API-key table is read outside the tenant filter, and it is scoped like
    /// the DbContext it reads through. The authentication handler itself is registered by the API layer, with
    /// the rest of the schemes.
    /// </summary>
    private static IServiceCollection AddEverdueApiKeys(this IServiceCollection services)
    {
        services.AddScoped<IApiKeyStore, ApiKeyStore>();
        return services;
    }

    /// <summary>
    /// The publisher is scoped so it shares the request's change tracker — that is what puts a delivery row in
    /// the same commit as the change it describes. The sender is a typed client: redirects off, cookies off,
    /// and a per-attempt timeout applied by the sender rather than by the client, so one slow receiver cannot
    /// consume the whole pass.
    /// </summary>
    private static IServiceCollection AddEverdueWebhooks(this IServiceCollection services)
    {
        services.AddSingleton<WebhookSecretProtector>();
        services.AddSingleton<WebhookUrlPolicy>();
        services.AddScoped<WebhookPublisher>();
        services.AddScoped<IWebhookPublisher>(sp => sp.GetRequiredService<WebhookPublisher>());
        services.AddScoped<IWebhookAdminSupport, WebhookAdminSupport>();

        services.AddHttpClient<WebhookSender>(WebhookSender.Configure)
            .ConfigurePrimaryHttpMessageHandler(WebhookSender.Handler);

        return services;
    }

    private static IServiceCollection AddEverdueNotifications(this IServiceCollection services)
    {
        services.AddScoped<INotificationRecipients, NotificationRecipients>();
        services.AddScoped<ITelegramLinkStore, TelegramLinkStore>();
        services.AddScoped<INotificationEnqueuer, NotificationEnqueuer>();
        return services;
    }

    /// <summary>
    /// One registration per channel. Adding SMS or Slack later is a line here and a class — no caller
    /// knows which channels exist, which is the point of the registry.
    /// </summary>
    private static IServiceCollection AddEverdueChannels(this IServiceCollection services)
    {
        services.AddSingleton<ChannelSecretProtector>();
        services.AddScoped<IChannelSettingsResolver, ChannelSettingsResolver>();
        services.AddScoped<IChannelRegistry, ChannelRegistry>();

        services.AddScoped<INotificationChannel, EmailChannel>();
        services.AddScoped<INotificationChannel, TelegramChannel>();
        services.AddScoped<INotificationChannel, WhatsAppChannel>();

        // Typed clients: pooled handlers, sane timeouts, and one place per provider that knows its
        // base address. A background service holding a raw HttpClient forever is how DNS goes stale.
        services.AddHttpClient<TelegramApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.telegram.org/");

            // Long polling holds the request open, so the timeout has to outlast the poll itself.
            client.Timeout = TimeSpan.FromSeconds(90);
        });

        services.AddHttpClient<WhatsAppCloudApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://graph.facebook.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }

    private static IServiceCollection AddEverdueOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.Section)).ValidateOnStart();
        services.AddOptions<EngineOptions>().Bind(configuration.GetSection(EngineOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<DigestOptions>().Bind(configuration.GetSection(DigestOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SmtpOptions>().Bind(configuration.GetSection(SmtpOptions.Section)).ValidateOnStart();
        services.AddOptions<BootstrapOptions>().Bind(configuration.GetSection(BootstrapOptions.Section)).ValidateOnStart();
        services.AddOptions<TenantOptions>().Bind(configuration.GetSection(TenantOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SecurityOptions>().Bind(configuration.GetSection(SecurityOptions.Section)).ValidateOnStart();
        services.AddOptions<NotificationOptions>().Bind(configuration.GetSection(NotificationOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ReminderOptions>().Bind(configuration.GetSection(ReminderOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<TelegramOptions>().Bind(configuration.GetSection(TelegramOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<AttachmentOptions>().Bind(configuration.GetSection(AttachmentOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<InsightsOptions>().Bind(configuration.GetSection(InsightsOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<AppOptions>().Bind(configuration.GetSection(AppOptions.Section)).ValidateOnStart();
        services.AddOptions<GoogleAuthOptions>().Bind(configuration.GetSection(GoogleAuthOptions.Section)).ValidateOnStart();

        // Checklist, entity-field, export, import and webhook collaborators. Each of these is enforced by a
        // handler, which is why the option classes live in Application; binding them to configuration is
        // still Infrastructure's job.
        services.AddOptions<ChecklistOptions>().Bind(configuration.GetSection(ChecklistOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<EntityFieldOptions>().Bind(configuration.GetSection(EntityFieldOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ExportOptions>().Bind(configuration.GetSection(ExportOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ImportOptions>().Bind(configuration.GetSection(ImportOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<WebhookOptions>().Bind(configuration.GetSection(WebhookOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<DemoOptions>().Bind(configuration.GetSection(DemoOptions.Section)).ValidateOnStart();

        return services;
    }

    private static IServiceCollection AddEverduePersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration.GetValue($"{DatabaseOptions.Section}:Provider", DatabaseProvider.Sqlite);
        var connectionString = ResolveConnectionString(configuration, provider);

        switch (provider)
        {
            case DatabaseProvider.Postgres:
                services.AddDbContext<EverdueDbContext, PostgresEverdueDbContext>(options =>
                {
                    options.UseNpgsql(connectionString);
                    ConfigureCommon(options);
                });
                break;

            case DatabaseProvider.Sqlite:
            default:
                services.AddDbContext<EverdueDbContext, SqliteEverdueDbContext>(options =>
                {
                    options.UseSqlite(connectionString);
                    ConfigureCommon(options);
                });
                break;
        }

        return services;

        static void ConfigureCommon(DbContextOptionsBuilder options) =>
            // Work items point at their owner with a required FK, and users are tenant-filtered.
            // The interaction is intentional here (a work item and its owner are always in the same
            // tenant), so the advisory warning is noise.
            options.ConfigureWarnings(w =>
                w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
    }

    private static IServiceCollection AddEverdueIdentity(this IServiceCollection services)
    {
        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 10;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<EverdueDbContext>()
            .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // How long a deactivated user or a demoted administrator can still act on an already-issued
        // cookie. The framework default is 30 minutes, which is a long time to keep letting someone
        // in after an admin has just removed their access.
        services.Configure<SecurityStampValidatorOptions>(options =>
            options.ValidationInterval = TimeSpan.FromMinutes(2));

        return services;
    }

    /// <summary>
    /// Auth cookies are encrypted with data-protection keys. Left unconfigured, ASP.NET Core looks
    /// for a user profile to store them in — and the container image runs as a system account with
    /// no home directory, so it silently falls back to an in-memory key ring. Every restart would
    /// then invalidate every cookie and log the whole team out.
    ///
    /// Keys live in the data directory instead: the same volume as the database, which is already
    /// the one thing a self-hoster has to back up. They are unencrypted at rest, exactly like the
    /// SQLite file beside them — protection is the volume's file permissions.
    /// </summary>
    private static IServiceCollection AddEverdueDataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var keys = new DirectoryInfo(Path.Combine(ResolveDataDirectory(configuration), "keys"));
        keys.Create();

        services
            .AddDataProtection()
            .PersistKeysToFileSystem(keys)
            // Pins the purpose string so cookies survive a rename of the entry assembly.
            .SetApplicationName("Everdue");

        return services;
    }

    /// <summary>
    /// Relative paths resolve against the binary, not the working directory: a Windows service or
    /// a systemd unit does not get to choose its cwd, and "where is my data" must have one answer.
    /// </summary>
    internal static string ResolveDataDirectory(IConfiguration configuration)
    {
        var configured = configuration.GetValue<string>("DataDir");

        var dataDir = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "data")
            : Path.GetFullPath(configured, AppContext.BaseDirectory);

        Directory.CreateDirectory(dataDir);
        return dataDir;
    }

    /// <summary>
    /// SQLite needs no configuration at all for the default install: the file lands under DataDir.
    /// </summary>
    internal static string ResolveConnectionString(IConfiguration configuration, DatabaseProvider provider)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        if (provider == DatabaseProvider.Postgres)
        {
            throw new InvalidOperationException(
                "Database:Provider is Postgres but ConnectionStrings:Default is empty.");
        }

        return $"Data Source={Path.Combine(ResolveDataDirectory(configuration), "everdue.db")}";
    }
}
