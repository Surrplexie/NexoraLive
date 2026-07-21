using NL.Core;
using NL.Server;

static int PrintUsage()
{
    Console.Error.WriteLine("""
        NL.Server — game-agnostic NLServer host

        Usage:
          NL.Server --game <minecraft|generic> --config <file.nle> --source <path|tcp://|ws://>
                    [--replay] [--rcon host:port:password] [--beamng-cmd host:port]
                    [--nl-action auto|tcp://host:port]
                    [--action-cmd "shell template"]
                    [--rcon-cmd "rcon template with {player} {event} {message}"]
                    [--streamer id] [--moderation-log path] [--sp-store path]
                    [--join-requirements path] [--join-gate] [--anti-cheat]
                    [--anomaly-auto-mod]

        Games:
          minecraft  — tails / replays a Minecraft Java server log (latest.log)
          generic    — NDJSON from any game/mod (file, tcp://, or ws://)

        Source (--source):
          <file path>              tail/replay a log or NDJSON file (default live follow)
          tcp://127.0.0.1:27021    NL listens; bridge connects and sends event lines
          ws://127.0.0.1:27021/nl/v1   bidirectional WebSocket (events in, actions out)

        Modes:
          (default)  live: follow file from EOF, or listen on tcp/ws
          --replay   read whole file once from start (--replay ignored for tcp/ws)

        Actions on Block (first match wins):
          --rcon host:port:password   Source RCON (Minecraft + any RCON game)
          --beamng-cmd host:port      BeamNG legacy UDP SCBN1 (default 127.0.0.1:27022)
          --nl-action auto            paired WebSocket action channel (with ws:// source)
          --nl-action tcp://host:port NL protocol v1 action lines (bridge listens)
          --action-cmd "..."          shell command with {player}{event}{decision}{message}
          (none)                      dry-run: print what would be applied

        Data (defaults under %LOCALAPPDATA%\NL\ when join-gate / moderation / auto-mod on):
          --streamer id
          --moderation-log path
          --sp-store path
          --join-requirements path

        Gates / detectors:
          --join-gate           evaluate JoinEligibilityEngine on playerJoin (Deny/Hold → kick)
          --anti-cheat          anomaly detectors → anomaly* GameEvents
          --anomaly-auto-mod    severity≥2 anomaly Blocks → graylist hold on SP profile

        Examples:
          dotnet run --project src/NL.Server -- --game minecraft --config samples/configs/minecraft.nle --source samples/logs/minecraft-sample.log --replay
          dotnet run --project src/NL.Server -- --game generic --config samples/configs/beamng.nle --source samples/events/beamng-sample.ndjson --replay --anti-cheat
          dotnet run --project src/NL.Server -- --game generic --config samples/configs/generic.nle --source ws://127.0.0.1:27021/nl/v1 --anti-cheat
          dotnet run --project src/NL.Server -- --game generic --config samples/configs/beamng.nle --source tcp://127.0.0.1:27021 --nl-action tcp://127.0.0.1:27023 --anti-cheat
        """);
    return 1;
}

var options = CliOptions.Parse(args);
if (options is null)
{
    return PrintUsage();
}

Console.WriteLine("NL Server — game-agnostic NLServer host");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var runner = new NlSessionRunner { Options = options };
return await runner.RunAsync(cts.Token);

internal static class CliOptions
{
    public static NlSessionOptions? Parse(string[] args)
    {
        // Legacy positional form kept for back-compat:
        //   <config.nle> <log-path> [rconHost] [rconPort] [rconPassword]
        if (args.Length >= 2 && !args[0].StartsWith('-'))
        {
            string? legacyRcon = null;
            if (args.Length >= 5)
            {
                legacyRcon = $"{args[2]}:{args[3]}:{args[4]}";
            }

            return new NlSessionOptions
            {
                Game = "minecraft",
                ConfigPath = args[0],
                SourcePath = args[1],
                Replay = false,
                RconEndpoint = legacyRcon,
            };
        }

        string? game = null;
        string? config = null;
        string? source = null;
        var replay = false;
        string? rcon = null;
        string? rconCmd = null;
        string? actionCmd = null;
        string? beamngCmd = null;
        string? nlAction = null;
        var streamerId = NlPaths.DefaultStreamerId;
        string? moderationLog = null;
        string? spStore = null;
        string? joinRequirements = null;
        var antiCheat = false;
        var joinGate = false;
        var anomalyAutoMod = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--game" when i + 1 < args.Length:
                    game = args[++i].ToLowerInvariant();
                    break;
                case "--config" when i + 1 < args.Length:
                    config = args[++i];
                    break;
                case "--source" when i + 1 < args.Length:
                    source = args[++i];
                    break;
                case "--replay":
                    replay = true;
                    break;
                case "--rcon" when i + 1 < args.Length:
                    rcon = args[++i];
                    break;
                case "--rcon-cmd" when i + 1 < args.Length:
                    rconCmd = args[++i];
                    break;
                case "--beamng-cmd" when i + 1 < args.Length:
                    beamngCmd = args[++i];
                    break;
                case "--nl-action" when i + 1 < args.Length:
                    nlAction = args[++i];
                    break;
                case "--action-cmd" when i + 1 < args.Length:
                    actionCmd = args[++i];
                    break;
                case "--streamer" when i + 1 < args.Length:
                    streamerId = args[++i];
                    break;
                case "--moderation-log" when i + 1 < args.Length:
                    moderationLog = args[++i];
                    break;
                case "--sp-store" when i + 1 < args.Length:
                    spStore = args[++i];
                    break;
                case "--join-requirements" when i + 1 < args.Length:
                    joinRequirements = args[++i];
                    break;
                case "--anti-cheat":
                    antiCheat = true;
                    break;
                case "--join-gate":
                    joinGate = true;
                    break;
                case "--anomaly-auto-mod":
                    anomalyAutoMod = true;
                    break;
                case "--help" or "-h":
                    return null;
                default:
                    Console.Error.WriteLine($"Unknown argument: {args[i]}");
                    return null;
            }
        }

        if (game is null || config is null || source is null)
        {
            return null;
        }

        if (game is not ("minecraft" or "generic"))
        {
            Console.Error.WriteLine($"Unknown --game '{game}'. Use minecraft or generic.");
            return null;
        }

        return new NlSessionOptions
        {
            Game = game,
            ConfigPath = config,
            SourcePath = source,
            Replay = replay,
            RconEndpoint = rcon,
            RconCommandTemplate = rconCmd,
            ActionCommand = actionCmd,
            BeamngCommandEndpoint = beamngCmd,
            NlActionEndpoint = nlAction,
            StreamerId = streamerId,
            ModerationLogPath = moderationLog,
            SpStorePath = spStore,
            JoinRequirementsPath = joinRequirements,
            AntiCheat = antiCheat,
            JoinGate = joinGate,
            AnomalyAutoMod = anomalyAutoMod,
        };
    }
}
