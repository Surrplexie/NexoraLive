using NL.Core;
using NL.Server.Core;

namespace NL.Fork.Core;

/// <summary>
/// Runs <see cref="HelloForkRuntime"/> against an in-process <see cref="RuleEngine"/> for tests
/// and local validation without a session bus.
/// </summary>
public sealed class EmbeddedForkSession
{
    public HelloForkRuntime Runtime { get; }

    public RuleEngine Engine { get; }

    public EmbeddedForkSession(string nleSource, ForkModManifest? mods = null, IJoinGate? joinGate = null)
    {
        Engine = RuleEngine.FromSource(nleSource);
        var sink = new RuleEngineForkDecisionSink(Engine, joinGate);
        Runtime = new HelloForkRuntime(sink, mods);
    }

    public EmbeddedForkSession(RuleEngine engine, ForkModManifest? mods = null, IJoinGate? joinGate = null)
    {
        Engine = engine;
        var sink = new RuleEngineForkDecisionSink(engine, joinGate);
        Runtime = new HelloForkRuntime(sink, mods);
    }
}
