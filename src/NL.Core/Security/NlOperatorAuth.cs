using System.Security.Cryptography;
using System.Text;

namespace NL.Core.Security;

/// <summary>Validates operator API keys for protected REST endpoints.</summary>
public static class NlOperatorAuth
{
    public const string HeaderName = "X-NL-Operator-Key";

    public static bool IsAuthorized(NlSecuritySettings settings, string? providedKey)
    {
        if (!settings.RequireOperatorAuth)
        {
            return true;
        }

        if (string.IsNullOrEmpty(settings.OperatorKey) || string.IsNullOrEmpty(providedKey))
        {
            return false;
        }

        return FixedTimeEquals(settings.OperatorKey, providedKey);
    }

    public static bool IsAuthorized(NlSecuritySettings settings, IEnumerable<string?> headerValues, string? authorizationHeader)
    {
        var provided = headerValues.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        if (string.IsNullOrWhiteSpace(provided)
            && !string.IsNullOrWhiteSpace(authorizationHeader)
            && authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            provided = authorizationHeader["Bearer ".Length..].Trim();
        }

        return IsAuthorized(settings, provided);
    }

    private static bool FixedTimeEquals(string expected, string provided)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return expectedBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
