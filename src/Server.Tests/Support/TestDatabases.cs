using Npgsql;
using Testcontainers.PostgreSql;

namespace Everdue.Server.Tests.Support;

public enum TestProvider
{
    Sqlite = 0,
    Postgres = 1,
}

public sealed record TestDatabase(TestProvider Provider, string ConnectionString, Func<ValueTask> Cleanup);

/// <summary>
/// The dual-provider discipline made executable: the same tests run on SQLite and PostgreSQL, so a
/// provider-specific query (or a DateTimeOffset that only sorts on one of them) fails immediately
/// rather than at a self-hoster's site.
/// </summary>
public static class TestDatabases
{
    private static readonly SemaphoreSlim ContainerGate = new(1, 1);
    private static PostgreSqlContainer? _container;
    private static string? _postgresUnavailableReason;

    /// <summary>Every provider the suite is expected to run on. Used as xUnit theory data.</summary>
    public static TheoryData<TestProvider> All => new() { TestProvider.Sqlite, TestProvider.Postgres };

    public static async Task<TestDatabase> CreateAsync(TestProvider provider)
        => provider switch
        {
            TestProvider.Postgres => await CreatePostgresAsync(),
            _ => CreateSqlite(),
        };

    private static TestDatabase CreateSqlite()
    {
        var path = Path.Combine(Path.GetTempPath(), "everdue-tests", $"{Guid.CreateVersion7():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        return new TestDatabase(TestProvider.Sqlite, $"Data Source={path}", () =>
        {
            foreach (var file in new[] { path, path + "-wal", path + "-shm" })
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                    // A WAL handle can outlive the test host briefly; the temp directory is disposable anyway.
                }
            }

            return ValueTask.CompletedTask;
        });
    }

    private static async Task<TestDatabase> CreatePostgresAsync()
    {
        var container = await EnsureContainerAsync();

        var databaseName = $"everdue_{Guid.CreateVersion7():N}";
        var admin = new NpgsqlConnectionStringBuilder(container.GetConnectionString());

        await using (var connection = new NpgsqlConnection(admin.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await command.ExecuteNonQueryAsync();
        }

        var target = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = databaseName };

        return new TestDatabase(TestProvider.Postgres, target.ConnectionString, async () =>
        {
            NpgsqlConnection.ClearAllPools();

            await using var connection = new NpgsqlConnection(admin.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
            await command.ExecuteNonQueryAsync();
        });
    }

    /// <summary>
    /// Started once for the whole assembly. When Docker is not available the suite says so and skips
    /// the PostgreSQL half rather than failing — a developer without Docker should still be able to
    /// run the tests, while CI (which always has it) runs the full matrix.
    /// </summary>
    private static async Task<PostgreSqlContainer> EnsureContainerAsync()
    {
        // CI runs one leg with this set (proving SQLite stands alone on a machine without Docker)
        // and one without it (the real dual-provider matrix).
        if (Environment.GetEnvironmentVariable("EVERDUE_TESTS_SKIP_POSTGRES") is "1" or "true")
        {
            Assert.Skip("PostgreSQL tests are disabled by EVERDUE_TESTS_SKIP_POSTGRES.");
        }

        if (_postgresUnavailableReason is not null)
        {
            Assert.Skip(_postgresUnavailableReason);
        }

        if (_container is not null)
        {
            return _container;
        }

        await ContainerGate.WaitAsync();

        try
        {
            if (_postgresUnavailableReason is not null)
            {
                Assert.Skip(_postgresUnavailableReason);
            }

            if (_container is null)
            {
                try
                {
                    // Build() validates the Docker endpoint, so both calls have to be inside the guard.
                    var container = new PostgreSqlBuilder("postgres:18-alpine")
                        .WithDatabase("everdue_template")
                        .Build();

                    await container.StartAsync();
                    _container = container;
                }
                catch (Exception e)
                {
                    _postgresUnavailableReason =
                        $"PostgreSQL tests need Docker, which is not available here ({e.GetType().Name}: {e.Message.Split('\n')[0]}).";
                    Assert.Skip(_postgresUnavailableReason);
                }
            }
        }
        finally
        {
            ContainerGate.Release();
        }

        return _container!;
    }
}
