using NL.Core;
using NL.Moderation;
using NL.Moderation.Core;

namespace NL.Moderation.Web;

/// <summary>Shared moderation service + data paths for the cross-platform web console.</summary>
public sealed class ModerationHostState
{
    public ModerationHostState(string? moderationLogPath = null, string? spStorePath = null)
    {
        NlPaths.EnsureRoot();
        ModerationLogPath = moderationLogPath ?? NlPaths.ModerationLog;
        SpStorePath = spStorePath ?? NlPaths.SpProfiles;
        Moderation = new ModerationService(
            new JsonlModerationStore(ModerationLogPath),
            new JsonFileSpProfileRepository(SpStorePath));
    }

    public ModerationService Moderation { get; }
    public string ModerationLogPath { get; }
    public string SpStorePath { get; }

    public object GetStatus() => new
    {
        dataRoot = NlPaths.Root,
        moderationLog = ModerationLogPath,
        spStore = SpStorePath,
    };
}

public sealed class ModerationActionRequest
{
    public string StreamerId { get; set; } = NlPaths.DefaultStreamerId;
    public string PlayerId { get; set; } = "";
    public string IssuedBy { get; set; } = "mod-web";
    public string? Reason { get; set; }
    public string? Game { get; set; }
}

public sealed class CreateProfileRequest
{
    public string PlayerId { get; set; } = "";
    public string? DisplayName { get; set; }
}
