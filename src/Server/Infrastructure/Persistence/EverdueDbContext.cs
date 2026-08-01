using System.Linq.Expressions;
using System.Reflection;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Infrastructure.Persistence;

/// <summary>
/// The one data layer. Abstract on purpose: the two concrete subclasses exist only so each
/// provider owns its own migrations folder while sharing a single model.
/// </summary>
public abstract class EverdueDbContext(DbContextOptions options, ITenantContext tenantContext)
    : IdentityUserContext<AppUser, Guid>(options), IEverdueDbContext
{
    private readonly ITenantContext _tenantContext = tenantContext;

    /// <summary>
    /// Referenced by the global query filters. EF parameterizes this property access, so a single
    /// compiled query serves every tenant — which is what makes the hosted version a no-op later.
    /// </summary>
    public Guid CurrentTenantId => _tenantContext.TenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Entity> Entities => Set<Entity>();

    public DbSet<Responsibility> Responsibilities => Set<Responsibility>();

    public DbSet<ResponsibilityEvent> ResponsibilityEvents => Set<ResponsibilityEvent>();

    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

    public DbSet<WorkItemEvent> WorkItemEvents => Set<WorkItemEvent>();

    public DbSet<Comment> Comments => Set<Comment>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

    public DbSet<DigestSubscription> DigestSubscriptions => Set<DigestSubscription>();

    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<SavedView> SavedViews => Set<SavedView>();

    public DbSet<ChecklistTemplateItem> ChecklistTemplateItems => Set<ChecklistTemplateItem>();

    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();

    public DbSet<EntityFieldDef> EntityFieldDefs => Set<EntityFieldDef>();

    /// <summary>
    /// Read through <c>IApiKeyStore</c> with the tenant filter ignored, because authentication happens
    /// before the tenant is known — the key is what resolves it. Same documented exemption as
    /// <see cref="ChannelSettings"/>, and nothing else reads this table without filtering.
    /// </summary>
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();

    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    /// <summary>
    /// The one table outside the tenant filter — a system-scope row must be readable while serving a
    /// tenant. Nothing but <c>IChannelSettingsResolver</c> reads it, and that always filters explicitly.
    /// </summary>
    public DbSet<ChannelSettings> ChannelSettings => Set<ChannelSettings>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTenant();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTenant();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Nothing that is inserted may forget its tenant. Callers therefore never set TenantId by hand,
    /// which is one fewer place a future query can leak across tenants.
    /// </summary>
    private void StampTenant()
    {
        var tenantId = CurrentTenantId;
        if (tenantId == Guid.Empty)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
            {
                entry.Entity.TenantId = tenantId;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EverdueDbContext).Assembly);
        ApplyTenantQueryFilters(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        ConfigureTimestamps(configurationBuilder);
    }

    /// <summary>
    /// How instants are stored. Overridden by the SQLite build — see <see cref="SqliteDateTimeOffsetConverter"/>
    /// for why the two providers cannot share one encoding.
    /// </summary>
    protected virtual void ConfigureTimestamps(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();

    /// <summary>
    /// One filter definition for every <see cref="ITenantOwned"/> table — isolation lives here and
    /// nowhere else, so no query can forget it.
    /// </summary>
    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        var apply = typeof(EverdueDbContext).GetMethod(nameof(ApplyTenantQueryFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.BaseType is null && typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                apply.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
            }
        }
    }

    private void ApplyTenantQueryFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned
    {
        Expression<Func<TEntity, bool>> filter = e => e.TenantId == CurrentTenantId;
        modelBuilder.Entity<TEntity>().HasQueryFilter(filter);
    }

}

/// <summary>SQLite build of the model. Owns <c>Infrastructure/Persistence/Migrations/Sqlite</c>.</summary>
public sealed class SqliteEverdueDbContext(DbContextOptions<SqliteEverdueDbContext> options, ITenantContext tenantContext)
    : EverdueDbContext(options, tenantContext)
{
    protected override void ConfigureTimestamps(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.Properties<DateTimeOffset>().HaveConversion<SqliteDateTimeOffsetConverter>();
}

/// <summary>PostgreSQL build of the model. Owns <c>Infrastructure/Persistence/Migrations/Postgres</c>.</summary>
public sealed class PostgresEverdueDbContext(DbContextOptions<PostgresEverdueDbContext> options, ITenantContext tenantContext)
    : EverdueDbContext(options, tenantContext);
