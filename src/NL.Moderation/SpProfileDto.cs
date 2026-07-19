using NL.Core.Sp;

namespace NL.Moderation;

/// <summary>
/// Plain serializable shadow of <see cref="SpProfile"/> — needed because <c>SpProfile</c>'s
/// per-streamer relationships are exposed as a read-only dictionary rather than a public
/// settable property, so <see cref="System.Text.Json"/> can't round-trip it directly.
/// </summary>
internal sealed class SpProfileDto
{
    public string Id { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public DateTimeOffset AccountCreatedAtUtc { get; set; }

    public SpVerification Verification { get; set; }

    public List<SpOffenseDto> Offenses { get; set; } = new();

    public List<SpRelationshipDto> Relationships { get; set; } = new();

    public static SpProfileDto FromProfile(SpProfile profile) => new()
    {
        Id = profile.Id,
        DisplayName = profile.DisplayName,
        AccountCreatedAtUtc = profile.AccountCreatedAtUtc,
        Verification = profile.Verification,
        Offenses = profile.Offenses.Select(SpOffenseDto.FromOffense).ToList(),
        Relationships = profile.Relationships.Values.Select(SpRelationshipDto.FromRelationship).ToList(),
    };

    public SpProfile ToProfile()
    {
        var profile = new SpProfile
        {
            Id = Id,
            DisplayName = DisplayName,
            AccountCreatedAtUtc = AccountCreatedAtUtc,
            Verification = Verification,
        };

        foreach (var offense in Offenses)
        {
            profile.Offenses.Add(offense.ToOffense());
        }

        foreach (var relationship in Relationships)
        {
            profile.SetRelationship(relationship.ToRelationship());
        }

        return profile;
    }
}

internal sealed class SpOffenseDto
{
    public string StreamerId { get; set; } = "";
    public DateTimeOffset IssuedAtUtc { get; set; }
    public string IssuedBy { get; set; } = "";
    public string Reason { get; set; } = "";
    public string? Game { get; set; }
    public string? PriorContext { get; set; }

    public static SpOffenseDto FromOffense(SpOffense offense) => new()
    {
        StreamerId = offense.StreamerId,
        IssuedAtUtc = offense.IssuedAtUtc,
        IssuedBy = offense.IssuedBy,
        Reason = offense.Reason,
        Game = offense.Game,
        PriorContext = offense.PriorContext,
    };

    public SpOffense ToOffense() => new(StreamerId, IssuedAtUtc, IssuedBy, Reason, Game, PriorContext);
}

internal sealed class SpRelationshipDto
{
    public string StreamerId { get; set; } = "";
    public SpStanding Standing { get; set; }
    public bool IsFollowing { get; set; }
    public bool IsSubscribed { get; set; }
    public List<SpRole> Roles { get; set; } = new();

    public static SpRelationshipDto FromRelationship(SpStreamerRelationship relationship) => new()
    {
        StreamerId = relationship.StreamerId,
        Standing = relationship.Standing,
        IsFollowing = relationship.IsFollowing,
        IsSubscribed = relationship.IsSubscribed,
        Roles = relationship.RolesOrEmpty.ToList(),
    };

    public SpStreamerRelationship ToRelationship() =>
        new(StreamerId, Standing, IsFollowing, IsSubscribed, Roles.ToHashSet());
}
