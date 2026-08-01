using Everdue.Server.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Everdue.Server.Infrastructure.Persistence;

/// <summary>
/// Design-time only. `dotnet ef` needs a provider to emit SQL for; it never opens these connections.
///
///   dotnet ef migrations add &lt;Name&gt; --context SqliteEverdueDbContext \
///       -o Infrastructure/Persistence/Migrations/Sqlite
/// </summary>
public sealed class SqliteDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SqliteEverdueDbContext>
{
    public SqliteEverdueDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SqliteEverdueDbContext>()
            .UseSqlite("Data Source=everdue-design-time.db")
            .Options;

        return new SqliteEverdueDbContext(options, new TenantContext());
    }
}

/// <summary>
///   dotnet ef migrations add &lt;Name&gt; --context PostgresEverdueDbContext \
///       -o Infrastructure/Persistence/Migrations/Postgres
/// </summary>
public sealed class PostgresDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PostgresEverdueDbContext>
{
    public PostgresEverdueDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("EVERDUE_DESIGN_TIME_POSTGRES")
                               ?? "Host=localhost;Port=5432;Database=everdue;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<PostgresEverdueDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new PostgresEverdueDbContext(options, new TenantContext());
    }
}
