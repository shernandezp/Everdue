using Everdue.Server.Application.Abstractions;
using Everdue.Server.Domain;
using Everdue.Server.Engine;
using Everdue.Server.Infrastructure.Identity;
using Everdue.Server.Infrastructure.Options;
using Everdue.Server.Infrastructure.Persistence;
using Everdue.Server.Infrastructure.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Tests.Support;

/// <summary>
/// A real DbContext over a private in-memory SQLite database, built by the real migrations. Engine
/// behaviour is only meaningful against the real schema — in particular the unique index that makes
/// double ticks harmless.
/// </summary>
public sealed class EngineHarness : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly List<EverdueDbContext> _contexts = [];

    private EngineHarness(SqliteConnection connection, TenantContext tenantContext)
    {
        _connection = connection;
        TenantContext = tenantContext;
    }

    public TenantContext TenantContext { get; }

    public TestClock Clock { get; } = new();

    public Tenant Tenant { get; private set; } = null!;

    public AppUser Owner { get; private set; } = null!;

    public EverdueDbContext Db { get; private set; } = null!;

    public static async Task<EngineHarness> CreateAsync(string timeZoneId = "America/Bogota")
    {
        // A named shared-cache in-memory database so several connections can see the same schema —
        // that is what lets the double-tick test use two independent contexts.
        var connection = new SqliteConnection($"Data Source=file:{Guid.CreateVersion7():N}?mode=memory&cache=shared");
        await connection.OpenAsync();

        var harness = new EngineHarness(connection, new TenantContext());
        harness.Db = harness.NewContext();
        await harness.Db.Database.MigrateAsync();

        harness.Tenant = new Tenant
        {
            Id = Guid.CreateVersion7(),
            Name = "Test tenant",
            TimeZoneId = timeZoneId,
            DigestHourLocal = 7,
            DefaultLanguage = Languages.Spanish,
            Active = true,
        };

        harness.Db.Tenants.Add(harness.Tenant);
        await harness.Db.SaveChangesAsync();
        harness.TenantContext.Resolve(harness.Tenant.Id);

        harness.Owner = new AppUser
        {
            Id = Guid.CreateVersion7(),
            TenantId = harness.Tenant.Id,
            UserName = "owner@test.local",
            NormalizedUserName = "OWNER@TEST.LOCAL",
            Email = "owner@test.local",
            NormalizedEmail = "OWNER@TEST.LOCAL",
            DisplayName = "Owner",
            Role = UserRole.Member,
            Active = true,
            SecurityStamp = Guid.CreateVersion7().ToString(),
            CreatedAt = harness.Clock.UtcNow,
        };

        harness.Db.Users.Add(harness.Owner);
        await harness.Db.SaveChangesAsync();

        return harness;
    }

    public EverdueDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<SqliteEverdueDbContext>()
            .UseSqlite(_connection)
            .Options;

        var context = new SqliteEverdueDbContext(options, TenantContext);
        _contexts.Add(context);
        return context;
    }

    /// <summary>What the engine tried to announce during the last tick. The ledger assertions ignore it.</summary>
    public RecordingNotificationEnqueuer Notifications { get; } = new();

    /// <summary>
    /// What the engine tried to send to subscribers. A recorder rather than a no-op so the catch-up guards can be
    /// asserted on counts — the ledger keeps every miss, but only recent ones are announced.
    /// </summary>
    public RecordingWebhookPublisher Webhooks { get; } = new();

    public OccurrenceEngine EngineOn(
        EverdueDbContext context,
        EngineOptions? options = null,
        NotificationOptions? notificationOptions = null)
        => new(
            context,
            new StubTenantProvider(Tenant),
            Clock,
            Options.Create(options ?? new EngineOptions()),
            Notifications,
            Webhooks,
            Options.Create(notificationOptions ?? new NotificationOptions()),
            NullLogger<OccurrenceEngine>.Instance);

    public OccurrenceEngine Engine(EngineOptions? options = null) => EngineOn(Db, options);

    public Responsibility AddResponsibility(
        RecurrenceKind kind,
        DateOnly startDate,
        int? daysOfWeekMask = null,
        int? dayOfMonth = null,
        int? monthOfYear = null,
        Guid? entityId = null,
        string title = "Follow up")
    {
        var responsibility = new Responsibility
        {
            Id = Guid.CreateVersion7(),
            TenantId = Tenant.Id,
            Title = title,
            OwnerUserId = Owner.Id,
            EntityId = entityId,
            RecurrenceKind = kind,
            DaysOfWeekMask = daysOfWeekMask,
            DayOfMonth = dayOfMonth,
            MonthOfYear = monthOfYear,
            StartDate = startDate,
            Active = true,
        };

        Db.Responsibilities.Add(responsibility);
        Db.SaveChanges();
        return responsibility;
    }

    public Entity AddEntity(string name = "Acme", EntityType type = EntityType.Customer)
    {
        var entity = new Entity { Id = Guid.CreateVersion7(), TenantId = Tenant.Id, Name = name, Type = type, Active = true };
        Db.Entities.Add(entity);
        Db.SaveChanges();
        return entity;
    }

    public async Task<List<WorkItem>> OccurrencesAsync(Guid responsibilityId)
        => await Db.WorkItems.AsNoTracking()
            .Where(w => w.ResponsibilityId == responsibilityId)
            .OrderBy(w => w.PeriodStart)
            .ToListAsync();

    public TimeZoneInfo TimeZone => Tenant.ResolveTimeZone();

    public async ValueTask DisposeAsync()
    {
        foreach (var context in _contexts)
        {
            await context.DisposeAsync();
        }

        await _connection.DisposeAsync();
    }

    private sealed class StubTenantProvider(Tenant tenant) : ITenantProvider
    {
        public Task<Tenant> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(tenant);

        public Task<TimeZoneInfo> GetTimeZoneAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(tenant.ResolveTimeZone());
    }
}
