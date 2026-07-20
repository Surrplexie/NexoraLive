using System.Text.Json;
using NL.AntiCheat.Core;
using NL.Core;
using NL.Core.Sp;
using NL.Moderation;
using NL.Moderation.Core;
using NL.Server.Core;

namespace NL.Server;

/// <summary>
/// Runs one wired NLServer session (join gate + anti-cheat + moderation + optional anomaly
/// auto-mod). Shared by the <c>NL.Server</c> CLI and <c>NL.SessionHost</c> WinForms shell.
/// </summary>
public sealed class NlSessionRunner
{
    private static readonly JsonSerializerOptions ProfileJson = new() { WriteIndented = true };

    public required NlSessionOptions Options { get; init; }

    public Action<string>? Log { get; init; }

    public NlServerHost? Host { get; private set; }

    public ModerationService? Moderation { get; private set; }

    public static SessionProfileFile LoadProfile(string path)
    {
        if (!File.Exists(path))
        {
            return new SessionProfileFile();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SessionProfileFile>(json, ProfileJson) ?? new SessionProfileFile();
    }

    public static void SaveProfile(string path, SessionProfileFile profile)
    {
        NlPaths.EnsureRoot();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(profile, ProfileJson));
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var o = Options;
        Write($"Game:   {o.Game}");
        Write($"Config: {o.ConfigPath}");
        Write($"Source: {o.SourcePath} ({(o.Replay ? "replay" : "live follow")})");
        Write($"Streamer: {o.StreamerId}");

        RuleEngine engine;
        try
        {
            engine = RuleEngine.FromSource(File.ReadAllText(o.ConfigPath));
        }
        catch (NlSyntaxException ex)
        {
            Write($"Failed to load config: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Write($"Failed to read config: {ex.Message}");
            return 1;
        }

        foreach (var warning in engine.LoadWarnings)
        {
            Write($"[load warning] {warning}");
        }

        IGameEventSource source = o.Game.ToLowerInvariant() switch
        {
            "minecraft" => LineFileEventSource.Minecraft(o.SourcePath, o.Replay),
            "generic" => LineFileEventSource.GenericJson(o.SourcePath, o.Replay),
            _ => throw new InvalidOperationException($"Unknown game '{o.Game}'."),
        };

        if (o.AntiCheat)
        {
            var thresholds = o.AnomalyThresholds
                ?? (o.BeamngCommandEndpoint is not null ? AnomalyThresholds.BeamNgFreeroam : null);
            var pipeline = thresholds is null
                ? AnomalyPipeline.CreateDefault()
                : AnomalyPipeline.CreateDefault(thresholds);
            source = new AnomalyDetectingEventSource(source, pipeline);
            Write(thresholds is null
                ? "Anti-cheat: ON (default thresholds)"
                : $"Anti-cheat: ON (teleport≤{thresholds.TeleportMaxDistance}, rate≤{thresholds.RateSpikeMaxEvents}/{thresholds.RateSpikeWindowMs}ms)");
        }

        var sink = await CreateSinkAsync(o, cancellationToken);

        ModerationService? moderation = null;
        ISpProfileRepository? profiles = null;
        var moderationPath = o.ModerationLogPath;
        var spPath = o.SpStorePath;

        if (o.JoinGate || moderationPath is not null || o.AnomalyAutoMod)
        {
            NlPaths.EnsureRoot();
            moderationPath ??= NlPaths.ModerationLog;
            spPath ??= NlPaths.SpProfiles;
            var store = new JsonlModerationStore(moderationPath);
            profiles = new JsonFileSpProfileRepository(spPath);
            moderation = new ModerationService(store, profiles);
            Moderation = moderation;
            Write($"Moderation log: {moderationPath}");
            Write($"SP store:       {spPath}");
        }

        IJoinGate? joinGate = null;
        if (o.JoinGate)
        {
            profiles ??= new JsonFileSpProfileRepository(spPath ?? NlPaths.SpProfiles);
            var requirements = o.JoinRequirements
                ?? JoinRequirementsStore.LoadOrDefault(o.JoinRequirementsPath ?? NlPaths.JoinRequirements);
            joinGate = new SpJoinGate(
                o.StreamerId,
                requirements,
                (id, name) =>
                {
                    var profile = profiles.GetOrCreate(id, name);
                    profiles.Save(profile);
                    return profile;
                });
            Write($"Join gate: ON (requirements from {o.JoinRequirementsPath ?? NlPaths.JoinRequirements})");
        }

        if (o.AnomalyAutoMod)
        {
            Write("Anomaly auto-mod: ON (severity≥2 Block → graylist hold)");
        }

        Write(o.Replay ? "Replaying source..." : "Listening for events... (stop to cancel)");
        Write("");

        await using (sink)
        {
            var host = new NlServerHost(engine, source, sink, joinGate);
            Host = host;
            try
            {
                await host.RunAsync(cancellationToken, async decision =>
                {
                    var player = decision.SessionEvent.PlayerName ?? "?";
                    var joinTag = decision.JoinGate is null
                        ? ""
                        : $" [join:{decision.JoinGate.JoinResult.Decision}]";
                    Write($"[{player}] {decision.SessionEvent.Event.Name} -> {decision.Result}{joinTag}");

                    if (moderation is not null)
                    {
                        var playerId = decision.JoinGate?.PlayerId
                                       ?? decision.SessionEvent.PlayerName;
                        await moderation.RecordAutomaticDecisionAsync(
                            o.StreamerId,
                            decision.SessionEvent.PlayerName,
                            decision.SessionEvent.Event,
                            decision.Result,
                            source: $"NL.Server:{o.Game}",
                            playerId: playerId,
                            cancellationToken: cancellationToken);

                        if (o.AnomalyAutoMod)
                        {
                            await TryAnomalyAutoModAsync(
                                moderation, o.StreamerId, decision, cancellationToken);
                        }
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // stop requested
            }

            Write("");
            Write($"NL Server stopped. Decisions: {host.Decisions.Count}");
        }

        return 0;
    }

    private async Task<IGameActionSink> CreateSinkAsync(NlSessionOptions o, CancellationToken cancellationToken)
    {
        if (o.RconEndpoint is { } endpoint)
        {
            var parts = endpoint.Split(':', 3);
            if (parts.Length == 3 && int.TryParse(parts[1], out var port))
            {
                var client = new RconClient();
                try
                {
                    await client.ConnectAndAuthenticateAsync(parts[0], port, parts[2], cancellationToken);
                    Write($"Actions: RCON {parts[0]}:{port}");
                    return o.RconCommandTemplate is not null
                        ? new RconActionSink(client, RconActionSink.Templated(o.RconCommandTemplate))
                        : new RconActionSink(client);
                }
                catch (Exception ex)
                {
                    Write($"RCON connection failed ({ex.Message}); falling back to dry-run.");
                    await client.DisposeAsync();
                }
            }
            else
            {
                Write("Invalid RCON endpoint (expected host:port:password); dry-run.");
            }
        }

        if (o.BeamngCommandEndpoint is { } beamngEp)
        {
            if (BeamNgUdpActionSink.TryParseEndpoint(beamngEp, out var host, out var port))
            {
                Write($"Actions: BeamNG UDP {host}:{port} (SCBN1 warn/recover/kick)");
                return new BeamNgUdpActionSink(host, port, Log);
            }

            Write("Invalid --beamng-cmd endpoint (expected host:port); dry-run.");
        }

        if (o.ActionCommand is not null)
        {
            Write($"Actions: process template -> {o.ActionCommand}");
            return new ProcessActionSink(o.ActionCommand);
        }

        Write("Actions: dry-run (no RCON / --beamng-cmd / --action-cmd configured)");
        return new ConsoleActionSink(Log);
    }

    /// <summary>High-severity anomaly Blocks get a graylist hold so join gate catches them next time.</summary>
    public static async Task TryAnomalyAutoModAsync(
        ModerationService moderation,
        string streamerId,
        HostedDecision decision,
        CancellationToken cancellationToken)
    {
        var evt = decision.SessionEvent.Event;
        if (decision.Result.Decision != Decision.Block)
        {
            return;
        }

        if (!evt.Name.StartsWith("anomaly", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!evt.Properties.TryGetValue("anomaly.severity", out var severity) || severity < 2)
        {
            return;
        }

        var playerId = decision.SessionEvent.PlayerName;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        moderation.GetOrCreateProfile(playerId, playerId);
        var reason = decision.Result.Message ?? $"{evt.Name} auto-mod";
        try
        {
            await moderation.IssueGraylistHoldAsync(
                streamerId, playerId, "anti-cheat-auto", reason, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // profile race — ignore
        }
    }

    private void Write(string line) => (Log ?? Console.WriteLine)(line);
}
