using NL.Client.Core;
using NL.Core.Sp;

namespace NL.Client;

public static class NlClientOverlayBuilder
{
    public static NlClientOverlayState Build(SpProfile profile, string streamerId)
    {
        var now = DateTimeOffset.UtcNow;
        var rel = profile.GetRelationship(streamerId);
        var warnings = profile.Offenses
            .Where(o => string.Equals(o.StreamerId, streamerId, StringComparison.OrdinalIgnoreCase) && o.IsActive(now))
            .OrderByDescending(o => o.IssuedAtUtc)
            .Take(5)
            .Select(o => o.Reason)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToList();

        return new NlClientOverlayState(
            profile.Id,
            streamerId,
            rel.Standing.ToString(),
            profile.ActiveOffenseCount(streamerId, now),
            warnings,
            ClipTriggerAvailable: rel.Standing != SpStanding.Banned,
            now);
    }
}
