using System.Globalization;
using System.Text.RegularExpressions;
using Npgsql;

namespace StackAlchemist.Engine.Data;

/// <summary>
/// libpq URI (what qavren-db's provisioner prints and what DATABASE_URL carries)
/// to an Npgsql connection string. Npgsql does not parse URIs itself.
/// <para>
/// Two pooler options are fixed here because DATABASE_URL always points at the Supavisor
/// transaction pooler in prod: no server-side prepared statements (<c>MaxAutoPrepare = 0</c>)
/// and no session reset on close (<c>NoResetOnClose</c>); see gavel-suite ADR-0017. Pool
/// size is a deployment setting and is applied where the data source is registered.
/// </para>
/// <para>
/// GSS session encryption is pinned off (<c>GssEncryptionMode = Disable</c>). Npgsql 10 changed
/// the default to <c>Prefer</c>, so every new physical connection first tries GSSAPI (and logs a
/// libgssapi load failure on a Linux image without Kerberos) before falling back to TLS. Nothing
/// in this stack uses Kerberos and the peer is the Supavisor pooler, so the Engine keeps the
/// Npgsql 9 handshake: straight to TLS.
/// </para>
/// <para>
/// Parsing is strict on purpose: a URL that silently lost its intent (a mis-cased
/// <c>sslmode</c>, a pgbouncer flag glued on with a second <c>?</c>) would fall back to a weaker
/// TLS mode without anyone noticing. Only <c>sslmode</c>, <c>application_name</c>,
/// <c>connect_timeout</c>, <c>options</c> and <c>sslrootcert</c> are accepted; every other
/// query parameter is rejected.
/// </para>
/// </summary>
public static partial class PostgresUrl
{
    /// <summary>
    /// Converts a <c>postgres://</c> / <c>postgresql://</c> URI to an Npgsql connection string.
    /// Returns <c>null</c> for a null or blank value (DATABASE_URL unset).
    /// </summary>
    /// <exception cref="FormatException">
    /// The value is not a valid postgres URI. This is the only exception type for bad input. The
    /// message never echoes the URL, its userinfo or any query value other than a short,
    /// letters-only <c>sslmode</c>, because the URI carries the database password.
    /// </exception>
    public static string? ToNpgsqlConnectionString(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new FormatException("DATABASE_URL is not a valid postgres:// URI.");
        if (uri.Scheme is not ("postgres" or "postgresql"))
            throw new FormatException("DATABASE_URL must be a postgres:// or postgresql:// URI.");

        var query = ParseQuery(uri.Query);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'));

        try
        {
            // Empty username / database stay unset so Npgsql applies its own defaults
            // (database defaults to the username), as libpq does.
            var b = new NpgsqlConnectionStringBuilder
            {
                Host = uri.DnsSafeHost, // no brackets around a literal IPv6 address, unlike uri.Host
                Port = uri.IsDefaultPort ? 5432 : uri.Port,
                Database = database.Length > 0 ? database : null,
                Username = username.Length > 0 ? username : null,
                Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
                MaxAutoPrepare = 0,
                NoResetOnClose = true,
                SslMode = SslMode.Prefer,
                GssEncryptionMode = GssEncryptionMode.Disable,
            };

            foreach (var (key, values) in query)
            {
                // sslmode is exempt so a repeated key is reported as an unrecognised sslmode.
                if (key != "sslmode" && values.Count > 1)
                    throw new FormatException($"DATABASE_URL repeats query parameter '{key}'.");

                var value = string.Join(',', values);
                switch (key)
                {
                    case "sslmode": b.SslMode = ParseSslMode(value); break;
                    case "application_name": b.ApplicationName = value; break;
                    case "connect_timeout": b.Timeout = ParseConnectTimeout(value); break;
                    case "options": b.Options = value; break;
                    case "sslrootcert": b.RootCertificate = value; break;
                    default:
                        throw new FormatException($"DATABASE_URL has an unsupported query parameter '{key}'.");
                }
            }

            return b.ConnectionString;
        }
        catch (ArgumentException)
        {
            // e.g. port 0 or a timeout past Npgsql's limit. No inner exception: its message can echo values.
            throw new FormatException("DATABASE_URL has an out-of-range or invalid component.");
        }
    }

    private static Dictionary<string, List<string>> ParseQuery(string rawQuery)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var query = rawQuery.StartsWith('?') ? rawQuery[1..] : rawQuery;

        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=', StringComparison.Ordinal);
            if (eq <= 0)
                throw new FormatException("DATABASE_URL has a malformed query string.");

            var key = Uri.UnescapeDataString(part[..eq]);
            var value = Uri.UnescapeDataString(part[(eq + 1)..]);
            if (!result.TryGetValue(key, out var values))
                result[key] = values = [];
            values.Add(value);
        }

        return result;
    }

    private static SslMode ParseSslMode(string value) => value switch
    {
        "disable" => SslMode.Disable,
        "allow" => SslMode.Allow,
        "prefer" => SslMode.Prefer,
        "require" => SslMode.Require,
        "verify-ca" => SslMode.VerifyCA,
        "verify-full" => SslMode.VerifyFull,
        // Quote the value only when it cannot be carrying anything else: a second '?' glues
        // the rest of the URL (a password, say) onto the sslmode value.
        _ => throw new FormatException(QuotableSslMode().IsMatch(value)
            ? $"DATABASE_URL has an unrecognised sslmode '{value}'."
            : "DATABASE_URL has an unrecognised sslmode."),
    };

    [GeneratedRegex("^[A-Za-z_-]{0,16}$")]
    private static partial Regex QuotableSslMode();

    private static int ParseConnectTimeout(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            ? seconds
            : throw new FormatException("DATABASE_URL has an invalid connect_timeout.");
}
