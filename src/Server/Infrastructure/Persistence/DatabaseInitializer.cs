using Everdue.Server.Application.Abstractions;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Demo;
using Everdue.Server.Infrastructure.Identity;
using Everdue.Server.Infrastructure.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Infrastructure.Persistence;

/// <summary>
/// First-run behaviour, in full: apply migrations, ensure the single configured tenant exists,
/// resolve it into <see cref="ITenantContext"/>, and seed the bootstrap admin if there are no users.
/// Everything here is idempotent — it runs on every start.
/// </summary>
public sealed class DatabaseInitializer(
    EverdueDbContext db,
    ITenantContext tenantContext,
    UserManager<AppUser> userManager,
    IClock clock,
    IOptions<DatabaseOptions> databaseOptions,
    IOptions<TenantOptions> tenantOptions,
    IOptions<BootstrapOptions> bootstrapOptions,
    DemoDataSeeder demo,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (databaseOptions.Value.Provider == DatabaseProvider.Sqlite)
        {
            await EnableSqliteWalAsync(cancellationToken);
        }

        if (databaseOptions.Value.MigrateOnStartup)
        {
            logger.LogInformation("Applying database migrations ({Provider})…", databaseOptions.Value.Provider);
            await db.Database.MigrateAsync(cancellationToken);
        }

        var tenant = await EnsureTenantAsync(cancellationToken);
        tenantContext.Resolve(tenant.Id);

        await EnsureBootstrapAdminAsync(tenant, cancellationToken);

        // Last, and only when explicitly asked for: it needs the tenant resolved and the admin in place, and it
        // refuses outright on a database that already holds data.
        await demo.SeedAsync(cancellationToken);
    }

    /// <summary>Resolves the tenant without touching the schema — used by tests and by the OpenAPI export path.</summary>
    public async Task ResolveTenantOnlyAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.AsNoTracking().OrderBy(t => t.Id).FirstOrDefaultAsync(cancellationToken);
        if (tenant is not null)
        {
            tenantContext.Resolve(tenant.Id);
        }
    }

    /// <summary>
    /// WAL is what makes SQLite fit the workload (readers never block the engine's writes). It is a
    /// persistent property of the file, but setting it on every start costs nothing and survives a
    /// database that was created elsewhere. PRAGMA cannot run inside a transaction, hence raw ADO.
    /// </summary>
    private async Task EnableSqliteWalAsync(CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        var mode = await command.ExecuteScalarAsync(cancellationToken);
        logger.LogInformation("SQLite journal mode: {Mode}.", mode);
    }

    private async Task<Tenant> EnsureTenantAsync(CancellationToken cancellationToken)
    {
        var existing = await db.Tenants.OrderBy(t => t.Id).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var options = tenantOptions.Value;
        if (!TimeZoneLookup.IsKnown(options.TimeZoneId))
        {
            throw new InvalidOperationException(
                $"Tenant:TimeZoneId '{options.TimeZoneId}' is not a time zone this machine knows. Use an IANA id such as 'America/Bogota'.");
        }

        var tenant = new Tenant
        {
            Id = Guid.CreateVersion7(),
            Name = options.Name,
            TimeZoneId = options.TimeZoneId,
            DigestHourLocal = options.DigestHourLocal,
            DefaultLanguage = Languages.Normalize(options.DefaultLanguage),
            Active = true,
        };

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded default tenant '{Name}' ({Id}) in {TimeZone}.", tenant.Name, tenant.Id, tenant.TimeZoneId);
        return tenant;
    }

    private async Task EnsureBootstrapAdminAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var options = bootstrapOptions.Value;
        if (string.IsNullOrWhiteSpace(options.AdminEmail) || string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            logger.LogWarning(
                "No users exist and Bootstrap:AdminEmail / Bootstrap:AdminPassword are not configured. " +
                "Nobody can sign in until they are set and the app is restarted.");
            return;
        }

        var admin = new AppUser
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Id,
            UserName = options.AdminEmail,
            Email = options.AdminEmail,
            EmailConfirmed = true,
            DisplayName = options.AdminDisplayName,
            Role = UserRole.Admin,
            Active = true,
            MustChangePassword = true,
            CreatedAt = clock.UtcNow,
        };

        var result = await userManager.CreateAsync(admin, options.AdminPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Could not create the bootstrap admin: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        logger.LogInformation("Seeded bootstrap admin '{Email}'. A password change is required on first login.", admin.Email);
    }
}
