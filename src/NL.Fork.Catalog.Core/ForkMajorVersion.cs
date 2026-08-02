namespace NL.Fork.Catalog.Core;

/// <summary>
/// Major-only version discipline — accepts <c>1.0</c>, <c>2.0</c>, rejects patch minors like
/// <c>1.2</c> / <c>1.4</c> (ROADMAP Phase N).
/// </summary>
public static class ForkMajorVersion
{
    public static bool TryNormalize(string? raw, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim().TrimStart('v', 'V');
        var parts = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1 && int.TryParse(parts[0], out var majorOnly))
        {
            normalized = $"{majorOnly}.0";
            return true;
        }

        if (parts.Length == 2
            && int.TryParse(parts[0], out var major)
            && int.TryParse(parts[1], out var minor)
            && minor == 0)
        {
            normalized = $"{major}.0";
            return true;
        }

        return false;
    }

    public static bool IsMajorOnly(string? raw) => TryNormalize(raw, out _);

    public static string NormalizeOrThrow(string raw)
    {
        if (!TryNormalize(raw, out var normalized))
        {
            throw new ArgumentException(
                $"Version '{raw}' is not a major version — only X.0 rows are allowed (e.g. 1.0, 2.0).",
                nameof(raw));
        }

        return normalized;
    }
}
