using NL.Core.Sp;
using Xunit;

namespace NL.Core.Tests;

public class JoinEligibilityEngineTests
{
    private const string StreamerId = "streamer-zed";
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

    private static SpProfile MakeProfile(
        DateTimeOffset createdAt,
        SpVerification verification = SpVerification.None) => new()
    {
        Id = "sp-test",
        DisplayName = "Test SP",
        AccountCreatedAtUtc = createdAt,
        Verification = verification,
    };

    [Fact]
    public void Evaluate_BannedStanding_AlwaysDenies()
    {
        var profile = MakeProfile(Now.AddYears(-5), SpVerification.Email);
        profile.SetRelationship(new SpStreamerRelationship(
            StreamerId, SpStanding.Banned, IsFollowing: true));

        var result = JoinEligibilityEngine.Evaluate(profile, StreamerId, JoinRequirements.None, Now);

        Assert.Equal(JoinDecision.Deny, result.Decision);
        Assert.Contains("banned", result.Reason);
    }

    [Fact]
    public void Evaluate_GraylistStanding_HoldsByDefault()
    {
        var profile = MakeProfile(Now.AddYears(-1));
        profile.SetRelationship(new SpStreamerRelationship(StreamerId, SpStanding.Graylist));

        var result = JoinEligibilityEngine.Evaluate(profile, StreamerId, JoinRequirements.None, Now);

        Assert.Equal(JoinDecision.Hold, result.Decision);
    }

    [Fact]
    public void Evaluate_GraylistStanding_DeniesWhenStreamerDisallowsHold()
    {
        var profile = MakeProfile(Now.AddYears(-1));
        profile.SetRelationship(new SpStreamerRelationship(StreamerId, SpStanding.Graylist));

        var requirements = JoinRequirements.None with { AllowGraylistWithHold = false };
        var result = JoinEligibilityEngine.Evaluate(profile, StreamerId, requirements, Now);

        Assert.Equal(JoinDecision.Deny, result.Decision);
    }

    [Fact]
    public void Evaluate_ModRole_BypassesEverythingExceptBan()
    {
        var profile = MakeProfile(Now.AddDays(-1)); // brand new, unverified
        profile.SetRelationship(new SpStreamerRelationship(
            StreamerId, Roles: new HashSet<SpRole> { SpRole.Mod }));

        var requirements = new JoinRequirements(
            RequireFollow: true, MinAccountAgeDays: 365, RequiredVerification: SpVerification.Id);
        var result = JoinEligibilityEngine.Evaluate(profile, StreamerId, requirements, Now);

        Assert.Equal(JoinDecision.Allow, result.Decision);
    }

    [Fact]
    public void Evaluate_ModRole_StillDeniedIfBanned()
    {
        var profile = MakeProfile(Now.AddYears(-3));
        profile.SetRelationship(new SpStreamerRelationship(
            StreamerId, SpStanding.Banned, Roles: new HashSet<SpRole> { SpRole.Mod }));

        var result = JoinEligibilityEngine.Evaluate(profile, StreamerId, JoinRequirements.None, Now);

        Assert.Equal(JoinDecision.Deny, result.Decision);
    }

    [Fact]
    public void Evaluate_RequireFollow_DeniesWhenNotFollowing()
    {
        var profile = MakeProfile(Now.AddYears(-1), SpVerification.Email);
        var requirements = new JoinRequirements(RequireFollow: true);

        var result = JoinEligibilityEngine.Evaluate(profile, StreamerId, requirements, Now);

        Assert.Equal(JoinDecision.Deny, result.Decision);
        Assert.Contains("follow", result.Reason);
    }

    [Fact]
    public void Evaluate_VipRole_BypassesFollowRequirementButNotAgeOrVerification()
    {
        var profile = MakeProfile(Now.AddDays(-1)); // too new, unverified
        profile.SetRelationship(new SpStreamerRelationship(
            StreamerId, Roles: new HashSet<SpRole> { SpRole.Vip }));

        var requirements = new JoinRequirements(
            RequireFollow: true, MinAccountAgeDays: 30, RequiredVerification: SpVerification.Email);
        var result = JoinEligibilityEngine.Evaluate(profile, StreamerId, requirements, Now);

        Assert.Equal(JoinDecision.Deny, result.Decision);
        Assert.Contains("age", result.Reason);
    }

    [Theory]
    [InlineData(10, 30, JoinDecision.Deny)]
    [InlineData(60, 30, JoinDecision.Allow)]
    public void Evaluate_MinAccountAge_GatesOnAgeThreshold(int ageDays, int minDays, JoinDecision expected)
    {
        var profile = MakeProfile(Now.AddDays(-ageDays));
        var requirements = new JoinRequirements(MinAccountAgeDays: minDays);

        var result = JoinEligibilityEngine.Evaluate(profile, StreamerId, requirements, Now);

        Assert.Equal(expected, result.Decision);
    }

    [Fact]
    public void Evaluate_RequiredVerification_DeniesWhenMissingFlag()
    {
        var profile = MakeProfile(Now.AddYears(-1), SpVerification.Phone);
        var requirements = new JoinRequirements(RequiredVerification: SpVerification.Email | SpVerification.Id);

        var result = JoinEligibilityEngine.Evaluate(profile, StreamerId, requirements, Now);

        Assert.Equal(JoinDecision.Deny, result.Decision);
        Assert.Contains("verification", result.Reason);
    }

    [Fact]
    public void Evaluate_ActiveOffensesUnderThreshold_Allows()
    {
        var profile = MakeProfile(Now.AddYears(-1), SpVerification.Email);
        profile.Offenses.Add(new SpOffense(StreamerId, Now.AddMonths(-6), "mod-1", "minor"));

        var requirements = new JoinRequirements(MaxActiveOffenses: 2);
        var result = JoinEligibilityEngine.Evaluate(profile, StreamerId, requirements, Now);

        Assert.Equal(JoinDecision.Allow, result.Decision);
    }

    [Fact]
    public void Evaluate_ActiveOffensesOverThreshold_Denies()
    {
        var profile = MakeProfile(Now.AddYears(-1), SpVerification.Email);
        profile.Offenses.Add(new SpOffense(StreamerId, Now.AddMonths(-6), "mod-1", "minor"));
        profile.Offenses.Add(new SpOffense(StreamerId, Now.AddMonths(-3), "mod-1", "minor 2"));
        profile.Offenses.Add(new SpOffense(StreamerId, Now.AddDays(-10), "mod-1", "minor 3"));

        var requirements = new JoinRequirements(MaxActiveOffenses: 2);
        var result = JoinEligibilityEngine.Evaluate(profile, StreamerId, requirements, Now);

        Assert.Equal(JoinDecision.Deny, result.Decision);
    }

    [Fact]
    public void Evaluate_ArchivedOffensesBeyondTwoYears_DoNotCountTowardThreshold()
    {
        var profile = MakeProfile(Now.AddYears(-5), SpVerification.Email);
        // 3 years old — beyond the 2-year active window, so archived and shouldn't count.
        profile.Offenses.Add(new SpOffense(StreamerId, Now.AddYears(-3), "mod-1", "old offense"));

        var requirements = new JoinRequirements(MaxActiveOffenses: 0);
        var result = JoinEligibilityEngine.Evaluate(profile, StreamerId, requirements, Now);

        Assert.Equal(JoinDecision.Allow, result.Decision);
        Assert.Equal(0, profile.ActiveOffenseCount(StreamerId, Now));
    }

    [Fact]
    public void Evaluate_NoRequirements_AllowsNormalStandingSp()
    {
        var profile = MakeProfile(Now.AddDays(-1));
        var result = JoinEligibilityEngine.Evaluate(profile, StreamerId, JoinRequirements.None, Now);

        Assert.Equal(JoinDecision.Allow, result.Decision);
    }

    [Fact]
    public void Evaluate_UnknownStreamer_UsesDefaultNormalStandingRelationship()
    {
        var profile = MakeProfile(Now.AddYears(-1));
        var result = JoinEligibilityEngine.Evaluate(profile, "some-other-streamer", JoinRequirements.None, Now);

        Assert.Equal(JoinDecision.Allow, result.Decision);
    }
}
