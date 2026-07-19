using System.Windows.Forms;

namespace NL.HotkeyDaemon;

internal static class Program
{
    private const string MutexName = "Global\\SCHotkeyDaemon_SingleInstance";

    /// <summary>
    /// Default user-scoped config location. Survives repo moves, and is the path that
    /// "Start at login" uses — so the daemon always loads the right file regardless of
    /// where the binary lives.
    /// </summary>
    public static readonly string DefaultConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NL", "hotkeys.nle");

    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Single-instance guard: if another copy is running, surface a tip and exit.
        using var mutex = new System.Threading.Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            // Can't use MessageBox (hidden window setup) but a brief tray balloon from a
            // temporary NotifyIcon works even for the short-lived second instance.
            using var tempIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                BalloonTipTitle = "NL Hotkey Daemon",
                BalloonTipText = "Already running — check the system tray.",
            };
            tempIcon.ShowBalloonTip(3000);
            System.Threading.Thread.Sleep(3500);
            tempIcon.Visible = false;
            return;
        }

        var sceConfigPath = ResolveConfigPath(args);

        // Auto-create %LOCALAPPDATA%\NL\hotkeys.nle from the embedded template if neither
        // the user-level file nor a repo sample exists anywhere.
        if (!File.Exists(sceConfigPath))
        {
            sceConfigPath = EnsureDefaultConfig();
        }

        Application.Run(new TrayAppContext(sceConfigPath));
    }

    private static string ResolveConfigPath(string[] args)
    {
        // 1. Explicit CLI arg wins.
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            return args[0];
        }

        // 2. Persistent user-scoped config (survives repo moves; used by Start at Login).
        if (File.Exists(DefaultConfigPath))
        {
            return DefaultConfigPath;
        }

        // 3. Repo sample — walk upward from binary so `dotnet run` works without args.
        var repoSample = FindRepoFile("samples/configs/hotkeys.nle", AppContext.BaseDirectory);
        if (File.Exists(repoSample))
        {
            return repoSample;
        }

        // 4. Fall back to the default path so EnsureDefaultConfig() creates it there.
        return DefaultConfigPath;
    }

    /// <summary>Creates a starter <c>hotkeys.nle</c> at the default location and returns its path.</summary>
    private static string EnsureDefaultConfig()
    {
        var dir = Path.GetDirectoryName(DefaultConfigPath)!;
        Directory.CreateDirectory(dir);

        File.WriteAllText(DefaultConfigPath, DefaultConfigTemplate);
        HotkeyLog.Append($"Created default config at {DefaultConfigPath}");
        return DefaultConfigPath;
    }

    /// <summary>Walks upward from the build output directory looking for the repo's
    /// samples/ folder — only used for development via <c>dotnet run</c>.</summary>
    private static string FindRepoFile(string relativePath, string baseDir)
    {
        var dir = new DirectoryInfo(baseDir);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return Path.Combine(baseDir, relativePath);
    }

    private const string DefaultConfigTemplate = """
        # NL Hotkey Daemon — default config
        # Edit this file and save; the daemon auto-reloads within ~1 second.
        # Full guide: https://github.com/your-org/nl/blob/main/NLE_GUIDE.md

        # ── Hotkey bindings ────────────────────────────────────────────────────────
        # Format: hotkey "Ctrl+Alt+<key>": actionName
        # Tip: pick combos unlikely to clash with in-game keys.

        hotkey "Ctrl+Alt+0": toggleMic
        hotkey "Ctrl+Alt+9": announce
        hotkey "Ctrl+Alt+8": toggleNlEvents
        hotkey "Ctrl+Alt+7": openLog
        hotkey "Ctrl+Alt+6": clipStream
        hotkey "Ctrl+Alt+5": focusOBS
        hotkey "Ctrl+Alt+4": muteDesktop

        # ── NLEvent rules ──────────────────────────────────────────────────────────
        # allow = action runs, block = action is skipped.
        # Change allow ↔ block and save to toggle any hotkey instantly.

        event toggleMic:
            allow

        event announce:
            warn "Thanks for watching!"
            allow

        event toggleNlEvents:
            allow

        event openLog:
            allow

        event clipStream:
            allow

        event focusOBS:
            allow

        event muteDesktop:
            allow
        """;
}
