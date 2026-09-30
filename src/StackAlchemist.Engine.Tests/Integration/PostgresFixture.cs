using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;
using Testcontainers.PostgreSql;

namespace StackAlchemist.Engine.Tests.Integration;

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}

/// <summary>
/// A real Postgres 17 with the SAME migrations the web app ships
/// (src/StackAlchemist.Web/drizzle/*.sql), so the Engine's SQL is tested against
/// the schema prod will actually have. Skips locally without Docker; on CI the
/// backend job runs on ubuntu-latest, which has Docker, so a skip there means the
/// gate stopped gating and the tests fail instead (IntegrationToolchain rule).
/// Migrations are applied once per container: CREATE TRIGGER in 0001 is not
/// idempotent.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Split exactly where drizzle's migrator splits: on the marker wherever it sits. drizzle-kit
    // emits most breakpoints on their own line, but glues the FK/index ones onto the statement's
    // line ("...;--> statement-breakpoint"), so a line-anchored pattern would merge those.
    private static readonly Regex StatementBreakpoint = new(@"-->\s*statement-breakpoint", RegexOptions.Compiled);

    // Built only once Docker is known to be there: Testcontainers' Build() already looks for a
    // Docker endpoint and throws without one, which would fail the collection instead of skipping.
    private PostgreSqlContainer? _pg;

    public NpgsqlDataSource DataSource { get; private set; } = null!;
    public bool Available { get; private set; }

    public async Task InitializeAsync()
    {
        if (!IntegrationToolchain.Available("docker", "info", "Docker (Testcontainers)", requiredOnCi: true))
            return;

        _pg = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await _pg.StartAsync();
        DataSource = new NpgsqlDataSourceBuilder(_pg.GetConnectionString()).Build();

        var root = RepoRoot();
        var files = Directory.GetFiles(Path.Combine(root, "src", "StackAlchemist.Web", "drizzle"), "*.sql").Order(StringComparer.Ordinal);
        await using var conn = await DataSource.OpenConnectionAsync();
        foreach (var file in files)
        {
            foreach (var stmt in StatementBreakpoint.Split(await File.ReadAllTextAsync(file)))
            {
                if (string.IsNullOrWhiteSpace(stmt)) continue;
                await using var cmd = new NpgsqlCommand(stmt, conn);
                await cmd.ExecuteNonQueryAsync();
            }
        }
        Available = true;
    }

    public async Task DisposeAsync()
    {
        if (DataSource is not null) await DataSource.DisposeAsync();
        if (_pg is not null) await _pg.DisposeAsync();
    }

    /// <summary>Walks up from the test binary until StackAlchemist.slnx is found.</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "StackAlchemist.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("StackAlchemist.slnx not found above the test binary.");
    }

    public async Task<Guid> SeedGenerationAsync(Guid? userId = null, string status = "pending", int tier = 1, int attempts = 0)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        if (userId is { } u)
        {
            await using var p = new NpgsqlCommand("insert into stackalchemist.profiles (id, email) values ($1, $2) on conflict (id) do nothing", conn)
            { Parameters = { new() { Value = u }, new() { Value = $"{u}@example.test" } } };
            await p.ExecuteNonQueryAsync();
        }
        await using var g = new NpgsqlCommand(
            "insert into stackalchemist.generations (user_id, mode, tier, status, attempt_count, project_type) values ($1, 'simple', $2, $3, $4, 'DotNetNextJs') returning id", conn)
        { Parameters = { new() { Value = (object?)userId ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Uuid }, new() { Value = tier }, new() { Value = status }, new() { Value = attempts } } };
        return (Guid)(await g.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// Moves a row's <c>updated_at</c> back by <paramref name="ago"/>. The <c>generations_updated_at</c>
    /// trigger rewrites <c>updated_at</c> on every UPDATE, so it is disabled around this one statement.
    /// All three statements share a transaction: <c>ALTER TABLE ... DISABLE TRIGGER</c> is transactional
    /// in Postgres and takes a SHARE ROW EXCLUSIVE lock, held until commit, which conflicts with the
    /// ROW EXCLUSIVE lock every UPDATE takes, so no other session writes the table while it is off.
    /// Backdate LAST: any later UPDATE to the row (by a test or the service) bumps it back to now().
    /// </summary>
    public async Task SetUpdatedAtAsync(Guid id, TimeSpan ago)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await using (var off = new NpgsqlCommand("alter table stackalchemist.generations disable trigger generations_updated_at", conn, tx))
            await off.ExecuteNonQueryAsync();
        await using (var set = new NpgsqlCommand("update stackalchemist.generations set updated_at = now() - $1::interval where id = $2", conn, tx)
                     { Parameters = { new() { Value = ago }, new() { Value = id } } })
            await set.ExecuteNonQueryAsync();
        await using (var on = new NpgsqlCommand("alter table stackalchemist.generations enable trigger generations_updated_at", conn, tx))
            await on.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }

    /// <summary>Runs a statement with positional (<c>$1</c>, <c>$2</c>, ...) arguments.</summary>
    public async Task ExecuteAsync(string sql, params object[] args)
    {
        await using var cmd = Command(sql, args);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>First column of the first row, for assertions; SQL NULL (or no row) reads as <c>default</c>.</summary>
    public async Task<T?> ReadAsync<T>(string sql, params object[] args)
    {
        await using var cmd = Command(sql, args);
        var value = await cmd.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)value;
    }

    private NpgsqlCommand Command(string sql, object[] args)
    {
        var cmd = DataSource.CreateCommand(sql);
        foreach (var arg in args)
            cmd.Parameters.Add(new NpgsqlParameter { Value = arg });
        return cmd;
    }
}
