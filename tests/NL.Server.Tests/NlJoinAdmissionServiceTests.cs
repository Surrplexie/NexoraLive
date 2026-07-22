using NL.Core;
using NL.Core.Sp;
using NL.Moderation;
using NL.Server;
using NL.Server.Core.Integration;
using Xunit;

namespace NL.Server.Tests;

public class NlJoinAdmissionServiceTests
{
    [Fact]
    public async Task BannedPlayer_IsDeniedAtAdmit()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"nl-admit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var previous = Environment.GetEnvironmentVariable("NL_DATA_ROOT");

        try
        {
            Environment.SetEnvironmentVariable("NL_DATA_ROOT", dir);
            var admission = NlJoinAdmissionService.CreateDefault(NlPaths.DefaultStreamerId);
            admission.Moderation.GetOrCreateProfile("eve", "Eve");
            await admission.Moderation.IssueBanAsync(NlPaths.DefaultStreamerId, "eve", "test", "cheating");

            var result = admission.Evaluate("eve", "Eve");

            Assert.Equal(JoinDecision.Deny, result.Decision);
            Assert.False(result.Admit);
            Assert.Equal(SpStanding.Banned, result.Standing);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NL_DATA_ROOT", previous);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CleanPlayer_IsAllowedAtAdmit()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"nl-admit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var previous = Environment.GetEnvironmentVariable("NL_DATA_ROOT");

        try
        {
            Environment.SetEnvironmentVariable("NL_DATA_ROOT", dir);
            var admission = NlJoinAdmissionService.CreateDefault(NlPaths.DefaultStreamerId);
            var result = admission.Evaluate("alice", "Alice");

            Assert.Equal(JoinDecision.Allow, result.Decision);
            Assert.True(result.Admit);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NL_DATA_ROOT", previous);
            Directory.Delete(dir, recursive: true);
        }
    }
}

public class NlSessionServerHelperTests
{
    [Fact]
    public void CreateManifest_IncludesAdmitAndBridgeUrls()
    {
        var bus = NlSessionBusHelper.CreateBusInfo("127.0.0.1", 27020, 27021, "tok", "sess1");
        var profile = new SessionProfileFile { StreamerId = "streamer-1", JoinGate = true };

        var manifest = NlSessionServerHelper.CreateManifest(
            bus, profile, "0.0.0.0", 27020, 27021, 27030, sessionRunning: true);

        Assert.Equal("streamer-1", manifest.StreamerId);
        Assert.Contains("/api/v1/session/admit", manifest.AdmitUrl);
        Assert.Contains("token=tok", manifest.BridgeConnectUrl);
        Assert.True(manifest.JoinGateEnabled);
        Assert.True(manifest.SessionRunning);
    }

    [Fact]
    public void ResolvePublicHttpBase_UsesNlPublicHost()
    {
        var previous = Environment.GetEnvironmentVariable("NL_PUBLIC_HOST");
        try
        {
            Environment.SetEnvironmentVariable("NL_PUBLIC_HOST", "nl.example.com");
            var url = NlSessionServerHelper.ResolvePublicHttpBase("0.0.0.0", 27020);
            Assert.Equal("http://nl.example.com:27020", url);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NL_PUBLIC_HOST", previous);
        }
    }
}
