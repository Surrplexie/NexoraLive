# NLE Guide — NexoraLive Events from Start to Finish

**Version:** aligned with prototype Phases 0, 0.5, and 0.6  
**Audience:** streamers, contributors, and anyone evaluating or using this repository  
**Status:** This guide describes what is **built and runnable today**, and how it relates to the longer-term NL vision in [`nl.txt`](nl.txt).

---

## Table of contents

1. [What is NL?](#1-what-is-nl)
2. [What is NLEvents (NLE)?](#2-what-is-nlevents-nle)
3. [What exists in this repo today](#3-what-exists-in-this-repo-today)
4. [Core concepts](#4-core-concepts)
5. [Getting started](#5-getting-started)
6. [The `.nle` language (v0.1)](#6-the-nle-language-v01)
7. [Writing your first config](#7-writing-your-first-config)
8. [Running the simulator (mock game)](#8-running-the-simulator-mock-game)
9. [Running the Hotkey Daemon (real Windows)](#9-running-the-hotkey-daemon-real-windows)
10. [How evaluation works](#10-how-evaluation-works)
11. [Project structure and architecture](#11-project-structure-and-architecture)
12. [Making changes and extending NLE](#12-making-changes-and-extending-nle)
13. [Samples and reference configs](#13-samples-and-reference-configs)
14. [Troubleshooting](#14-troubleshooting)
15. [Roadmap: what comes next](#15-roadmap-what-comes-next)
16. [Glossary](#16-glossary)
17. [Further reading](#17-further-reading)

---

## 1. What is NL?

**NL (NexoraLive)** is a conceptual cross-platform system for streamers who host community multiplayer sessions. The full vision — described in [`nl.txt`](nl.txt) — includes:

- **NLServer** — hosted game sessions with streamer-defined rules
- **StreamPlayer (SP)** — a relationship model between streamers and viewers/fans
- **NLEvents** — streamer-authored gameplay rules enforced automatically
- **Moderation, anti-cheat, social integration, economy features**, and more

That document is a **vision**, not a shipping product. This repository is a **prototype**: it implements the heart of NLEvents (a config language + rule engine) and one real-world slice of the vision (Windows global hotkeys gated by those same rules).

Think of NL as the long-term platform, and **NLE (NexoraLive Events / NLEvents)** as the rules layer that says *what players or actions are allowed, blocked, or warned* during a session.

---

## 2. What is NLEvents (NLE)?

NLEvents is the **rules and automation config system** for NL. In the full product, a streamer would write (or download) an NLE config per game or mini-game. During a live session, NL would:

1. Observe gameplay events (shoot, respawn, leave boundary, use item, etc.)
2. Look up the matching rule block in the streamer's `.nle` config
3. Return **Allow**, **Block**, or **Allow/Block with a warning message**

The prototype proves that loop end-to-end:

| Mode | Event source | Effect |
|------|----------------|--------|
| **Simulator** | Fake scripted events (`MockGameEventSource`) | Prints decisions to the console |
| **Hotkey Daemon** | Real keyboard presses on Windows | Performs real actions (mic mute, notifications, OBS clip, etc.) only if rules allow |

Both modes use the **same parser and rule engine** (`NL.Core`). That is intentional: hotkey actions are just another kind of "event name" evaluated by the same rules.

### Design principles (from the vision)

- **Streamer-authored, not general-purpose code** — `.nle` is a small DSL with fixed keywords (`event`, `if`, `block`, `allow`, `warn`, …), not Python or JSON executed as a script.
- **Plain text for now** — samples in this repo are unencrypted `.nle` files. The vision mentions closed-source encrypted configs later; the prototype keeps everything readable for learning and testing.
- **Rules should be clear** — more precise rules help both enforcement and (eventually) anti-cheat signals. Vague configs give vague results.

---

## 3. What exists in this repo today

| Component | Purpose |
|-----------|---------|
| `NL.Core` | Lexer, parser, AST, `RuleEngine` |
| `NL.Simulator` | CLI that runs mock events through a `.nle` config |
| `NL.HotkeyDaemon.Core` | Hotkey combo parsing, allow/skip dispatch logic |
| `NL.HotkeyDaemon` | Windows tray app: global hotkeys + real actions |
| `tests/` | Unit tests for core and daemon logic |
| `samples/configs/*.nle` | Example rule configs |
| `docs/` | Language spec, architecture, daemon docs |

**Built:** Phases 0, 0.5, 0.6 (see [ROADMAP.md](ROADMAP.md))  
**Not built:** Real NLServer, real game hooks, SP/whitelisting, moderation UI, anti-cheat, economy, mobile app, etc.

---

## 4. Core concepts

### 4.1 Config file (`.nle`)

A text file containing:

- **`hotkey` declarations** (optional, used by the Hotkey Daemon) — bind a key combo to an action name
- **`event` blocks** — rules for a named event or action

Example (unified format, Phase 0.6+):

```nle
hotkey "Ctrl+Alt+0": toggleMic

event toggleMic:
    allow
```

### 4.2 Event

Something that happens and can be ruled on. In the simulator, examples include `shoot`, `respawn`, `leaveBoundary`. In the daemon, event names are **action names** like `toggleMic`, `announce`, `clipStream`.

Technically, an event is a **name** plus optional **properties** (e.g. `player.health = 40`).

### 4.3 Rule block

An `event <name>:` block with an indented body of statements. The engine finds the block whose name matches the incoming event and executes its statements top to bottom until a final **allow** or **block** decision is reached.

### 4.4 Decision

| Outcome | Meaning in simulator | Meaning in Hotkey Daemon |
|---------|----------------------|---------------------------|
| **Allow** | Action permitted | Real handler runs (mute mic, show notification, …) |
| **Block** | Action rejected | Hotkey press skipped; reason logged |
| **Warn** | Message attached to the next allow/block | Notification text for `announce`; logged for others |

### 4.5 Default behavior

- **No rule for an event name** → **Allow** (nothing to enforce)
- **Branch with no explicit action** → **Allow** (with a load-time warning)
- **`warn` without a following action** → **Allow** with that warning message

---

## 5. Getting started

### 5.1 Requirements

- **[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)** or newer
- **Windows 10/11** — only required for `NL.HotkeyDaemon` (tray app). Simulator and tests run on any OS with .NET 8.

Optional for `clipStream`:

- **OBS Studio 28+** with **Tools → WebSocket Server** enabled

### 5.2 Clone or open the project

Work inside the `nl/` folder (where this guide lives).

### 5.3 Build and test

```bash
dotnet build src/NL.sln
dotnet test src/NL.sln
```

### 5.4 Quick try: simulator

```bash
dotnet run --project src/NL.Simulator -- samples/configs/full-session.nle
```

You should see a narrated list of mock player actions and Allow/Block decisions.

### 5.5 Quick try: Hotkey Daemon (Windows)

```bash
dotnet run --project src/NL.HotkeyDaemon
```

A tray icon appears. Default config: `samples/configs/hotkeys.nle`. Try **Ctrl+Alt+0** (toggle mic), **Ctrl+Alt+9** (announce), **Ctrl+Alt+8** (master enable/disable).

---

## 6. The `.nle` language (v0.1)

Formal spec: [`docs/NLEVENT_LANGUAGE_SPEC_v0.1.md`](docs/NLEVENT_LANGUAGE_SPEC_v0.1.md)

### 6.1 File extension

`.nle` — NLEvent config

### 6.2 Lexical rules

| Rule | Detail |
|------|--------|
| Indentation | **Spaces only** (tabs are a parse error). Python-style blocks after `:`. |
| Comments | `#` to end of line |
| Blank lines | Ignored |
| Strings | Double quotes: `"stay in the zone"` |
| Numbers | `0`, `100`, `3.5` |
| Identifiers | Letters, digits, `_`, `.` (e.g. `player.health`) |

### 6.3 Grammar (informal)

```
config      := { hotkeyDecl | eventBlock }

hotkeyDecl  := "hotkey" STRING ":" IDENT NEWLINE

eventBlock  := "event" IDENT ":" NEWLINE INDENT { statement } DEDENT

statement   := actionStmt | ifStmt

actionStmt  := ("block" | "allow" | "deny") NEWLINE
             | "warn" STRING NEWLINE

ifStmt      := "if" condition ":" NEWLINE INDENT { statement } DEDENT
               [ "else" ":" NEWLINE INDENT { statement } DEDENT ]

condition   := operand COMPARATOR operand

operand     := IDENT | NUMBER | STRING

COMPARATOR  := ">" | "<" | ">=" | "<=" | "==" | "!="
```

### 6.4 Keywords

| Keyword | Role |
|---------|------|
| `hotkey` | Top-level: bind keyboard combo to action name (daemon) |
| `event` | Top-level: start a rule block for an event/action name |
| `if` / `else` | Conditional branches |
| `block` / `deny` | Reject the action (aliases in v0.1) |
| `allow` | Permit the action |
| `warn` | Attach a message to the **next** allow/block in the same block |

### 6.5 Top-level ordering

`hotkey` lines and `event` blocks may be **interleaved** in any order.

- **Duplicate event names** → parse error
- **Duplicate hotkey combos** (case-insensitive) → warning; first binding wins
- **Same action on multiple hotkeys** → allowed

### 6.6 Mock vocabulary (simulator only)

Until real game integration (Roadmap Phase 3), the simulator uses a fixed script:

| Event | Typical properties |
|-------|-------------------|
| `shoot` | — |
| `respawn` | `player.health` |
| `leaveBoundary` | — |
| `useItem` | — |

| Property | Type (v0.1) | Notes |
|----------|-------------|--------|
| `player.health` | number | e.g. `0` = dead |
| `player.hasItem` | 0 or 1 | boolean stand-in |

### 6.7 Out of scope in v0.1

- `and` / `or`, boolean literals
- Variables, functions, loops
- SP roles, server state, moderation APIs
- Encrypted/closed packaging

---

## 7. Writing your first config

### 7.1 Minimal: block all shooting

```nle
event shoot:
    block
```

Save as `my-rules.nle`, then:

```bash
dotnet run --project src/NL.Simulator -- my-rules.nle
```

### 7.2 Conditional respawn

Only allow respawn when the player is dead (`health == 0`):

```nle
event respawn:
    if player.health > 0:
        block
    else:
        allow
```

### 7.3 Warning + block (boundary)

```nle
event leaveBoundary:
    warn "stay within the zone"
    block
```

The warning text is included in the simulator output when the action is blocked.

### 7.4 Full session example

See [`samples/configs/full-session.nle`](samples/configs/full-session.nle) — combines shoot, respawn, and boundary rules.

### 7.5 Hotkey daemon config (unified)

See [`samples/configs/hotkeys.nle`](samples/configs/hotkeys.nle):

```nle
hotkey "Ctrl+Alt+0": toggleMic
hotkey "Ctrl+Alt+9": announce
hotkey "Ctrl+Alt+8": toggleNlEvents
hotkey "Ctrl+Alt+7": openLog
hotkey "Ctrl+Alt+6": clipStream

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
```

**Tip:** Change `allow` to `block` on any event and save — the daemon auto-reloads within about a second.

### 7.6 Authoring checklist

1. One `event` block per event/action name you care about
2. Every `if` branch should end in `allow` or `block` (or nested `if` that does)
3. Use `warn` immediately before the allow/block that should carry the message
4. For hotkeys, pick combos unlikely to conflict with games (e.g. **Ctrl+Alt+digit**)
5. Run the simulator or daemon and check logs for **parse warnings** and **load warnings**

---

## 8. Running the simulator (mock game)

The simulator answers: *"If these rules were active, what would happen for a fixed sequence of fake in-game events?"*

### 8.1 Commands

```bash
# Built-in default config
dotnet run --project src/NL.Simulator

# Specific file
dotnet run --project src/NL.Simulator -- samples/configs/no-shooting.nle
dotnet run --project src/NL.Simulator -- samples/configs/boundary-enforcement.nle
dotnet run --project src/NL.Simulator -- samples/configs/conditional-respawn.nle
dotnet run --project src/NL.Simulator -- samples/configs/full-session.nle
```

### 8.2 Example output

```
NL Simulator - NLEvents prototype
Config: samples/configs/full-session.nle

- PlayerA fires a weapon
    event:    shoot
    decision: Block

- PlayerB tries to respawn while still alive (health 40)
    event:    respawn {player.health=40}
    decision: Block
...
```

### 8.3 Workflow for rule authors

1. Edit a `.nle` file
2. Re-run the simulator
3. Adjust rules until decisions match your intent
4. Reuse the same file (or event blocks) in the Hotkey Daemon for action gating

The simulator does **not** read `hotkey` lines for behavior — they are parsed but ignored by `RuleEngine`. Only `event` blocks affect evaluation.

---

## 9. Running the Hotkey Daemon (real Windows)

Implements nl.txt **Feature 1**: global shortcuts so you do not need an in-game menu for common actions.

### 9.1 Start the daemon

```bash
dotnet run --project src/NL.HotkeyDaemon
```

Or with a custom config:

```bash
dotnet run --project src/NL.HotkeyDaemon -- C:\path\to\my-hotkeys.nle
```

With no arguments, the app searches upward from the build output for `samples/configs/hotkeys.nle`.

### 9.2 Tray menu

| Item | Action |
|------|--------|
| **Status** | Shows whether NLEvents is enabled and which config file is loaded |
| **Reload config** | Re-parse `.nle` and re-register hotkeys |
| **Open log** | Opens `%LOCALAPPDATA%\NL\hotkeys.log` in Notepad |
| **Open OBS config** | Creates/opens `obs.json` next to your `.nle` file |
| **Exit** | Unregisters hotkeys and quits |

### 9.3 Auto-reload

Saving the `.nle` file triggers a reload after an **800ms debounce** (handles editors that write the file in two steps). No restart required.

### 9.4 Built-in actions

| Action | Effect |
|--------|--------|
| `toggleMic` | Mute/unmute default Windows recording device (NAudio) |
| `announce` | Tray balloon; text from `warn "..."` in the matching `event announce:` block |
| `toggleNlEvents` | Master switch: when off, all other actions are skipped (this one always works) |
| `openLog` | Open hotkey log in Notepad |
| `clipStream` | OBS WebSocket v5 `SaveReplayBuffer` (2s timeout; errors are logged, not fatal) |

Every key press is logged to:

`%LOCALAPPDATA%\NL\hotkeys.log`

### 9.5 Hotkey combo syntax

Format: `Modifier(+Modifier...)+Key`

**Modifiers:** `Ctrl` / `Control`, `Alt`, `Shift`, `Win` / `Windows` / `Meta`  
**Keys:** `0`–`9`, `A`–`Z`, `F1`–`F24`, `Space`, `Enter`, `Esc`, `Tab`, arrows, `Insert`, `Delete`, `Home`, `End`, `PageUp`, `PageDown`

Invalid combos or Windows registration failures are logged; the daemon keeps running.

### 9.6 OBS sidecar (`obs.json`)

Place next to your `.nle` file (or use tray → **Open OBS config**):

```json
{
  "host": "localhost",
  "port": 4455,
  "password": ""
}
```

Match **OBS → Tools → WebSocket Server Settings**. If the file is missing, defaults are `localhost:4455` with no password.

Requirements for `clipStream`:

1. OBS 28+ running
2. WebSocket server enabled
3. Replay buffer active in OBS

### 9.7 End-to-end flow (hotkey press)

```
Key pressed (WM_HOTKEY)
    → ActionDispatcher (enabled? rule says allow?)
        → Allow: ActionHandlers (mic / notify / OBS / notepad)
        → Skip: log + tray notification
    → HotkeyLog append
```

The **same** `RuleEngine` that powers the simulator decides allow/skip for each action name.

### 9.8 Deprecated: `hotkeys.json`

Phase 0.6 merged bindings into `.nle`. [`samples/hotkeys/hotkeys.json`](samples/hotkeys/hotkeys.json) remains as a documented legacy example only.

---

## 10. How evaluation works

### 10.1 Pipeline

```
.nle text
  → Lexer (tokens + Indent/Dedent)
  → Parser (ConfigAst: events + hotkey declarations)
  → RuleEngine (per-event evaluation)
  → ActionResult (Allow | Block, optional message)
```

### 10.2 Statement execution (inside an event block)

Statements run **in order**:

1. **`warn "message"`** — stores message for the next terminal action
2. **`allow` / `block` / `deny`** — stops and returns decision (+ pending warn if any)
3. **`if condition:`** — evaluates condition against event properties; runs `then` or `else` body recursively

### 10.3 Conditions

Single comparison only (v0.1):

```nle
if player.health > 0:
    block
```

Comparators: `>`, `<`, `>=`, `<=`, `==`, `!=`

Left/right operands: identifier, number, or string.

### 10.4 Warnings at load time

**Parser warnings** (non-fatal): e.g. duplicate hotkey combo  
**Load warnings** from `RuleEngine`: e.g. an `if` branch with no explicit allow/block

The Hotkey Daemon writes these to `hotkeys.log` on startup and reload.

---

## 11. Project structure and architecture

```
nl/
├── NLE_GUIDE.md          ← this document
├── nl.txt                ← full NL vision (hypothetical)
├── ROADMAP.md            ← phased plan; what's done vs planned
├── README.md             ← quick repo overview
├── docs/
│   ├── NLEVENT_LANGUAGE_SPEC_v0.1.md
│   ├── ARCHITECTURE.md
│   └── HOTKEY_DAEMON.md
├── samples/
│   ├── configs/*.nle
│   ├── obs.json
│   └── hotkeys/hotkeys.json   (deprecated)
├── src/
│   ├── NL.Core/               ← language + engine
│   ├── NL.Simulator/          ← mock CLI
│   ├── NL.HotkeyDaemon.Core/  ← testable hotkey logic
│   └── NL.HotkeyDaemon/       ← Windows tray app
└── tests/
    ├── NL.Core.Tests/
    └── NL.HotkeyDaemon.Core.Tests/
```

### 11.1 Key types (`NL.Core`)

| Type | Role |
|------|------|
| `Lexer` | Source → tokens |
| `Parser` | Tokens → `ConfigAst` |
| `ConfigAst` | List of `EventBlock` + `HotkeyDeclaration` |
| `RuleEngine` | `GameEvent` → `ActionResult` |
| `GameEvent` | Name + property bag (integration seam for real games) |
| `NlSyntaxException` | Parse/lex errors with line numbers |

### 11.2 Why split Core / Simulator / Daemon?

- **`NL.Core`** stays free of Windows or game APIs → portable, unit-testable
- **`NL.Simulator`** proves rules without hardware
- **`NL.HotkeyDaemon`** is the thin, platform-specific shell that feeds **action names** into the same engine

Detailed diagram: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)

---

## 12. Making changes and extending NLE

### 12.1 For streamers (no code)

1. Copy a sample from `samples/configs/`
2. Edit rules and hotkeys
3. Test with simulator and/or daemon
4. Iterate using log warnings

### 12.2 For contributors (code)

Typical extension points:

| Goal | Where to change |
|------|-----------------|
| New language keyword / syntax | `Token.cs`, `Lexer.cs`, `Parser.cs`, AST types, tests |
| New evaluation behavior | `RuleEngine.cs`, tests |
| New hotkey action | `ActionHandlers.cs`, sample `hotkeys.nle`, docs |
| New mock events | `MockGameEventSource.cs`, simulator samples |
| Real game events (future) | Emit `GameEvent` from game hook; keep `RuleEngine` unchanged |

Always run:

```bash
dotnet test src/NL.sln
```

### 12.3 Adding a new daemon action (sketch)

1. Choose an action name (e.g. `myAction`)
2. Add handler branch in `ActionHandlers.Perform`
3. Add `hotkey "..." : myAction` and `event myAction: allow` to your `.nle`
4. Document in `docs/HOTKEY_DAEMON.md`

Gate behavior is entirely in the `event myAction:` block — no C# change needed to allow/block.

### 12.4 Git note

If this folder lives inside a larger git repo (e.g. your user profile), consider `git init` inside `nl/` before committing NL work, so history stays scoped to the project.

---

## 13. Samples and reference configs

| File | Demonstrates |
|------|----------------|
| `samples/configs/no-shooting.nle` | Simple `block` |
| `samples/configs/boundary-enforcement.nle` | `warn` + `block` |
| `samples/configs/conditional-respawn.nle` | `if` / `else` with `player.health` |
| `samples/configs/full-session.nle` | All three patterns combined |
| `samples/configs/hotkeys.nle` | Unified hotkeys + rules for the daemon |
| `samples/obs.json` | OBS WebSocket defaults for `clipStream` |

---

## 14. Troubleshooting

### Parse errors

- **"tabs are not allowed"** — use spaces for indentation (typically 4 per level)
- **"duplicate event X"** — only one `event X:` block per file
- **"expected ... got ..."** — check colons, newlines, and string quotes

Run the simulator on the file; line numbers are reported in exceptions.

### Simulator vs daemon mismatch

- Simulator ignores hotkey registration; daemon needs both valid `hotkey` lines **and** matching `event` blocks
- Unlisted events/actions default to **allow**

### Hotkey does nothing

1. Check `%LOCALAPPDATA%\NL\hotkeys.log` for registration failures
2. Another app may own the same combo (`RegisterHotKey` conflict)
3. Confirm `event <action>:` is not `block`
4. Confirm NLEvents is not disabled (`toggleNlEvents` / tray Status)

### Mic toggle affects wrong device

`toggleMic` uses Windows **default communications** recording device. Set the correct mic as default in Windows Sound settings before going live.

### clipStream fails

- OBS not running → log shows timeout message
- WebSocket disabled → enable in OBS settings
- Replay buffer not started → start replay buffer in OBS
- Wrong password → fix `obs.json`

### Config changes not applied

- Save the file (daemon watches the `.nle` path only)
- Wait ~1 second for debounce
- Or use tray → **Reload config**
- Check log for reload errors

---

## 15. Roadmap: what comes next

See [`ROADMAP.md`](ROADMAP.md) for the full checklist. Summary:

| Phase | Focus | Status |
|-------|--------|--------|
| **0** | Language + rule engine + simulator | Done |
| **0.5** | Windows hotkey daemon | Done |
| **0.6** | Unified `.nle`, auto-reload, openLog, clipStream | Done |
| **1** | GUI/HUD for authoring configs | Planned |
| **2** | StreamPlayer model + join rules | Planned |
| **3** | Real game + NLServer integration | Planned |
| **4+** | Moderation, anti-cheat, economy, etc. | Vision only |

The prototype is meant to be **personally usable today** (hotkeys + rules) while staying aligned with the larger NL design in `nl.txt`.

---

## 16. Glossary

| Term | Meaning |
|------|---------|
| **NL** | NexoraLive — the overall platform concept |
| **NLE / NLEvents** | NexoraLive Events — streamer rules configs and enforcement |
| **`.nle`** | NLEvent config file format (plain text in this repo) |
| **SP** | StreamPlayer — viewer/fan participant in the NL vision |
| **NLS / NLServer** | Hosted session server in the NL vision (not implemented here) |
| **Event** | Named occurrence evaluated by rules (`shoot`, `toggleMic`, …) |
| **Action** | In the daemon: side effect tied to an event name (`toggleMic`, …) |
| **RuleEngine** | Component that returns Allow/Block for a `GameEvent` |
| **MockGameEventSource** | Fixed fake events for the simulator CLI |

---

## 17. Further reading

| Document | Contents |
|----------|----------|
| [`nl.txt`](nl.txt) | Original full NL vision |
| [`ROADMAP.md`](ROADMAP.md) | Phased build plan and checkboxes |
| [`README.md`](README.md) | Short repo intro and commands |
| [`docs/NLEVENT_LANGUAGE_SPEC_v0.1.md`](docs/NLEVENT_LANGUAGE_SPEC_v0.1.md) | Formal grammar |
| [`docs/HOTKEY_DAEMON.md`](docs/HOTKEY_DAEMON.md) | Daemon-focused reference |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Phase 0 component diagram |

---

*This guide is the single start-to-end entry point for NLE in this repository. When the project gains a config GUI (Phase 1) or real game integration (Phase 3), extend the sections above rather than scattering one-off docs.*
