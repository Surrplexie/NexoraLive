namespace NL.Fleet.Core;

public sealed record MultiGameCatalogEntryStatus(
    string GameId,
    string? DockerImage,
    string? MajorVersion,
    bool HasDockerImage);

public sealed record MultiGameCatalogCheckResult(
    bool Passed,
    IReadOnlyList<MultiGameCatalogEntryStatus> Games,
    IReadOnlyList<string> MissingDockerImages);

public sealed record MultiGameValidationCheck(
    string Id,
    string Description,
    bool Passed,
    string? Detail = null);

public sealed record MultiGameValidationReport(
    bool MultiGamePassed,
    IReadOnlyList<MultiGameValidationCheck> Checks,
    DateTimeOffset EvaluatedAtUtc);

public sealed record MultiGameStatus(
    bool Enabled,
    bool LiveProductionEnabled,
    bool GaEnabled,
    bool CatalogEnabled,
    bool PartnershipEnabled,
    IReadOnlyList<string> RequiredGameIds,
    DateTimeOffset ObservedAtUtc);
