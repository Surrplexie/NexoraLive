using System.Diagnostics;
using NL.Core;
using NL.Server.Core;

namespace NL.Server;

/// <summary>
/// Runs an external process for each Block — escape hatch for any game that doesn't speak
/// RCON. Placeholders: <c>{player}</c>, <c>{event}</c>, <c>{decision}</c>, <c>{message}</c>.
/// Example: <c>--action-cmd "powershell -File kick.ps1 -Player {player} -Reason {message}"</c>
/// </summary>
public sealed class ProcessActionSink : IGameActionSink
{
    private readonly string _commandTemplate;

    public ProcessActionSink(string commandTemplate) => _commandTemplate = commandTemplate;

    public async Task ApplyAsync(SessionEvent sessionEvent, ActionResult result, CancellationToken cancellationToken)
    {
        var rendered = _commandTemplate
            .Replace("{player}", sessionEvent.PlayerName ?? "", StringComparison.Ordinal)
            .Replace("{event}", sessionEvent.Event.Name, StringComparison.Ordinal)
            .Replace("{decision}", result.Decision.ToString(), StringComparison.Ordinal)
            .Replace("{message}", result.Message ?? "", StringComparison.Ordinal);

        var psi = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
            Arguments = OperatingSystem.IsWindows() ? $"/C {rendered}" : $"-c \"{EscapeSh(rendered)}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start action process.");

        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            var err = await process.StandardError.ReadToEndAsync(cancellationToken);
            Console.Error.WriteLine($"[action] process exited {process.ExitCode}: {err.Trim()}");
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static string EscapeSh(string value) => value.Replace("\"", "\\\"", StringComparison.Ordinal);
}
