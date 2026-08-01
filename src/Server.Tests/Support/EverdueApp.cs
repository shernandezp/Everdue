using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Domain;
using Everdue.Server.Engine;
using Everdue.Server.Infrastructure.Identity;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Everdue.Server.Tests.Support;

/// <summary>
/// The real host — real pipeline, real migrations, real database — with two things swapped: the
/// clock, and the background services, which the tests drive by hand so nothing is timing-dependent.
/// </summary>
public sealed class EverdueApp : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@everdue.test";
    public const string AdminPassword = "Everdue2026Admin!";
    public const string MemberEmail = "member@everdue.test";
    public const string MemberPassword = "Everdue2026Member!";

    private TestDatabase _database = null!;
    private IReadOnlyDictionary<string, string> _overrides = new Dictionary<string, string>();
    private TestChannel? _channel;
    private Action<IServiceCollection>? _services;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public TestClock Clock { get; } = new(DateTimeOffset.Parse("2026-07-28T15:00:00Z"));

    public TestProvider Provider => _database.Provider;

    /// <summary>The stand-in provider, when one was supplied. Outbox tests assert against what it saw.</summary>
    public TestChannel Channel => _channel ?? throw new InvalidOperationException("This app was started without a test channel.");

    public static async Task<EverdueApp> StartAsync(
        TestProvider provider,
        IReadOnlyDictionary<string, string>? settings = null,
        TestChannel? channel = null)
    {
        var app = new EverdueApp
        {
            _database = await TestDatabases.CreateAsync(provider),
            _overrides = settings ?? new Dictionary<string, string>(),
            _channel = channel,
        };

        // Forces the host to build (and therefore migrate and seed) before the first request.
        _ = app.Services;

        await app.SeedUsersAsync();
        return app;
    }

    /// <summary>
    /// For the tests that have to stand in for an outbound provider: the same real host, with one
    /// registration swapped.
    /// </summary>
    public static async Task<EverdueApp> StartWithServicesAsync(
        TestProvider provider,
        Action<IServiceCollection> configure,
        IReadOnlyDictionary<string, string>? settings = null)
    {
        var app = new EverdueApp
        {
            _database = await TestDatabases.CreateAsync(provider),
            _overrides = settings ?? new Dictionary<string, string>(),
            _services = configure,
        };

        _ = app.Services;

        await app.SeedUsersAsync();
        return app;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting, not ConfigureAppConfiguration: WebApplicationBuilder reads Configuration while
        // registering services, which happens before any IHostBuilder configuration callback runs.
        // Settings applied the other way arrive too late and the app silently falls back to the
        // default SQLite file — every test class would then share one database.
        foreach (var (key, value) in new Dictionary<string, string>
                 {
                     ["Database:Provider"] = _database.Provider.ToString(),
                     ["ConnectionStrings:Default"] = _database.ConnectionString,
                     ["Tenant:Name"] = "Everdue tests",
                     ["Tenant:TimeZoneId"] = "America/Bogota",
                     ["Tenant:DefaultLanguage"] = Languages.Spanish,
                     ["Bootstrap:AdminEmail"] = AdminEmail,
                     ["Bootstrap:AdminPassword"] = AdminPassword,

                     // The tests tick the engine themselves; a timer racing the assertions would make
                     // every occurrence count non-deterministic.
                     ["Engine:Enabled"] = "false",
                     ["Digest:Enabled"] = "false",
                 })
        {
            builder.UseSetting(key, value);
        }

        // Per-test overrides win, so a test can dial a setting down to something it can assert on.
        foreach (var (key, value) in _overrides)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);

            if (_channel is not null)
            {
                // The registry keys implementations by channel, so the real one has to go rather
                // than sit alongside a second registration for the same enum value.
                services.RemoveAll<INotificationChannel>();
                services.AddSingleton<INotificationChannel>(_channel);
            }

            _services?.Invoke(services);
        });
    }

    public HttpClient NewClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public async Task<HttpClient> SignInAsync(string email, string password)
    {
        var client = NewClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, Json);
        await response.ShouldBeSuccessAsync();
        return client;
    }

    public Task<HttpClient> SignInAsAdminAsync() => SignInAsync(AdminEmail, AdminPassword);

    public Task<HttpClient> SignInAsMemberAsync() => SignInAsync(MemberEmail, MemberPassword);

    public async Task<T> ScopedAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        using var scope = Services.CreateScope();
        return await work(scope.ServiceProvider);
    }

    public Task ScopedAsync(Func<IServiceProvider, Task> work) => ScopedAsync<object?>(async services =>
    {
        await work(services);
        return null;
    });

    /// <summary>Runs one engine tick against the current <see cref="Clock"/> value.</summary>
    public Task TickEngineAsync() => ScopedAsync(services => services.GetRequiredService<OccurrenceEngine>().TickAsync());

    /// <summary>One pass of the outbox. Driven by hand so nothing in the suite depends on a timer.</summary>
    public Task<int> DispatchNotificationsAsync()
        => Services.GetRequiredService<NotificationDispatcherService>().RunOnceAsync(CancellationToken.None);

    public Task<int> RunRemindersAsync()
        => Services.GetRequiredService<DueTodayReminderService>().RunOnceAsync(CancellationToken.None);

    /// <summary>Everything the given user has been told, oldest first.</summary>
    public Task<IReadOnlyList<Notification>> NotificationsForAsync(Guid userId) => ScopedAsync(async services =>
    {
        var db = services.GetRequiredService<EverdueDbContext>();

        IReadOnlyList<Notification> rows = await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync();

        return rows;
    });

    public Task<IReadOnlyList<NotificationDelivery>> DeliveriesAsync() => ScopedAsync(async services =>
    {
        var db = services.GetRequiredService<EverdueDbContext>();

        IReadOnlyList<NotificationDelivery> rows = await db.NotificationDeliveries.AsNoTracking()
            .Include(d => d.Notification)
            .OrderBy(d => d.NextAttemptAt)
            .ToListAsync();

        return rows;
    });

    /// <summary>
    /// Puts a user on a channel with an address to match, the way linking Telegram or an
    /// administrator entering a phone number would.
    /// </summary>
    public Task ReachableOnAsync(Guid userId, NotificationChannel channel) => ScopedAsync(async services =>
    {
        var db = services.GetRequiredService<EverdueDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == userId);

        switch (channel)
        {
            case NotificationChannel.Telegram:
                user.TelegramChatId = 987654;
                break;

            case NotificationChannel.WhatsApp:
                user.WhatsAppPhoneE164 = "+573001112233";
                break;
        }

        var preferences = NotificationPreferences.Parse(user.NotificationPreferencesJson);
        preferences.Channel = channel;
        user.NotificationPreferencesJson = preferences.ToJson();

        await db.SaveChangesAsync();
    });

    /// <summary>Writes a tenant-scope channel configuration, exactly as the settings screen does.</summary>
    public Task ConfigureChannelAsync(NotificationChannel channel, string configJson) => ScopedAsync(async services =>
    {
        var resolver = services.GetRequiredService<IChannelSettingsResolver>();
        var tenantId = services.GetRequiredService<ITenantContext>().TenantId;
        await resolver.SaveAsync(tenantId, channel, configJson, active: true);
    });

    public Task<Guid> TenantIdAsync() => ScopedAsync(async services =>
    {
        var tenantContext = services.GetRequiredService<ITenantContext>();
        return await Task.FromResult(tenantContext.TenantId);
    });

    public Task<Guid> UserIdAsync(string email) => ScopedAsync(async services =>
    {
        var db = services.GetRequiredService<EverdueDbContext>();
        return await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
    });

    /// <summary>
    /// Clears the bootstrap admin's forced-password-change flag and adds a member account.
    /// The forced change itself is covered by its own test; every other test starts past it.
    /// </summary>
    private async Task SeedUsersAsync()
    {
        await ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            var users = services.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
            var tenantContext = services.GetRequiredService<ITenantContext>();

            var admin = await db.Users.SingleAsync(u => u.Email == AdminEmail);
            admin.MustChangePassword = false;
            await db.SaveChangesAsync();

            if (!await db.Users.AnyAsync(u => u.Email == MemberEmail))
            {
                var member = new AppUser
                {
                    Id = Guid.CreateVersion7(),
                    TenantId = tenantContext.TenantId,
                    UserName = MemberEmail,
                    Email = MemberEmail,
                    EmailConfirmed = true,
                    DisplayName = "Member",
                    Role = UserRole.Member,
                    Active = true,
                    MustChangePassword = false,
                    CreatedAt = Clock.UtcNow,
                };

                var result = await users.CreateAsync(member, MemberPassword);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
                }
            }
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.Cleanup();
    }
}
