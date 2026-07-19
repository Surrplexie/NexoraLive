using System.Windows.Forms;

namespace NL.ConfigEditor;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Optional: open a specific file passed via CLI (e.g. from the daemon tray menu).
        var initialPath = args.Length > 0 ? args[0] : null;

        // If no path given, default to the persistent user config (same as the daemon uses).
        if (initialPath is null)
        {
            // Shared %LOCALAPPDATA%\NL\hotkeys.nle (same path as Hotkey Daemon / NlPaths).
            var defaultPath = NL.Core.NlPaths.HotkeysNle;
            if (File.Exists(defaultPath))
                initialPath = defaultPath;
        }

        Application.Run(new EditorForm(initialPath));
    }
}
