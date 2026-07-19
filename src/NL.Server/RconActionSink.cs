using NL.Core;
using NL.Server.Core;

namespace NL.Server;

/// <summary>
/// Minecraft (and any Source-RCON-compatible) action sink: Block on join → kick; Block on
/// anything else → tell. Command templates can be overridden for other RCON games.
/// </summary>
public sealed class RconActionSink : IGameActionSink
{
    private readonly RconClient _client;
    private readonly Func<SessionEvent, ActionResult, string?> _commandFactory;

    public RconActionSink(RconClient client, Func<SessionEvent, ActionResult, string?>? commandFactory = null)
    {
        _client = client;
        _commandFactory = commandFactory ?? DefaultMinecraftCommands;
    }

    public static string? DefaultMinecraftCommands(SessionEvent sessionEvent, ActionResult result)
    {
        if (sessionEvent.PlayerName is null)
        {
            return null;
        }

        var message = result.Message ?? "Action blocked by this session's NLEvents rules.";
        return sessionEvent.Event.Name == "playerJoin"
            ? $"kick {sessionEvent.PlayerName} {message}"
            : $"tell {sessionEvent.PlayerName} {message}";
    }

    /// <summary>
    /// Generic RCON command using placeholders: <c>{player}</c>, <c>{event}</c>,
    /// <c>{decision}</c>, <c>{message}</c>. Example: <c>say NL blocked {player} ({event})</c>.
    /// </summary>
    public static Func<SessionEvent, ActionResult, string?> Templated(string template) =>
        (session, result) => template
            .Replace("{player}", session.PlayerName ?? "", StringComparison.Ordinal)
            .Replace("{event}", session.Event.Name, StringComparison.Ordinal)
            .Replace("{decision}", result.Decision.ToString(), StringComparison.Ordinal)
            .Replace("{message}", result.Message ?? "", StringComparison.Ordinal);

    public async Task ApplyAsync(SessionEvent sessionEvent, ActionResult result, CancellationToken cancellationToken)
    {
        var command = _commandFactory(sessionEvent, result);
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        await _client.SendCommandAsync(command, cancellationToken);
    }

    public async ValueTask DisposeAsync() => await _client.DisposeAsync();
}
