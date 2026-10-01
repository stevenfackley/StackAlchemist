using System.Text.Json;
using Npgsql;

namespace StackAlchemist.Engine.Data;

/// <summary>Log redaction shared by the Npgsql-backed services.</summary>
public static class PostgresErrors
{
    /// <summary>
    /// What of a failure may reach the logs. A <see cref="PostgresException"/>'s message can quote the
    /// offending value (22P02 echoes its input), and a <see cref="JsonException"/> comes from a
    /// jsonb column a client may have written, so for those only the SQLSTATE and constraint, or the
    /// JSON path, are kept and the exception itself is dropped. Anything else (a refused connection,
    /// a timeout, cancellation) carries no row data and is logged whole.
    /// </summary>
    public static (Exception? Loggable, string Reason) Redact(Exception ex) => ex switch
    {
        PostgresException pg => (null, $"SQLSTATE {pg.SqlState}, constraint {pg.ConstraintName ?? "-"}"),
        JsonException json => (null, $"JSON that does not fit the model at {json.Path ?? "$"}"),
        _ => (ex, ex.GetType().Name),
    };
}
