using System.Threading.Channels;
using NL.Server.Core;
using NL.Server.Core.Generic;

namespace NL.Server.Integration;

/// <summary>Maps newline-delimited text into <see cref="SessionEvent"/>s (shared by file/TCP/WS).</summary>
public sealed class LineStreamEventSource : IGameEventSource
{
    private readonly IAsyncEnumerable<string> _lines;
    private readonly LineToSessionEvents _mapLine;

    public LineStreamEventSource(IAsyncEnumerable<string> lines, LineToSessionEvents mapLine)
    {
        _lines = lines;
        _mapLine = mapLine;
    }

    public static LineStreamEventSource GenericJson(IAsyncEnumerable<string> lines) =>
        new(lines, line =>
        {
            var parsed = GenericJsonLineParser.TryParse(line);
            return parsed is null ? Array.Empty<SessionEvent>() : new[] { parsed };
        });

    public async IAsyncEnumerable<SessionEvent> ReadEventsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var line in _lines.WithCancellation(cancellationToken))
        {
            IEnumerable<SessionEvent> mapped;
            try
            {
                mapped = _mapLine(line);
            }
            catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException)
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
