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
        var supabaseUrl = config["Supabase:Url"];
        var serviceRoleKey = config["Supabase:ServiceRoleKey"];

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceRoleKey))
        {
            logger.LogDebug(
                "Supabase not configured — skipping delivery status update for {Id}", generationId);
            return;
        }

        var payload = new Dictionary<string, object?>
        {
            ["status"] = state.ToString().ToLowerInvariant(),
            ["updated_at"] = DateTime.UtcNow.ToString("O"),
        };

        if (downloadUrl is not null)
            payload["download_url"] = downloadUrl;

        if (errorMessage is not null)
            payload["error_message"] = errorMessage;

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

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Supabase updated: generation {Id} → {State}", generationId, state);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning(
                    "Supabase PATCH returned {Code} for generation {Id}: {Body}",
                    (int)response.StatusCode, generationId, body);
            }
        }
        catch (Exception ex)
        {
            // Delivery failures must not crash the pipeline — log and continue.
            logger.LogError(ex, "Failed to update Supabase for generation {Id}", generationId);
        }
    }
}
