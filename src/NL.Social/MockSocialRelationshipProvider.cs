using System.Text.Json;
using NL.Social.Core;

namespace NL.Social;

/// <summary>
/// Reads follow/sub/discord/live status from a JSON fixture — default for dev and tests.
/// Copy <c>samples/social/mock-social.json</c> into <see cref="NlSocialPaths.MockData"/>.
/// </summary>
public sealed class MockSocialRelationshipProvider : ISocialRelationshipProvider, ILiveStreamMonitor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _path;
    private readonly object _lock = new();
    private MockSocialDocument? _document;

    public MockSocialRelationshipProvider(string? path = null) =>
        _path = path ?? NlSocialPaths.MockData;

    public Task<SocialRelationshipStatus> GetStatusAsync(
        SocialGateContext context,
        CancellationToken cancellationToken = default)
    {
        var doc = Load();
        if (doc?.Relationships is null)
        {
            return Task.FromResult(SocialRelationshipStatus.Unknown);
        }

        if (!doc.Relationships.TryGetValue(context.PlayerId, out var byStreamer)
            || !byStreamer.TryGetValue(context.StreamerId, out var entry))
        {
            return Task.FromResult(SocialRelationshipStatus.Unknown);
        }

        return Task.FromResult(new SocialRelationshipStatus(
            entry.IsFollowing,
            entry.IsSubscribed,
            entry.IsDiscordMember,
            "mock"));
    }

    public Task<LiveStreamStatus> GetStatusAsync(
        StreamerSocialConfig config,
        CancellationToken cancellationToken = default)
    {
        var doc = Load();
        if (doc?.LiveStatus is null
            || !doc.LiveStatus.TryGetValue(config.StreamerId, out var entry))
        {
            return Task.FromResult(new LiveStreamStatus(false, null, null, DateTimeOffset.UtcNow));
        }

        NlSocialPlatform? platform = null;
        if (!string.IsNullOrWhiteSpace(entry.Platform)
            && NlSocialPlatformNames.TryParse(entry.Platform, out var parsed))
        {
            platform = parsed;
        }

        return Task.FromResult(new LiveStreamStatus(
            entry.IsLive,
            platform ?? config.LivePlatform,
            entry.Title,
            DateTimeOffset.UtcNow));
    }

    private MockSocialDocument Load()
    {
        lock (_lock)
        {
            if (_document is not null)
            {
                return _document;
            }

            if (!File.Exists(_path))
            {
                _document = new MockSocialDocument();
                return _document;
            }

            var json = File.ReadAllText(_path);
            _document = JsonSerializer.Deserialize<MockSocialDocument>(json, JsonOptions)
                        ?? new MockSocialDocument();
            return _document;
        }
    }

    public void Reload() => _document = null;

    private sealed class MockSocialDocument
    {
        public Dictionary<string, Dictionary<string, MockRelationshipEntry>>? Relationships { get; set; }

        public Dictionary<string, MockLiveEntry>? LiveStatus { get; set; }
    }

    private sealed class MockRelationshipEntry
    {
        public bool IsFollowing { get; set; }

        public bool IsSubscribed { get; set; }

        public bool IsDiscordMember { get; set; }
    }

    private sealed class MockLiveEntry
    {
        public bool IsLive { get; set; }

        public string? Platform { get; set; }

        public string? Title { get; set; }
    }
}
