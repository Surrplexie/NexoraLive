namespace NL.Core.Security;

/// <summary>
/// Demo/public deployment security settings (Phase E). Loaded from environment at startup.
/// </summary>
public sealed class NlSecuritySettings
{
    public const string PublicModeVariable = "NL_PUBLIC_MODE";
    public const string OperatorKeyVariable = "NL_OPERATOR_KEY";
    public const string BusTokenVariable = "NL_BUS_TOKEN";
    public const string CorsOriginsVariable = "NL_CORS_ORIGINS";
    public const string BindVariable = "NL_BIND";

    public bool PublicMode { get; init; }

    public string? OperatorKey { get; init; }

    public string? BusToken { get; init; }

    public IReadOnlyList<string> CorsOrigins { get; init; } = Array.Empty<string>();

    public string BindHost { get; init; } = "127.0.0.1";

    /// <summary>When true, write endpoints require a valid operator key.</summary>
    public bool RequireOperatorAuth => PublicMode || !string.IsNullOrEmpty(OperatorKey);

    public static NlSecuritySettings LoadFromEnvironment()
    {
        var bind = Environment.GetEnvironmentVariable(BindVariable) ?? "127.0.0.1";
        var publicMode = ParseBool(Environment.GetEnvironmentVariable(PublicModeVariable));
        var operatorKey = NullIfEmpty(Environment.GetEnvironmentVariable(OperatorKeyVariable));
        var busToken = NullIfEmpty(Environment.GetEnvironmentVariable(BusTokenVariable));
        var corsRaw = NullIfEmpty(Environment.GetEnvironmentVariable(CorsOriginsVariable));

        if (publicMode && string.IsNullOrEmpty(busToken))
        {
            throw new InvalidOperationException(
                $"{BusTokenVariable} is required when {PublicModeVariable} is enabled. " +
                "Set a fixed token before exposing the session server publicly.");
        }

        if (publicMode && string.IsNullOrEmpty(operatorKey))
        {
            throw new InvalidOperationException(
                $"{OperatorKeyVariable} is required when {PublicModeVariable} is enabled. " +
                "Set an operator API key for moderation and session control.");
        }

        return new NlSecuritySettings
        {
            PublicMode = publicMode,
            OperatorKey = operatorKey,
            BusToken = busToken,
            CorsOrigins = ParseCorsOrigins(corsRaw, publicMode),
            BindHost = bind,
        };
    }

    /// <summary>Resolves the session bus token. Public mode requires a configured token.</summary>
    public static string ResolveBusToken(NlSecuritySettings settings)
    {
        if (!string.IsNullOrEmpty(settings.BusToken))
        {
            return settings.BusToken!;
        }

        return Guid.NewGuid().ToString("N");
    }

    public object ToPublicInfo() => new
    {
        publicMode = PublicMode,
        operatorAuthRequired = RequireOperatorAuth,
        busTokenConfigured = !string.IsNullOrEmpty(BusToken),
        corsConfigured = CorsOrigins.Count > 0,
        corsOrigins = CorsOrigins,
    };

    private static IReadOnlyList<string> ParseCorsOrigins(string? raw, bool publicMode)
    {
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(o => o.Length > 0)
                .ToArray();
        }

        if (!publicMode)
        {
            return Array.Empty<string>();
        }

        var publicHttp = NullIfEmpty(Environment.GetEnvironmentVariable("NL_PUBLIC_HTTP"));
        if (publicHttp is not null)
        {
            return new[] { publicHttp.Trim().TrimEnd('/') };
        }

        return Array.Empty<string>();
    }

    private static bool ParseBool(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && (value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase));

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
