using System.Text.Json;
using System.Text.Json.Serialization;
using NL.Core;
using NL.Moderation.Core;

namespace NL.Moderation;

/// <summary>
/// Durable <see cref="IModerationStore"/> backed by a JSON-Lines file (one JSON object per
/// line, append-only) — same "plain text, no database" spirit as the rest of the repo
/// (`hotkeys.log`, `.nle` files). Default location: <c>%LOCALAPPDATA%\NL\moderation.jsonl</c>.
/// Reads re-parse the whole file each call, which is fine at prototype scale; a real deployment
/// would want an index or a real database — see docs/MODERATION.md.
/// </summary>
public sealed class JsonlModerationStore : IModerationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly object _lock = new();

    public JsonlModerationStore(string path)
    {
        _path = path;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public Task AppendAsync(ModerationRecord record, CancellationToken cancellationToken = default)
    {
        var line = JsonSerializer.Serialize(record, JsonOptions);
        lock (_lock)
        {
            File.AppendAllText(_path, line + Environment.NewLine);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ModerationRecord>> GetRecentAsync(
        string streamerId, int count, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ModerationRecord> result = ReadAll()
            .Where(r => r.StreamerId == streamerId)
            .OrderByDescending(r => r.TimestampUtc)
            .Take(count)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<ModerationRecord>> GetForPlayerAsync(
        string streamerId, string playerId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ModerationRecord> result = ReadAll()
            .Where(r => r.StreamerId == streamerId && r.PlayerId == playerId)
            .OrderByDescending(r => r.TimestampUtc)
            .ToList();
        return Task.FromResult(result);
    }

    private List<ModerationRecord> ReadAll()
    {
        lock (_lock)
        {
            if (!File.Exists(_path))
            {
                return new List<ModerationRecord>();
            }

            var records = new List<ModerationRecord>();
            foreach (var line in File.ReadLines(_path))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var record = JsonSerializer.Deserialize<ModerationRecord>(line, JsonOptions);
                if (record is not null)
                {
                    records.Add(record);
                }
            }

            return records;
        }
    }
}
