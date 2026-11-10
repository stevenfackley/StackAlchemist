namespace StackAlchemist.Engine.Hosting;

/// <summary>
/// Bridges the flat <c>.env</c> names to nested IConfiguration paths. The .env file uses
/// SCREAMING_SNAKE_CASE; ASP.NET Core config uses Colon:Hierarchy. Mapping them here lets
/// services keep reading <c>config["Anthropic:ApiKey"]</c> etc.
/// </summary>
public static class EngineEnvironment
{
    /// <summary>The environment variable's value, or null when it is unset or blank.</summary>
    public static string? Get(string key)
    {
        var v = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    public static IConfigurationBuilder AddEngineEnvironment(this IConfigurationBuilder configuration) =>
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // LLM
            ["Anthropic:ApiKey"]              = Get("ANTHROPIC_API_KEY"),
            ["Anthropic:Model"]               = Get("ANTHROPIC_MODEL"),
            ["Anthropic:Effort"]              = Get("ANTHROPIC_EFFORT"),
            // Cloudflare R2
            ["CloudflareR2:AccountId"]        = Get("R2_ACCOUNT_ID"),
            ["CloudflareR2:AccessKeyId"]      = Get("R2_ACCESS_KEY_ID"),
            ["CloudflareR2:SecretAccessKey"]  = Get("R2_SECRET_ACCESS_KEY"),
            ["CloudflareR2:BucketName"]       = Get("R2_BUCKET_NAME"),
            // Stripe
            ["Stripe:PublishableKey"]         = Get("NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY"),
            ["Stripe:SecretKey"]              = Get("STRIPE_SECRET_KEY"),
            ["Stripe:WebhookSecret"]          = Get("STRIPE_WEBHOOK_SECRET"),
            // Engine hardening
            ["Engine:ServiceKey"]             = Get("ENGINE_SERVICE_KEY"),
            ["Engine:AllowedOrigins"]         = Get("NEXT_PUBLIC_APP_URL") ?? "http://localhost:3000",
            // BYOK: engine-side decryption secret for stored per-user API keys. Mirrors the web app,
            // which encrypts with BYOK_ENCRYPTION_KEY; ByokKeyProtector falls back to Engine:ServiceKey
            // for keys saved before the distinct-key requirement (finding I3).
            ["Byok:EncryptionKey"]            = Get("BYOK_ENCRYPTION_KEY"),
            // Resend transactional email
            ["Resend:ApiKey"]                 = Get("RESEND_API_KEY"),
            ["Resend:FromEmail"]              = Get("RESEND_FROM_EMAIL"),
        }.Where(kv => kv.Value is not null).ToDictionary(kv => kv.Key, kv => kv.Value));

    /// <summary>
    /// Production startup validation: fail fast on missing critical secrets so a misconfigured
    /// deployment surfaces immediately. DATABASE_URL is required too; AddGenerationStore (shared
    /// with the Worker) enforces it.
    /// </summary>
    public static void RequireProductionSecrets(IConfiguration configuration)
    {
        var required = new[]
        {
            ("ANTHROPIC_API_KEY",     configuration["Anthropic:ApiKey"]),
            ("R2_ACCESS_KEY_ID",      configuration["CloudflareR2:AccessKeyId"]),
            ("STRIPE_WEBHOOK_SECRET", configuration["Stripe:WebhookSecret"]),
            ("ENGINE_SERVICE_KEY",    configuration["Engine:ServiceKey"]),
        };
        foreach (var (name, value) in required)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException(
                    $"Required environment variable '{name}' is not set. " +
                    "Set it via user-secrets or environment before starting in Production.");
        }
    }
}
