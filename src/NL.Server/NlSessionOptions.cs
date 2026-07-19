using NL.Core;
using NL.Core.Sp;

namespace NL.Server;

/// <summary>Everything needed to run one NLServer session (CLI or Session Host).</summary>
public sealed class NlSessionOptions
{
    public required string Game { get; init; }
    public required string ConfigPath { get; init; }
    public required string SourcePath { get; init; }
    public bool Replay { get; init; }
    public string? RconEndpoint { get; init; } // host:port:password
    public string? RconCommandTemplate { get; init; }
    public string? ActionCommand { get; init; }
    /// <summary>BeamNG bridge command UDP endpoint (<c>host:port</c>), default <c>127.0.0.1:27022</c>.</summary>
    public string? BeamngCommandEndpoint { get; init; }
    public string StreamerId { get; init; } = NlPaths.DefaultStreamerId;
    public string? ModerationLogPath { get; init; }
    public string? SpStorePath { get; init; }
    public string? JoinRequirementsPath { get; init; }
    public bool AntiCheat { get; init; }
    public bool JoinGate { get; init; }
    public bool AnomalyAutoMod { get; init; }
    public JoinRequirements? JoinRequirements { get; init; }
}

/// <summary>Persisted Session Host profile under <see cref="NlPaths.SessionProfile"/>.</summary>
public sealed class SessionProfileFile
{
    public string StreamerId { get; set; } = NlPaths.DefaultStreamerId;
    public string Game { get; set; } = "minecraft";
    public string ConfigPath { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string? RconEndpoint { get; set; }
    public string? BeamngCommandEndpoint { get; set; }
    public bool AntiCheat { get; set; } = true;
    public bool JoinGate { get; set; } = true;
    public bool AnomalyAutoMod { get; set; }
    public bool UseDefaultDataPaths { get; set; } = true;

    public NlSessionOptions ToSessionOptions(bool replay = false) => new()
    {
        Game = Game,
        ConfigPath = ConfigPath,
        SourcePath = SourcePath,
        Replay = replay,
        RconEndpoint = RconEndpoint,
        BeamngCommandEndpoint = BeamngCommandEndpoint,
        StreamerId = string.IsNullOrWhiteSpace(StreamerId) ? NlPaths.DefaultStreamerId : StreamerId,
        ModerationLogPath = UseDefaultDataPaths ? NlPaths.ModerationLog : null,
        SpStorePath = UseDefaultDataPaths ? NlPaths.SpProfiles : null,
        JoinRequirementsPath = UseDefaultDataPaths ? NlPaths.JoinRequirements : null,
        AntiCheat = AntiCheat,
        JoinGate = JoinGate,
        AnomalyAutoMod = AnomalyAutoMod,
    };
}
