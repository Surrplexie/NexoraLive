# NL Hotkey Daemon

A real, Windows-only background tray app implementing nl.txt's own "Feature 1" (global
keyboard shortcuts for common NLServer-menu-style actions, so you don't have to open an
in-game menu and scroll). Every hotkey press flows through the existing
[`NL.Core.RuleEngine`](../src/NL.Core/RuleEngine.cs), so a `.nle` config can genuinely
allow/block each action without touching any code.

Unlike `NL.Simulator`, this is not a mocked demo: it registers real Win32 global hotkeys,
mutes your real default microphone, writes a real log file, shows real Windows tray
notifications, and can trigger OBS to save a replay clip.

## Requirements

- Windows (this project targets `net8.0-windows` and uses WinForms + Win32 APIs).
- .NET 8 SDK.
- OBS 28+ (optional) with Tools → WebSocket Server enabled for `clipStream`.

## Running it

```bash
dotnet run --project src/NL.HotkeyDaemon
```

With no arguments it looks for `samples/configs/hotkeys.nle` by walking up from the build
output folder. You can also point it at your own file:

```bash
dotnet run --project src/NL.HotkeyDaemon -- path\to\your.nle
```

A tray icon appears (right-click it for Status / Reload config / Open log / Open OBS config /
**Start at login** toggle / Exit). The app keeps running in the background — there's no visible window.

The daemon **auto-reloads** the `.nle` file whenever you save it (800ms debounce), so you
can edit your config live without using the tray menu.

**Single-instance guard:** if the daemon is already running and a second copy is launched,
the second copy shows a tray notification and exits immediately without registering any hotkeys.

**Start at login:** toggling this in the tray menu writes or removes a registry key at
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. The startup command always points to
`%LOCALAPPDATA%\NL\hotkeys.nle` so it works even after you move or update the binary.

## Unified config: one `.nle` file

As of Phase 0.6, hotkey bindings and NLEvent rules live in the **same** `.nle` file.
`hotkeys.json` is no longer used (it's kept in `samples/hotkeys/` as a deprecated example).

```nle
# Hotkey bindings at the top
hotkey "Ctrl+Alt+0": toggleMic
hotkey "Ctrl+Alt+9": announce
hotkey "Ctrl+Alt+8": toggleNlEvents
hotkey "Ctrl+Alt+7": openLog
hotkey "Ctrl+Alt+6": clipStream

# NLEvent rules below
event toggleMic:
    allow

event announce:
    warn "Thanks for watching!"
    allow
```

Binding format: `hotkey "<combo>": <actionName>`

- `<combo>` is `Modifier(+Modifier...)+Key`, e.g. `"Ctrl+Alt+0"`, `"Shift+F1"`, `"Win+Alt+K"`.
  Recognized modifiers: `Ctrl`/`Control`, `Alt`, `Shift`, `Win`/`Windows`/`Meta`. Recognized
  keys: `0`-`9`, `A`-`Z`, `F1`-`F24`, and named keys (`Space`, `Enter`, `Esc`, `Tab`,
  arrow keys, `Insert`/`Delete`/`Home`/`End`/`PageUp`/`PageDown`).
- `<actionName>` is any identifier; it becomes the event name evaluated by the rule block below.
- A duplicate combo is silently dropped (first one wins) with a warning in the log.
- A combo that fails to parse is skipped (warning in log) — it won't crash the daemon.

## Built-in actions

| Action name      | Real effect |
|-------------------|-------------|
| `toggleMic`       | Mutes/unmutes your default recording device via NAudio (Windows Core Audio). |
| `announce`        | Shows a tray balloon notification. Customize the text with `warn "..."` in the matching `.nle` event block. |
| `toggleNlEvents`  | Master on/off switch. While off, every other hotkey action is skipped. This action itself always still runs so you can't lock yourself out. |
| `openLog`         | Opens `%LOCALAPPDATA%\NL\hotkeys.log` in Notepad. |
| `clipStream`      | Sends a SaveReplayBuffer request to OBS over WebSocket v5. Requires OBS 28+ with WebSocket server enabled. |
| `focusOBS`        | Brings the OBS window to the foreground (un-minimises it if needed). Looks for `obs64` or `obs` process. |
| `muteDesktop`     | Toggles master mute on the default Windows audio playback device (all desktop audio). |

Every press — performed or skipped — is appended to `%LOCALAPPDATA%\NL\hotkeys.log`.

## Gating actions with `.nle` rules

Because each action name is evaluated through the same `RuleEngine` as `NL.Simulator`, you can
write ordinary NLEvent rules against these action names:

```nle
event toggleMic:
    block   # temporarily disable this hotkey entirely

event announce:
    warn "Thanks for watching!"
    allow   # this becomes the real notification text

event clipStream:
    block   # OBS not running today — skip to avoid the timeout
```

Save the file and the daemon picks it up within a second (FileSystemWatcher + 800ms debounce).

## OBS config (`obs.json`)

Place an `obs.json` next to your `.nle` file (the tray's "Open OBS config" creates it for you):

```json
{ "host": "localhost", "port": 4455, "password": "" }
```

- `host` / `port`: match whatever you set in OBS → Tools → WebSocket Server Settings.
- `password`: leave empty if you haven't set one. The daemon uses the OBS v5 SHA-256 auth
  scheme if a password is present.
- If `obs.json` is absent, the daemon defaults to `localhost:4455` with no password.
- `clipStream` has a 2-second timeout. If OBS isn't running it fails gracefully and logs the
  reason; the daemon never crashes over a missed OBS connection.

## Windows-specific caveats

- **Hotkey conflicts**: `RegisterHotKey` fails if another running app already owns that exact
  combo; the failure is written to the log rather than crashing the daemon.
- **Global, not app-scoped**: these hotkeys fire no matter which window has focus (including
  games), by design. Don't pick a combo you need for something else while gaming.
- **Microphone device**: `toggleMic` controls whichever device Windows currently treats as the
  default *communications* recording device — the same one most voice chat/streaming software
  uses. Verify it before relying on it live.
- **NAudio dependency**: the one third-party package in the project (MIT license), used only
  for the Core Audio API wrapper.
