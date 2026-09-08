using NL.Fleet.Core;

namespace NL.Fleet;

public sealed class BetaProgramService
{
    private readonly NlBetaSettings _settings;
    private readonly JsonBetaWaitlistStore _store;

    public BetaProgramService(NlBetaSettings settings, JsonBetaWaitlistStore store)
    {
        _settings = settings;
        _store = store;
    }

    public NlBetaSettings Settings => _settings;

    public BetaProgramStatus GetStatus()
    {
        var entries = _store.List();
        var approved = entries.Count(e => e.Status == BetaWaitlistStatus.Approved);
        var pending = entries.Count(e => e.Status == BetaWaitlistStatus.Pending);
        return new BetaProgramStatus(
            _settings.Enabled,
            _settings.WaitlistOpen,
            _settings.MaxApprovedStreamers,
            pending,
            approved,
            Math.Max(0, _settings.MaxApprovedStreamers - approved),
            DateTimeOffset.UtcNow);
    }

    public IReadOnlyList<BetaWaitlistEntry> ListWaitlist() => _store.List();

    public BetaWaitlistEntry SignUp(string displayName, string contact, string? twitchHandle, string? requestedGameId)
    {
        if (!_settings.Enabled)
        {
            throw new InvalidOperationException("Beta program is not enabled.");
        }

        if (!_settings.WaitlistOpen)
        {
            throw new InvalidOperationException("Beta waitlist is closed.");
        }

        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(contact))
        {
            throw new ArgumentException("displayName and contact are required.");
        }

        var normalizedContact = contact.Trim().ToLowerInvariant();
        var existing = _store.List().FirstOrDefault(e =>
            string.Equals(e.Contact, normalizedContact, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var entry = new BetaWaitlistEntry(
            Guid.NewGuid().ToString("N")[..12],
            displayName.Trim(),
            normalizedContact,
            string.IsNullOrWhiteSpace(twitchHandle) ? null : twitchHandle.Trim(),
            string.IsNullOrWhiteSpace(requestedGameId) ? null : requestedGameId.Trim(),
            BetaWaitlistStatus.Pending,
            null,
            DateTimeOffset.UtcNow,
            null);
        return _store.Save(entry);
    }

    public BetaWaitlistEntry Approve(string id, string? streamerId = null)
    {
        var entry = _store.Get(id) ?? throw new InvalidOperationException("Waitlist entry not found.");
        if (entry.Status == BetaWaitlistStatus.Approved)
        {
            return entry;
        }

        var status = GetStatus();
        if (status.ApprovedCount >= _settings.MaxApprovedStreamers)
        {
            throw new InvalidOperationException("Beta streamer capacity reached.");
        }

        var approvedStreamerId = string.IsNullOrWhiteSpace(streamerId)
            ? $"beta-{entry.Id}"
            : streamerId.Trim();

        var updated = entry with
        {
            Status = BetaWaitlistStatus.Approved,
            ApprovedStreamerId = approvedStreamerId,
            ResolvedAtUtc = DateTimeOffset.UtcNow,
        };
        return _store.Save(updated);
    }

    public BetaWaitlistEntry Reject(string id)
    {
        var entry = _store.Get(id) ?? throw new InvalidOperationException("Waitlist entry not found.");
        var updated = entry with
        {
            Status = BetaWaitlistStatus.Rejected,
            ResolvedAtUtc = DateTimeOffset.UtcNow,
        };
        return _store.Save(updated);
    }

    public BetaStreamerCheckResult CheckStreamer(string streamerId)
    {
        if (!_settings.Enabled || !_settings.EnforceStreamerAllowlist)
        {
            return new BetaStreamerCheckResult(true);
        }

        if (_settings.OperatorStreamers.Any(s =>
                string.Equals(s, streamerId, StringComparison.OrdinalIgnoreCase)))
        {
            return new BetaStreamerCheckResult(true);
        }

        if (_store.IsApprovedStreamer(streamerId))
        {
            return new BetaStreamerCheckResult(true);
        }

        return new BetaStreamerCheckResult(
            false,
            "Streamer is not approved for public beta. Join the waitlist at /beta.html");
    }
}
