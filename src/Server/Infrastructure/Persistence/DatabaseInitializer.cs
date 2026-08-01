using System.Security.Cryptography;
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
        var configured = !string.IsNullOrWhiteSpace(options.AdminEmail) && !string.IsNullOrWhiteSpace(options.AdminPassword);

        // With nothing configured, first run must still produce an app somebody can sign into — a
        // zero-config start that boots to a locked door is indistinguishable from a broken install.
        // So an admin is generated, its password printed once, and MustChangePassword makes the
        // printed password unusable beyond the first sign-in.
        var email = configured ? options.AdminEmail! : GeneratedAdminEmail;
        var password = configured ? options.AdminPassword! : GeneratePassword();

        var admin = new AppUser
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Id,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = options.AdminDisplayName,
            Role = UserRole.Admin,
            Active = true,
            MustChangePassword = true,
            CreatedAt = clock.UtcNow,
        };

        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Could not create the bootstrap admin: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        if (configured)
        {
            logger.LogInformation("Seeded bootstrap admin '{Email}'. A password change is required on first login.", admin.Email);
            return;
        }

        // Warning, not Information: this is the only time the password is ever shown, and it must
        // survive an install whose minimum log level is Warning.
        logger.LogWarning(
            "No bootstrap credentials were configured, so a first-run admin was generated.\n" +
            "==========================================================================\n" +
            "  FIRST-RUN ADMIN - shown only this once\n" +
            "  Email:    {Email}\n" +
            "  Password: {Password}\n" +
            "  Sign in now; a password change is forced at first sign-in.\n" +
            "==========================================================================",
            admin.Email,
            password);
    }

    private const string GeneratedAdminEmail = "admin@everdue.local";

    /// <summary>
    /// 20 alphanumeric characters with the lookalikes (0/O, 1/l/I) removed — this password is read
    /// off a log line and typed once. One character of each class is forced so the Identity policy
    /// (length, digit, lower, upper) can never reject its own bootstrap.
    /// </summary>
    private static string GeneratePassword()
    {
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string digits = "23456789";
        const string all = lower + upper + digits;

        var chars = new char[20];
        chars[0] = Pick(lower);
        chars[1] = Pick(upper);
        chars[2] = Pick(digits);
        for (var i = 3; i < chars.Length; i++)
        {
            chars[i] = Pick(all);
        }

        // Fisher–Yates, so the forced classes do not always sit at the front.
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);

        static char Pick(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
    }
}
