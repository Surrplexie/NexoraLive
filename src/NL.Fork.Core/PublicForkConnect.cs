namespace NL.Fork.Core;

/// <summary>
/// Phase T2-lite: rewrite loopback game connect URIs to a public hostname
/// so VPS manifests never hand viewers <c>127.0.0.1</c>.
/// Native TCP (RimWorld / Minecraft) is not replaced by the fleet WSS relay.
/// </summary>
public static class PublicForkConnect
{
    public const string HostVariable = "NL_FORK_PUBLIC_CONNECT_HOST";
    public const string PortVariable = "NL_FORK_PUBLIC_CONNECT_PORT";

    public static readonly HashSet<string> NativeSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "rimworld",
        "kenshi",
        "minecraft",
        "beamng-sidecar",
    };

    public static bool IsNativeGameConnect(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || !TrySplit(endpoint, out var scheme, out _, out _, out _))
        {
            return false;
        }

        return NativeSchemes.Contains(scheme);
    }

    public static string? ResolvePublicHost()
    {
        var explicitHost = StripHost(Environment.GetEnvironmentVariable(HostVariable));
        if (!string.IsNullOrWhiteSpace(explicitHost))
        {
            return explicitHost;
        }

        var publicHost = StripHost(Environment.GetEnvironmentVariable("NL_PUBLIC_HOST"));
        if (!string.IsNullOrWhiteSpace(publicHost) && !IsLoopbackHost(publicHost))
        {
            return publicHost;
        }

        var vps = StripHost(Environment.GetEnvironmentVariable("NL_VPS_DOMAIN"));
        if (!string.IsNullOrWhiteSpace(vps) && !IsLoopbackHost(vps))
        {
            return vps;
        }

        var baseUrl = Environment.GetEnvironmentVariable("NL_PUBLIC_BASE_URL")
            ?? Environment.GetEnvironmentVariable("NL_PUBLIC_HTTP");
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            && !string.IsNullOrWhiteSpace(uri.Host)
            && !IsLoopbackHost(uri.Host))
        {
            return uri.Host;
        }

        return null;
    }

    public static bool IsLoopbackHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return true;
        }

        var h = host.Trim().Trim('[', ']');
        return h is "127.0.0.1" or "::1" or "localhost" or "0.0.0.0" or "+" or "*";
    }

    public static bool IsPrivateOrLoopbackEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return true;
        }

        if (endpoint.StartsWith("docker://", StringComparison.OrdinalIgnoreCase)
            || endpoint.StartsWith("process://localhost", StringComparison.OrdinalIgnoreCase)
            || endpoint.StartsWith("k8s://", StringComparison.OrdinalIgnoreCase)
            || endpoint.StartsWith("mock://", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!TrySplit(endpoint, out _, out var host, out _, out _))
        {
            return true;
        }

        return IsLoopbackHost(host);
    }

    /// <summary>
    /// Replace loopback host on native game URIs when a public host is configured.
    /// Non-native URIs are returned unchanged (fleet WSS mask still applies).
    /// </summary>
    public static string RewriteForPublic(string? endpoint, string? publicHost = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return endpoint ?? "";
        }

        if (!IsNativeGameConnect(endpoint) || !TrySplit(endpoint, out var scheme, out var host, out var port, out var rest))
        {
            return endpoint;
        }

        var target = StripHost(publicHost) ?? ResolvePublicHost();
        if (string.IsNullOrWhiteSpace(target) || !IsLoopbackHost(host))
        {
            return endpoint;
        }

        var portOverride = Environment.GetEnvironmentVariable(PortVariable);
        var usePort = int.TryParse(portOverride, out var parsed) && parsed > 0 ? parsed : port;
        var portPart = usePort is > 0 ? $":{usePort}" : "";
        return $"{scheme}://{target}{portPart}{rest}";
    }

    public static bool IsT2LiteReady(string? sampleNativeEndpoint = null)
    {
        var host = ResolvePublicHost();
        if (string.IsNullOrWhiteSpace(host) || IsLoopbackHost(host))
        {
            return false;
        }

        var sample = sampleNativeEndpoint ?? "rimworld://127.0.0.1:25555";
        var published = RewriteForPublic(sample, host);
        return !IsPrivateOrLoopbackEndpoint(published);
    }

    public static object Status()
    {
        var host = ResolvePublicHost();
        var sample = RewriteForPublic("rimworld://127.0.0.1:25555", host);
        return new
        {
            phase = "T2-lite",
            publicConnectHost = host,
            ready = IsT2LiteReady(),
            rimworldExample = sample,
            note = "Native TCP (rimworld://host:25555). Open 25555/tcp on the VPS. One RimWorld fork per host port.",
        };
    }

    private static string? StripHost(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var t = raw.Trim();
        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Uri.TryCreate(t, UriKind.Absolute, out var uri) ? uri.Host : t;
        }

        return t.TrimEnd('/');
    }

    private static bool TrySplit(
        string endpoint,
        out string scheme,
        out string host,
        out int port,
        out string rest)
    {
        scheme = "";
        host = "";
        port = -1;
        rest = "";
        var sep = endpoint.IndexOf("://", StringComparison.Ordinal);
        if (sep <= 0)
        {
            return false;
        }

        scheme = endpoint[..sep];
        var after = endpoint[(sep + 3)..];
        var slash = after.IndexOf('/');
        var authority = slash >= 0 ? after[..slash] : after;
        rest = slash >= 0 ? after[slash..] : "";
        var colon = authority.LastIndexOf(':');
        if (colon > 0 && int.TryParse(authority[(colon + 1)..], out var p))
        {
            host = authority[..colon];
            port = p;
        }
        else
        {
            host = authority;
        }

        return !string.IsNullOrWhiteSpace(scheme) && !string.IsNullOrWhiteSpace(host);
    }
}
