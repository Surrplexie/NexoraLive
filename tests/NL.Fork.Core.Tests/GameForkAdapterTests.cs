using NL.Fork.Core;
using Xunit;

namespace NL.Fork.Core.Tests;

public class GameForkAdapterManifestTests
{
    private const string SampleManifest = """
        {
          "gameId": "example-game",
          "displayName": "Example Game",
          "majorVersion": "1.0",
          "connectScheme": "example",
          "playerConnectPort": 27000,
          "dockerImage": "nl-fork-example-game:latest",
          "dockerfile": "docker/fork-example-game/Dockerfile",
          "defaultNleTemplate": "samples/configs/example-game.nle",
          "integrationDir": "integrations/example-game",
          "dogfoodScript": "scripts/nl-dogfood-flow-example-game.ps1",
          "buildImageKey": "example-game",
          "requiredEvents": ["sessionStart", "playerJoin", "playerLeave", "playerChat"],
          "requiredActions": ["warn", "kick", "tell"],
          "health": { "type": "file", "path": "/data/fork-status.json", "readyField": "sessionStarted" }
        }
        """;

    [Fact]
    public void Parse_RoundTripsCoreFields()
    {
        var adapter = GameForkAdapterManifest.Parse(SampleManifest);
        Assert.Equal("example-game", adapter.GameId);
        Assert.Equal("example", adapter.ConnectScheme);
        Assert.Equal(27000, adapter.PlayerConnectPort);
        Assert.Contains("playerJoin", adapter.RequiredEvents);
        Assert.Contains("kick", adapter.RequiredActions);
        Assert.Equal("file", adapter.Health.Type);
    }

    [Fact]
    public void Parse_RejectsUnknownActionVerb()
    {
        var bad = SampleManifest.Replace("\"tell\"", "\"banhammer\"");
        Assert.Throws<InvalidOperationException>(() => GameForkAdapterManifest.Parse(bad));
    }

    [Fact]
    public void ToForkGameProfile_NormalizesNlePath()
    {
        var adapter = GameForkAdapterManifest.Parse(SampleManifest);
        var profile = adapter.ToForkGameProfile();
        Assert.Equal("nl-fork-example-game:latest", profile.DockerImage);
        Assert.Equal("configs/example-game.nle", profile.DefaultNleTemplate);
        Assert.Equal("example", profile.ConnectScheme);
        Assert.Equal(27000, profile.PlayerConnectPort);
    }

    [Fact]
    public async Task ProbeHealth_FileReady()
    {
        var adapter = GameForkAdapterManifest.Parse(SampleManifest);
        var ready = await adapter.ProbeHealthAsync(new GameForkHealthContext(
            StatusFileContents: """{"sessionStarted": true, "players": 1}"""));
        Assert.True(ready.Ready);

        var notReady = await adapter.ProbeHealthAsync(new GameForkHealthContext(
            StatusFileContents: """{"sessionStarted": false}"""));
        Assert.False(notReady.Ready);
    }
}

public class GameForkAdapterChecklistTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && dir is not null; i++, dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "integrations"))
                && Directory.Exists(Path.Combine(dir.FullName, "samples")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate repo root from test BaseDirectory");
    }

    [Fact]
    public void Discover_FindsBuiltInAdapters()
    {
        var root = FindRepoRoot();
        var adapters = GameForkAdapterManifest.Discover(root);
        var ids = adapters.Select(a => a.GameId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("hello-fork", ids);
        Assert.Contains("minecraft", ids);
        Assert.Contains("minecraft-paper", ids);
        Assert.Contains("beamng", ids);
        Assert.Contains("example-game", ids);
        Assert.Contains("rimworld", ids);
        Assert.Contains("kenshi", ids);
        Assert.DoesNotContain("_template", ids);
    }

    [Fact]
    public void ExampleGame_PassesChecklist()
    {
        var root = FindRepoRoot();
        var adapter = GameForkAdapterManifest.LoadFromFile(
            Path.Combine(root, "integrations", "example-game", "adapter.manifest.json"));
        var report = GameForkAdapterChecklist.Evaluate(root, adapter);
        Assert.True(report.Passed, Format(report));
    }

    [Fact]
    public void AllDiscoveredAdapters_PassChecklist()
    {
        var root = FindRepoRoot();
        var registry = GameForkAdapterRegistry.FromRepo(root);
        Assert.NotEmpty(registry.All);

        foreach (var adapter in registry.All)
        {
            var report = GameForkAdapterChecklist.Evaluate(root, adapter);
            Assert.True(report.Passed, Format(report));
        }
    }

    [Fact]
    public void ForkGameProfiles_ResolvesExampleGameFromManifest()
    {
        var root = FindRepoRoot();
        ForkGameProfiles.AdapterRegistry = GameForkAdapterRegistry.FromRepo(root);
        try
        {
            var profile = ForkGameProfiles.Resolve("example-game");
            Assert.Equal("nl-fork-example-game:latest", profile.DockerImage);
            Assert.Equal("example", profile.ConnectScheme);
            Assert.Equal(27000, profile.PlayerConnectPort);
        }
        finally
        {
            ForkGameProfiles.AdapterRegistry = null;
        }
    }

    private static string Format(GameForkAdapterChecklistReport report)
    {
        var fails = report.Items.Where(i => !i.Passed).Select(i => $"{i.Id}: {i.Detail}");
        return $"{report.GameId} checklist failed — " + string.Join("; ", fails);
    }
}
