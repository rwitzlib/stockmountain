using MarketViewer.Api.Config;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MarketViewer.Api.Services;

public class ClerkWebhookVerifier(
    IOptions<ClerkWebhookConfig> config,
    ILogger<ClerkWebhookVerifier> logger)
{
    private static readonly TimeSpan TimestampTolerance = TimeSpan.FromMinutes(5);

    public bool Verify(string payload, IHeaderDictionary headers)
    {
        if (string.IsNullOrWhiteSpace(config.Value.SigningSecret))
        {
            logger.LogError("Clerk webhook signing secret is not configured");
            return false;
        }

        if (!TryGetHeader(headers, "svix-id", out var svixId) ||
            !TryGetHeader(headers, "svix-timestamp", out var svixTimestamp) ||
            !headers.TryGetValue("svix-signature", out var svixSignature))
        {
            logger.LogWarning("Clerk webhook request is missing required Svix headers");
            return false;
        }

        if (!long.TryParse(svixTimestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestampSeconds))
        {
            logger.LogWarning("Clerk webhook request has an invalid Svix timestamp");
            return false;
        }

        var timestamp = DateTimeOffset.FromUnixTimeSeconds(timestampSeconds);
        if ((DateTimeOffset.UtcNow - timestamp).Duration() > TimestampTolerance)
        {
            logger.LogWarning("Clerk webhook request timestamp is outside the allowed tolerance");
            return false;
        }

        var signedPayload = $"{svixId}.{svixTimestamp}.{payload}";
        string expectedSignature;
        try
        {
            expectedSignature = ComputeSignature(signedPayload, config.Value.SigningSecret);
        }
        catch (FormatException ex)
        {
            logger.LogError(ex, "Clerk webhook signing secret is not valid base64");
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);

        foreach (var signature in ParseSignatures(svixSignature))
        {
            var signatureBytes = Encoding.UTF8.GetBytes(signature);
            if (signatureBytes.Length == expectedBytes.Length &&
                CryptographicOperations.FixedTimeEquals(signatureBytes, expectedBytes))
            {
                return true;
            }
        }

        logger.LogWarning("Clerk webhook signature verification failed for Svix message {SvixId}", svixId);
        return false;
    }

    private static bool TryGetHeader(IHeaderDictionary headers, string name, out string value)
    {
        value = string.Empty;

        if (!headers.TryGetValue(name, out var headerValues))
        {
            return false;
        }

        value = headerValues.FirstOrDefault() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string ComputeSignature(string payload, string signingSecret)
    {
        var secret = signingSecret.StartsWith("whsec_", StringComparison.Ordinal)
            ? signingSecret["whsec_".Length..]
            : signingSecret;

        var key = Convert.FromBase64String(secret);
        using var hmac = new HMACSHA256(key);
        var signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));

        return Convert.ToBase64String(signature);
    }

    private static IEnumerable<string> ParseSignatures(StringValues signatureHeader)
    {
        return signatureHeader
            .SelectMany(value => value?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [])
            .Select(value => value.Split(',', 2))
            .Where(parts => parts.Length == 2 && parts[0] == "v1")
            .Select(parts => parts[1]);
    }
}
