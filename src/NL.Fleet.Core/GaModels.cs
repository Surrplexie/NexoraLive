namespace NL.Fleet.Core;

public sealed record GaStreamerEntry(
    string Id,
    string DisplayName,
    string Contact,
    string? TwitchHandle,
    string? PreferredGameId,
    string StreamerId,
    DateTimeOffset RegisteredAtUtc);

public sealed record GaProgramStatus(
    bool Enabled,
    bool OpenSignup,
    int RegisteredStreamers,
    int CatalogGameCount,
    int RequiredCatalogGames,
    string SlaTier,
    DateTimeOffset GeneratedAtUtc);

public sealed record GaCatalogCheckResult(
    bool Passed,
    int ActiveGameCount,
    IReadOnlyList<string> RequiredGameIds,
    IReadOnlyList<string> MissingGameIds,
    IReadOnlyList<string> PresentGameIds);

public sealed record GaValidationCheck(
    string Id,
    string Description,
    bool Passed,
    string? Detail = null);

public sealed record GaValidationReport(
    bool GaPassed,
    IReadOnlyList<GaValidationCheck> Checks,
    IReadOnlyList<FleetSloStatus>? ProductionSlos,
    DateTimeOffset EvaluatedAtUtc);
