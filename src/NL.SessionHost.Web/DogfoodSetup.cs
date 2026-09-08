using NL.Core;
using NL.Identity;
using NL.Moderation;
using NL.Server;
using NL.Social;

namespace NL.SessionHost.Web;

/// <summary>End-to-end dogfood stream setup (operator → client join → teardown).</summary>
public static class DogfoodSetup
{
    private static readonly System.Text.Json.JsonSerializerOptions ProfileJson = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static SessionProfileFile BuildProfile(string repoRoot, string? gameId = null, string? socialMode = null)
    {
        var samplePath = Path.Combine(repoRoot, "samples", "dogfood", "session-profile-dogfood.json");
        if (!File.Exists(samplePath))
        {
            throw new FileNotFoundException("Dogfood profile sample missing.", samplePath);
        }

        var json = File.ReadAllText(samplePath);
        var profile = System.Text.Json.JsonSerializer.Deserialize<SessionProfileFile>(json, ProfileJson)
            ?? throw new InvalidOperationException("Could not parse dogfood profile.");

        profile.StreamerId = string.IsNullOrWhiteSpace(profile.StreamerId)
            ? "dogfood-streamer"
            : profile.StreamerId.Trim();

        var resolvedGameId = string.IsNullOrWhiteSpace(gameId)
            ? (string.IsNullOrWhiteSpace(profile.GameId) ? "hello-fork" : profile.GameId.Trim())
            : gameId.Trim();

        var (configFile, bridgeGame) = ResolveDogfoodGame(resolvedGameId);
        profile.GameId = resolvedGameId;
        profile.Game = bridgeGame;
        profile.GameMajorVersion ??= "1.0";
        profile.PlatformAppId = ResolvePlatformAppId(resolvedGameId, profile.PlatformAppId);
        profile.ForkOrchestratorEnabled = true;
        profile.PartnershipGateEnabled = false;
        profile.RequireGameOwnership = true;

        ApplySocialProfileFlags(profile, socialMode);

        profile.ConfigPath = NlSampleConfigPaths.Resolve(configFile);
        if (!File.Exists(profile.ConfigPath))
        {
            throw new FileNotFoundException($"{configFile} not found.", profile.ConfigPath);
        }

        return profile;
    }

    public static void EnsureMockOwnership(string repoRoot)
    {
        NlIdentityPaths.EnsureRoot();
        var dest = NlIdentityPaths.MockOwnershipConfig;
        var src = Path.Combine(repoRoot, "samples", "identity", "mock-ownership.json");
        if (!File.Exists(src))
        {
            return;
        }

        // Always refresh from sample so new titles (e.g. RimWorld 294100) are present for dogfood.
        File.Copy(src, dest, overwrite: true);
    }

    /// <summary>Install join requirements, streamer channels, and mock live fixtures for social dogfood.</summary>
    public static SocialDogfoodAssetsStatus EnsureSocialDogfoodAssets(
        string repoRoot,
        string socialMode,
        string streamerId = "dogfood-streamer")
    {
        var mode = NormalizeSocialMode(socialMode);
        if (mode is null)
        {
            return new SocialDogfoodAssetsStatus(false, null, false, false, false);
        }

        NlPaths.EnsureRoot();
        NlSocialPaths.EnsureRoot();

        CopySample(
            Path.Combine(repoRoot, "samples", "social", "dogfood-join-requirements.json"),
            NlPaths.JoinRequirements);

        var streamerConfigPath = NlSocialPaths.StreamerConfig;
        CopyStreamerConfig(repoRoot, streamerId, streamerConfigPath);

        var mockCopied = false;
        if (mode is "mock" or "live")
        {
            CopySample(
                Path.Combine(repoRoot, "samples", "social", "dogfood-mock-social.json"),
                NlSocialPaths.MockData);
            mockCopied = true;
        }

        return new SocialDogfoodAssetsStatus(
            true,
            mode,
            File.Exists(NlPaths.JoinRequirements),
            File.Exists(streamerConfigPath),
            mockCopied);
    }

    public static string? NormalizeSocialMode(string? socialMode)
    {
        if (string.IsNullOrWhiteSpace(socialMode))
        {
            return null;
        }

        return socialMode.Trim().ToLowerInvariant() switch
        {
            "off" or "none" or "false" => null,
            "mock" => "mock",
            "live" => "live",
            _ => throw new ArgumentException($"Invalid socialMode '{socialMode}'. Use mock or live."),
        };
    }

    public static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && !string.IsNullOrEmpty(dir); i++)
        {
            if (Directory.Exists(Path.Combine(dir, "samples", "dogfood")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName ?? "";
        }

        return Directory.GetCurrentDirectory();
    }

    private static void ApplySocialProfileFlags(SessionProfileFile profile, string? socialMode)
    {
        var mode = NormalizeSocialMode(socialMode);
        if (mode is null)
        {
            profile.SocialGateEnabled = false;
            profile.JoinGate = false;
            profile.RequireLiveStream = false;
            return;
        }

        profile.SocialGateEnabled = true;
        profile.JoinGate = true;
        profile.RequireLiveStream = true;
    }

    private static void CopySample(string source, string destination)
    {
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("Dogfood social sample missing.", source);
        }

        var dir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.Copy(source, destination, overwrite: true);
    }

    private static void CopyStreamerConfig(string repoRoot, string streamerId, string destination)
    {
        var source = Path.Combine(repoRoot, "samples", "social", "dogfood-streamer-social.json");
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("Dogfood streamer social sample missing.", source);
        }

        var json = File.ReadAllText(source);
        json = json.Replace("123456789", ResolveEnv("DOGFOOD_TWITCH_BROADCASTER_ID", "123456789"));
        json = json.Replace("987654321", ResolveEnv("DOGFOOD_DISCORD_GUILD_ID", "987654321"));
        json = json.Replace("\"dogfood\"", $"\"{ResolveEnv("DOGFOOD_KICK_SLUG", "dogfood")}\"");

        var dir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(destination, json);
    }

    private static string ResolveEnv(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name)?.Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string ResolvePlatformAppId(string gameId, string? existing)
    {
        var id = gameId.Trim().ToLowerInvariant();
        var fromGame = id switch
        {
            "rimworld" or "rimworld-together" => "294100",
            "kenshi" or "ken" => "233860",
            "beamng" or "beamng-drive" => "284160",
            "minecraft" or "minecraft-java" or "minecraft-paper" => "440", // dogfood placeholder
            "hello-fork" => "hello-fork",
            "example-game" => "example-game",
            _ => null,
        };

        if (!string.IsNullOrWhiteSpace(fromGame))
        {
            return fromGame;
        }

        return string.IsNullOrWhiteSpace(existing) ? "440" : existing.Trim();
    }

    private static (string ConfigFile, string BridgeGame) ResolveDogfoodGame(string gameId)
    {
        var id = gameId.Trim().ToLowerInvariant();
        return id switch
        {
            "minecraft" or "minecraft-java" or "minecraft-paper" => ("minecraft.nle", "minecraft"),
            "beamng" or "beamng-drive" => ("beamng.nle", "beamng"),
            "rimworld" or "rimworld-together" => ("rimworld.nle", "rimworld"),
            "kenshi" or "ken" => ("kenshi.nle", "kenshi"),
            "example-game" => ("example-game.nle", "generic"),
            _ => ("fork-hello.nle", "generic"),
        };
    }
}

public sealed record DogfoodSetupRequest(string? GameId, string? SocialMode);

public sealed record SocialDogfoodSetupRequest(string? SocialMode, string? GameId);

public sealed record SocialDogfoodAssetsStatus(
    bool Configured,
    string? SocialMode,
    bool JoinRequirementsReady,
    bool StreamerConfigReady,
    bool MockSocialReady);

public sealed record DogfoodStatus(
    bool SessionRunning,
    bool ForkOrchestratorEnabled,
    string? ForkSessionId,
    int ActiveForkSessions,
    string StreamerId,
    bool MockOwnershipReady,
    bool SocialGateEnabled,
    bool JoinGateEnabled,
    bool RequireLiveStream,
    string? SocialMode,
    bool JoinRequirementsReady,
    bool StreamerConfigReady,
    bool MockSocialReady);
