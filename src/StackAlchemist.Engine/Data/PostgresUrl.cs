using System.Web;
using Npgsql;

namespace StackAlchemist.Engine.Data;

/// <summary>
/// libpq URI (what qavren-db's provisioner prints and what DATABASE_URL carries)
/// to an Npgsql connection string. Npgsql does not parse URIs itself. The pooler
/// options are fixed here because DATABASE_URL always points at the Supavisor
/// transaction pooler in prod: no server-side prepared statements, no session
/// reset on close (gavel-suite ADR-0017).
/// </summary>
public static class PostgresUrl
{
    /// <summary>
    /// Converts a <c>postgres://</c> / <c>postgresql://</c> URI to an Npgsql connection string.
    /// Returns <c>null</c> for a null or blank value (DATABASE_URL unset).
    /// </summary>
    /// <exception cref="FormatException">
    /// The value is not a URI or not a postgres URI. The message never echoes the value,
    /// because the URI carries the database password.
    /// </exception>
    public static string? ToNpgsqlConnectionString(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new FormatException("DATABASE_URL is not a valid postgres:// URI.");
        if (uri.Scheme is not ("postgres" or "postgresql"))
            throw new FormatException($"DATABASE_URL must be a postgres:// URI, got scheme '{uri.Scheme}'.");

        var userInfo = uri.UserInfo.Split(':', 2);
        var query = HttpUtility.ParseQueryString(uri.Query);

        var b = new NpgsqlConnectionStringBuilder
        {
            Host = uri.DnsSafeHost, // no brackets around a literal IPv6 address, unlike uri.Host
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            MaxAutoPrepare = 0,
            NoResetOnClose = true,
            MaxPoolSize = 10,
            SslMode = query["sslmode"] switch
            {
                "require" => SslMode.Require,
                "verify-ca" => SslMode.VerifyCA,
                "verify-full" => SslMode.VerifyFull,
                "disable" => SslMode.Disable,
                _ => SslMode.Prefer,
            },
        };
        return b.ConnectionString;
    }
}
