using System.Runtime.CompilerServices;
using System.Text;

namespace NL.Server;

/// <summary>
/// Reads lines from a growing text file. Used by every file-backed game adapter (Minecraft
/// logs, generic NDJSON pipes, etc.).
/// </summary>
public sealed class LineFileReader
{
    private readonly string _path;
    private readonly TimeSpan _pollInterval;
    private readonly bool _fromStart;
    private readonly bool _follow;

    /// <param name="fromStart">If true, replay existing content first (useful for --replay / tests).</param>
    /// <param name="follow">If true, keep polling for new lines after the current end (live mode).</param>
    public LineFileReader(string path, bool fromStart = false, bool follow = true, TimeSpan? pollInterval = null)
    {
        _path = path;
        _fromStart = fromStart;
        _follow = follow;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(250);
    }

    public async IAsyncEnumerable<string> ReadLinesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!File.Exists(_path) && !cancellationToken.IsCancellationRequested)
        {
            if (!_follow)
            {
                yield break;
            }

            await Task.Delay(_pollInterval, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(_path))
        {
            yield break;
        }

        using var stream = new FileStream(
            _path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        if (!_fromStart)
        {
            stream.Seek(0, SeekOrigin.End);
        }

        var pending = new StringBuilder();
        var readBuffer = new byte[8192];

        do
        {
            int read;
            while ((read = await stream.ReadAsync(readBuffer, cancellationToken)) > 0)
            {
                pending.Append(Encoding.UTF8.GetString(readBuffer, 0, read));

                int newlineIndex;
                while ((newlineIndex = IndexOfNewline(pending)) >= 0)
                {
                    var line = pending.ToString(0, newlineIndex).TrimEnd('\r');
                    pending.Remove(0, newlineIndex + 1);
                    yield return line;
                }
            }

            if (!_follow)
            {
                if (pending.Length > 0)
                {
                    yield return pending.ToString().TrimEnd('\r');
                    pending.Clear();
                }

                yield break;
            }

            await Task.Delay(_pollInterval, cancellationToken);
        }
        while (!cancellationToken.IsCancellationRequested);
    }

    private static int IndexOfNewline(StringBuilder sb)
    {
        for (var i = 0; i < sb.Length; i++)
        {
            if (sb[i] == '\n')
            {
                return i;
            }
        }

        return -1;
    }
}
