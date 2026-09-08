using NL.Core;
using NL.Server.Core;
using NL.Server.Core.Integration;

namespace NL.Server.Integration;

/// <summary>Sends standard NL action NDJSON lines over an <see cref="INlActionChannel"/>.</summary>
public sealed class NlIntegrationActionSink : IGameActionSink
{
    private readonly Func<INlActionChannel?> _channelFactory;
    private readonly Action<string>? _log;

    public NlIntegrationActionSink(INlActionChannel channel, Action<string>? log = null)
        : this(() => channel, log)
    {
    }

    public NlIntegrationActionSink(Func<INlActionChannel?> channelFactory, Action<string>? log = null)
    {
        _channelFactory = channelFactory;
        _log = log;
    }

    public async Task ApplyAsync(SessionEvent sessionEvent, ActionResult result, CancellationToken cancellationToken)
    {
        var channel = _channelFactory();
        if (channel is null)
        {
            _log?.Invoke(
                $"  [nl action] skipped (no bridge connected) for {sessionEvent.PlayerName ?? "?"}/{sessionEvent.Event.Name}");
            return;
        }

        var line = NlActionEnvelope.Serialize(sessionEvent, result);
        await channel.SendLineAsync(line, cancellationToken);
        _log?.Invoke(
            $"  [nl action] {NlStandardActions.ChooseAction(sessionEvent)} → {sessionEvent.PlayerName ?? "?"}/{sessionEvent.Event.Name}: {result.Message ?? ""}");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>Connects to a bridge-hosted TCP action listener and sends action lines.</summary>
public sealed class NlTcpClientActionSink : IGameActionSink, IAsyncDisposable
{
    private readonly NlTcpClientActionChannel _channel;
    private readonly NlIntegrationActionSink _inner;

    private NlTcpClientActionSink(NlTcpClientActionChannel channel, Action<string>? log)
    {
        _channel = channel;
        _inner = new NlIntegrationActionSink(channel, log);
    }

    public static async Task<NlTcpClientActionSink> ConnectAsync(
        string host,
        int port,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        var channel = await NlTcpClientActionChannel.ConnectAsync(host, port, cancellationToken);
        return new NlTcpClientActionSink(channel, log);
    }

    public Task ApplyAsync(SessionEvent sessionEvent, ActionResult result, CancellationToken cancellationToken) =>
        _inner.ApplyAsync(sessionEvent, result, cancellationToken);

    public async ValueTask DisposeAsync() => await _channel.DisposeAsync();
}
