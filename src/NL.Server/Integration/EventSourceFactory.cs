using NL.Server.Core;
using NL.Server.Core.Integration;

namespace NL.Server.Integration;

/// <summary>Result of creating an event source from <see cref="NlSessionOptions.SourcePath"/>.</summary>
public sealed class IntegrationBootstrap : IAsyncDisposable
{
    public IGameEventSource EventSource { get; }
    public IActiveActionChannelProvider? ActionChannelProvider { get; }
    private readonly IAsyncDisposable? _disposable;

    public IntegrationBootstrap(
        IGameEventSource eventSource,
        IActiveActionChannelProvider? actionChannelProvider = null,
        IAsyncDisposable? disposable = null)
    {
        EventSource = eventSource;
        ActionChannelProvider = actionChannelProvider;
        _disposable = disposable;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposable is not null)
        {
            await _disposable.DisposeAsync();
        }
    }
}

public static class EventSourceFactory
{
    public static IntegrationBootstrap Create(NlSessionOptions options, Action<string>? log = null)
    {
        if (!NlSourceUri.TryParse(options.SourcePath, out var uri) || uri is null)
        {
            throw new InvalidOperationException($"Invalid --source '{options.SourcePath}'.");
        }

        if (uri.Kind == NlSourceKind.File)
        {
            IGameEventSource source = options.Game.ToLowerInvariant() switch
            {
                "minecraft" => LineFileEventSource.Minecraft(options.SourcePath, options.Replay),
                "generic" => LineFileEventSource.GenericJson(options.SourcePath, options.Replay),
                _ => throw new InvalidOperationException($"Unknown game '{options.Game}'."),
            };
            return new IntegrationBootstrap(source);
        }

        if (options.Replay)
        {
            log?.Invoke("[nl] --replay ignored for tcp/ws sources (live listen only).");
        }

        if (options.Game != "generic")
        {
            throw new InvalidOperationException(
                "Network --source (tcp:// or ws://) requires --game generic.");
        }

        return uri.Kind switch
        {
            NlSourceKind.TcpListen => CreateTcp(uri.Host, uri.Port, log),
            NlSourceKind.WebSocketListen => CreateWebSocket(uri.Host, uri.Port, log, options.BusToken),
            _ => throw new InvalidOperationException($"Unsupported source URI '{options.SourcePath}'."),
        };
    }

    private static IntegrationBootstrap CreateTcp(string host, int port, Action<string>? log)
    {
        var listener = new NlTcpListenerEventSource(host, port, log);
        return new IntegrationBootstrap(listener, disposable: listener);
    }

    private static IntegrationBootstrap CreateWebSocket(string host, int port, Action<string>? log, string? busToken)
    {
        Func<string?, bool>? validate = null;
        if (!string.IsNullOrEmpty(busToken))
        {
            validate = token => string.Equals(token, busToken, StringComparison.Ordinal);
        }

        var listener = new NlWebSocketListenerEventSource(host, port, log, validate);
        return new IntegrationBootstrap(
            listener,
            actionChannelProvider: listener,
            disposable: listener);
    }
}
