using System.Text.Json;
using NL.Fork.Catalog.Core;
using NL.Partnership.Core;

namespace NL.Partnership;

public sealed class JsonPublisherRegistry : IPublisherRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();
    private List<PublisherRegistration> _publishers = [];

    public JsonPublisherRegistry(string? path = null)
    {
        _path = path ?? NlPartnershipPaths.Publishers;
        Load();
    }

    public IReadOnlyList<PublisherRegistration> List()
    {
        lock (_lock)
        {
            return _publishers.ToList();
        }
    }

    public PublisherRegistration? Get(string publisherId)
    {
        lock (_lock)
        {
            return _publishers.FirstOrDefault(p =>
                string.Equals(p.PublisherId, publisherId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public PublisherRegistration Save(PublisherRegistration publisher)
    {
        lock (_lock)
        {
            var idx = _publishers.FindIndex(p =>
                string.Equals(p.PublisherId, publisher.PublisherId, StringComparison.OrdinalIgnoreCase));
            var saved = publisher with
            {
                RegisteredAtUtc = publisher.RegisteredAtUtc ?? DateTimeOffset.UtcNow,
            };
            if (idx >= 0)
            {
                _publishers[idx] = saved;
            }
            else
            {
                _publishers.Add(saved);
            }

            Persist();
            return saved;
        }
    }

    public PublisherRegistration SetTitleStatus(string publisherId, string gameId, PublisherTitleStatus status)
    {
        lock (_lock)
        {
            var idx = _publishers.FindIndex(p =>
                string.Equals(p.PublisherId, publisherId, StringComparison.OrdinalIgnoreCase));
            if (idx < 0)
            {
                throw new InvalidOperationException($"Unknown publisher '{publisherId}'.");
            }

            var pub = _publishers[idx];
            var titles = pub.Titles.ToList();
            var titleIdx = titles.FindIndex(t => string.Equals(t.GameId, gameId, StringComparison.OrdinalIgnoreCase));
            if (titleIdx < 0)
            {
                titles.Add(new PublisherTitle(gameId, PartnershipTier.Official, status));
            }
            else
            {
                titles[titleIdx] = titles[titleIdx] with { Status = status };
            }

            var updated = pub with { Titles = titles };
            _publishers[idx] = updated with { RegisteredAtUtc = updated.RegisteredAtUtc ?? DateTimeOffset.UtcNow };
            Persist();
            return _publishers[idx];
        }
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        var json = File.ReadAllText(_path);
        _publishers = JsonSerializer.Deserialize<List<PublisherRegistration>>(json, JsonOptions) ?? [];
    }

    private void Persist()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(_publishers, JsonOptions));
    }
}
