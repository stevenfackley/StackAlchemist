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
            logger.LogWarning(ex, "Failed to look up owner email for generation {Id}", generationId);
            return null;
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

    private async Task InvokeRpcAsync(
        string functionName,
        Dictionary<string, object?> payload,
        CancellationToken ct)
    {
        var supabaseUrl = config["Supabase:Url"];
        var serviceRoleKey = config["Supabase:ServiceRoleKey"];

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceRoleKey))
        {
            logger.LogDebug("Supabase not configured — skipping RPC {Function}", functionName);
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
                logger.LogWarning(
                    "Supabase RPC {Function} returned {Code}: {Body}",
                    functionName,
                    (int)response.StatusCode,
                    body);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to invoke Supabase RPC {Function}", functionName);
        }
    }
}
