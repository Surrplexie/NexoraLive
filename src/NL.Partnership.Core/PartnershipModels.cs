using NL.Fork.Catalog.Core;

namespace NL.Partnership.Core;

public enum PublisherTitleStatus
{
    OptedIn,
    OptedOut,
    Pending,
}

public sealed record PublisherRegistration(
    string PublisherId,
    string DisplayName,
    string? ContactEmail = null,
    List<PublisherTitle>? Titles = null,
    DateTimeOffset? RegisteredAtUtc = null)
{
    public List<PublisherTitle> Titles { get; init; } = Titles ?? [];
}

public sealed record PublisherTitle(
    string GameId,
    PartnershipTier Tier,
    PublisherTitleStatus Status = PublisherTitleStatus.OptedIn,
    string? LegalNoticeOverride = null);

public sealed record PlatformOptInEntry(
    string Platform,
    string AppId,
    string GameId,
    PartnershipTier Tier,
    bool Enabled = true,
    DateTimeOffset? UpdatedAtUtc = null);

public sealed record AtOwnRiskAcknowledgment(
    string PlayerId,
    string GameId,
    string DisclaimerVersion,
    DateTimeOffset AcknowledgedAtUtc);

public sealed record PublisherBanEntry(
    string GameId,
    string PlatformUserId,
    string Reason,
    DateTimeOffset BannedAtUtc,
    DateTimeOffset? ExpiresAtUtc = null,
    string? PublisherId = null);

public sealed record PartnershipLegalBundle(
    string GameId,
    PartnershipTier Tier,
    string TierLabel,
    string SessionDisclaimer,
    string NoProgressTransferNotice,
    string NoDlcSaleNotice,
    string? PublisherNotice = null,
    bool RequiresAcknowledgment = false,
    string DisclaimerVersion = "2026.1");

public sealed record PartnershipGateResult(
    bool Allowed,
    bool RequiresAcknowledgment = false,
    string? DenyReason = null,
    PartnershipTier Tier = PartnershipTier.AtOwnRisk,
    PartnershipLegalBundle? Legal = null)
{
    public static PartnershipGateResult Allow(PartnershipTier tier, PartnershipLegalBundle? legal = null) =>
        new(true, RequiresAcknowledgment: false, Tier: tier, Legal: legal);

    public static PartnershipGateResult RequireAck(PartnershipLegalBundle legal) =>
        new(false, RequiresAcknowledgment: true, DenyReason: legal.SessionDisclaimer, Tier: legal.Tier, Legal: legal);

    public static PartnershipGateResult Deny(string reason, PartnershipTier tier = PartnershipTier.AtOwnRisk) =>
        new(false, DenyReason: reason, Tier: tier);
}

public sealed record PublisherDashboardSnapshot(
    string PublisherId,
    string DisplayName,
    IReadOnlyList<PublisherTitle> Titles,
    int SessionJoinCount,
    int ActiveBanCount,
    DateTimeOffset GeneratedAtUtc);

public sealed record BanSyncWebhookRequest(
    string Action,
    string GameId,
    string PlatformUserId,
    string? Reason = null,
    DateTimeOffset? ExpiresAtUtc = null,
    string? PublisherId = null);

public sealed class PlayOnNlSdkSpec
{
    public required string SpecVersion { get; init; }
    public required string Summary { get; init; }
    public required PlayOnNlOwnershipFlow Ownership { get; init; }
    public required PlayOnNlForkAuthFlow ForkAuth { get; init; }
    public required PlayOnNlDisclaimerFlow Disclaimer { get; init; }
    public required PlayOnNlMenuEntry MenuEntry { get; init; }
    public required PlayOnNlDeepLink DeepLink { get; init; }
}

public sealed record PlayOnNlOwnershipFlow(
    string Description,
    string TokenEndpoint,
    string[] RequiredClaims);

public sealed record PlayOnNlForkAuthFlow(
    string Description,
    string ManifestUrl,
    string AdmitUrl);

public sealed record PlayOnNlDisclaimerFlow(
    string Description,
    string LegalUrlTemplate,
    string AcknowledgeUrl);

public sealed record PlayOnNlMenuEntry(
    string Description,
    string ButtonLabel,
    string ImplementationNotes);

public sealed record PlayOnNlDeepLink(
    string Scheme,
    string Template,
    string Example);
