namespace NL.Core.Security;

/// <summary>REST paths that mutate session or moderation state (Phase E operator auth).</summary>
public static class NlSecurityPaths
{
    private static readonly HashSet<(string Method, string Path)> OperatorProtected = new(StringTupleComparer.Instance)
    {
        ("PUT", "/api/v1/session/profile"),
        ("POST", "/api/v1/session/bus-defaults"),
        ("POST", "/api/v1/session/start"),
        ("POST", "/api/v1/session/stop"),
        ("POST", "/api/v1/moderation/profiles"),
        ("POST", "/api/v1/moderation/warning"),
        ("POST", "/api/v1/moderation/ban"),
        ("POST", "/api/v1/moderation/graylist"),
        ("POST", "/api/v1/moderation/clear"),
        ("PUT", "/api/v1/editor/config"),
        ("POST", "/api/v1/editor/apply"),
        ("POST", "/api/v1/editor/reset"),
    };

    public static bool RequiresOperatorAuth(string method, string? path)
    {
        var normalized = NormalizePath(path);
        return OperatorProtected.Contains((method.ToUpperInvariant(), normalized));
    }

    internal static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var trimmed = path.Trim();
        if (trimmed.Length > 1 && trimmed.EndsWith('/'))
        {
            trimmed = trimmed.TrimEnd('/');
        }

        return trimmed.Length == 0 ? "/" : trimmed;
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string Method, string Path)>
    {
        public static StringTupleComparer Instance { get; } = new();

        public bool Equals((string Method, string Path) x, (string Method, string Path) y) =>
            string.Equals(x.Method, y.Method, StringComparison.Ordinal)
            && string.Equals(x.Path, y.Path, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Method, string Path) obj) =>
            HashCode.Combine(
                obj.Method.GetHashCode(StringComparison.Ordinal),
                obj.Path.GetHashCode(StringComparison.OrdinalIgnoreCase));
    }
}
