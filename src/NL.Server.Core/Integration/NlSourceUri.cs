namespace NL.Server.Core.Integration;

public enum NlSourceKind
{
    File,
    TcpListen,
    WebSocketListen,
}

/// <summary>Parses --source and --nl-action URIs for NL integration transports.</summary>
public sealed class NlSourceUri
{
    public NlSourceKind Kind { get; init; }
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = NlIntegrationProtocol.DefaultEventPort;
    public string Raw { get; init; } = "";

    public static bool TryParse(string? value, out NlSourceUri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var raw = value.Trim();
        if (!raw.Contains("://", StringComparison.Ordinal))
        {
            uri = new NlSourceUri { Kind = NlSourceKind.File, Raw = raw };
            return true;
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        var host = parsed.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            host = "127.0.0.1";
        }

        var port = parsed.Port > 0 ? parsed.Port : NlIntegrationProtocol.DefaultEventPort;
        var scheme = parsed.Scheme.ToLowerInvariant();

        uri = scheme switch
        {
            "tcp" => new NlSourceUri
            {
                Kind = NlSourceKind.TcpListen,
                Host = host,
                Port = port,
                Raw = raw,
            },
            "ws" or "wss" => new NlSourceUri
            {
                Kind = NlSourceKind.WebSocketListen,
                Host = host,
                Port = port,
                Raw = raw,
            },
            "file" => new NlSourceUri { Kind = NlSourceKind.File, Raw = parsed.LocalPath },
            _ => null,
        };

        return uri is not null;
    }

    public static NlSourceUri ParseActionEndpoint(string value, int defaultPort = 0)
    {
        if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return new NlSourceUri { Kind = NlSourceKind.WebSocketListen, Raw = "auto" };
        }

        if (!TryParse(value, out var uri) || uri is null)
        {
            throw new FormatException($"Invalid NL action endpoint '{value}'. Use auto or tcp://host:port.");
        }

        if (uri.Kind != NlSourceKind.TcpListen)
        {
            throw new FormatException($"NL action endpoint must be tcp://host:port or auto, not '{value}'.");
        }

        if (defaultPort > 0 && uri.Port == NlIntegrationProtocol.DefaultEventPort)
        {
            uri = new NlSourceUri
            {
                Kind = uri.Kind,
                Host = uri.Host,
                Port = defaultPort,
                Raw = uri.Raw,
            };
        }

        return uri;
    }
}
