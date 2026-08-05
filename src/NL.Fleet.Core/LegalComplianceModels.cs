namespace NL.Fleet.Core;

public sealed record LegalComplianceDocument(
    string Id,
    string Title,
    string Path,
    bool Published);

public sealed record LegalComplianceManifestPublic(
    string LegalVersion,
    int MinimumAgeYears,
    string CookieConsentBannerId,
    IReadOnlyList<LegalComplianceDocument> Documents,
    IReadOnlyList<string> Subprocessors,
    DateTimeOffset PublishedAtUtc);

public sealed record LegalComplianceOnboardingPaths(
    bool TermsPageEnabled,
    bool PrivacyPageEnabled,
    bool LegalCenterEnabled,
    bool CookiePolicyEnabled,
    bool SubprocessorsEnabled,
    bool DpaEnabled,
    bool CookieConsentBannerEnabled);

public sealed record LegalComplianceAuditEntry(
    string Action,
    string SubjectId,
    string? Detail,
    DateTimeOffset RecordedAtUtc);

public sealed record LegalComplianceValidationCheck(
    string Id,
    string Description,
    bool Passed,
    string? Detail = null);

public sealed record LegalComplianceValidationReport(
    bool LegalCompliancePassed,
    IReadOnlyList<LegalComplianceValidationCheck> Checks,
    DateTimeOffset EvaluatedAtUtc);

public sealed record LegalComplianceStatus(
    bool Enabled,
    bool DevMode,
    string LegalVersion,
    int DocumentCount,
    int SubprocessorCount,
    int AuditEntryCount,
    bool ScaleReliabilityEnabled,
    DateTimeOffset ObservedAtUtc);
