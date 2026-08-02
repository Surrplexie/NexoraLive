using NL.Client.Core;
using Xunit;

namespace NL.Client.Tests;

public sealed class MockNlClientSessionApi : INlClientSessionApi
{
    public bool SessionRunning { get; set; } = true;

    public bool AdmitAllowed { get; set; } = true;

    public bool RequiresAck { get; set; }

    public bool AckRecorded { get; private set; }

    public Task<IReadOnlyList<NlClientStreamerInfo>> ListStreamersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<NlClientStreamerInfo>>([
            new NlClientStreamerInfo("default-streamer", true, "Demo stream", "twitch", "hello-fork"),
        ]);

    public Task<NlClientManifest?> GetManifestAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<NlClientManifest?>(new NlClientManifest(
            "sess1",
            "default-streamer",
            "http://127.0.0.1:27020",
            "ws://127.0.0.1:27021/nl/v1?token=t",
            "http://127.0.0.1:27020/api/v1/session/admit",
            "mock://fork/s1",
            "Official",
            RequiresAck,
            SessionRunning,
            "hello-fork",
            "1.0"));

    public Task<NlClientAdmitResponse> AdmitAsync(NlClientJoinRequest request, CancellationToken cancellationToken = default)
    {
        if (RequiresAck && !request.AtOwnRiskAcknowledged)
        {
            return Task.FromResult(new NlClientAdmitResponse(
                false,
                "Ack required",
                "Hold",
                true,
                "AtOwnRisk",
                "/api/v1/partnership/legal/gameA"));
        }

        return Task.FromResult(new NlClientAdmitResponse(
            AdmitAllowed,
            AdmitAllowed ? null : "Denied",
            AdmitAllowed ? "Allow" : "Deny",
            false,
            "Official",
            null));
    }

    public Task<bool> AcknowledgeAtOwnRiskAsync(string playerId, string gameId, CancellationToken cancellationToken = default)
    {
        AckRecorded = true;
        return Task.FromResult(true);
    }

    public Task<NlClientOverlayState?> GetOverlayAsync(string playerId, string streamerId, CancellationToken cancellationToken = default) =>
        Task.FromResult<NlClientOverlayState?>(new NlClientOverlayState(
            playerId, streamerId, "Normal", 0, [], true, DateTimeOffset.UtcNow));

    public Task<NlClientMobileActionResult> MobileModerationAsync(NlClientMobileActionRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new NlClientMobileActionResult(true));
}

public class NlClientDeepLinkTests
{
    [Fact]
    public void TryParse_ValidJoinLink()
    {
        Assert.True(NlClientDeepLink.TryParse(
            "nlclient://join?streamer=default-streamer&game=hello-fork&major=1.0",
            out var req));
        Assert.Equal("default-streamer", req.StreamerId);
        Assert.Equal("hello-fork", req.GameId);
        Assert.Equal("1.0", req.MajorVersion);
    }

    [Fact]
    public void Build_RoundTrip()
    {
        var link = NlClientDeepLink.Build(new NlClientDeepLinkRequest("s1", "gameA", "2.0", "p1"));
        Assert.True(NlClientDeepLink.TryParse(link, out var req));
        Assert.Equal("gameA", req.GameId);
    }
}

public class NlInviteBlockerTests
{
    [Fact]
    public void Blocks_NlAdmitUrl()
    {
        var result = NlInviteBlocker.Evaluate("http://127.0.0.1:27020/api/v1/session/admit");
        Assert.True(result.Blocked);
    }

    [Fact]
    public void Allows_NonNlInvite()
    {
        var result = NlInviteBlocker.Evaluate("steam://join/12345");
        Assert.False(result.Blocked);
    }
}

public class NlClientJoinFlowTests
{
    [Fact]
    public async Task JoinFlow_Completes_WhenSessionLiveAndOwnershipProvided()
    {
        var api = new MockNlClientSessionApi();
        var flow = new NlClientJoinFlowService(api);
        var result = await flow.ExecuteAsync(new NlClientJoinRequest(
            "sp1",
            "default-streamer",
            PlatformUserId: "76561198000000001"));

        Assert.True(result.Success);
        Assert.Equal(NlClientJoinStep.Completed, result.Step);
        Assert.NotNull(result.Launch);
    }

    [Fact]
    public async Task JoinFlow_RequiresAck_ForAtOwnRisk()
    {
        var api = new MockNlClientSessionApi { RequiresAck = true };
        var flow = new NlClientJoinFlowService(api);
        var first = await flow.ExecuteAsync(new NlClientJoinRequest(
            "sp1",
            "default-streamer",
            PlatformUserId: "76561198000000001"));

        Assert.Equal(NlClientJoinStep.RequiresAtOwnRiskAck, first.Step);

        var second = await flow.ExecuteAsync(new NlClientJoinRequest(
            "sp1",
            "default-streamer",
            PlatformUserId: "76561198000000001",
            AtOwnRiskAcknowledged: true));

        Assert.True(second.Success);
        Assert.True(api.AckRecorded);
    }

    [Fact]
    public async Task JoinFlow_DeepLink_ParsesAndRuns()
    {
        var api = new MockNlClientSessionApi();
        var flow = new NlClientJoinFlowService(api);
        var result = await flow.ExecuteDeepLinkAsync(
            "nlclient://join?streamer=default-streamer&game=hello-fork&major=1.0",
            "sp1",
            "76561198000000001");

        Assert.True(result.Success);
    }

    [Fact]
    public async Task JoinFlow_SessionOffline_Fails()
    {
        var api = new MockNlClientSessionApi { SessionRunning = false };
        var flow = new NlClientJoinFlowService(api);
        var result = await flow.ExecuteAsync(new NlClientJoinRequest("sp1", "default-streamer", PlatformUserId: "1"));
        Assert.Equal(NlClientJoinStep.SessionOffline, result.Step);
    }
}
