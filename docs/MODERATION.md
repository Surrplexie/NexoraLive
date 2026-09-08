# NL Moderation Tooling (Phase 4)

Phase 4 gives Phase 0/3's rule-engine decisions and Phase 2's SP standing model a durable audit
trail and a small admin/mod dashboard, per nl.txt's moderation section and
[`ROADMAP.md`](../ROADMAP.md) Phase 4:

> Warnings/bans/analytics dashboard for admins/mods, consuming the same rule-engine decisions
> and logs as an audit trail.

## Pieces

| Project | Kind | Responsibility |
|---|---|---|
| `src/NL.Moderation.Core` | pure, cross-platform | `ModerationRecord`, `IModerationStore`, `ISpProfileRepository`, `ModerationService` — no file/network I/O, fully unit-testable |
| `src/NL.Moderation` | I/O, cross-platform | `JsonlModerationStore` (append-only JSON-Lines log), `JsonFileSpProfileRepository` (JSON file of `SpProfile`s) |
| `src/NL.ModerationConsole` | WinForms (`net8.0-windows`) | Windows admin/mod dashboard |
| `src/NL.Moderation.Web` | ASP.NET (`net8.0`) | Cross-platform web dashboard (Phase C) — see [NL_HEADLESS_LINUX.md](NL_HEADLESS_LINUX.md) |

This mirrors the split already used for Phase 3 (`NL.Server.Core` pure / `NL.Server` I/O) and
Phase 2 (`NL.Core/Sp` pure model / `JoinEligibilityEngine` pure evaluator): keep the decision
logic testable without touching a disk, push actual file/network I/O to the outer layer.

## `ModerationRecord` — the audit trail

One entry per event, `ModerationActionKind`:

- **`AutomaticDecision`** — a `RuleEngine` / `NlServerHost` decision (Allow/Block + message),
  logged verbatim with no SP standing change. This is the "log of `ActionResult`s" the roadmap
  asks for.
- **`Warning`** — a mod/admin issued a warning; adds a `SpOffense` to the SP's history.
- **`Ban`** — a mod/admin banned the SP from this streamer; adds a `SpOffense` and sets
  `SpStanding.Banned`. A banned SP is immediately denied the next time `JoinEligibilityEngine`
  evaluates their join attempt — Phase 4 actions feed directly back into Phase 2's join flow.
- **`GraylistHold`** — puts the SP under investigation (`SpStanding.Graylist`); no offense is
  recorded (nl.txt: could be a false accusation).
- **`StandingCleared`** — resets the SP back to `SpStanding.Normal` (investigation resolved,
  ban lifted, etc.). Past offenses are **never removed**, only standing changes — matches
  nl.txt's "offenses add up for 2 years before being archived" rule already implemented by
  `SpOffense.IsActive`.

Every record carries `StreamerId`, `PlayerId`/`PlayerName`, `EventName`, `Source` (e.g.
`NL.Server:minecraft` or `manual`), and — for mod actions — `IssuedBy`.

## `ModerationService` — the one entry point

Consuming code (CLI, WinForms, `NL.Server`) only ever talks to `ModerationService`, never the
stores directly (same pattern as `JoinEligibilityEngine` being the one entry point for Phase 2):

- `RecordAutomaticDecisionAsync(streamerId, playerName, gameEvent, result, source)` — audit log
  only, no standing change.
- `IssueWarningAsync` / `IssueBanAsync` / `IssueGraylistHoldAsync` / `ClearStandingAsync` — real
  mod actions; each returns/updates the affected `SpProfile` and appends a `ModerationRecord`.
- `GetRecentActionsAsync(streamerId, count)` — newest-first audit trail for a streamer.
- `GetPlayerActionsAsync(streamerId, playerId)` — every record for one SP with one streamer.
- `GetOffenseHistory(streamerId, playerId)` — current standing + active offense count + full
  offense list for the "SP Offense History" view; returns `null` for an unknown SP.

## Wired into `NL.Server` (Phase 3)

`NL.Server` gained three optional CLI flags:

```
--streamer id            streamer id these events belong to (default "default-streamer")
--moderation-log path     append every decision to this JSON-Lines file as an audit trail
```

When `--moderation-log` is set, every decision `NlServerHost` produces — Allow or Block, from
any adapter (Minecraft, generic NDJSON, or a future one) — is appended via
`ModerationService.RecordAutomaticDecisionAsync` before the action sink runs. `NlServerHost.RunAsync`'s
`onDecision` callback is `Func<HostedDecision, Task>` (awaited) specifically so this logging can
be async I/O without any change to the pure `NL.Server.Core` host loop itself.

```
dotnet run --project src/NL.Server -- --game generic --config samples/configs/generic.nle \
  --source samples/events/generic-sample.ndjson --replay \
  --streamer streamer-zed --moderation-log samples/moderation.jsonl
```

## `NL.ModerationConsole` — the dashboard

A small WinForms app (same style as `NL.ConfigEditor`: plain code-behind `Form`, `ListView` in
Details view, `TableLayoutPanel`/`GroupBox` layout, a status bar):

- **Recent Actions** (left) — every `ModerationRecord` for the current streamer, newest first;
  clicking a row that has a resolved `PlayerId` jumps straight to that SP's offense history.
- **SP Offense History** (top right) — look up a player id, see their current standing
  (color-coded: green Normal, orange Graylist, red Banned) and full offense list with
  active/archived status.
- **Issue Action** (bottom right) — reason box + buttons for Warning / Ban / Graylist Hold /
  Clear Standing, plus a "Create Profile If Missing" helper for onboarding a SP id the console
  hasn't seen yet.

File → **Open Moderation Log…** / **Open SP Profile Store…** let you point the dashboard at a
specific `.jsonl` / `.json` pair (defaults to `%LOCALAPPDATA%\NL\moderation.jsonl` and
`%LOCALAPPDATA%\NL\sp-profiles.json` — the same defaults you'd wire `NL.Server` to write to).

## Persistence format notes

- **`JsonlModerationStore`** — one JSON object per line, append-only (same "plain text, no
  database" spirit as `hotkeys.log` and `.nle` files elsewhere in the repo). Reads re-parse the
  whole file each call, which is fine at prototype scale.
- **`JsonFileSpProfileRepository`** — a single JSON file, rewritten whole on every mutation.
  `SpProfile`'s per-streamer relationships are exposed as a read-only dictionary
  (`SpProfile.Relationships`, added in Phase 4) so the repository can enumerate and serialize
  every relationship, not just look one up by streamer id.

A real deployment would swap both stores for a real database without changing
`ModerationService` or the dashboard — they only depend on `IModerationStore` /
`ISpProfileRepository`.
