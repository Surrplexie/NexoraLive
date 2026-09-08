using System.Net;

namespace NL.Server.Core.Integration;

/// <summary>Normalizes bind hosts for network listeners (Docker / LAN vs localhost-only).</summary>
public static class NlListenHost
{
    /// <summary>TCP listen address: loopback for explicit localhost; any interface for wildcard binds.</summary>
    public static IPAddress ResolveTcpAddress(string host) =>
        host switch
        {
            "localhost" or "127.0.0.1" => IPAddress.Loopback,
            "*" or "+" or "0.0.0.0" => IPAddress.Any,
            _ => IPAddress.Parse(host),
        };

    /// <summary><see cref="HttpListener"/> prefix host segment.</summary>
    public static string ResolveHttpListenerHost(string host) =>
        host switch
        {
            "localhost" or "127.0.0.1" => "127.0.0.1",
            "*" or "+" or "0.0.0.0" => "+",
            _ => host,
        };

    public static bool IsAllInterfaces(string host) =>
        host is "*" or "+" or "0.0.0.0";
}
