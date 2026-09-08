using System.Text.Json;
using NL.Core;
using NL.Fork.Orchestrator.Core;

namespace NL.Fork.Orchestrator;

/// <summary>
/// Prepares an ephemeral workspace: copies .nle + join requirements; world/ is wiped on destroy.
/// Moderation JSONL and SP profiles remain in shared <see cref="NlPaths.Root"/>.
/// </summary>
public static class ForkSessionWorkspace
{
    public static ForkSessionWorkspaceLayout Prepare(
        string sessionId,
        string nleSourcePath,
        string modsJsonPath)
    {
        NlForkOrchestratorPaths.EnsureRoot();
        var root = NlForkOrchestratorPaths.SessionWorkspace(sessionId);
        var world = Path.Combine(root, "world");
        Directory.CreateDirectory(world);

        var rules = Path.Combine(root, "rules.nle");
        if (File.Exists(nleSourcePath))
        {
            File.Copy(nleSourcePath, rules, overwrite: true);
        }
        else
        {
            File.WriteAllText(rules, "# missing nle source\n");
        }

        var modsDest = Path.Combine(root, "mods.json");
        if (File.Exists(modsJsonPath))
        {
            File.Copy(modsJsonPath, modsDest, overwrite: true);
        }
        else
        {
            File.WriteAllText(modsDest, """{"mods":[]}""");
        }

        InjectSharedConfig(root);

        var layout = new ForkSessionWorkspaceLayout(
            root,
            rules,
            modsDest,
            world,
            Path.Combine(root, "fork-status.json"),
            Path.Combine(root, "session-meta.json"));

        File.WriteAllText(layout.SessionMetaPath, JsonSerializer.Serialize(new
        {
            sessionId,
            createdAtUtc = DateTimeOffset.UtcNow,
            dataRoot = NlPaths.Root,
            ephemeralWorld = world,
            noProgressTransfer = true,
        }, new JsonSerializerOptions { WriteIndented = true }));

        return layout;
    }

    public static void Destroy(string workspacePath)
    {
        if (!Directory.Exists(workspacePath))
        {
            return;
        }

        var world = Path.Combine(workspacePath, "world");
        if (Directory.Exists(world))
        {
            Directory.Delete(world, recursive: true);
        }

        try
        {
            Directory.Delete(workspacePath, recursive: true);
        }
        catch
        {
            // best-effort on Windows file locks
        }
    }

    private static void InjectSharedConfig(string workspaceRoot)
    {
        CopyIfExists(NlPaths.JoinRequirements, Path.Combine(workspaceRoot, "join-requirements.json"));
        WritePointer(NlPaths.ModerationLog, Path.Combine(workspaceRoot, "moderation-log.path"));
        WritePointer(NlPaths.SpProfiles, Path.Combine(workspaceRoot, "sp-profiles.path"));
        WritePointer(NlPaths.SessionProfile, Path.Combine(workspaceRoot, "session-profile.path"));
    }

    private static void CopyIfExists(string source, string dest)
    {
        if (File.Exists(source))
        {
            File.Copy(source, dest, overwrite: true);
        }
    }

    private static void WritePointer(string target, string pointerFile) =>
        File.WriteAllText(pointerFile, target);
}
