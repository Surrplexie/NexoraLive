namespace NL.Fork.Core;

/// <summary>Extended fork runtime surface for status reporting and factory wiring.</summary>
public interface IForkRuntimeDetails : IForkRuntime
{
    ForkModManifest Mods { get; }
}
