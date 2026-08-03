using NL.Identity;
using NL.Server;

namespace NL.SessionHost.Web;

/// <summary>End-to-end dogfood stream setup (operator → client join → teardown).</summary>
public static class DogfoodSetup
{
    private static readonly System.Text.Json.JsonSerializerOptions ProfileJson = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static SessionProfileFile BuildProfile(string repoRoot)
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
        profile.GameId ??= "hello-fork";
        profile.GameMajorVersion ??= "1.0";
        profile.PlatformAppId ??= "440";
        profile.ForkOrchestratorEnabled = true;
        profile.PartnershipGateEnabled = false;
        profile.SocialGateEnabled = false;
        profile.JoinGate = false;
        profile.RequireGameOwnership = true;

        profile.ConfigPath = NlSampleConfigPaths.Resolve("fork-hello.nle");
        if (!File.Exists(profile.ConfigPath))
        {
            throw new FileNotFoundException("fork-hello.nle not found.", profile.ConfigPath);
        }

        return profile;
    }

    public static void EnsureMockOwnership(string repoRoot)
    {
        NlIdentityPaths.EnsureRoot();
        var dest = NlIdentityPaths.MockOwnershipConfig;
        if (File.Exists(dest))
        {
            return;
        }

        var src = Path.Combine(repoRoot, "samples", "identity", "mock-ownership.json");
        if (!File.Exists(src))
        {
            return;
        }

        File.Copy(src, dest);
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
}

public sealed record DogfoodStatus(
    bool SessionRunning,
    bool ForkOrchestratorEnabled,
    string? ForkSessionId,
    int ActiveForkSessions,
    string StreamerId,
    bool MockOwnershipReady);
