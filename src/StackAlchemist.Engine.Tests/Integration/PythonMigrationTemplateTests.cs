using FluentAssertions;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// The FastAPI sets' migration path (StackAlchemist#491). The schema SQL used to sit in
/// <c>alembic/versions/001_initial_schema.sql</c>, which Alembic never runs (it only loads Python
/// revisions), and <c>alembic</c> itself could not start: <c>alembic.ini</c> had no logging
/// sections for <c>env.py</c>'s <c>fileConfig</c> and no <c>prepend_sys_path</c> to import
/// <c>app</c>. Tables came from <c>create_all</c> at startup instead, and nothing a customer
/// could migrate from. Each piece below was verified against a real Postgres with
/// <c>alembic upgrade head</c> (tables created, revision 0001 recorded, a re-run a no-op).
/// </summary>
public sealed class PythonMigrationTemplateTests
{
    private static string Backend(string set, params string[] parts) =>
        Path.Combine([V1TemplateHarness.ResolveTemplatesRoot(), set, .. parts]);

    public static TheoryData<string> Sets => new() { "V1-Python-React", "V2-Python-React" };

    [Theory]
    [MemberData(nameof(Sets))]
    public void InitialRevision_IsAPythonRevisionThatAppliesTheGeneratedSql(string set)
    {
        var revision = File.ReadAllText(Backend(set, "backend", "alembic", "versions", "0001_initial_schema.py"));

        revision.Should().Contain("down_revision = None", "it is the first revision");
        revision.Should().Contain("\"001_initial_schema.sql\"", "it applies the SQL generated for the entities");
        File.Exists(Backend(set, "backend", "alembic", "versions", "001_initial_schema.sql")).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(Sets))]
    public void Alembic_CanStart_AndTargetsTheAppsDatabase(string set)
    {
        var ini = File.ReadAllText(Backend(set, "backend", "alembic.ini"));
        ini.Should().Contain("prepend_sys_path = .", "env.py imports the app package");
        ini.Should().Contain("[loggers]", "env.py calls fileConfig on this file");

        File.ReadAllText(Backend(set, "backend", "alembic", "env.py"))
            .Should().Contain("settings.database_url", "migrations must reach the app's database, not alembic.ini's localhost");
    }

    [Theory]
    [MemberData(nameof(Sets))]
    public void MigrationsOwnTheSchema_AndRunBeforeTheServer(string set)
    {
        File.ReadAllText(Backend(set, "backend", "app", "main.py"))
            .Should().NotContain("create_all", "create_all would create tables the first migration then collides with");
        File.ReadAllText(Backend(set, "infra", "Dockerfile.backend"))
            .Should().Contain("alembic upgrade head &&");
        File.ReadAllText(Backend(set, "infra", "docker-compose.yml"))
            .Should().Contain("condition: service_healthy", "the migration needs Postgres accepting connections");
    }
}
