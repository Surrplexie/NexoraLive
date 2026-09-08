using NL.Identity;
using NL.Identity.Core;
using NL.Moderation;
using NL.Social;
using NL.Social.Core;

namespace NL.Server;

/// <summary>Creates SP profiles and streamer social config for unified NL accounts.</summary>
public sealed class NlUnifiedAccountProvisioner : IUnifiedAccountProvisioner
{
    private readonly ModerationHostState _moderation;
    private readonly NlSocialHost _social;

    public NlUnifiedAccountProvisioner(ModerationHostState moderation, NlSocialHost social)
    {
        _moderation = moderation;
        _social = social;
    }

    public void EnsurePlayerProfile(string accountId, string displayName)
    {
        var profile = _moderation.Moderation.GetOrCreateProfile(accountId, displayName);
        profile.NlAccountId = accountId;
        _moderation.Moderation.SaveProfile(profile);
        _social.LinkStore.Save(new SpSocialLinks(accountId));
    }

    public void EnsureStreamerConfig(string accountId, string streamerId, string displayName)
    {
        EnsurePlayerProfile(accountId, displayName);
        var existing = _social.StreamerStore.GetOrDefault(streamerId);
        if (string.IsNullOrWhiteSpace(existing.StreamerId) || existing.StreamerId == streamerId)
        {
            _social.StreamerStore.Save(existing with { StreamerId = streamerId });
        }
    }
}
