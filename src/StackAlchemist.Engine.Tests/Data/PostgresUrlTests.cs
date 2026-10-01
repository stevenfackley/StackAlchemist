using FluentAssertions;
using Npgsql;
using StackAlchemist.Engine.Data;

namespace StackAlchemist.Engine.Tests.Data;

/// <summary>Unit tests for <see cref="PostgresUrl"/>.</summary>
public sealed class PostgresUrlTests
{
    private static NpgsqlConnectionStringBuilder Convert(string url) =>
        new(PostgresUrl.ToNpgsqlConnectionString(url));

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
        b.GssEncryptionMode.Should().Be(GssEncryptionMode.Disable); // Npgsql 10 defaults to Prefer
    }

    [Fact]
    public void Pins_gss_encryption_off_even_without_query_parameters()
    {
        // Npgsql 10 made GSS session encryption Prefer by default; the Engine has no Kerberos and
        // talks to the Supavisor pooler, so it must keep the Npgsql 9 handshake (straight to TLS).
        new NpgsqlConnectionStringBuilder().GssEncryptionMode
            .Should().NotBe(GssEncryptionMode.Disable, "otherwise this pin is redundant and the test proves nothing");
        Convert("postgresql://u:p@localhost/db").GssEncryptionMode
            .Should().Be(GssEncryptionMode.Disable);
    }

    [Fact]
    public void Leaves_pool_size_to_the_registration()
    {
        var cs = PostgresUrl.ToNpgsqlConnectionString("postgres://u:p@localhost:5432/db")!;

        new NpgsqlConnectionStringBuilder(cs).MaxPoolSize
            .Should().Be(new NpgsqlConnectionStringBuilder().MaxPoolSize);
        cs.Should().NotContain("Pool Size");
    }

    [Fact]
    public void Defaults_port_and_leaves_ssl_prefer_when_unspecified()
    {
        var b = Convert("postgresql://u:p@localhost/db");
        b.Port.Should().Be(5432);
        b.SslMode.Should().Be(SslMode.Prefer);
    }

    [Theory]
    [InlineData("disable", SslMode.Disable)]
    [InlineData("allow", SslMode.Allow)]
    [InlineData("prefer", SslMode.Prefer)]
    [InlineData("require", SslMode.Require)]
    [InlineData("verify-ca", SslMode.VerifyCA)]
    [InlineData("verify-full", SslMode.VerifyFull)]
    public void Maps_every_libpq_sslmode(string sslmode, SslMode expected) =>
        Convert($"postgres://u:p@db.example.com:5432/app?sslmode={sslmode}").SslMode.Should().Be(expected);

    [Theory]
    [InlineData("?sslmode=Require", "Require")] // wrong casing
    [InlineData("?sslmode=", "")]
    [InlineData("?sslmode=verify_full", "verify_full")]
    public void Rejects_an_unrecognised_sslmode_and_names_it(string query, string shown)
    {
        var ex = FluentActions.Invoking(() => PostgresUrl.ToNpgsqlConnectionString($"postgres://u:p@h/db{query}"))
            .Should().Throw<FormatException>().Which;

        ex.Message.Should().Be($"DATABASE_URL has an unrecognised sslmode '{shown}'.");
    }

    [Theory]
    [InlineData("?sslmode=require&sslmode=disable")] // repeated key: joined with a comma
    [InlineData("?sslmode=require?password=hunter2")] // a second '?' glues the rest onto the value
    [InlineData("?sslmode=averyveryverylongmode")]    // past 16 characters
    public void Rejects_an_unquotable_sslmode_without_echoing_it(string query)
    {
        var ex = FluentActions.Invoking(() => PostgresUrl.ToNpgsqlConnectionString($"postgres://u:p@h/db{query}"))
            .Should().Throw<FormatException>().Which;

        ex.Message.Should().Be("DATABASE_URL has an unrecognised sslmode.");
        ex.Message.Should().NotContain("hunter2").And.NotContain("password");
    }

    [Fact]
    public void Rejects_a_pgbouncer_flag_glued_on_with_a_second_question_mark()
    {
        // "?pgbouncer=true?sslmode=require" is one parameter named pgbouncer, not two.
        var ex = FluentActions.Invoking(
                () => PostgresUrl.ToNpgsqlConnectionString("postgres://u:p@h/db?pgbouncer=true?sslmode=require"))
            .Should().Throw<FormatException>().Which;

        ex.Message.Should().Be("DATABASE_URL has an unsupported query parameter 'pgbouncer'.");
    }

    [Fact]
    public void Maps_the_allowlisted_query_parameters()
    {
        var b = Convert(
            "postgres://u:p@h:6543/db?application_name=stackalchemist-engine&connect_timeout=7" +
            "&options=-c%20search_path%3Dstackalchemist&sslrootcert=%2Fetc%2Fssl%2Fsupabase-ca.crt&sslmode=verify-full");

        b.ApplicationName.Should().Be("stackalchemist-engine");
        b.Timeout.Should().Be(7);
        b.Options.Should().Be("-c search_path=stackalchemist");
        b.RootCertificate.Should().Be("/etc/ssl/supabase-ca.crt");
        b.SslMode.Should().Be(SslMode.VerifyFull);
    }

    [Fact]
    public void Unsupported_query_parameter_errors_name_the_key_and_never_the_value()
    {
        var ex = FluentActions.Invoking(
                () => PostgresUrl.ToNpgsqlConnectionString("postgres://u:p@h/db?password=hunter2"))
            .Should().Throw<FormatException>().Which;

        ex.Message.Should().Be("DATABASE_URL has an unsupported query parameter 'password'.");
        ex.Message.Should().NotContain("hunter2");
    }

    [Theory]
    [InlineData("?SSLMODE=require")] // libpq keys are case-sensitive
    [InlineData("?target_session_attrs=any")]
    public void Rejects_other_query_parameters(string query) =>
        FluentActions.Invoking(() => PostgresUrl.ToNpgsqlConnectionString($"postgres://u:p@h/db{query}"))
            .Should().Throw<FormatException>().WithMessage("*unsupported query parameter*");

    [Fact]
    public void Rejects_a_repeated_allowlisted_parameter() =>
        FluentActions.Invoking(
                () => PostgresUrl.ToNpgsqlConnectionString("postgres://u:p@h/db?application_name=a&application_name=b"))
            .Should().Throw<FormatException>().WithMessage("*repeats query parameter 'application_name'*");

    [Fact]
    public void A_bare_query_token_is_rejected_without_being_echoed()
    {
        var ex = FluentActions.Invoking(() => PostgresUrl.ToNpgsqlConnectionString("postgres://u:p@h/db?hunter2"))
            .Should().Throw<FormatException>().Which;

        ex.Message.Should().NotContain("hunter2");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("")]
    [InlineData("99999")] // parses, then Npgsql rejects it as out of range
    public void Rejects_a_bad_connect_timeout(string value) =>
        FluentActions.Invoking(() => PostgresUrl.ToNpgsqlConnectionString($"postgres://u:p@h/db?connect_timeout={value}"))
            .Should().Throw<FormatException>();

    [Theory]
    [InlineData("postgres://u:p@localhost:0/db")]
    [InlineData("postgres://u:p@localhost:65535/db?connect_timeout=99999")]
    public void Out_of_range_components_surface_as_format_exception_without_an_inner_exception(string url)
    {
        var ex = FluentActions.Invoking(() => PostgresUrl.ToNpgsqlConnectionString(url))
            .Should().Throw<FormatException>().Which;

        ex.Message.Should().Be("DATABASE_URL has an out-of-range or invalid component.");
        ex.InnerException.Should().BeNull();
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
    public void A_schemeless_url_is_rejected_without_echoing_the_username_or_password()
    {
        // The username parses as the URI scheme here, so a message that quoted the scheme would leak it.
        var ex = FluentActions.Invoking(() => PostgresUrl.ToNpgsqlConnectionString("stackalchemist.kenqz:hunter2@host/db"))
            .Should().Throw<FormatException>().Which;

        ex.Message.Should().NotContain("stackalchemist.kenqz").And.NotContain("hunter2");
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

    [Fact]
    public void Strips_brackets_from_a_literal_ipv6_host() =>
        Convert("postgres://u:p@[::1]:5432/db").Host.Should().Be("::1");

    [Fact]
    public void Percent_decodes_the_database_name() =>
        Convert("postgres://u:p@h/my%20db").Database.Should().Be("my db");

    [Fact]
    public void An_empty_path_leaves_the_database_unset_so_npgsql_defaults_it_to_the_username() =>
        Convert("postgres://u:p@h:5432").Database.Should().BeNull();

    [Fact]
    public void A_url_without_userinfo_leaves_credentials_unset()
    {
        var b = Convert("postgres://localhost:5432/db");

        b.Host.Should().Be("localhost");
        b.Username.Should().BeNull();
        b.Password.Should().BeNull();
    }

    [Fact]
    public void A_username_without_a_password_leaves_the_password_unset()
    {
        var b = Convert("postgres://alice@localhost/db");

        b.Username.Should().Be("alice");
        b.Password.Should().BeNull();
    }
}
