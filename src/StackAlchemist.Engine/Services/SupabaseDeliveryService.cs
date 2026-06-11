using System.Net.Http.Json;
using System.Text.Json;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Updates the <c>generations</c> table in Supabase via its PostgREST HTTP API so the
/// frontend receives real-time status updates through Supabase Realtime.
/// </summary>
public sealed partial class SupabaseDeliveryService(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<SupabaseDeliveryService> logger) : IDeliveryService
{
    public const string HttpClientName = "Supabase";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task UpdateStatusAsync(
        string generationId,
        GenerationState state,
        string? downloadUrl = null,
        string? errorMessage = null,
        string? errorCategory = null,
        CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["status"] = state.ToString().ToLowerInvariant(),
            ["updated_at"] = DateTime.UtcNow.ToString("O"),
        };

        if (downloadUrl is not null)
            payload["download_url"] = downloadUrl;

        if (errorMessage is not null)
            payload["error_message"] = errorMessage;

        if (errorCategory is not null)
            payload["error_category"] = errorCategory;

        if (state is GenerationState.Success or GenerationState.Failed)
            payload["completed_at"] = DateTime.UtcNow.ToString("O");

        await PatchGenerationAsync(
            generationId,
            payload,
            ct,
            critical: state is GenerationState.Success or GenerationState.Failed);
        LogSupabaseUpdated(logger, generationId, state);
    }

    public async Task UpdateStatusAsync(
        string generationId,
        string status,
        CancellationToken ct,
        string? errorMessage = null,
        string? errorCategory = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["status"] = status,
            ["updated_at"] = DateTime.UtcNow.ToString("O"),
        };

        if (errorMessage is not null)
            payload["error_message"] = errorMessage;

        if (errorCategory is not null)
            payload["error_category"] = errorCategory;

        if (status is "success" or "failed")
            payload["completed_at"] = DateTime.UtcNow.ToString("O");

        await PatchGenerationAsync(
            generationId,
            payload,
            ct,
            critical: status is "success" or "failed");
    }

    public async Task CompletePreviewAsync(
        string generationId,
        IReadOnlyDictionary<string, string> files,
        CancellationToken ct)
    {
        var nowIso = DateTime.UtcNow.ToString("O");

        // One atomic terminal write: the file map lands in the JSONB column and the row
        // flips to success together, so the Realtime UPDATE the UI sees already carries
        // a renderable preview. Dictionary keys (file paths) serialize verbatim — STJ's
        // PropertyNamingPolicy only touches POCO property names, not dictionary keys.
        var payload = new Dictionary<string, object?>
        {
            ["preview_files_json"] = files,
            ["status"] = "success",
            ["updated_at"] = nowIso,
            ["completed_at"] = nowIso,
        };

        await PatchGenerationAsync(generationId, payload, ct, critical: true);
        LogPreviewCompleted(logger, generationId, files.Count);
    }

    public async Task UpdateSchemaAsync(
        string generationId,
        GenerationSchema schema,
        CancellationToken ct)
    {
        var payload = new Dictionary<string, object?>
        {
            ["schema_json"] = schema,
            ["updated_at"] = DateTime.UtcNow.ToString("O"),
        };

        await PatchGenerationAsync(generationId, payload, ct);
    }

    public async Task UpdateTokenUsageAsync(
        string generationId,
        int inputTokens,
        int outputTokens,
        string model,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model))
            return;

        var payload = new Dictionary<string, object?>
        {
            ["gen_id"] = generationId,
            ["input_delta"] = inputTokens,
            ["output_delta"] = outputTokens,
            ["model_name"] = model,
        };

        await InvokeRpcAsync("increment_token_usage", payload, ct);
    }

    public async Task AppendBuildLogAsync(
        string generationId,
        string logChunk,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(logChunk))
            return;

        var payload = new Dictionary<string, object?>
        {
            ["gen_id"] = generationId,
            ["chunk"] = logChunk,
        };

        await InvokeRpcAsync("append_build_log", payload, ct);
    }

    public async Task<string?> GetGenerationOwnerEmailAsync(string generationId, CancellationToken ct)
    {
        var supabaseUrl = config["Supabase:Url"];
        var serviceRoleKey = config["Supabase:ServiceRoleKey"];

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceRoleKey))
            return null;

        try
        {
            // PostgREST embedded resource: select profile email through the user_id FK.
            var endpoint = $"{supabaseUrl.TrimEnd('/')}/rest/v1/generations?id=eq.{generationId}&select=profiles(email)";
            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("apikey", serviceRoleKey);
            request.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");

            var client = httpClientFactory.CreateClient(HttpClientName);
            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return null;

            var row = doc.RootElement[0];
            if (!row.TryGetProperty("profiles", out var profiles) ||
                profiles.ValueKind != JsonValueKind.Object)
                return null;

            return profiles.TryGetProperty("email", out var email) && email.ValueKind == JsonValueKind.String
                ? email.GetString()
                : null;
        }
        catch (Exception ex)
        {
            LogOwnerEmailLookupFailed(logger, ex, generationId);
            return null;
        }
    }

    public async Task<int> FailStaleNonTerminalAsync(TimeSpan olderThan, CancellationToken ct)
    {
        var supabaseUrl = config["Supabase:Url"];
        var serviceRoleKey = config["Supabase:ServiceRoleKey"];

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceRoleKey))
            return 0;

        var nowIso = DateTime.UtcNow.ToString("O");
        var cutoffIso = DateTime.UtcNow.Subtract(olderThan).ToString("O");

        // One PATCH sweeps every row still parked in a non-terminal status whose last
        // update predates the cutoff. PostgREST applies the body to all matching rows.
        // The set is "every status except the two terminal ones (success, failed)" — a
        // restart can orphan a job mid-packing/uploading just as easily as mid-build.
        var endpoint =
            $"{supabaseUrl.TrimEnd('/')}/rest/v1/generations" +
            "?status=in.(pending,extracting_schema,generating_code,generating,building,packing,uploading)" +
            $"&updated_at=lt.{Uri.EscapeDataString(cutoffIso)}";

        var payload = new Dictionary<string, object?>
        {
            ["status"] = "failed",
            ["error_message"] = "Generation did not complete — the engine restarted while this job was in flight.",
            ["updated_at"] = nowIso,
            ["completed_at"] = nowIso,
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, endpoint)
            {
                Content = JsonContent.Create(payload, options: JsonOpts),
            };
            request.Headers.Add("apikey", serviceRoleKey);
            request.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");
            // return=representation so the response carries the affected rows to count.
            request.Headers.Add("Prefer", "return=representation");

            var client = httpClientFactory.CreateClient(HttpClientName);
            var response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                LogSupabasePatchNonOk(logger, (int)response.StatusCode, "stale-sweep", body);
                return 0;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var count = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.GetArrayLength()
                : 0;

            if (count > 0)
                LogStaleReconciled(logger, count);

            return count;
        }
        catch (Exception ex)
        {
            LogStaleReconcileFailed(logger, ex);
            return 0;
        }
    }

    public async Task<GenerationSnapshot?> GetGenerationSnapshotAsync(string generationId, CancellationToken ct)
    {
        var supabaseUrl = config["Supabase:Url"];
        var serviceRoleKey = config["Supabase:ServiceRoleKey"];

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceRoleKey))
            return null;

        try
        {
            var endpoint =
                $"{supabaseUrl.TrimEnd('/')}/rest/v1/generations?id=eq.{generationId}" +
                "&select=id,status,tier,mode,prompt,project_type,schema_json,personalization_json,attempt_count,updated_at";
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("apikey", serviceRoleKey);
            request.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");

            var client = httpClientFactory.CreateClient(HttpClientName);
            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return null;

            return ParseSnapshot(doc.RootElement[0]);
        }
        catch (Exception ex)
        {
            LogSnapshotReadFailed(logger, ex, generationId);
            return null;
        }
    }

    private static GenerationSnapshot? ParseSnapshot(JsonElement row)
    {
        var id = row.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
            ? idEl.GetString()
            : null;
        if (id is null) return null;

        var status = row.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String
            ? st.GetString()!
            : "pending";
        var tier = row.TryGetProperty("tier", out var ti) && ti.ValueKind == JsonValueKind.Number
            ? ti.GetInt32()
            : 0;
        var mode = row.TryGetProperty("mode", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
        var prompt = row.TryGetProperty("prompt", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
        ProjectType? projectType =
            row.TryGetProperty("project_type", out var pt) && pt.ValueKind == JsonValueKind.String &&
            Enum.TryParse<ProjectType>(pt.GetString(), ignoreCase: true, out var parsedPt)
                ? parsedPt
                : null;

        GenerationSchema? schema = null;
        if (row.TryGetProperty("schema_json", out var s) &&
            s.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            schema = JsonSerializer.Deserialize<GenerationSchema>(s.GetRawText());
        }

        GenerationPersonalization? personalization = null;
        if (row.TryGetProperty("personalization_json", out var pj) &&
            pj.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            personalization = JsonSerializer.Deserialize<GenerationPersonalization>(
                pj.GetRawText(), SnapshotJson);
        }

        var attemptCount = row.TryGetProperty("attempt_count", out var ac) && ac.ValueKind == JsonValueKind.Number
            ? ac.GetInt32()
            : 0;
        var updatedAt = row.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String &&
                        DateTimeOffset.TryParse(ua.GetString(), out var parsedUa)
            ? parsedUa
            : DateTimeOffset.MinValue;

        return new GenerationSnapshot(
            id, status, tier, mode, prompt, projectType, schema, personalization, attemptCount, updatedAt);
    }

    private static readonly JsonSerializerOptions SnapshotJson = new() { PropertyNameCaseInsensitive = true };

    // ── Shared PATCH helper ─────────────────────────────────────────────────

    private async Task PatchGenerationAsync(
        string generationId,
        Dictionary<string, object?> payload,
        CancellationToken ct,
        bool critical = false)
    {
        var supabaseUrl = config["Supabase:Url"];
        var serviceRoleKey = config["Supabase:ServiceRoleKey"];

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceRoleKey))
        {
            LogSupabasePatchSkipped(logger, generationId);
            return;
        }

        var endpoint = $"{supabaseUrl.TrimEnd('/')}/rest/v1/generations?id=eq.{generationId}";
        var client = httpClientFactory.CreateClient(HttpClientName);

        // Terminal writes (success/failed) are what the UI blocks on — a dropped one
        // strands the row forever, so retry once. Progress pings are disposable (the
        // next ping supersedes them), so they fire a single time.
        var maxAttempts = critical ? 2 : 1;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                // Fresh request per attempt: HttpRequestMessage cannot be resent.
                using var request = new HttpRequestMessage(HttpMethod.Patch, endpoint)
                {
                    Content = JsonContent.Create(payload, options: JsonOpts),
                };
                request.Headers.Add("apikey", serviceRoleKey);
                request.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");
                request.Headers.Add("Prefer", "return=minimal");

                var response = await client.SendAsync(request, ct);

                if (response.IsSuccessStatusCode)
                    return;

                var body = await response.Content.ReadAsStringAsync(ct);
                LogSupabasePatchNonOk(logger, (int)response.StatusCode, generationId, body);
            }
            catch (Exception ex)
            {
                LogSupabasePatchFailed(logger, ex, generationId);
            }

            if (attempt < maxAttempts)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        // Reached only when every attempt failed. For terminal writes that means the
        // row is now stranded in a non-terminal state — surface it at Error.
        if (critical)
            LogSupabaseCriticalPatchFailed(logger, generationId);
    }

    private async Task InvokeRpcAsync(
        string functionName,
        Dictionary<string, object?> payload,
        CancellationToken ct)
    {
        var supabaseUrl = config["Supabase:Url"];
        var serviceRoleKey = config["Supabase:ServiceRoleKey"];

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceRoleKey))
        {
            LogSupabaseRpcSkipped(logger, functionName);
            return;
        }

        var endpoint = $"{supabaseUrl.TrimEnd('/')}/rest/v1/rpc/{functionName}";
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload, options: JsonOpts),
        };
        request.Headers.Add("apikey", serviceRoleKey);
        request.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");
        request.Headers.Add("Prefer", "return=minimal");

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            var response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                LogSupabaseRpcNonOk(logger, functionName, (int)response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            LogSupabaseRpcFailed(logger, ex, functionName);
        }
    }

    // ── LoggerMessage source-gen ──────────────────────────────────────────────

    [LoggerMessage(EventId = 500, Level = LogLevel.Information, Message = "Supabase updated: generation {Id} → {State}")]
    private static partial void LogSupabaseUpdated(ILogger logger, string id, GenerationState state);

    [LoggerMessage(EventId = 501, Level = LogLevel.Warning, Message = "Failed to look up owner email for generation {Id}")]
    private static partial void LogOwnerEmailLookupFailed(ILogger logger, Exception ex, string id);

    [LoggerMessage(EventId = 511, Level = LogLevel.Information, Message = "Tier-0 preview written: generation {Id} → success with {Count} inline files")]
    private static partial void LogPreviewCompleted(ILogger logger, string id, int count);

    [LoggerMessage(EventId = 502, Level = LogLevel.Debug, Message = "Supabase not configured — skipping patch for {Id}")]
    private static partial void LogSupabasePatchSkipped(ILogger logger, string id);

    [LoggerMessage(EventId = 503, Level = LogLevel.Warning, Message = "Supabase PATCH returned {Code} for generation {Id}: {Body}")]
    private static partial void LogSupabasePatchNonOk(ILogger logger, int code, string id, string body);

    [LoggerMessage(EventId = 504, Level = LogLevel.Error, Message = "Failed to patch Supabase for generation {Id}")]
    private static partial void LogSupabasePatchFailed(ILogger logger, Exception ex, string id);

    [LoggerMessage(EventId = 508, Level = LogLevel.Error, Message = "CRITICAL: terminal status PATCH for generation {Id} failed after retries — row may be stranded in a non-terminal state")]
    private static partial void LogSupabaseCriticalPatchFailed(ILogger logger, string id);

    [LoggerMessage(EventId = 509, Level = LogLevel.Information, Message = "Reconciliation: marked {Count} stale generation(s) as failed")]
    private static partial void LogStaleReconciled(ILogger logger, int count);

    [LoggerMessage(EventId = 510, Level = LogLevel.Error, Message = "Reconciliation sweep failed")]
    private static partial void LogStaleReconcileFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 512, Level = LogLevel.Warning, Message = "Failed to read generation snapshot for {Id}")]
    private static partial void LogSnapshotReadFailed(ILogger logger, Exception ex, string id);

    [LoggerMessage(EventId = 505, Level = LogLevel.Debug, Message = "Supabase not configured — skipping RPC {Function}")]
    private static partial void LogSupabaseRpcSkipped(ILogger logger, string function);

    [LoggerMessage(EventId = 506, Level = LogLevel.Warning, Message = "Supabase RPC {Function} returned {Code}: {Body}")]
    private static partial void LogSupabaseRpcNonOk(ILogger logger, string function, int code, string body);

    [LoggerMessage(EventId = 507, Level = LogLevel.Error, Message = "Failed to invoke Supabase RPC {Function}")]
    private static partial void LogSupabaseRpcFailed(ILogger logger, Exception ex, string function);
}
