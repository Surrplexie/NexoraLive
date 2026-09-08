using System.Text;

namespace NL.HotkeyDaemon;

/// <summary>Real, persistent audit trail of every hotkey press and what the daemon did about
/// it - a tiny stand-in for the "transparency" logging idea from nl.txt.</summary>
internal static class HotkeyLog
{
    public static string LogFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NL", "hotkeys.log");

    public static void Append(string line)
    {
        var directory = Path.GetDirectoryName(LogFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var timestamped = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {line}";
        File.AppendAllText(LogFilePath, timestamped + Environment.NewLine, Encoding.UTF8);
    }
}
