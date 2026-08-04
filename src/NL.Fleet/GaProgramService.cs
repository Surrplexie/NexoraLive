using NL.Fleet.Core;

namespace NL.Fleet;

public sealed class GaProgramService
{
    private readonly NlGaSettings _settings;
    private readonly JsonGaStreamerStore _store;

    public GaProgramService(NlGaSettings settings, JsonGaStreamerStore store)
    {
        _settings = settings;
        _store = store;
    }

    public NlGaSettings Settings => _settings;

    public GaProgramStatus GetStatus(int catalogGameCount)
    {
        var registered = _store.List().Count;
        return new GaProgramStatus(
            _settings.Enabled,
            _settings.OpenSignup,
            registered,
            catalogGameCount,
            Math.Max(_settings.MinCatalogGames, _settings.RequiredGameIds.Count),
            _settings.SlaTier,
            DateTimeOffset.UtcNow);
    }

    public IReadOnlyList<GaStreamerEntry> ListStreamers() => _store.List();

    public GaStreamerEntry Register(
        string displayName,
        string contact,
        string? twitchHandle,
        string? preferredGameId,
        string? streamerId = null)
    {
        if (!_settings.Enabled)
        {
            throw new InvalidOperationException("General availability program is not enabled.");
        }

        if (!_settings.OpenSignup)
        {
            throw new InvalidOperationException("GA open signup is closed.");
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

        var id = Guid.NewGuid().ToString("N")[..12];
        var resolvedStreamerId = string.IsNullOrWhiteSpace(streamerId)
            ? SlugStreamerId(displayName, id)
            : streamerId.Trim();

        var entry = new GaStreamerEntry(
            id,
            displayName.Trim(),
            normalizedContact,
            string.IsNullOrWhiteSpace(twitchHandle) ? null : twitchHandle.Trim(),
            string.IsNullOrWhiteSpace(preferredGameId) ? null : preferredGameId.Trim(),
            resolvedStreamerId,
            DateTimeOffset.UtcNow);
        return _store.Save(entry);
    }

    public bool IsStreamerAllowed(string streamerId)
    {
        if (!_settings.Enabled || _settings.OpenSignup)
        {
            return true;
        }

        return _store.GetByStreamerId(streamerId) is not null;
    }

    private static string SlugStreamerId(string displayName, string id)
    {
        var slug = new string(displayName
            .Trim()
            .ToLowerInvariant()
            .Where(ch => char.IsLetterOrDigit(ch) || ch == '-')
            .ToArray());
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "streamer";
        }

        return $"{slug}-{id[..6]}";
    }
}
