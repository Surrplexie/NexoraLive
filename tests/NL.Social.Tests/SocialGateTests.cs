using NL.Core;
using NL.Core.Sp;
using NL.Moderation;
using NL.Moderation.Core;
using NL.Server;
using NL.Social;
using NL.Social.Core;
using Xunit;

namespace NL.Social.Tests;

public class MockSocialProviderTests
{
    private static string FixturePath =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "samples", "social", "mock-social.json"));

    [Fact]
    public async Task MockProvider_ReturnsFollowStatus_FromFixture()
    {
        var provider = new MockSocialRelationshipProvider(FixturePath);
        var context = new SocialGateContext(
            "default-streamer",
            "follower-sp",
            new SpSocialLinks("follower-sp"),
            StreamerSocialConfig.Empty("default-streamer"),
            true,
            false,
            false);

        var status = await provider.GetStatusAsync(context);

        Assert.True(status.IsFollowing);
        Assert.False(status.IsSubscribed);
        Assert.True(status.IsDiscordMember);
        Assert.Equal("mock", status.Source);
    }

    [Fact]
    public async Task MockLiveMonitor_ReturnsLive_FromFixture()
    {
        var monitor = new MockSocialRelationshipProvider(FixturePath);
        var config = StreamerSocialConfig.Empty("default-streamer");

        var live = await monitor.GetStatusAsync(config);

        Assert.True(live.IsLive);
        Assert.Equal(NlSocialPlatform.Twitch, live.Platform);
    }

    [Fact]
    public async Task MockLiveMonitor_ReturnsOffline_ForUnknownStreamer()
    {
        var monitor = new MockSocialRelationshipProvider(FixturePath);
        var config = StreamerSocialConfig.Empty("offline-streamer");

        var live = await monitor.GetStatusAsync(config);

        Assert.False(live.IsLive);
    }
}

public class SocialGateAdmissionTests
{
    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nl-social-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public async Task Admit_DeniesNonFollower_WhenRequireFollowAndSocialEnabled()
    {
        var root = TempDir();
        var mockPath = Path.Combine(root, "mock-social.json");
        File.Copy(
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "samples", "social", "mock-social.json")),
            mockPath);

        Environment.SetEnvironmentVariable("NL_DATA_ROOT", root);
        Environment.SetEnvironmentVariable("NL_SOCIAL_ROOT", Path.Combine(root, "social"));
        Directory.CreateDirectory(Path.Combine(root, "social"));
        File.Copy(mockPath, Path.Combine(root, "social", "mock-social.json"));

        var requirements = new JoinRequirements(RequireFollow: true);
        JoinRequirementsStore.Save(Path.Combine(root, "join-requirements.json"), requirements);

        var moderation = new ModerationService(
            new JsonlModerationStore(Path.Combine(root, "mod.jsonl")),
            new JsonFileSpProfileRepository(Path.Combine(root, "sp.json")));

        var admission = new NlJoinAdmissionService(
            moderation,
            NlPaths.DefaultStreamerId,
            requirements);

        var social = new NlSocialHost(new NlSocialSettings
        {
            Enabled = true,
            Mode = NlSocialMode.Mock,
        });

        var deny = await admission.EvaluateAsync(
            new NlAdmitPlayerRequest { PlayerId = "stranger-sp", DisplayName = "Stranger" },
            new SessionProfileFile { SocialGateEnabled = true },
            identity: null,
            social);

        Assert.Equal(JoinDecision.Deny, deny.Decision);

        var allow = await admission.EvaluateAsync(
            new NlAdmitPlayerRequest { PlayerId = "follower-sp", DisplayName = "Follower" },
            new SessionProfileFile { SocialGateEnabled = true },
            identity: null,
            social);

        Assert.Equal(JoinDecision.Allow, allow.Decision);
    }

    [Fact]
    public async Task Admit_HoldsGraylist_WhenAllowedByRequirements()
    {
        var root = TempDir();
        Environment.SetEnvironmentVariable("NL_DATA_ROOT", root);

        var moderation = new ModerationService(
            new JsonlModerationStore(Path.Combine(root, "mod.jsonl")),
            new JsonFileSpProfileRepository(Path.Combine(root, "sp.json")));

        var profile = moderation.GetOrCreateProfile("gray-sp", "Gray");
        profile.SetRelationship(new SpStreamerRelationship(
            NlPaths.DefaultStreamerId,
            SpStanding.Graylist,
            IsFollowing: true));
        moderation.SaveProfile(profile);

        var requirements = new JoinRequirements(AllowGraylistWithHold: true);
        var admission = new NlJoinAdmissionService(
            moderation,
            NlPaths.DefaultStreamerId,
            requirements);

        var result = await admission.EvaluateAsync(
            new NlAdmitPlayerRequest { PlayerId = "gray-sp" },
            null,
            null);

        Assert.Equal(JoinDecision.Hold, result.Decision);
    }
}

public class OffenseArchiveTests
{
    [Fact]
    public void GetOffenseHistory_SplitsActiveAndArchived()
    {
        var now = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var profiles = new InMemorySpProfileRepository();
        var service = new ModerationService(
            new InMemoryModerationStore(),
            profiles,
            () => now);

        var profile = profiles.GetOrCreate("sp-archive", "Archive");
        profile.Offenses.Add(new SpOffense(
            NlPaths.DefaultStreamerId,
            now.AddYears(-3),
            "mod",
            "old"));
        profile.Offenses.Add(new SpOffense(
            NlPaths.DefaultStreamerId,
            now.AddDays(-30),
            "mod",
            "recent"));
        profiles.Save(profile);

        var history = service.GetOffenseHistory(NlPaths.DefaultStreamerId, "sp-archive", now);

        Assert.NotNull(history);
        Assert.Equal(1, history!.ActiveOffenseCount);
        Assert.Single(history.ActiveOffenses);
        Assert.Single(history.ArchivedOffenses);
        Assert.Equal(2, history.Offenses.Count);
    }
}

internal sealed class InMemorySpProfileRepository : ISpProfileRepository
{
    private readonly Dictionary<string, SpProfile> _profiles = new();

    public SpProfile? Find(string playerId) =>
        _profiles.TryGetValue(playerId, out var p) ? p : null;

    public SpProfile GetOrCreate(string playerId, string displayName)
    {
        if (_profiles.TryGetValue(playerId, out var existing))
        {
            return existing;
        }

        var created = new SpProfile { Id = playerId, DisplayName = displayName };
        _profiles[playerId] = created;
        return created;
    }

    public void Save(SpProfile profile) => _profiles[profile.Id] = profile;

    public IReadOnlyList<SpProfile> All() => _profiles.Values.ToList();
}

internal sealed class InMemoryModerationStore : IModerationStore
{
    public Task AppendAsync(ModerationRecord record, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<ModerationRecord>> GetRecentAsync(
        string streamerId, int count, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ModerationRecord>>(Array.Empty<ModerationRecord>());

    public Task<IReadOnlyList<ModerationRecord>> GetForPlayerAsync(
        string streamerId, string playerId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ModerationRecord>>(Array.Empty<ModerationRecord>());
}
