namespace NL.Fork.Orchestrator;

public static class NlForkOrchestratorPaths
{
    public static string Root
    {
        get
        {
            var overrideRoot = Environment.GetEnvironmentVariable("NL_FORK_ORCHESTRATOR_ROOT");
            if (!string.IsNullOrWhiteSpace(overrideRoot))
            {
                return Path.GetFullPath(overrideRoot);
            }

            return Path.Combine(NL.Core.NlPaths.Root, "fork-orchestrator");
        }
    }

    public static string Sessions => Path.Combine(Root, "sessions");

    public static string Store =>
        Environment.GetEnvironmentVariable("NL_FORK_ORCHESTRATOR_STORE")
        ?? Path.Combine(Root, "active-sessions.json");

    public static string Audit =>
        Path.Combine(Root, "orchestrator-audit.jsonl");

    public static string SessionWorkspace(string sessionId) =>
        Path.Combine(Sessions, sessionId);

    public static void EnsureRoot()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Sessions);
    }
}
