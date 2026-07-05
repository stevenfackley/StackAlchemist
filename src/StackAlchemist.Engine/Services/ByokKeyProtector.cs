using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Generators;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// C# port of the Next.js BYOK encryption scheme (src/lib/actions.ts <c>encryptApiKeyOverride</c>).
/// Decrypts the stored <c>"v1:iv:tag:ciphertext"</c> envelope: derive key =
/// scrypt(secret, salt="stackalchemist-byok-v1", 32) with Node's DEFAULT scrypt params
/// (N=16384, r=8, p=1), then AES-256-GCM. Compatibility is proven byte-for-byte by a
/// Node-generated golden vector in <c>ByokKeyProtectorTests</c>.
///
/// Secret resolution mirrors the web app: <c>Byok:EncryptionKey</c> (BYOK_ENCRYPTION_KEY) with a
/// backward-compat fallback to <c>Engine:ServiceKey</c> (ENGINE_SERVICE_KEY) so keys saved before
/// the distinct-key requirement (finding I3) still decrypt. NEW saves on the web side require
/// BYOK_ENCRYPTION_KEY.
///
/// This type NEVER logs the ciphertext, the derived key, or the plaintext.
/// </summary>
public sealed partial class ByokKeyProtector(IConfiguration config, ILogger<ByokKeyProtector> logger)
{
    private const string Salt = "stackalchemist-byok-v1";
    private const int ScryptN = 16384;
    private const int ScryptR = 8;
    private const int ScryptP = 1;
    private const int KeyLenBytes = 32;
    private const int TagLenBytes = 16;
    private const int MinSecretLength = 32;

    /// <summary>
    /// Decrypts a stored ciphertext to its plaintext API key, or returns null when the ciphertext
    /// is absent, the encryption secret is unconfigured, or decryption fails (wrong secret,
    /// corruption, bad envelope). A null return degrades gracefully — the caller falls back to the
    /// global Anthropic key for Anthropic-model users, or fails BYOK-only providers cleanly. Never
    /// throws, and never logs key material.
    /// </summary>
    public string? TryDecrypt(string? ciphertext)
    {
        if (string.IsNullOrWhiteSpace(ciphertext))
            return null;

        var secret = ResolveSecret();
        if (secret.Length < MinSecretLength)
        {
            LogSecretUnconfigured(logger);
            return null;
        }

        try
        {
            var parts = ciphertext.Split(':');
            if (parts.Length != 4 || parts[0] != "v1")
            {
                LogMalformedCiphertext(logger);
                return null;
            }

            var iv = Convert.FromBase64String(parts[1]);
            var tag = Convert.FromBase64String(parts[2]);
            var data = Convert.FromBase64String(parts[3]);

            var key = SCrypt.Generate(
                Encoding.UTF8.GetBytes(secret),
                Encoding.UTF8.GetBytes(Salt),
                ScryptN, ScryptR, ScryptP, KeyLenBytes);

            using var aes = new AesGcm(key, TagLenBytes);
            var plaintext = new byte[data.Length];
            aes.Decrypt(iv, data, tag, plaintext);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            // Undecryptable stored key (wrong/rotated secret, corruption, bad base64). Log the
            // exception TYPE only — never the ciphertext or key — and degrade gracefully.
            LogDecryptFailed(logger, ex.GetType().Name);
            return null;
        }
    }

    private string ResolveSecret() =>
        config["Byok:EncryptionKey"] ?? config["Engine:ServiceKey"] ?? string.Empty;

    [LoggerMessage(EventId = 810, Level = LogLevel.Warning,
        Message = "BYOK decryption secret is not configured (Byok:EncryptionKey / Engine:ServiceKey) or is shorter than 32 chars — stored keys cannot be decrypted; falling back to global config")]
    private static partial void LogSecretUnconfigured(ILogger logger);

    [LoggerMessage(EventId = 811, Level = LogLevel.Warning,
        Message = "Stored BYOK key has an unexpected envelope format — ignoring it")]
    private static partial void LogMalformedCiphertext(ILogger logger);

    [LoggerMessage(EventId = 812, Level = LogLevel.Warning,
        Message = "Stored BYOK key could not be decrypted ({ErrorType}) — ignoring it; the encryption secret may have rotated")]
    private static partial void LogDecryptFailed(ILogger logger, string errorType);
}
