namespace NL.Core.Sp;

/// <summary>
/// Evaluates whether a <see cref="SpProfile"/> may join a specific streamer's NLServer, given
/// that streamer's <see cref="JoinRequirements"/> (nl.txt section 2, ROADMAP.md Phase 2). This
/// mirrors Phase 0's <see cref="RuleEngine"/> in spirit — deterministic, side-effect-free,
/// "facts in, decision out" — but is a dedicated evaluator rather than routed through the
/// `.nle` grammar, because standing/roles/offenses are categorical account data, not numeric
/// per-event properties like <see cref="GameEvent"/>.
/// </summary>
public static class JoinEligibilityEngine
{
    /// <summary>
    /// Checks are applied in this order, matching nl.txt's own narrative (banned first, then
    /// graylist/investigation, then privileged-role bypass, then the streamer's configurable
    /// requirements):
    /// 1. Banned standing → always <see cref="JoinDecision.Deny"/>, no exceptions.
    /// 2. Graylist standing (and not Admin/Mod) → <see cref="JoinDecision.Hold"/> for review,
    ///    or Deny if the streamer disallows graylist joins entirely.
    /// 3. Admin/Mod for this streamer → bypasses every requirement below.
    /// 4. Follow/subscription requirements → bypassed by Admin/Mod/Vip/Friend.
    /// 5. Minimum account age.
    /// 6. Required verification flags.
    /// 7. Maximum active offenses with this streamer.
    /// </summary>
    public static JoinResult Evaluate(
        SpProfile profile, string streamerId, JoinRequirements requirements, DateTimeOffset nowUtc)
    {
        var relationship = profile.GetRelationship(streamerId);

        if (relationship.Standing == SpStanding.Banned)
        {
            return JoinResult.Deny("SP is banned from this streamer's NLServer.");
        }

        if (relationship.Standing == SpStanding.Graylist && !relationship.IsPrivileged)
        {
            return requirements.AllowGraylistWithHold
                ? JoinResult.Hold("SP is graylisted; held pending streamer/mod review.")
                : JoinResult.Deny("SP is graylisted and this streamer requires full standing to join.");
        }

        if (relationship.IsPrivileged)
        {
            var roleNames = string.Join("/", relationship.RolesOrEmpty.Where(r => r is SpRole.Admin or SpRole.Mod));
            return JoinResult.Allow($"Allowed via {roleNames} role (bypasses join requirements).");
        }

        if (!relationship.BypassesSocialRequirements)
        {
            if (requirements.RequireFollow && !relationship.IsFollowing)
            {
                return JoinResult.Deny("Streamer requires SPs to follow before joining.");
            }

            if (requirements.RequireSubscription && !relationship.IsSubscribed)
            {
                return JoinResult.Deny("Streamer requires SPs to be subscribed before joining.");
            }
        }

        var accountAgeDays = profile.AccountAgeDays(nowUtc);
        if (accountAgeDays < requirements.MinAccountAgeDays)
        {
            return JoinResult.Deny(
                $"Account age {accountAgeDays:F0}d is below the required {requirements.MinAccountAgeDays}d.");
        }

        var missingVerification = requirements.RequiredVerification & ~profile.Verification;
        if (missingVerification != SpVerification.None)
        {
            return JoinResult.Deny($"Missing required verification: {missingVerification}.");
        }

        var activeOffenses = profile.ActiveOffenseCount(streamerId, nowUtc);
        if (activeOffenses > requirements.MaxActiveOffenses)
        {
            return JoinResult.Deny(
                $"SP has {activeOffenses} active offense(s) with this streamer, " +
                $"exceeding the max of {requirements.MaxActiveOffenses}.");
        }

        return JoinResult.Allow();
    }
}
