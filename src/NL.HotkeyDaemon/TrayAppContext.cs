using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using NL.Core;
using NL.Core.Ast;
using NL.HotkeyDaemon.Core;

namespace NL.HotkeyDaemon;

/// <summary>
/// Owns the tray icon, the hidden hotkey window, the loaded NLEvents rules, and the hotkey
/// action bindings. Everything upstream (NL.Core, NL.HotkeyDaemon.Core) is pure and
/// unit-tested; this class is the thin, manually verified layer that actually touches Windows.
/// </summary>
internal sealed class TrayAppContext : ApplicationContext
{
    private const string RegistryRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string RegistryValueName = "SCHotkeyDaemon";

    private readonly string _sceConfigPath;
    private readonly HotkeyWindow _hotkeyWindow;
    private readonly NotifyIcon _trayIcon;
    private readonly ActionHandlers _handlers = new();

    private ActionDispatcher _dispatcher;
    private FileSystemWatcher? _watcher;
    private System.Windows.Forms.Timer? _debounceTimer;
    private string? _lastError;

    public TrayAppContext(string sceConfigPath)
    {
        _sceConfigPath = sceConfigPath;

        var (engine, hotkeys, obsConfig) = LoadConfigOrFallback(sceConfigPath);
        _dispatcher = new ActionDispatcher(engine);
        _handlers.OBSConfig = obsConfig;

        _hotkeyWindow = new HotkeyWindow();
        _hotkeyWindow.HotkeyPressed += OnHotkeyPressed;

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "NL Hotkey Daemon",
            ContextMenuStrip = BuildMenu(),
            Visible = true,
        };

        RegisterHotkeys(hotkeys);
        StartWatcher();

        HotkeyLog.Append("NL Hotkey Daemon started.");

        if (_lastError is not null)
        {
            Notify("NL: config error", _lastError);
        }
        else
        {
            Notify("NL Hotkey Daemon", $"Running with {hotkeys.Count} hotkey(s). Config: {Path.GetFileName(_sceConfigPath)}");
        }
    }

    private void OnHotkeyPressed(HotkeyBinding binding)
    {
        var decision = _dispatcher.Dispatch(binding.Action);

        var resultText = decision.ShouldPerform
            ? _handlers.Perform(decision.Action, decision.Message)
            : $"Skipped: {decision.Message}";

        HotkeyLog.Append($"{binding.Combo} -> '{decision.Action}' [{decision.Outcome}] {resultText}");
        Notify(binding.Combo.ToString(), resultText);
    }

    private void Notify(string title, string message)
    {
        _trayIcon.BalloonTipTitle = title;
        _trayIcon.BalloonTipText = message;
        _trayIcon.ShowBalloonTip(4000);
    }

    // ── Config loading ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to load the config; on error stores <see cref="_lastError"/> and returns a
    /// no-op engine so the daemon still starts (and the tray icon is clickable to fix the file).
    /// </summary>
    private (RuleEngine engine, List<HotkeyBinding> hotkeys, OBSConfig obsConfig) LoadConfigOrFallback(string path)
    {
        try
        {
            var result = LoadConfig(path);
            _lastError = null;
            return result;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            HotkeyLog.Append($"[config error] {ex.Message}");
            return (new RuleEngine(new ConfigAst([])), [], new OBSConfig());
        }
    }

    private static (RuleEngine engine, List<HotkeyBinding> hotkeys, OBSConfig obsConfig) LoadConfig(string path)
    {
        var source = File.ReadAllText(path);
        var ast = Parser.Parse(source, out var parseWarnings);

        foreach (var w in parseWarnings)
        {
            HotkeyLog.Append($"[.nle parse warning] {w}");
        }

        var engine = new RuleEngine(ast);
        foreach (var w in engine.LoadWarnings)
        {
            HotkeyLog.Append($"[.nle rule warning] {w}");
        }

        var hotkeys = ResolveHotkeys(ast);
        var obsConfig = OBSConfig.LoadFromFileOrDefault(path);

        return (engine, hotkeys, obsConfig);
    }

    private static List<HotkeyBinding> ResolveHotkeys(ConfigAst ast)
    {
        var bindings = new List<HotkeyBinding>();
        foreach (var decl in ast.HotkeyDeclarations)
        {
            if (!HotkeyCombo.TryParse(decl.ComboText, out var combo, out var error))
            {
                HotkeyLog.Append($"[hotkey] line {decl.Line}: cannot parse '{decl.ComboText}': {error}; skipped");
                continue;
            }

            bindings.Add(new HotkeyBinding(combo!, decl.Action));
        }

        return bindings;
    }

    private void RegisterHotkeys(List<HotkeyBinding> hotkeys)
    {
        foreach (var binding in hotkeys)
        {
            if (!_hotkeyWindow.TryRegister(binding, out var error))
            {
                HotkeyLog.Append($"[hotkey registration failed] {binding.Combo} -> '{binding.Action}': {error}");
            }
        }
    }

    // ── FileSystemWatcher + debounce ───────────────────────────────────────────────────────────

    private void StartWatcher()
    {
        var dir = Path.GetDirectoryName(_sceConfigPath)!;
        var file = Path.GetFileName(_sceConfigPath);

        _watcher = new FileSystemWatcher(dir, file)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };

        _watcher.Changed += OnFileChanged;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _hotkeyWindow.Invoke(() =>
        {
            if (_debounceTimer is null)
            {
                _debounceTimer = new System.Windows.Forms.Timer { Interval = 800 };
                _debounceTimer.Tick += (_, _) =>
                {
                    _debounceTimer.Stop();
                    ReloadConfig();
                };
            }

            _debounceTimer.Stop();
            _debounceTimer.Start();
        });
    }

    // ── Context menu ───────────────────────────────────────────────────────────────────────────

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Status",              null, (_, _) => ShowStatus());
        menu.Items.Add("Reload config",       null, (_, _) => ReloadConfig());
        menu.Items.Add("Open Config Editor",  null, (_, _) => OpenConfigEditor());
        menu.Items.Add("Open log",            null, (_, _) => _handlers.Perform("openLog", null));
        menu.Items.Add("Open OBS config",     null, (_, _) => OpenOBSConfig());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Start at login",      null, (_, _) => ToggleStartAtLogin());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit",                null, (_, _) => ExitThread());

        // Tick the login item if it is already registered.
        // Menu item index 6 is "Start at login" (after adding the editor item).
        if (menu.Items[6] is ToolStripMenuItem loginItem)
        {
            loginItem.Checked = IsStartAtLoginEnabled();
        }

        return menu;
    }

    private void OpenConfigEditor()
    {
        // Look for NL.ConfigEditor.exe next to the daemon binary first (published layout),
        // then fall back to a sibling project build output (dev layout).
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "NL.ConfigEditor.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "NL.ConfigEditor", "NL.ConfigEditor.exe"),
        };

        var editorExe = candidates.FirstOrDefault(File.Exists);

        if (editorExe is not null)
        {
            Process.Start(new ProcessStartInfo(editorExe, $"\"{_sceConfigPath}\"") { UseShellExecute = true });
        }
        else
        {
            // Editor not built yet — open the config in Notepad as a fallback.
            HotkeyLog.Append("[editor] NL.ConfigEditor.exe not found — opening config in Notepad.");
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{_sceConfigPath}\"") { UseShellExecute = true });
            Notify("NL Hotkey Daemon", "Config Editor not found — opened in Notepad instead. Run: dotnet run --project src/NL.ConfigEditor");
        }
    }

    private void ShowStatus()
    {
        var state = _dispatcher.Enabled ? "ENABLED" : "DISABLED";
        var configName = Path.GetFileName(_sceConfigPath);

        if (_lastError is not null)
        {
            Notify("NL: config error", $"Last load failed: {_lastError}\nFix the file and reload.");
            return;
        }

        var loginState = IsStartAtLoginEnabled() ? "yes" : "no";
        Notify("NL Hotkey Daemon", $"NLEvents {state} | Config: {configName} | Start at login: {loginState}");
    }

    private void ReloadConfig()
    {
        var wasEnabled = _dispatcher.Enabled;
        _hotkeyWindow.UnregisterAll();

        try
        {
            var (engine, hotkeys, obsConfig) = LoadConfig(_sceConfigPath);
            _lastError = null;
            _dispatcher = new ActionDispatcher(engine, wasEnabled);
            _handlers.OBSConfig = obsConfig;
            RegisterHotkeys(hotkeys);

            HotkeyLog.Append($"Config reloaded ({hotkeys.Count} hotkey(s)).");
            Notify("NL Hotkey Daemon", $"Config reloaded — {hotkeys.Count} hotkey(s).");
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            HotkeyLog.Append($"[reload error] {ex.Message}");
            // Surface the exact error so the streamer knows what line to fix.
            Notify("NL: reload failed", ex.Message);
        }
    }

    private void OpenOBSConfig()
    {
        var dir = Path.GetDirectoryName(_sceConfigPath) ?? ".";
        var obsPath = Path.Combine(dir, "obs.json");

        if (!File.Exists(obsPath))
        {
            File.WriteAllText(obsPath, """
                {
                  "host": "localhost",
                  "port": 4455,
                  "password": ""
                }
                """);
        }

        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{obsPath}\"") { UseShellExecute = true });
    }

    // ── Start at login ─────────────────────────────────────────────────────────────────────────

    private static bool IsStartAtLoginEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, writable: false);
        return key?.GetValue(RegistryValueName) is not null;
    }

    private void ToggleStartAtLogin()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, writable: true);
        if (key is null)
        {
            Notify("NL: registry error", "Could not open HKCU Run key.");
            return;
        }

        if (IsStartAtLoginEnabled())
        {
            key.DeleteValue(RegistryValueName, throwOnMissingValue: false);
            HotkeyLog.Append("Start at login disabled.");
            Notify("NL Hotkey Daemon", "Start at login: disabled.");
        }
        else
        {
            // Always point to the stable %LOCALAPPDATA%\NL\hotkeys.nle path — not the build
            // output dir, which changes between builds. This path is created by EnsureDefaultConfig.
            var exe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            var configArg = Program.DefaultConfigPath;
            key.SetValue(RegistryValueName, $"\"{exe}\" \"{configArg}\"");
            HotkeyLog.Append($"Start at login enabled: {exe} \"{configArg}\"");
            Notify("NL Hotkey Daemon", "Start at login: enabled.");
        }

        // Refresh the tick mark on the menu item.
        if (_trayIcon.ContextMenuStrip?.Items[6] is ToolStripMenuItem loginItem)
        {
            loginItem.Checked = IsStartAtLoginEnabled();
        }
    }

    // ── Cleanup ────────────────────────────────────────────────────────────────────────────────

    protected override void ExitThreadCore()
    {
        HotkeyLog.Append("NL Hotkey Daemon exiting.");
        _watcher?.Dispose();
        _debounceTimer?.Dispose();
        _hotkeyWindow.UnregisterAll();
        _hotkeyWindow.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.ExitThreadCore();
    }
}
