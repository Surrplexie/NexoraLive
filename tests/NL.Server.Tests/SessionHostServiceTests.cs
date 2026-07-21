using NL.Core;
using NL.Server;
using Xunit;

namespace NL.Server.Tests;

public class SessionHostServiceTests
{
    [Fact]
    public async Task ReplayOnce_EndsIdle()
    {
        var configPath = ResolveSampleConfig("generic.nle");
        var eventsPath = Path.Combine(Path.GetTempPath(), $"nl-test-{Guid.NewGuid():N}.ndjson");
        await File.WriteAllTextAsync(
            eventsPath,
            """{"nl":1,"event":"shoot","player":"Alice","props":{"weapon.damage":1}}""" + "\n");

        var service = new SessionHostService();
        var options = new NlSessionOptions
        {
            Game = "generic",
            ConfigPath = configPath,
            SourcePath = eventsPath,
            Replay = true,
            AntiCheat = false,
            JoinGate = false,
        };

        var lines = new List<string>();
        service.LogAppended += lines.Add;

        var exit = await service.StartAsync(options);
        Assert.Equal(0, exit);
        Assert.Equal(SessionHostState.Idle, service.State);
        Assert.Contains(lines, l => l.Contains("Session started.", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("Session ended.", StringComparison.Ordinal));

        File.Delete(eventsPath);
    }

    [Fact]
    public void Stop_WhenIdle_IsNoOp()
    {
        var service = new SessionHostService();
        service.Stop();
        Assert.Equal(SessionHostState.Idle, service.State);
    }

    private static string ResolveSampleConfig(string name)
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "configs", name)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "samples", "configs", name)),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "samples", "configs", name)),
        };

        return candidates.FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException($"Sample config not found: {name}");
    }
}
