namespace NL.Core.Sp;

/// <summary>
/// Everything about a SP's relationship with one specific streamer: standing, social/sub
/// status with that streamer, and any roles granted by that streamer. Everything here is
/// scoped per-streamer because nl.txt is explicit that standing/roles are not global — a SP
/// can be a Mod for one streamer and simply Normal (or Banned) with another.
/// </summary>
public sealed record SpStreamerRelationship(
    string StreamerId,
    SpStanding Standing = SpStanding.Normal,
    bool IsFollowing = false,
    bool IsSubscribed = false,
    IReadOnlySet<SpRole>? Roles = null)
{
    private static readonly IReadOnlySet<SpRole> EmptyRoles = new HashSet<SpRole>();

    public IReadOnlySet<SpRole> RolesOrEmpty => Roles ?? EmptyRoles;

    public bool HasRole(SpRole role) => RolesOrEmpty.Contains(role);

    public bool IsPrivileged => HasRole(SpRole.Admin) || HasRole(SpRole.Mod);

    public bool BypassesSocialRequirements => IsPrivileged || HasRole(SpRole.Vip) || HasRole(SpRole.Friend);
}
