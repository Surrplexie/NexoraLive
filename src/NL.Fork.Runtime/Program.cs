using NL.Core;
using NL.Fork.Core;

var parser = new ArgParser(args);

if (parser.Flag("help") || parser.Flag("h"))
{
    PrintHelp();
    return 0;
}

var wsUrl = parser.Get("url") ?? Environment.GetEnvironmentVariable("NL_FORK_WS_URL");
var modsPath = parser.Get("mods") ?? Environment.GetEnvironmentVariable("NL_FORK_MODS");
var statusPath = parser.Get("status") ?? Environment.GetEnvironmentVariable("NL_FORK_STATUS")
    ?? Path.Combine(NlPaths.Root, "fork-status.json");
var admitUrl = parser.Get("admit-url") ?? Environment.GetEnvironmentVariable("NL_FORK_ADMIT_URL");
var loop = parser.Flag("loop");
var interval = parser.GetDouble("interval", 8);
var embeddedConfig = parser.Get("config");

NlPaths.EnsureRoot();
var mods = string.IsNullOrWhiteSpace(modsPath) ? new ForkModManifest() : ForkModLoader.LoadFromFile(modsPath);

Action<string> log = line => Console.WriteLine(line);

ForkRuntimeHost host;
if (!string.IsNullOrWhiteSpace(embeddedConfig))
{
    var nle = File.ReadAllText(embeddedConfig);
    host = ForkRuntimeHost.CreateEmbedded(nle, mods);
    log("[fork] embedded mode (no session bus)");
}
else
{
    if (string.IsNullOrWhiteSpace(wsUrl))
    {
        Console.Error.WriteLine("Missing --url or NL_FORK_WS_URL");
        return 1;
    }

    var bridge = new ForkNlBridgeClient(log: log);
    await bridge.ConnectAsync(new Uri(wsUrl), CancellationToken.None);
    Func<string, Task<bool>>? admit = string.IsNullOrWhiteSpace(admitUrl)
        ? null
        : p => ForkAdmitClient.TryAdmitAsync(admitUrl!, p, CancellationToken.None);
    host = ForkRuntimeHost.CreateRemote(bridge, mods, admit, statusPath, log);
    log($"[fork] remote mode → {wsUrl}");
}

await using (host)
{
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    if (loop)
    {
        log($"[fork] demo loop every {interval}s");
        await host.RunDemoLoopAsync(interval, admitUrl, cts.Token);
    }
    else
    {
        host.WriteStatus(sessionStarted: true);
        await host.Runtime.TryJoinAsync("Alice", cts.Token);
        await host.Runtime.TryJoinAsync("Bob", cts.Token);
        var shoot = await host.Runtime.TryShootAsync("Alice", "Bob", 12, cts.Token);
        log($"[fork] shoot → committed={shoot.Committed} decision={shoot.Decision}");
        if (host.Runtime.World.TryGetPlayer("Bob", out var bob) && bob is not null)
        {
            log($"[fork] Bob health={bob.Health}");
        }

        host.WriteStatus(sessionStarted: true);
        log("[fork] one-shot complete");
    }
}

return 0;

static void PrintHelp()
{
    Console.WriteLine("""
NL.Fork.Runtime — Phase P hello-fork (server-side enforcement)

  --url ws://127.0.0.1:27021/nl/v1?token=...   NL session bus (required unless --config)
  --config path/to/rules.nle                   Embedded RuleEngine (no bus)
  --mods path/to/mods.json                     Server-side mod manifest
  --status path/to/fork-status.json            Operator status file
  --admit-url http://host/api/v1/session/admit Pre-connect join gate
  --loop                                       Repeat demo scenario
  --interval 8                                 Loop seconds (default 8)

Env: NL_FORK_WS_URL, NL_FORK_MODS, NL_FORK_STATUS, NL_FORK_ADMIT_URL
""");
}

internal sealed class ArgParser
{
    private readonly string[] _args;

    public ArgParser(string[] args) => _args = args;

    public bool Flag(string name) =>
        _args.Any(a => string.Equals(a, $"--{name}", StringComparison.OrdinalIgnoreCase));

    public string? Get(string name)
    {
        for (var i = 0; i < _args.Length - 1; i++)
        {
            if (string.Equals(_args[i], $"--{name}", StringComparison.OrdinalIgnoreCase))
            {
                return _args[i + 1];
            }
        }

        return null;
    }

    public double GetDouble(string name, double defaultValue)
    {
        var raw = Get(name);
        return double.TryParse(raw, out var value) ? value : defaultValue;
    }
}
