namespace StackAlchemist.Engine.Services;

/// <summary>
/// Stripe rejects a Checkout Session whose metadata values exceed 500 characters, and the web app
/// accepts prompts up to 2,000. The full prompt lives on the generations row, which the webhook
/// reads back through process_checkout_completed, so metadata only needs a clipped copy.
/// </summary>
public static class StripeMetadata
{
    public const int MaxValueLength = 500;

    /// <summary>Clips <paramref name="value"/> to 500 characters without splitting a surrogate pair; null becomes "".</summary>
    public static string Clip(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= MaxValueLength)
            return value ?? "";

        var cut = MaxValueLength;
        if (char.IsHighSurrogate(value[cut - 1]))
            cut--;
        return value[..cut];
    }
}
