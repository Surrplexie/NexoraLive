namespace NL.Server;

/// <summary>Phase K — public demo hardening (rate limits, WS caps, session bounds).</summary>
public sealed class NlHardeningSettings
{
    public const string EnabledVariable = "NL_HARDENING";
    public const string AdmitRateVariable = "NL_ADMIT_RATE_PER_MIN";
    public const string PublicReadRateVariable = "NL_PUBLIC_READ_RATE_PER_MIN";
    public const string WsMaxConnectionsVariable = "NL_WS_MAX_CONNECTIONS";
    public const string WsMaxPerIpVariable = "NL_WS_MAX_CONNECTIONS_PER_IP";
    public const string WsConnectRateVariable = "NL_WS_CONNECT_RATE_PER_MIN";
    public const string DemoSessionMaxHoursVariable = "NL_DEMO_SESSION_MAX_HOURS";
    public const string EditorEvaluateRateVariable = "NL_EDITOR_EVALUATE_RATE_PER_MIN";

    public bool Enabled { get; init; }

    public int AdmitRatePerMinute { get; init; } = 120;

    public int PublicReadRatePerMinute { get; init; } = 300;

    public int EditorEvaluateRatePerMinute { get; init; } = 120;

    public int WebSocketMaxConnections { get; init; } = 16;

    public int WebSocketMaxConnectionsPerIp { get; init; } = 4;

    public int WebSocketConnectRatePerMinute { get; init; } = 30;

    /// <summary>Zero disables demo session auto-restart on max duration.</summary>
    public TimeSpan DemoSessionMaxDuration { get; init; } = TimeSpan.FromHours(12);

    public static NlHardeningSettings LoadFromEnvironment(bool publicMode = false)
    {
        var enabledRaw = Environment.GetEnvironmentVariable(EnabledVariable);
        var enabled = enabledRaw is null
            ? publicMode
            : ParseBool(enabledRaw);

        var admit = ParseInt(Environment.GetEnvironmentVariable(AdmitRateVariable), 120);
        var read = ParseInt(Environment.GetEnvironmentVariable(PublicReadRateVariable), 300);
        var wsMax = ParseInt(Environment.GetEnvironmentVariable(WsMaxConnectionsVariable), 16);
        var wsPerIp = ParseInt(Environment.GetEnvironmentVariable(WsMaxPerIpVariable), 4);
        var wsRate = ParseInt(Environment.GetEnvironmentVariable(WsConnectRateVariable), 30);
        var maxHours = ParseInt(Environment.GetEnvironmentVariable(DemoSessionMaxHoursVariable), 12);
        var editorEval = ParseInt(Environment.GetEnvironmentVariable(EditorEvaluateRateVariable), 120);

        return new NlHardeningSettings
        {
            Enabled = enabled,
            AdmitRatePerMinute = Math.Max(1, admit),
            PublicReadRatePerMinute = Math.Max(10, read),
            EditorEvaluateRatePerMinute = Math.Max(10, editorEval),
            WebSocketMaxConnections = Math.Max(1, wsMax),
            WebSocketMaxConnectionsPerIp = Math.Max(1, wsPerIp),
            WebSocketConnectRatePerMinute = Math.Max(1, wsRate),
            DemoSessionMaxDuration = maxHours > 0 ? TimeSpan.FromHours(maxHours) : TimeSpan.Zero,
        };
    }

    public object ToPublicInfo() => new
    {
        enabled = Enabled,
        admitRatePerMinute = AdmitRatePerMinute,
        publicReadRatePerMinute = PublicReadRatePerMinute,
        editorEvaluateRatePerMinute = EditorEvaluateRatePerMinute,
        webSocketMaxConnections = WebSocketMaxConnections,
        webSocketMaxConnectionsPerIp = WebSocketMaxConnectionsPerIp,
        webSocketConnectRatePerMinute = WebSocketConnectRatePerMinute,
        demoSessionMaxHours = DemoSessionMaxDuration.TotalHours,
    };

    private static bool ParseBool(string value) =>
        value.Equals("1", StringComparison.OrdinalIgnoreCase)
        || value.Equals("true", StringComparison.OrdinalIgnoreCase)
        || value.Equals("yes", StringComparison.OrdinalIgnoreCase);

    private static int ParseInt(string? value, int defaultValue) =>
        int.TryParse(value, out var parsed) ? parsed : defaultValue;
}
