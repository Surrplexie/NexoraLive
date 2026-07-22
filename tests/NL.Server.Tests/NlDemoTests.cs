using NL.Core;
using NL.Moderation;
using NL.Server;
using NL.Server.Core.Integration;
using System.Net;
using Xunit;

namespace NL.Server.Tests;

public class NlDemoSettingsTests
{
    [Fact]
    public void LoadFromEnvironment_DisabledByDefault()
    {
        using var env = EnvScope.With((NlDemoSettings.DemoModeVariable, null));
        var settings = NlDemoSettings.LoadFromEnvironment();
        Assert.False(settings.Enabled);
    }

    [Fact]
    public void LoadFromEnvironment_EnabledWithIntervalAndConfig()
    {
        using var env = EnvScope.With(
            (NlDemoSettings.DemoModeVariable, "true"),
            (NlDemoSettings.ResetIntervalVariable, "30"),
            (NlDemoSettings.ConfigVariable, "generic.nle"),
            (NlDemoSettings.StartupDelayVariable, "100"));

        var settings = NlDemoSettings.LoadFromEnvironment();
        Assert.True(settings.Enabled);
        Assert.Equal(TimeSpan.FromMinutes(30), settings.ResetInterval);
        Assert.Equal("generic.nle", settings.ConfigFileName);
        Assert.Equal(TimeSpan.FromMilliseconds(100), settings.StartupDelay);
    }

    [Fact]
    public void LoadFromEnvironment_ZeroIntervalDisablesPeriodicReset()
    {
        using var env = EnvScope.With(
            (NlDemoSettings.DemoModeVariable, "1"),
            (NlDemoSettings.ResetIntervalVariable, "0"));

        var settings = NlDemoSettings.LoadFromEnvironment();
        Assert.True(settings.Enabled);
        Assert.Equal(TimeSpan.Zero, settings.ResetInterval);
    }
}

public class NlDemoResetTests
{
    [Fact]
    public void ResetDataFiles_ClearsModerationAndSpStore()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nl-demo-{Guid.NewGuid():N}");
        var modLog = Path.Combine(root, "moderation.jsonl");
        var spStore = Path.Combine(root, "sp-profiles.json");
        Directory.CreateDirectory(root);
        File.WriteAllText(modLog, """{"kind":"automatic"}""" + Environment.NewLine);
        File.WriteAllText(spStore, """[{"id":"Eve"}]""");

        try
        {
            NlDemoReset.ResetDataFiles(modLog, spStore);
            Assert.Equal("", File.ReadAllText(modLog));
            Assert.Equal("[]", File.ReadAllText(spStore));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ResetAndReload_ClearsInMemoryProfiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nl-demo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var modLog = Path.Combine(root, "moderation.jsonl");
        var spStore = Path.Combine(root, "sp-profiles.json");

        try
        {
            Environment.SetEnvironmentVariable("NL_DATA_ROOT", root);
            var host = new ModerationHostState(modLog, spStore);
            host.Moderation.GetOrCreateProfile("Eve", "Eve");
            await host.Moderation.IssueBanAsync(
                NlPaths.DefaultStreamerId, "Eve", "test", "demo reset", null);

            NlDemoReset.ResetAndReload(host);

            var history = host.Moderation.GetOffenseHistory(NlPaths.DefaultStreamerId, "Eve");
            Assert.Null(history);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NL_DATA_ROOT", null);
            Directory.Delete(root, recursive: true);
        }
    }
}

public class NlListenHostTests
{
    [Theory]
    [InlineData("0.0.0.0", true)]
    [InlineData("+", true)]
    [InlineData("127.0.0.1", false)]
    public void ResolveTcpAddress_WildcardUsesAny(string host, bool any)
    {
        var address = NlListenHost.ResolveTcpAddress(host);
        Assert.Equal(any ? IPAddress.Any : IPAddress.Loopback, address);
    }

    [Theory]
    [InlineData("0.0.0.0", "+")]
    [InlineData("127.0.0.1", "127.0.0.1")]
    public void ResolveHttpListenerHost_MapsWildcard(string host, string expected)
    {
        Assert.Equal(expected, NlListenHost.ResolveHttpListenerHost(host));
    }
}

internal sealed class EnvScope : IDisposable
{
    private readonly Dictionary<string, string?> _previous = new();

    private EnvScope(IEnumerable<(string key, string? value)> pairs)
    {
        foreach (var (key, value) in pairs)
        {
            _previous[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    public static EnvScope With(params (string key, string? value)[] pairs) => new(pairs);

    public void Dispose()
    {
        foreach (var (key, value) in _previous)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
