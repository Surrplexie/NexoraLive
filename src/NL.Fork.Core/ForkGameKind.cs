namespace NL.Fork.Core;

/// <summary>Which real-game fork runtime profile to load (Phase P).</summary>
public enum ForkGameKind
{
    Hello,
    Minecraft,
    Beamng,
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
        _ => "hello",
    };
}

public static class ForkGameProfiles
{
    public static ForkGameProfile Resolve(string? gameId)
    {
        var id = gameId?.Trim().ToLowerInvariant() ?? "";
        return id switch
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
            "hello-fork" => new ForkGameProfile(
                ForkGameKind.Hello,
                "nl-fork-hello:latest",
                "configs/fork-hello.nle"),
            _ => new ForkGameProfile(
                ForkGameKind.Hello,
                "nl-fork-hello:latest",
                "configs/fork-hello.nle"),
        };
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
            _ => ForkGameKind.Hello,
        };
    }
}
