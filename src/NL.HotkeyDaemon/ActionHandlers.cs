using System.Diagnostics;
using NAudio.CoreAudioApi;
using NL.HotkeyDaemon.Core;

namespace NL.HotkeyDaemon;

/// <summary>
/// The real, per-action effects that run once <see cref="ActionDispatcher"/> has decided an
/// action is allowed. <see cref="OBSConfig"/> is injected so clipStream can reach the right
/// OBS WebSocket endpoint without hardcoding a path.
/// </summary>
internal sealed class ActionHandlers
{
    private readonly MicController _mic = new();

    public OBSConfig OBSConfig { get; set; } = new();

    /// <summary>Performs the real effect for <paramref name="action"/> and returns a
    /// human-readable description for logging/notifications.</summary>
    public string Perform(string action, string? dispatcherMessage)
    {
        return action switch
        {
            "toggleMic" => DescribeMicState(_mic.ToggleMute()),
            "announce" => dispatcherMessage ?? "(no message configured)",
            ActionDispatcher.ToggleNlEventsAction => dispatcherMessage ?? "NLEvents toggled",
            "openLog" => OpenLog(),
            "clipStream" => FireClipStream(),
            "focusOBS" => FocusOBS(),
            "muteDesktop" => ToggleDesktopMute(),
            _ => dispatcherMessage ?? $"'{action}' has no built-in real handler yet",
        };
    }

    private static string OpenLog()
    {
        var directory = Path.GetDirectoryName(HotkeyLog.LogFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(HotkeyLog.LogFilePath))
        {
            File.WriteAllText(HotkeyLog.LogFilePath, string.Empty);
        }

        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{HotkeyLog.LogFilePath}\"") { UseShellExecute = true });
        return "Log opened";
    }

    private string FireClipStream()
    {
        // Fire-and-forget on a thread pool thread so WM_HOTKEY message pump is never blocked.
        var cfg = OBSConfig;
        _ = Task.Run(async () =>
        {
            var result = await OBSController.SaveReplayAsync(cfg).ConfigureAwait(false);
            HotkeyLog.Append($"clipStream OBS result: {result}");
        });

        return "Saving clip…";
    }

    /// <summary>Brings an OBS window to the foreground (tries both obs64 and obs process names).</summary>
    private static string FocusOBS()
    {
        var proc = Process.GetProcessesByName("obs64").FirstOrDefault()
                   ?? Process.GetProcessesByName("obs").FirstOrDefault();

        if (proc is null)
        {
            return "OBS not running";
        }

        var hwnd = proc.MainWindowHandle;
        if (hwnd == IntPtr.Zero)
        {
            return "OBS window not found (minimised to tray?)";
        }

        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        NativeMethods.SetForegroundWindow(hwnd);
        return "OBS focused";
    }

    /// <summary>Toggles the master mute on the default Windows audio playback (render) device.</summary>
    private static string ToggleDesktopMute()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            device.AudioEndpointVolume.Mute = !device.AudioEndpointVolume.Mute;
            return device.AudioEndpointVolume.Mute ? "Desktop audio muted" : "Desktop audio unmuted";
        }
        catch (Exception ex)
        {
            return $"muteDesktop failed: {ex.Message}";
        }
    }

    private static string DescribeMicState(bool isNowMuted) => isNowMuted ? "Microphone muted" : "Microphone unmuted";
}
