namespace NL.Client;

public sealed class NlClientSettings
{
    public const string BaseUrlVariable = "NL_CLIENT_SESSION_URL";

    public string SessionBaseUrl { get; init; } = "http://127.0.0.1:27020";

    public string? OperatorKey { get; init; }

    public static NlClientSettings LoadFromEnvironment()
    {
        var baseUrl = Environment.GetEnvironmentVariable(BaseUrlVariable)
            ?? Environment.GetEnvironmentVariable("NL_PUBLIC_HTTP")
            ?? "http://127.0.0.1:27020";

        return new NlClientSettings
        {
            SessionBaseUrl = baseUrl.Trim().TrimEnd('/'),
            OperatorKey = Environment.GetEnvironmentVariable("NL_OPERATOR_KEY"),
        };
    }
}
