namespace NL.Fleet.Core;

public sealed record DistributionClientRelease(
    string Platform,
    string DownloadUrl,
    string? Sha256,
    bool PackageAvailable);

public sealed record DistributionClientManifestPublic(
    string Version,
    string DeepLinkScheme,
    string DeepLinkExample,
    string WebClientUrl,
    string StreamerSignupUrl,
    string CatalogUrl,
    IReadOnlyList<DistributionClientRelease> Releases,
    DateTimeOffset PublishedAtUtc);

public sealed record DistributionOnboardingPaths(
    bool LandingPageEnabled,
    bool DownloadPageEnabled,
    bool WebClientEnabled,
    bool StreamerSignupEnabled,
    bool IdentityLinkEnabled,
    bool CatalogBrowserEnabled);

public sealed record DistributionValidationCheck(
    string Id,
    string Description,
    bool Passed,
    string? Detail = null);

public sealed record DistributionValidationReport(
    bool DistributionPassed,
    IReadOnlyList<DistributionValidationCheck> Checks,
    DateTimeOffset EvaluatedAtUtc);

public sealed record DistributionStatus(
    bool Enabled,
    bool DevMode,
    bool GaOpenSignup,
    string ClientVersion,
    string? PublicBaseUrl,
    bool ClientPackageAvailable,
    DateTimeOffset ObservedAtUtc);
