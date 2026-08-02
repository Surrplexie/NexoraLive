using NL.Fork.Catalog.Core;
using NL.Partnership.Core;

namespace NL.Partnership;

public sealed class PartnershipGateService
{
    private readonly IAtOwnRiskAcknowledgmentStore _acks;
    private readonly IPublisherBanStore _bans;
    private readonly IPlatformOptInStore _platformOptIn;
    private readonly IPublisherRegistry _publishers;

    public PartnershipGateService(
        IAtOwnRiskAcknowledgmentStore acks,
        IPublisherBanStore bans,
        IPlatformOptInStore platformOptIn,
        IPublisherRegistry publishers)
    {
        _acks = acks;
        _bans = bans;
        _platformOptIn = platformOptIn;
        _publishers = publishers;
    }

    public PartnershipLegalBundle GetLegal(string gameId, PartnershipTier tier, string? overrideNotice = null) =>
        PartnershipLegalTemplates.ForGame(gameId, tier, overrideNotice);

    public PartnershipGateResult EvaluateAdmit(
        string playerId,
        string gameId,
        PartnershipTier tier,
        string? platformUserId = null,
        string? platform = null,
        string? appId = null,
        bool atOwnRiskAcknowledged = false,
        string? legalOverride = null,
        DateTimeOffset? nowUtc = null)
    {
        nowUtc ??= DateTimeOffset.UtcNow;
        tier = ResolveTier(gameId, tier, platform, appId);

        if (!string.IsNullOrWhiteSpace(platformUserId)
            && _bans.IsBanned(gameId, platformUserId, nowUtc.Value))
        {
            return PartnershipGateResult.Deny(
                "Publisher ban list: you cannot join this title on NL.",
                tier);
        }

        var pub = _publishers.List().FirstOrDefault(p =>
            p.Titles.Any(t =>
                string.Equals(t.GameId, gameId, StringComparison.OrdinalIgnoreCase)
                && t.Status == PublisherTitleStatus.OptedOut));
        if (pub is not null)
        {
            return PartnershipGateResult.Deny("Publisher has opted this title out of NL sessions.", tier);
        }

        var legal = GetLegal(gameId, tier, legalOverride);

        if (tier != PartnershipTier.AtOwnRisk)
        {
            return PartnershipGateResult.Allow(tier, legal);
        }

        var existing = _acks.Get(playerId, gameId);
        if (existing is not null
            && string.Equals(existing.DisclaimerVersion, legal.DisclaimerVersion, StringComparison.Ordinal))
        {
            return PartnershipGateResult.Allow(tier, legal);
        }

        if (atOwnRiskAcknowledged)
        {
            _acks.Save(new AtOwnRiskAcknowledgment(
                playerId,
                gameId,
                legal.DisclaimerVersion,
                nowUtc.Value));
            return PartnershipGateResult.Allow(tier, legal);
        }

        return PartnershipGateResult.RequireAck(legal);
    }

    public AtOwnRiskAcknowledgment RecordAcknowledgment(string playerId, string gameId, PartnershipTier tier)
    {
        var legal = GetLegal(gameId, tier);
        var ack = new AtOwnRiskAcknowledgment(
            playerId,
            gameId,
            legal.DisclaimerVersion,
            DateTimeOffset.UtcNow);
        _acks.Save(ack);
        return ack;
    }

    public PartnershipTier ResolveTier(
        string gameId,
        PartnershipTier catalogTier,
        string? platform,
        string? appId)
    {
        if (!string.IsNullOrWhiteSpace(platform) && !string.IsNullOrWhiteSpace(appId))
        {
            var optIn = _platformOptIn.Find(platform, appId);
            if (optIn is not null
                && string.Equals(optIn.GameId, gameId, StringComparison.OrdinalIgnoreCase)
                && optIn.Tier != PartnershipTier.AtOwnRisk)
            {
                return optIn.Tier;
            }
        }

        var pubTitle = _publishers.List()
            .SelectMany(p => p.Titles.Select(t => (p, t)))
            .FirstOrDefault(x =>
                string.Equals(x.t.GameId, gameId, StringComparison.OrdinalIgnoreCase)
                && x.t.Status == PublisherTitleStatus.OptedIn);
        if (pubTitle.t is not null && pubTitle.t.Tier != PartnershipTier.AtOwnRisk)
        {
            return pubTitle.t.Tier;
        }

        return catalogTier;
    }
}
