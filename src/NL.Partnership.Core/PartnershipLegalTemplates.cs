using NL.Fork.Catalog.Core;

namespace NL.Partnership.Core;

public static class PartnershipLegalTemplates
{
    public const string DisclaimerVersion = "2026.1";

    public const string NoProgressTransferNotice =
        "Session progress, inventory, and rankings on this NL fork do not sync to publisher cloud saves, MMR, or live-service backends.";

    public const string NoDlcSaleNotice =
        "NexoraLive does not sell game copies, DLC, or in-game currency. You must own a legitimate license through the publisher or platform.";

    public static PartnershipLegalBundle ForGame(string gameId, PartnershipTier tier, string? overrideNotice = null)
    {
        var requiresAck = tier == PartnershipTier.AtOwnRisk;
        return new PartnershipLegalBundle(
            gameId,
            tier,
            PartnershipTierLabels.DisplayName(tier),
            SessionDisclaimer: overrideNotice ?? PartnershipTierLabels.LegalNotice(tier),
            NoProgressTransferNotice,
            NoDlcSaleNotice,
            PublisherNotice: tier == PartnershipTier.Official ? "Publisher-approved NL integration." : null,
            RequiresAcknowledgment: requiresAck,
            DisclaimerVersion);
    }
}
