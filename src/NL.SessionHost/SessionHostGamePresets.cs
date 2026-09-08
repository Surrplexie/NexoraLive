namespace NL.SessionHost;

/// <summary>Session Host UI presets — maps friendly game names to NL.Server profile fields.</summary>
internal enum SessionHostGamePreset
{
    Minecraft,
    BeamNg,
    Generic,
}

internal enum SessionHostActionField
{
    Rcon,
    BeamngUdp,
    NlAction,
}

internal sealed class SessionHostGamePresetInfo
{
    public required SessionHostGamePreset Preset { get; init; }
    public required string DisplayName { get; init; }
    /// <summary>Value passed to NL.Server <c>--game</c>.</summary>
    public required string EngineGame { get; init; }
    public required string ActionLabel { get; init; }
    public required string ActionPlaceholder { get; init; }
    public required SessionHostActionField ActionField { get; init; }
    public string? DefaultActionValue { get; init; }
    public bool ShowAdvancedAction { get; init; }
    public bool DefaultJoinGate { get; init; }
    public bool DefaultAnomalyAutoMod { get; init; }

    public static IReadOnlyList<SessionHostGamePresetInfo> All { get; } =
    [
        new()
        {
            Preset = SessionHostGamePreset.Minecraft,
            DisplayName = "Minecraft (Java)",
            EngineGame = "minecraft",
            ActionLabel = "RCON port",
            ActionPlaceholder = "127.0.0.1:25575:your-password",
            ActionField = SessionHostActionField.Rcon,
            ShowAdvancedAction = false,
            DefaultJoinGate = true,
            DefaultAnomalyAutoMod = false,
        },
        new()
        {
            Preset = SessionHostGamePreset.BeamNg,
            DisplayName = "BeamNG.drive",
            EngineGame = "generic",
            ActionLabel = "Action UDP port",
            ActionPlaceholder = "127.0.0.1:27022",
            ActionField = SessionHostActionField.BeamngUdp,
            DefaultActionValue = "127.0.0.1:27022",
            ShowAdvancedAction = false,
            DefaultJoinGate = false,
            DefaultAnomalyAutoMod = false,
        },
        new()
        {
            Preset = SessionHostGamePreset.Generic,
            DisplayName = "Generic / other (NDJSON)",
            EngineGame = "generic",
            ActionLabel = "Action channel (optional)",
            ActionPlaceholder = "auto, tcp://host:port, or leave empty for dry-run",
            ActionField = SessionHostActionField.NlAction,
            ShowAdvancedAction = true,
            DefaultJoinGate = false,
            DefaultAnomalyAutoMod = false,
        },
    ];

    public static SessionHostGamePresetInfo Get(SessionHostGamePreset preset) =>
        All.First(p => p.Preset == preset);

    public static SessionHostGamePreset InferFromProfile(string game, string? rcon, string? beamng, string? nlAction)
    {
        if (string.Equals(game, "minecraft", StringComparison.OrdinalIgnoreCase))
        {
            return SessionHostGamePreset.Minecraft;
        }

        if (!string.IsNullOrWhiteSpace(beamng))
        {
            return SessionHostGamePreset.BeamNg;
        }

        if (string.Equals(game, "generic", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(nlAction))
        {
            // Generic game with no NL action often means BeamNG-style NDJSON file follow.
            return SessionHostGamePreset.BeamNg;
        }

        return SessionHostGamePreset.Generic;
    }
}
