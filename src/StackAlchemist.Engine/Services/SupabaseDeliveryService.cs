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
    IPendingWriteBuffer pendingWrites,
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

    // "Every status except the two terminal ones (success, failed)" — a restart can
    // orphan a job mid-packing/uploading just as easily as mid-build.
    private const string NonTerminalStatusFilter =
        "status=in.(pending,extracting_schema,generating_code,generating,building,packing,uploading)";

    public async Task<IReadOnlyList<GenerationSnapshot>> GetStaleNonTerminalAsync(TimeSpan olderThan, CancellationToken ct)
    {
        var supabaseUrl = config["Supabase:Url"];
        var serviceRoleKey = config["Supabase:ServiceRoleKey"];

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceRoleKey))
            return [];

        var cutoffIso = DateTime.UtcNow.Subtract(olderThan).ToString("O");
        var endpoint =
            $"{supabaseUrl.TrimEnd('/')}/rest/v1/generations" +
            $"?{NonTerminalStatusFilter}" +
            $"&updated_at=lt.{Uri.EscapeDataString(cutoffIso)}" +
            "&select=id,status,tier,mode,prompt,project_type,schema_json,personalization_json,attempt_count,updated_at";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("apikey", serviceRoleKey);
            request.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");

            var client = httpClientFactory.CreateClient(HttpClientName);
            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                LogSupabasePatchNonOk(logger, (int)response.StatusCode, "stale-list", body);
                return [];
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return [];

            var rows = new List<GenerationSnapshot>();
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                if (ParseSnapshot(row) is { } snapshot)
                    rows.Add(snapshot);
            }
            return rows;
        }
        catch (Exception ex)
        {
            LogStaleReconcileFailed(logger, ex);
            return [];
        }
    }

    public async Task<bool> TryClaimForRequeueAsync(GenerationSnapshot row, TimeSpan olderThan, CancellationToken ct)
    {
        var payload = new Dictionary<string, object?>
        {
            ["status"] = "pending",
            ["attempt_count"] = row.AttemptCount + 1,
            ["error_message"] = null,
            ["error_category"] = null,
        };

        return await TryConditionalPatchAsync(row, olderThan, payload, "requeue-claim", ct);
    }

    public async Task<bool> TryFailStaleRowAsync(
        GenerationSnapshot row,
        TimeSpan olderThan,
        string errorMessage,
        string errorCategory,
        CancellationToken ct)
    {
        var nowIso = DateTime.UtcNow.ToString("O");
        var payload = new Dictionary<string, object?>
        {
            ["status"] = "failed",
            ["error_message"] = errorMessage,
            ["error_category"] = errorCategory,
            ["completed_at"] = nowIso,
        };

        var failed = await TryConditionalPatchAsync(row, olderThan, payload, "stale-fail", ct);
        if (failed)
            LogStaleReconciled(logger, 1);
        return failed;
    }

    /// <summary>
    /// CAS via PostgREST conditional PATCH: the filter pins (id, status, attempt_count,
    /// updated_at &lt; cutoff). Postgres serializes competing UPDATEs; the loser's filter no
    /// longer matches (status changed, attempt_count changed, and the set_updated_at trigger
    /// bumped updated_at past the cutoff) — three independent guards, no new columns needed.
    /// </summary>
    private async Task<bool> TryConditionalPatchAsync(
        GenerationSnapshot row,
        TimeSpan olderThan,
        Dictionary<string, object?> payload,
        string opName,
        CancellationToken ct)
    {
        var supabaseUrl = config["Supabase:Url"];
        var serviceRoleKey = config["Supabase:ServiceRoleKey"];

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceRoleKey))
            return false;

        var cutoffIso = DateTime.UtcNow.Subtract(olderThan).ToString("O");
        var endpoint =
            $"{supabaseUrl.TrimEnd('/')}/rest/v1/generations" +
            $"?id=eq.{row.Id}" +
            $"&status=eq.{Uri.EscapeDataString(row.Status)}" +
            $"&attempt_count=eq.{row.AttemptCount}" +
            $"&updated_at=lt.{Uri.EscapeDataString(cutoffIso)}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, endpoint)
            {
                Content = JsonContent.Create(payload, options: JsonOpts),
            };
            request.Headers.Add("apikey", serviceRoleKey);
            request.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");
            request.Headers.Add("Prefer", "return=representation");

            var client = httpClientFactory.CreateClient(HttpClientName);
            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                LogSupabasePatchNonOk(logger, (int)response.StatusCode, $"{opName}:{row.Id}", body);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() == 1;
        }
        catch (Exception ex)
        {
            LogSupabasePatchFailed(logger, ex, row.Id);
            return false;
        }
    }

    public async Task<bool> TryBeginExtractionAsync(string generationId, CancellationToken ct)
    {
        var supabaseUrl = config["Supabase:Url"];
        var serviceRoleKey = config["Supabase:ServiceRoleKey"];

        // Unconfigured (local dev / tests): proceed — there is no row to guard.
        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceRoleKey))
            return true;

        // pending and failed are the only legal entry states (failed = user retry).
        // A double-submit loses this CAS and gets rejected by the endpoint.
        var endpoint =
            $"{supabaseUrl.TrimEnd('/')}/rest/v1/generations" +
            $"?id=eq.{generationId}&status=in.(pending,failed)";

        var payload = new Dictionary<string, object?>
        {
            ["status"] = "extracting_schema",
            ["error_message"] = null,
            ["error_category"] = null,
            ["updated_at"] = DateTime.UtcNow.ToString("O"),
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, endpoint)
            {
                Content = JsonContent.Create(payload, options: JsonOpts),
            };
            request.Headers.Add("apikey", serviceRoleKey);
            request.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");
            request.Headers.Add("Prefer", "return=representation");

            var client = httpClientFactory.CreateClient(HttpClientName);
            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                LogSupabasePatchNonOk(logger, (int)response.StatusCode, $"begin-extraction:{generationId}", body);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() == 1;
        }
        catch (Exception ex)
        {
            LogSupabasePatchFailed(logger, ex, generationId);
            return false;
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
        // strands the row forever, so they get a deep exponential-backoff budget
        // (1s/2s/4s/8s between 5 tries, ~15s busy + HTTP timeouts ≈ 30s envelope).
        // Progress pings are disposable (the next ping supersedes them): 2 quick tries.
        var maxAttempts = critical ? 5 : 2;

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

                // Non-transient 4xx (CHECK violation, bad column, auth) never succeeds
                // on retry — bail straight to the failure path instead of burning the budget.
                var code = (int)response.StatusCode;
                if (code is >= 400 and < 500 && code is not 408 and not 429)
                    break;
            }
            catch (Exception ex)
            {
                LogSupabasePatchFailed(logger, ex, generationId);
            }

            if (attempt < maxAttempts)
            {
                try
                {
                    var delay = critical
                        ? TimeSpan.FromSeconds(Math.Pow(2, attempt - 1))
                        : TimeSpan.FromMilliseconds(500);
                    await Task.Delay(delay, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        // Reached only when every attempt failed. For terminal writes, buffer the
        // payload so the periodic reconciler can re-flush it once Supabase recovers —
        // a transient outage no longer strands the row in a non-terminal state.
        if (critical)
        {
            pendingWrites.Enqueue(new PendingGenerationWrite(generationId, payload, DateTimeOffset.UtcNow));
            LogSupabaseCriticalPatchFailed(logger, generationId);
        }
    }

    public async Task<bool> TryPatchOnceAsync(
        string generationId, Dictionary<string, object?> payload, CancellationToken ct)
    {
        var supabaseUrl = config["Supabase:Url"];
        var serviceRoleKey = config["Supabase:ServiceRoleKey"];

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceRoleKey))
            return false;

        try
        {
            var endpoint = $"{supabaseUrl.TrimEnd('/')}/rest/v1/generations?id=eq.{generationId}";
            using var request = new HttpRequestMessage(HttpMethod.Patch, endpoint)
            {
                Content = JsonContent.Create(payload, options: JsonOpts),
            };
            request.Headers.Add("apikey", serviceRoleKey);
            request.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");
            request.Headers.Add("Prefer", "return=minimal");

            var client = httpClientFactory.CreateClient(HttpClientName);
            var response = await client.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            LogSupabasePatchFailed(logger, ex, generationId);
            return false;
        }
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
        var client = httpClientFactory.CreateClient(HttpClientName);

        // Token-usage and build-log RPCs are additive nice-to-haves: one quick retry,
        // then give up. Fresh request per attempt — HttpRequestMessage cannot be resent.
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
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
                LogSupabaseRpcNonOk(logger, functionName, (int)response.StatusCode, body);
            }
            catch (Exception ex)
            {
                LogSupabaseRpcFailed(logger, ex, functionName);
            }

            if (attempt < 2)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
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
