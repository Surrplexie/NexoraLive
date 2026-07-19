namespace NL.Core.Sp;

/// <summary>
/// A streamer's configurable join requirements (nl.txt section 2: "listed requirements
/// include, but are not limited to; being subscribed or following... social media platforms
/// connected... account must be xyz age... account must be verified... minimum of xyz number
/// of verified warnings, bans"). All fields default to "no requirement" so an empty instance
/// means anyone in Normal standing can join freely.
/// </summary>
public sealed record JoinRequirements(
    bool RequireFollow = false,
    bool RequireSubscription = false,
    int MinAccountAgeDays = 0,
    SpVerification RequiredVerification = SpVerification.None,
    int MaxActiveOffenses = int.MaxValue,
    bool AllowGraylistWithHold = true)
{
    public static readonly JoinRequirements None = new();
}
