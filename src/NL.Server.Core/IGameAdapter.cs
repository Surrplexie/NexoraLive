namespace NL.Server.Core;

/// <summary>
/// One concrete game integration: how to turn that game's raw feed into
/// <see cref="SessionEvent"/>s and (optionally) how to send block decisions back.
/// Built-in adapters today: <c>minecraft</c> and <c>generic</c> (NDJSON). Any other game
/// can implement this interface or emit the generic NDJSON format with no C# changes.
/// </summary>
public interface IGameAdapter
{
    /// <summary>Stable id used on the CLI (<c>--game minecraft</c>, <c>--game generic</c>).</summary>
    string Id { get; }

    string DisplayName { get; }
}
