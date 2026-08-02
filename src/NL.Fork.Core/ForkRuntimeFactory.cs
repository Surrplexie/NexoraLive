namespace NL.Fork.Core;

public static class ForkRuntimeFactory
{
    public static IForkRuntimeDetails Create(
        ForkGameKind game,
        IForkDecisionSink decisions,
        ForkModManifest? mods = null,
        Func<string, Task<bool>>? admitAsync = null) =>
        game switch
        {
            ForkGameKind.Minecraft => new MinecraftForkRuntime(decisions, mods, admitAsync),
            ForkGameKind.Beamng => new BeamngForkRuntime(decisions, mods, admitAsync),
            _ => new HelloForkRuntime(decisions, mods, admitAsync),
        };

    public static IForkRuntimeDetails CreateEmbedded(
        ForkGameKind game,
        string nleSource,
        ForkModManifest? mods = null,
        NL.Server.Core.IJoinGate? joinGate = null)
    {
        var engine = NL.Core.RuleEngine.FromSource(nleSource);
        var sink = new RuleEngineForkDecisionSink(engine, joinGate);
        return Create(game, sink, mods);
    }
}
