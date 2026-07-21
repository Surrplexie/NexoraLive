namespace NL.Server.Integration;

/// <summary>Outbound action channel for bidirectional NL integration sessions.</summary>
public interface INlActionChannel
{
    Task SendLineAsync(string line, CancellationToken cancellationToken);
}

/// <summary>Transport that exposes the currently connected bridge for outbound actions.</summary>
public interface IActiveActionChannelProvider
{
    INlActionChannel? GetActiveActionChannel();
}
