using NL.Core;
using NL.Moderation;
using NL.Moderation.Core;
using NL.Server;
using System.Text.Json;
using Xunit;

namespace NL.Server.Tests;

public class NlSpectatorRateLimiterTests
{
    [Fact]
    public void TryAcquire_RespectsLimit()
    {
        var limiter = new NlSpectatorRateLimiter(maxPerMinute: 2, window: TimeSpan.FromMinutes(1));
        Assert.True(limiter.TryAcquire("client-a"));
        Assert.True(limiter.TryAcquire("client-a"));
        Assert.False(limiter.TryAcquire("client-a"));
        Assert.True(limiter.TryAcquire("client-b"));
    }
}

public class NlSpectatorScenariosTests
{
    [Fact]
    public void ToNdjsonLine_ProducesValidJson()
    {
        var scenario = NlSpectatorScenarios.Find("shoot");
        Assert.NotNull(scenario);
        var line = NlSpectatorScenarios.ToNdjsonLine(scenario!);
        using var doc = JsonDocument.Parse(line);
        Assert.Equal(1, doc.RootElement.GetProperty("nl").GetInt32());
        Assert.Equal("shoot", doc.RootElement.GetProperty("event").GetString());
        Assert.Equal("Visitor", doc.RootElement.GetProperty("player").GetString());
    }

    [Fact]
    public void List_ContainsExpectedDemoScenarios()
    {
        var ids = NlSpectatorScenarios.List().Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("shoot", ids);
        Assert.Contains("caps-chat", ids);
        Assert.Contains("join", ids);
    }
}

public class NlSpectatorServiceTests
{
    [Fact]
    public async Task GetDecisionsAsync_FiltersAutomaticOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nl-spec-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var modLog = Path.Combine(root, "moderation.jsonl");
        var spStore = Path.Combine(root, "sp-profiles.json");

        try
        {
            var host = new ModerationHostState(modLog, spStore);
            await host.Moderation.RecordAutomaticDecisionAsync(
                "default-streamer",
                "Alice",
                GameEvent.Simple("shoot"),
                ActionResult.Block(),
                "test");
            host.Moderation.GetOrCreateProfile("Eve", "Eve");
            await host.Moderation.IssueBanAsync("default-streamer", "Eve", "mod", "test", null);

            var service = new NlSpectatorService(new NlSpectatorSettings());
            var decisions = await service.GetDecisionsAsync(host, "default-streamer", null, 10, CancellationToken.None);

            Assert.Single(decisions);
            Assert.Equal("shoot", decisions[0].EventName);
            Assert.Equal("Block", decisions[0].Decision);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TriggerScenario_WhenDisabled_Returns503()
    {
        var service = new NlSpectatorService(new NlSpectatorSettings { TriggersEnabled = false });
        var result = await service.TriggerScenarioAsync(
            "shoot", "127.0.0.1", sessionRunning: true, "127.0.0.1", 27021, "token", CancellationToken.None);

        Assert.Equal(503, result.StatusCode);
    }
}

public class NlSpectatorSettingsTests
{
    [Fact]
    public void LoadFromEnvironment_DefaultsTriggersOn()
    {
        using var env = EnvScope.With((NlSpectatorSettings.TriggersEnabledVariable, null));
        var settings = NlSpectatorSettings.LoadFromEnvironment();
        Assert.True(settings.TriggersEnabled);
        Assert.Equal(12, settings.TriggerRatePerMinute);
    }
}
