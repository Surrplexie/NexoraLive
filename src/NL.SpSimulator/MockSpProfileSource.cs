using NL.Core.Sp;

namespace NL.SpSimulator;

/// <summary>A single fake join attempt, with a human-readable description for CLI output.</summary>
public sealed record ScriptedJoinAttempt(
    string Description, SpProfile Profile, string StreamerId, JoinRequirements Requirements);

/// <summary>
/// Stands in for real join attempts (see ROADMAP.md Phase 3 for real NLServer integration).
/// Produces a fixed sequence of fake SP profiles against a fake streamer's join requirements,
/// exercising every branch of <see cref="JoinEligibilityEngine"/>: banned, graylist/hold,
/// Mod/Admin bypass, Vip/Friend social-requirement bypass, account age, verification, and the
/// active-offense threshold.
/// </summary>
public static class MockSpProfileSource
{
    private const string StreamerId = "streamer-zed";
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

    public static IEnumerable<ScriptedJoinAttempt> DefaultScript()
    {
        var strict = new JoinRequirements(
            RequireFollow: true,
            RequireSubscription: false,
            MinAccountAgeDays: 30,
            RequiredVerification: SpVerification.Email,
            MaxActiveOffenses: 2);

        yield return new ScriptedJoinAttempt(
            "Alice — long-time follower, verified, clean record",
            Profile: new SpProfile
                {
                    Id = "sp-alice",
                    DisplayName = "Alice",
                    AccountCreatedAtUtc = Now.AddYears(-2),
                    Verification = SpVerification.Email | SpVerification.TwoFactor,
                }
                .WithRelationship(StreamerId, isFollowing: true),
            StreamerId,
            strict);

        yield return new ScriptedJoinAttempt(
            "Bob — brand-new account, not following, unverified",
            Profile: new SpProfile
            {
                Id = "sp-bob",
                DisplayName = "Bob",
                AccountCreatedAtUtc = Now.AddDays(-1),
            },
            StreamerId,
            strict);

        yield return new ScriptedJoinAttempt(
            "Cara — banned from this streamer after a prior offense",
            Profile: new SpProfile
                {
                    Id = "sp-cara",
                    DisplayName = "Cara",
                    AccountCreatedAtUtc = Now.AddYears(-1),
                    Verification = SpVerification.Email,
                }
                .WithRelationship(StreamerId, standing: SpStanding.Banned, isFollowing: true),
            StreamerId,
            strict);

        yield return new ScriptedJoinAttempt(
            "Dax — graylisted pending investigation",
            Profile: new SpProfile
                {
                    Id = "sp-dax",
                    DisplayName = "Dax",
                    AccountCreatedAtUtc = Now.AddYears(-1),
                    Verification = SpVerification.Email,
                }
                .WithRelationship(StreamerId, standing: SpStanding.Graylist, isFollowing: true),
            StreamerId,
            strict);

        yield return new ScriptedJoinAttempt(
            "Erin — community Mod for this streamer (bypasses everything but a ban)",
            Profile: new SpProfile
                {
                    Id = "sp-erin",
                    DisplayName = "Erin",
                    AccountCreatedAtUtc = Now.AddDays(-2),
                }
                .WithRelationship(StreamerId, roles: new HashSet<SpRole> { SpRole.Mod }),
            StreamerId,
            strict);

        yield return new ScriptedJoinAttempt(
            "Faye — Vip, doesn't follow, but Vip bypasses the follow requirement",
            Profile: new SpProfile
                {
                    Id = "sp-faye",
                    DisplayName = "Faye",
                    AccountCreatedAtUtc = Now.AddYears(-1),
                    Verification = SpVerification.Email,
                }
                .WithRelationship(StreamerId, roles: new HashSet<SpRole> { SpRole.Vip }),
            StreamerId,
            strict);

        var profileWithOffenses = new SpProfile
        {
            Id = "sp-grace",
            DisplayName = "Grace",
            AccountCreatedAtUtc = Now.AddYears(-1),
            Verification = SpVerification.Email,
        }.WithRelationship(StreamerId, isFollowing: true);

        profileWithOffenses.Offenses.Add(new SpOffense(
            StreamerId, Now.AddMonths(-2), "mod-erin", "AFK-blocking a chokepoint"));
        profileWithOffenses.Offenses.Add(new SpOffense(
            StreamerId, Now.AddMonths(-1), "mod-erin", "Repeated griefing after warning"));
        profileWithOffenses.Offenses.Add(new SpOffense(
            StreamerId, Now.AddDays(-3), "streamer-zed", "Ignored boundary rule mid-session"));

        yield return new ScriptedJoinAttempt(
            "Grace — meets every requirement but has 3 active offenses (max is 2)",
            profileWithOffenses,
            StreamerId,
            strict);

        yield return new ScriptedJoinAttempt(
            "Hank — casual streamer with no join requirements at all",
            Profile: new SpProfile
            {
                Id = "sp-hank",
                DisplayName = "Hank",
                AccountCreatedAtUtc = Now.AddDays(-1),
            },
            StreamerId,
            JoinRequirements.None);
    }

    private static SpProfile WithRelationship(
        this SpProfile profile,
        string streamerId,
        SpStanding standing = SpStanding.Normal,
        bool isFollowing = false,
        bool isSubscribed = false,
        IReadOnlySet<SpRole>? roles = null)
    {
        profile.SetRelationship(new SpStreamerRelationship(streamerId, standing, isFollowing, isSubscribed, roles));
        return profile;
    }
}
