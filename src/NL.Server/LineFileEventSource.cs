using System.Text.Json;
using NL.Server.Core;
using NL.Server.Core.Generic;

namespace NL.Server;

/// <summary>Turns each raw text line into zero or more <see cref="SessionEvent"/>s.</summary>
public delegate IEnumerable<SessionEvent> LineToSessionEvents(string rawLine);

/// <summary>
/// File-backed <see cref="IGameEventSource"/> used by Minecraft and generic NDJSON adapters.
/// </summary>
public sealed class LineFileEventSource : IGameEventSource
{
    private readonly LineFileReader _reader;
    private readonly LineToSessionEvents _mapLine;

    public LineFileEventSource(LineFileReader reader, LineToSessionEvents mapLine)
    {
        _reader = reader;
        _mapLine = mapLine;
    }

    public static LineFileEventSource Minecraft(string path, bool replay)
    {
        var mapper = new MinecraftEventMapper();
        return new LineFileEventSource(
            new LineFileReader(path, fromStart: replay, follow: !replay),
            line => mapper.MapAll(MinecraftLogParser.Parse(line)));
    }

    public static LineFileEventSource GenericJson(string path, bool replay) =>
        new(
            new LineFileReader(path, fromStart: replay, follow: !replay),
            line =>
            {
                var parsed = GenericJsonLineParser.TryParse(line);
                return parsed is null ? Array.Empty<SessionEvent>() : new[] { parsed };
            });

    public async IAsyncEnumerable<SessionEvent> ReadEventsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var line in _reader.ReadLinesAsync(cancellationToken))
        {
            IEnumerable<SessionEvent> mapped;
            try
            {
                mapped = _mapLine(line);
            }
            catch (Exception ex) when (ex is FormatException or JsonException)
            {
                Console.Error.WriteLine($"[source] skipped bad line: {ex.Message}");
                continue;
            }

            foreach (var sessionEvent in mapped)
            {
                yield return sessionEvent;
            }
        }
    }
}
