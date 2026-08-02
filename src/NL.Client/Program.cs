using NL.Client.Core;

namespace NL.Client;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var settings = NlClientSettings.LoadFromEnvironment();
        using var api = new HttpNlClientSessionApi(settings);
        var flow = new NlClientJoinFlowService(api);

        if (args.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        return args[0].ToLowerInvariant() switch
        {
            "join" => await RunJoinAsync(flow, args),
            "deeplink" => await RunDeepLinkAsync(flow, args),
            "streamers" => await RunStreamersAsync(api),
            "block-invite" => RunBlockInvite(args),
            "help" or "--help" or "-h" => PrintHelpReturn(),
            _ => Unknown(args[0]),
        };
    }

    private static async Task<int> RunJoinAsync(NlClientJoinFlowService flow, string[] args)
    {
        var playerId = Arg(args, "--player", "sp-demo-1");
        var streamerId = Arg(args, "--streamer", "default-streamer");
        var platformUser = Arg(args, "--platform-user", "76561198000000001");
        var ack = HasFlag(args, "--ack");

        var result = await flow.ExecuteAsync(new NlClientJoinRequest(
            playerId,
            streamerId,
            PlatformUserId: platformUser,
            Platform: Arg(args, "--platform", "steam"),
            AtOwnRiskAcknowledged: ack));

        PrintResult(result);
        return result.Success ? 0 : 1;
    }

    private static async Task<int> RunDeepLinkAsync(NlClientJoinFlowService flow, string[] args)
    {
        var link = Arg(args, "--url", "");
        if (string.IsNullOrWhiteSpace(link))
        {
            Console.Error.WriteLine("--url required.");
            return 1;
        }

        var result = await flow.ExecuteDeepLinkAsync(
            link,
            Arg(args, "--player", "sp-demo-1"),
            Arg(args, "--platform-user", "76561198000000001"),
            HasFlag(args, "--ack"));

        PrintResult(result);
        return result.Success ? 0 : 1;
    }

    private static async Task<int> RunStreamersAsync(INlClientSessionApi api)
    {
        var list = await api.ListStreamersAsync();
        foreach (var s in list)
        {
            Console.WriteLine($"{s.StreamerId}\t{(s.IsLive ? "LIVE" : "offline")}\t{s.GameId ?? "-"}");
        }

        return 0;
    }

    private static int RunBlockInvite(string[] args)
    {
        var url = Arg(args, "--url", "");
        var result = NlInviteBlocker.Evaluate(url, Arg(args, "--host", null));
        Console.WriteLine($"blocked={result.Blocked} reason={result.Reason}");
        return result.Blocked ? 2 : 0;
    }

    private static void PrintResult(NlClientJoinFlowResult result)
    {
        Console.WriteLine($"step={result.Step} success={result.Success}");
        if (!string.IsNullOrWhiteSpace(result.Message))
        {
            Console.WriteLine(result.Message);
        }

        if (result.Launch is not null)
        {
            Console.WriteLine($"launch: {result.Launch.CommandLine}");
            Console.WriteLine($"fork: {result.Launch.ForkConnectEndpoint}");
        }
    }

    private static string Arg(string[] args, string name, string? fallback)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return fallback ?? "";
    }

    private static bool HasFlag(string[] args, string name) =>
        args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

    private static int PrintHelpReturn()
    {
        PrintHelp();
        return 0;
    }

    private static int Unknown(string cmd)
    {
        Console.Error.WriteLine($"Unknown command: {cmd}");
        PrintHelp();
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            NL Client shell (Phase R)

            Commands:
              join --player ID --streamer ID [--platform-user STEAM64] [--ack]
              deeplink --url nlclient://join?... [--player ID] [--platform-user STEAM64] [--ack]
              streamers
              block-invite --url INVITE_URL [--host expected-host]

            Environment:
              NL_CLIENT_SESSION_URL  Session host base URL (default http://127.0.0.1:27020)
              NL_OPERATOR_KEY          Optional operator key for manifest secrets
            """);
    }
}
