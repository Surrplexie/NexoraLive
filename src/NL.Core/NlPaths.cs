namespace NL.Core;

/// <summary>
/// Canonical on-disk locations under <c>%LOCALAPPDATA%\NL\</c> so the Hotkey Daemon,
/// Config Editor, NLServer, Moderation Console, and Session Host all share one streamer
/// identity store (SP profiles, moderation audit log, join requirements, session profiles).
/// </summary>
public static class NlPaths
{
    public static string Root =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NL");

    public static string ModerationLog => Path.Combine(Root, "moderation.jsonl");

    public static string SpProfiles => Path.Combine(Root, "sp-profiles.json");

    public static string JoinRequirements => Path.Combine(Root, "join-requirements.json");

    public static string HotkeysNle => Path.Combine(Root, "hotkeys.nle");

    public static string SessionProfile => Path.Combine(Root, "session-profile.json");

    /// <summary>NDJSON event stream written by the BeamNG.drive NL bridge Lua mod.</summary>
    public static string BeamngEvents => Path.Combine(Root, "beamng-events.ndjson");

    /// <summary>Kick requests queued by the BeamNG bridge for the BeamMP <c>NL_Kick</c> server plugin.</summary>
    public static string BeamngKicks => Path.Combine(Root, "beamng-kicks.ndjson");

    /// <summary>Default localhost UDP port the BeamNG bridge listens on for Block actions.</summary>
    public const int BeamngCommandPort = 27022;

    public static string DefaultStreamerId => "default-streamer";

    public static void EnsureRoot() => Directory.CreateDirectory(Root);
}
