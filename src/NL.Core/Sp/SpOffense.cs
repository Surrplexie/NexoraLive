namespace NL.Core.Sp;

/// <summary>
/// A single logged offense against a SP for one streamer (nl.txt section 2: "SP offenses add
/// up for 2 years before being archived... It will state the time, who issued it, why, from
/// what streamer, game, and past actions leading to it"). Offenses are never deleted, only
/// archived once they age out of <see cref="ActiveWindow"/> — see <see cref="IsActive"/>.
/// </summary>
public sealed record SpOffense(
    string StreamerId,
    DateTimeOffset IssuedAtUtc,
    string IssuedBy,
    string Reason,
    string? Game = null,
    string? PriorContext = null)
{
    /// <summary>Offenses count toward join decisions for 2 years, then archive passively.</summary>
    public static readonly TimeSpan ActiveWindow = TimeSpan.FromDays(365 * 2);

    /// <summary>True while this offense still counts toward join/eligibility decisions.</summary>
    public bool IsActive(DateTimeOffset nowUtc) => nowUtc - IssuedAtUtc < ActiveWindow;

    /// <summary>When this offense moves from the active list to the passive archive.</summary>
    public DateTimeOffset ArchivesAtUtc => IssuedAtUtc + ActiveWindow;
}
