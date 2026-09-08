namespace NL.Fork.Core;

/// <summary>Which real-game fork runtime profile to load (Phase P / T1).</summary>
public enum ForkGameKind
{
    Hello,
    Minecraft,
    Beamng,
    RimWorld,
    Kenshi,
}

public sealed record ForkGameProfile(
    ForkGameKind Game,
    string DockerImage,
    string DefaultNleTemplate,
    int? PlayerConnectPort = null,
    string? ConnectScheme = null)
{
    public string GameArg => Game switch
    {
        ForkGameKind.Minecraft => "minecraft",
        ForkGameKind.Beamng => "beamng",
        ForkGameKind.RimWorld => "rimworld",
        ForkGameKind.Kenshi => "kenshi",
        _ => "hello",
    };
}

public static class ForkGameProfiles
{
    /// <summary>
    /// Optional Phase T0 registry. When set, unknown game ids resolve from
    /// <c>integrations/*/adapter.manifest.json</c> instead of falling back to hello-fork.
    /// </summary>
    public static GameForkAdapterRegistry? AdapterRegistry { get; set; }

    public static ForkGameProfile Resolve(string? gameId)
    {
        var id = gameId?.Trim().ToLowerInvariant() ?? "";

        // Built-in profiles stay authoritative for known titles.
        var builtIn = id switch
        {
            "minecraft" or "minecraft-java" => new ForkGameProfile(
                ForkGameKind.Minecraft,
                "nl-fork-minecraft:latest",
                "configs/minecraft.nle",
                PlayerConnectPort: 25565,
                ConnectScheme: "minecraft"),
            "minecraft-paper" => new ForkGameProfile(
                ForkGameKind.Minecraft,
                "nl-fork-minecraft-paper:latest",
                "configs/minecraft.nle",
                PlayerConnectPort: 25565,
                ConnectScheme: "minecraft"),
            "beamng" or "beamng-drive" => new ForkGameProfile(
                ForkGameKind.Beamng,
                "nl-fork-beamng:latest",
                "configs/beamng.nle",
                ConnectScheme: "beamng-sidecar"),
            "rimworld" or "rimworld-together" => new ForkGameProfile(
                ForkGameKind.RimWorld,
                "nl-fork-rimworld:latest",
                "configs/rimworld.nle",
                PlayerConnectPort: 25555,
                ConnectScheme: "rimworld"),
            "kenshi" or "ken" => new ForkGameProfile(
                ForkGameKind.Kenshi,
                "nl-fork-kenshi:latest",
                "configs/kenshi.nle",
                PlayerConnectPort: 23386,
                ConnectScheme: "kenshi"),
            "hello-fork" => new ForkGameProfile(
                ForkGameKind.Hello,
                "nl-fork-hello:latest",
                "configs/fork-hello.nle"),
            "example-game" => null, // prefer T0 manifest when present
            _ => null,
        };

        if (builtIn is not null)
        {
            return builtIn;
        }

        if (AdapterRegistry is not null && AdapterRegistry.TryGet(id, out var adapter))
        {
            return adapter.ToForkGameProfile();
        }

        // Last resort: discover from default relative integrations path (local dogfood).
        try
        {
            var root = FindRepoRoot();
            if (root is not null)
            {
                var discovered = GameForkAdapterRegistry.FromRepo(root);
                if (discovered.TryGet(id, out var fromDisk))
                {
                    return fromDisk.ToForkGameProfile();
                }
            }
        }
        catch
        {
            // Ignore discovery failures; fall through to hello-fork default.
        }

        return new ForkGameProfile(
            ForkGameKind.Hello,
            "nl-fork-hello:latest",
            "configs/fork-hello.nle");
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "NL.sln"))
                || File.Exists(Path.Combine(dir.FullName, "src", "NL.sln"))
                || Directory.Exists(Path.Combine(dir.FullName, "integrations")))
            {
                return dir.FullName;
            }
        }

        return null;
    }

    public static ForkGameKind ParseGameArg(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ForkGameKind.Hello;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "minecraft" or "mc" => ForkGameKind.Minecraft,
            "beamng" or "beam" => ForkGameKind.Beamng,
            "rimworld" or "rw" or "rimworld-together" => ForkGameKind.RimWorld,
            "kenshi" or "ken" => ForkGameKind.Kenshi,
            _ => ForkGameKind.Hello,
        };
    }
}
