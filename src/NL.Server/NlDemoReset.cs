using NL.Core;
using NL.Moderation;

namespace NL.Server;

/// <summary>Clears demo moderation/SP data so the public loop does not accumulate forever.</summary>
public static class NlDemoReset
{
    public static void ResetDataFiles(string? moderationLogPath = null, string? spStorePath = null)
    {
        NlPaths.EnsureRoot();
        var moderationLog = moderationLogPath ?? NlPaths.ModerationLog;
        var spStore = spStorePath ?? NlPaths.SpProfiles;

        var modDir = Path.GetDirectoryName(moderationLog);
        if (!string.IsNullOrEmpty(modDir))
        {
            Directory.CreateDirectory(modDir);
        }

        var spDir = Path.GetDirectoryName(spStore);
        if (!string.IsNullOrEmpty(spDir))
        {
            Directory.CreateDirectory(spDir);
        }

        File.WriteAllText(moderationLog, "");
        File.WriteAllText(spStore, "[]");
    }

    public static void ResetAndReload(ModerationHostState moderation)
    {
        ResetDataFiles(moderation.ModerationLogPath, moderation.SpStorePath);
        moderation.ReloadStores();
    }
}
