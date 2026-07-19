namespace NL.Core.Sp;

/// <summary>
/// Roles a SP can hold with a particular streamer, on top of the always-present base SP role
/// (nl.txt section 2: "SP is a default permanent role for all users including streamers,
/// admins and mods... customizable ranks, subscriber perks, VIP access systems"). A profile can
/// hold more than one of these at once for the same streamer (e.g. a Mod who is also a Vip).
/// </summary>
public enum SpRole
{
    /// <summary>Base role every account has by default; rarely need to state explicitly.</summary>
    Sp,

    /// <summary>Elevated community/subscriber tier (perks, may bypass some join requirements).</summary>
    Friend,

    /// <summary>Subscriber/VIP access tier (perks, may bypass some join requirements).</summary>
    Vip,

    /// <summary>Can moderate: accept graylisted SPs, issue offenses, etc.</summary>
    Mod,

    /// <summary>Full administrative control over the streamer's NLServer.</summary>
    Admin,
}
