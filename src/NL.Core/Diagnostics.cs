namespace NL.Core;

/// <summary>
/// Raised for any lexical or syntax problem in a .nle file. Carries the offending
/// source line so the CLI/tests can surface a useful message to the streamer authoring
/// the config, rather than a raw stack trace.
/// </summary>
public sealed class NlSyntaxException : Exception
{
    public int Line { get; }

    public NlSyntaxException(string message, int line)
        : base($"line {line}: {message}")
    {
        Line = line;
    }
}
