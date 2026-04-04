using System.Net.Http.Json;
using System.Text.Json;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Updates the <c>generations</c> table in Supabase via its PostgREST HTTP API so the
/// frontend receives real-time status updates through Supabase Realtime.
/// </summary>
public sealed class SupabaseDeliveryService(
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

        if (state is GenerationState.Success or GenerationState.Failed)
            payload["completed_at"] = DateTime.UtcNow.ToString("O");

        await PatchGenerationAsync(generationId, payload, ct);
        logger.LogInformation("Supabase updated: generation {Id} → {State}", generationId, state);
    }

    public async Task UpdateStatusAsync(
        string generationId,
        string status,
        CancellationToken ct,
        string? errorMessage = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["status"] = status,
            ["updated_at"] = DateTime.UtcNow.ToString("O"),
        };

        if (errorMessage is not null)
            payload["error_message"] = errorMessage;

        if (status is "success" or "failed")
            payload["completed_at"] = DateTime.UtcNow.ToString("O");

        await PatchGenerationAsync(generationId, payload, ct);
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

    public async Task AppendBuildLogAsync(
        string generationId,
        string logChunk,
        CancellationToken ct)
    {
        // PostgREST doesn't support append natively, so we use an RPC or
        // concatenate on the client. For simplicity, we fetch + append + patch.
        // In production, a Postgres function would be better.
        var supabaseUrl = config["Supabase:Url"];
        var serviceRoleKey = config["Supabase:ServiceRoleKey"];

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceRoleKey))
            return;

        // Fetch current build_log
        var fetchUrl = $"{supabaseUrl.TrimEnd('/')}/rest/v1/generations?id=eq.{generationId}&select=build_log";
        var fetchReq = new HttpRequestMessage(HttpMethod.Get, fetchUrl);
        fetchReq.Headers.Add("apikey", serviceRoleKey);
        fetchReq.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            var fetchRes = await client.SendAsync(fetchReq, ct);
            var rows = await fetchRes.Content.ReadFromJsonAsync<List<Dictionary<string, object?>>>(ct);
            var existing = rows?.FirstOrDefault()?["build_log"]?.ToString() ?? "";

            var updated = string.IsNullOrEmpty(existing) ? logChunk : existing + "\n" + logChunk;

            await PatchGenerationAsync(generationId, new Dictionary<string, object?>
            {
                ["build_log"] = updated,
                ["updated_at"] = DateTime.UtcNow.ToString("O"),
            }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to append build log for generation {Id}", generationId);
        }
    }

    // ── Shared PATCH helper ─────────────────────────────────────────────────

    private async Task PatchGenerationAsync(
        string generationId,
        Dictionary<string, object?> payload,
        CancellationToken ct)
    {
        var supabaseUrl = config["Supabase:Url"];
        var serviceRoleKey = config["Supabase:ServiceRoleKey"];

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceRoleKey))
        {
            logger.LogDebug(
                "Supabase not configured — skipping patch for {Id}", generationId);
            return;
        }

        var endpoint = $"{supabaseUrl.TrimEnd('/')}/rest/v1/generations?id=eq.{generationId}";

        var request = new HttpRequestMessage(HttpMethod.Patch, endpoint)
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
                logger.LogWarning(
                    "Supabase PATCH returned {Code} for generation {Id}: {Body}",
                    (int)response.StatusCode, generationId, body);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to patch Supabase for generation {Id}", generationId);
        }
    }
}
