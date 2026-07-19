namespace NL.Core.Sp;

/// <summary>
/// A SP's standing with one particular streamer (nl.txt section 2: "SPs are in 3 main
/// standing for a streamer, normal, graylist, and banned"). Standing is always scoped to a
/// single streamer — a SP can be <see cref="Banned"/> by one streamer and <see cref="Normal"/>
/// with every other.
/// </summary>
public enum SpStanding
{
    /// <summary>Able to join the streamer's NLServer freely.</summary>
    Normal,

    /// <summary>Under investigation (accidental/disputed offense, pending review). May be
    /// allowed to join with a manual hold, depending on the streamer's <see cref="JoinRequirements"/>.</summary>
    Graylist,

    /// <summary>Fully banned: cannot join the NLServer or see the streamer's activity.</summary>
    Banned,
}
