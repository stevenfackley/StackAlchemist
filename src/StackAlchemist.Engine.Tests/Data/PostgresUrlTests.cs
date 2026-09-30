using FluentAssertions;
using Npgsql;
using StackAlchemist.Engine.Data;

namespace StackAlchemist.Engine.Tests.Data;

/// <summary>Unit tests for <see cref="PostgresUrl"/>.</summary>
public sealed class PostgresUrlTests
{
    [Fact]
    public void Converts_a_pooler_url_with_sslmode_require()
    {
        var cs = PostgresUrl.ToNpgsqlConnectionString(
            "postgres://stackalchemist.kenqz:p%40ss%3Aword@aws-1-us-east-1.pooler.supabase.com:6543/postgres?sslmode=require");

        var b = new NpgsqlConnectionStringBuilder(cs);
        b.Host.Should().Be("aws-1-us-east-1.pooler.supabase.com");
        b.Port.Should().Be(6543);
        b.Database.Should().Be("postgres");
        b.Username.Should().Be("stackalchemist.kenqz");
        b.Password.Should().Be("p@ss:word");            // percent-decoded
        b.SslMode.Should().Be(SslMode.Require);
        b.MaxAutoPrepare.Should().Be(0);                 // transaction pooler: no server-side prepare
        b.NoResetOnClose.Should().BeTrue();              // transaction pooler: no DISCARD ALL
    }

    [Fact]
    public void Defaults_port_and_leaves_ssl_prefer_when_unspecified()
    {
        var b = new NpgsqlConnectionStringBuilder(PostgresUrl.ToNpgsqlConnectionString("postgresql://u:p@localhost/db"));
        b.Port.Should().Be(5432);
        b.SslMode.Should().Be(SslMode.Prefer);
    }

    [Theory]
    [InlineData("verify-ca", SslMode.VerifyCA)]
    [InlineData("verify-full", SslMode.VerifyFull)]
    [InlineData("disable", SslMode.Disable)]
    public void Maps_sslmode_query_values(string sslmode, SslMode expected)
    {
        var b = new NpgsqlConnectionStringBuilder(
            PostgresUrl.ToNpgsqlConnectionString($"postgres://u:p@db.example.com:5432/app?sslmode={sslmode}"));
        b.SslMode.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Null_or_blank_maps_to_null(string? url) =>
        PostgresUrl.ToNpgsqlConnectionString(url).Should().BeNull();

    [Fact]
    public void Rejects_a_non_postgres_scheme() =>
        FluentActions.Invoking(() => PostgresUrl.ToNpgsqlConnectionString("https://example.com/db"))
            .Should().Throw<FormatException>();

    [Fact]
    public void Strips_brackets_from_a_literal_ipv6_host()
    {
        var b = new NpgsqlConnectionStringBuilder(PostgresUrl.ToNpgsqlConnectionString("postgres://u:p@[::1]:5432/db"));
        b.Host.Should().Be("::1");
    }

    [Fact]
    public void Error_messages_never_echo_the_url_or_password()
    {
        foreach (var bad in new[] { "https://u:hunter2@example.com/db", "u:hunter2@not a uri" })
        {
            FluentActions.Invoking(() => PostgresUrl.ToNpgsqlConnectionString(bad))
                .Should().Throw<FormatException>()
                .Which.Message.Should().NotContain("hunter2");
        }
    }
}
