namespace NL.Core.Sp;

/// <summary>Outcome of a join-eligibility check. Unlike Phase 0's <see cref="Decision"/>
/// (strictly Allow/Block), join attempts also have a <see cref="Hold"/> outcome for graylisted
/// SPs pending streamer/mod review (nl.txt: "graylist is a user in investigation").</summary>
public enum JoinDecision
{
    Allow,
    Deny,
    Hold,
}
