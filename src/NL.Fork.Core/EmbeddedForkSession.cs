using NL.Core;
using NL.Server.Core;

namespace NL.Fork.Core;

/// <summary>
/// Runs a game fork runtime against an in-process <see cref="RuleEngine"/> for tests
/// and local validation without a session bus.
/// </summary>
public sealed class EmbeddedForkSession
{
    public IForkRuntimeDetails Runtime { get; }

    public RuleEngine Engine { get; }

    public ForkGameKind Game { get; }

    public EmbeddedForkSession(
        string nleSource,
        ForkModManifest? mods = null,
        IJoinGate? joinGate = null,
        ForkGameKind game = ForkGameKind.Hello)
    {
        Game = game;
        Engine = RuleEngine.FromSource(nleSource);
        Runtime = ForkRuntimeFactory.CreateEmbedded(game, nleSource, mods, joinGate);
    }

    public EmbeddedForkSession(
        RuleEngine engine,
        ForkModManifest? mods = null,
        IJoinGate? joinGate = null,
        ForkGameKind game = ForkGameKind.Hello)
    {
        Game = game;
        Engine = engine;
        var sink = new RuleEngineForkDecisionSink(engine, joinGate);
        Runtime = ForkRuntimeFactory.Create(game, sink, mods);
    }

    /// <summary>Backward-compatible accessor for hello-fork tests.</summary>
    public HelloForkRuntime HelloRuntime => Runtime as HelloForkRuntime
        ?? throw new InvalidOperationException("Runtime is not HelloForkRuntime.");
}
