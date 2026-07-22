# NL Roadmap / Progress Guide

This tracks progress against the ideas in [`nl.txt`](nl.txt), broken into phases roughly
ordered by dependency (each later phase leans on earlier ones). Only Phases 0, 0.5, 0.6, and 0.7
are actually built right now — everything else is a plan, not a promise or a deadline.

Status legend: `[x]` done, `[~]` partially done / in progress, `[ ]` not started.

## Phase 0 — NLEvents language + rule engine (current)

The "streamer writes rules, engine enforces them" mechanic, against a mocked/simulated game
event stream (no real game, no networking, no anti-cheat yet).

- [x] `NLEVENT_LANGUAGE_SPEC_v0.1.md` — minimal grammar (event blocks, if/else, block/allow/
      deny/warn, simple comparisons)
- [x] Lexer (indentation-based tokenizer, `.nle` -> tokens)
- [x] Parser (tokens -> AST)
- [x] `RuleEngine` (AST + `GameEvent` -> `ActionResult`)
- [x] `MockGameEventSource` + `NL.Simulator` CLI (runnable demo, no real game needed)
- [x] Unit tests (lexer, parser, rule engine)
- [x] Sample `.nle` configs mirroring nl.txt scenarios
- [ ] Try the language on a few more streamer-authored scenarios and see where v0.1's grammar
      feels too limited (multi-condition rules? more event/property vocabulary?) before
      moving on to Phase 1

## Phase 0.5 — Real Windows hotkey daemon (current)

nl.txt's own "Feature 1": global keyboard shortcuts for common actions, so a streamer doesn't
have to open an in-game menu and scroll. This is the first piece that's genuinely real rather
than mocked — it runs on your actual Windows desktop, not against `MockGameEventSource`.

- [x] `NL.HotkeyDaemon.Core` — OS-independent `HotkeyCombo` parsing, `HotkeyConfig` JSON
      loader, and `ActionDispatcher` (reuses the Phase 0 `RuleEngine` to decide allow/skip per
      action, plus the "enable/disable NLEvents" master switch nl.txt calls out by name)
- [x] `NL.HotkeyDaemon` — real Windows tray app: Win32 `RegisterHotKey`/`WM_HOTKEY` global
      hotkeys, real microphone mute toggle (NAudio), real tray notifications, a real persistent
      log file (`%LOCALAPPDATA%\NL\hotkeys.log`)
- [x] Sample `hotkeys.json` + `hotkeys.nle` and unit tests for all OS-independent logic
- [ ] Try it day-to-day and see which real actions would actually be useful beyond the initial
      three (mic toggle / announce / master switch) - e.g. launching/focusing OBS, muting game
      audio, or other Windows-side automation
- [x] Consider whether hotkey bindings should live inside the `.nle` file itself instead of a
      separate `hotkeys.json` — resolved in Phase 0.6 (unified file, one source of truth)

## Phase 0.6 — Daemon polish (current)

Day-to-day usability improvements on top of Phase 0.5.

- [x] **Unified config**: `hotkey "Ctrl+Alt+0": toggleMic` top-level clause in the `.nle` file;
      `hotkeys.json` is now deprecated. New grammar: `hotkeyDecl := "hotkey" STRING ":" IDENT NEWLINE`.
      Duplicate combos produce a parser warning (not an error); first binding wins.
- [x] **Auto-reload**: `FileSystemWatcher` + 800ms debounce on the `.nle` file — save and the
      daemon picks up changes within ~1 second without using the tray menu.
- [x] **`openLog` action**: opens `%LOCALAPPDATA%\NL\hotkeys.log` in Notepad via hotkey.
- [x] **`clipStream` action**: sends a SaveReplayBuffer command to OBS over WebSocket v5
      (built-in .NET 8 WebSocket + SHA-256 auth, no new NuGet packages). Config lives in
      `obs.json` next to the `.nle` file; gracefully handles OBS-not-running with a tray notice.
- [x] Tray menu "Open OBS config" item (creates `obs.json` if absent, opens in Notepad).
- [x] Try it day-to-day and identify next real actions — resolved in Phase 0.7

## Phase 0.7 — Validate & harden (current)

Daily-use hardening so the daemon is reliable enough to run every stream session.

- [x] **Single-instance guard**: named Win32 mutex; second copy shows a notification and exits
- [x] **Persistent config path**: default config at `%LOCALAPPDATA%\NL\hotkeys.nle`; auto-created
      from a starter template if absent; `dotnet run` still falls back to the repo sample
- [x] **Start at login**: tray menu toggle writes/removes `HKCU\...\Run` registry key; startup
      command always references the `%LOCALAPPDATA%\NL\hotkeys.nle` path, not the build output
- [x] **`and` / `or` conditions**: compound conditions in `if` statements (left-associative,
      short-circuit); new `ConditionExpr` AST hierarchy (`Condition`, `CompoundCondition`);
      full test coverage in parser and rule-engine tests
- [x] **`focusOBS` action**: brings the OBS 28+ window to foreground (un-minimises, sets focus)
- [x] **`muteDesktop` action**: toggles master mute on the default Windows audio playback device
- [x] **Error UX**: parse/reload errors produce a tray notification with the exact error message;
      `Status` menu shows error state and points to the offending file
- [x] `samples/configs/compound-conditions.nle` — new sample demonstrating `and`/`or` rules
- [ ] Try the daemon over several real sessions and note the next friction point

## Phase 1 — Config authoring UX ✓

nl.txt itself calls the current "type it by hand" approach a stopgap ("HUD and GUI will be
used in a next update"). This phase delivers a dedicated WinForms Config Editor (`NL.ConfigEditor`)
that lets you build and test `.nle` files visually, with a live Rule-Engine preview pane.

- [x] **`NL.ConfigEditor` project** — WinForms app (`net8.0-windows`) added to `NL.sln`;
      references only `NL.Core` (pure, cross-platform)
- [x] **In-memory model** — mutable POCOs (`HotkeyEntry`, `SimpleConditionEntry`, `ConditionEntry`,
      `StatementEntry`, `EventEntry`, `ConfigModel`) that map 1-to-1 with the v0.1 grammar
- [x] **`NleWriter`** — converts the editor model to a valid `.nle` string at any time;
      result is always parseable by `NL.Core.Parser`
- [x] **`NleLoader`** — uses `NL.Core.Parser` to load an existing `.nle` file and converts the
      resulting AST back into the editor model (full round-trip)
- [x] **`HotkeyDialog`** — add/edit a hotkey binding (combo + action, validated inputs)
- [x] **`StatementDialog`** — add/edit any statement type: `allow`, `block`, `deny`, `warn`,
      or `if` with up to three `and`/`or` chained conditions plus then/else branches
- [x] **`EditorForm`** — main editor window:
      - *Hotkey Bindings* panel: list view with add/edit/remove/move up-down
      - *Event Rules* panel: event name list (add/rename/remove) + statement list (add/edit/remove/move)
      - *Rule Engine Preview* pane: type event name + optional `name = value` properties,
        click **▶ Evaluate** — runs the live `RuleEngine` on the current model and shows
        ✓ ALLOW (green) or ✗ BLOCK (red) with any warn message
      - *Generated .nle* pane: always-live dark-theme readonly view of the emitted `.nle` text;
        **Copy** and **Save to hotkeys.nle** shortcuts
      - File menu: New / Open… / Save / Save As… / Exit with unsaved-changes guard
      - Tools menu: "Open in Daemon (notify to reload)", "Copy .nle to clipboard"
      - Opens `%LOCALAPPDATA%\NL\hotkeys.nle` by default (same path as the daemon)
- [x] **Tray integration** — "Open Config Editor" item in the daemon tray menu; launches
      `NL.ConfigEditor.exe` (next to daemon binary in published layout) or falls back to
      Notepad with a tooltip if the editor hasn't been built yet
- [ ] Consider adding syntax highlighting to the raw `.nle` pane (Phase 1.5 quality-of-life)

## Phase 2 — SP relationship + join flow ✓

The StreamPlayer (SP) model from nl.txt section 2: normal / graylist / banned standing, join
requirements, and the role set (Admin, Mod, VIP, Friend tiers, base SP). Still no real
networking — this is a data model + rules exercise, similar in spirit to Phase 0. See
[docs/SP_MODEL.md](docs/SP_MODEL.md) for the full writeup.

- [x] **Data model** (`src/NL.Core/Sp/`) — `SpStanding` (normal/graylist/banned, per streamer),
      `SpRole` (Sp/Friend/Vip/Mod/Admin, per streamer), `SpVerification` (flags: email/phone/
      2FA/id), `SpOffense` (with the 2-year `ActiveWindow`/archive rule via `IsActive(now)`),
      `SpStreamerRelationship` (standing/roles/follow/sub per streamer), `SpProfile` (account +
      offense history + per-streamer relationship map)
- [x] **Join-eligibility rules engine** — `JoinEligibilityEngine.Evaluate(profile, streamerId,
      requirements, now)`, a dedicated deterministic evaluator (not routed through the `.nle`
      grammar — see docs/SP_MODEL.md for why) returning `Allow`/`Deny`/`Hold` + reason.
      `JoinRequirements` covers follow/sub required, min account age, required verification
      flags, and a max-active-offenses threshold; Admin/Mod bypass everything but a ban,
      Vip/Friend bypass follow/sub requirements only
- [x] **`NL.SpSimulator`** CLI harness — fixed fake SP profiles + a fake streamer's
      `JoinRequirements`, printing each join attempt's decision (mirrors `NL.Simulator`)
- [x] Unit tests for every branch (ban, graylist hold vs. deny, role bypasses, account age,
      verification, offense threshold, 2-year archival) — `tests/NL.Core.Tests/JoinEligibilityEngineTests.cs`
- [ ] Try it against a few more streamer-authored requirement profiles and see whether
      join eligibility should eventually be authorable from `.nle`-style config too (Phase 2.5)

## Phase 3 — NLServer + real game integration ✓

Game-agnostic NLServer host: real events from any game feed the existing `RuleEngine`, with
optional real actions on Block. Minecraft is one built-in adapter; **any other game** can
integrate by emitting NDJSON (or implementing `IGameEventSource`). See
[docs/NLSERVER.md](docs/NLSERVER.md) (Minecraft specifics also in
[docs/NLSERVER_MINECRAFT.md](docs/NLSERVER_MINECRAFT.md)).

- [x] **Game-agnostic host** — `IGameEventSource` / `IGameActionSink` / `NlServerHost` /
      `SessionEvent`; `RuleEngine` unchanged
- [x] **`minecraft` adapter** — log parser + mapper + RCON (kick/tell); expanded death
      templates; sample replay log
- [x] **`generic` adapter** — NDJSON for any game/mod/plugin (`event` / `player` / `props`);
      sample stream + `.nle`
- [x] **Action channels** — dry-run console, Source RCON (+ templates), `--action-cmd` process
      sink for non-RCON games
- [x] **CLI** — `--game` / `--config` / `--source` / `--replay` / `--rcon` / `--action-cmd`
      (legacy Minecraft positional args still work)
- [x] End-to-end validated with `--replay` on checked-in samples (Minecraft + generic)
- [x] Unit tests for parsers, mapper, host loop, RCON framing
- [ ] Optional later: native per-title adapters (Paper plugin, etc.) — not required for
      Phase 3; NDJSON covers arbitrary games without new C# per title

## Phase 4 — Moderation tooling ✓

Warnings/bans/analytics dashboard for admins/mods, consuming the same rule-engine decisions
and logs as an audit trail (nl.txt section on moderation/roles). See
[docs/MODERATION.md](docs/MODERATION.md) for the full writeup.

- [x] **Audit trail** — `ModerationRecord` (`src/NL.Moderation.Core/`) logs every `ActionResult`
      (Allow/Block from `RuleEngine`/`NlServerHost`) plus who/what triggered it; a mod/admin
      action (warning/ban/graylist/clear) is a distinct `ModerationActionKind` with an
      `IssuedBy`. `IModerationStore` (pure interface) / `JsonlModerationStore`
      (`src/NL.Moderation/` — real JSON-Lines file, append-only)
- [x] **`ModerationService`** — the one entry point: records automatic decisions; issues
      warnings/bans/graylist holds/standing-clears against a Phase 2 `SpProfile`
      (`ISpProfileRepository` / `JsonFileSpProfileRepository`); queries recent actions and
      per-SP offense history. A ban immediately feeds back into Phase 2's
      `JoinEligibilityEngine` (banned SPs are denied on their next join attempt)
- [x] **Wired into `NL.Server`** — new `--streamer` / `--moderation-log` CLI flags; every
      decision from any Phase 3 adapter is appended to the audit log before the action sink runs
- [x] **`NL.ModerationConsole`** — WinForms admin/mod dashboard: recent actions list, SP offense
      history lookup (standing + offense list), and issue warning/ban/graylist/clear buttons
- [x] Unit tests for `ModerationService` (including a cross-phase test: a Phase 4 ban is
      enforced by the Phase 2 `JoinEligibilityEngine`) and the JSON-Lines/JSON file stores
- [ ] Optional later: real analytics (trends, repeat-offender flags) beyond raw recent-actions
      and offense-history views — not required for Phase 4's "basic admin/mod views" scope

## Phase 5 — Anti-cheat signals ✓

Config-driven anomaly flags that build on Phase 0's engine (nl.txt: "the more complex the
NLEvent config, the better the anti-cheat"), not a full standalone anti-cheat. See
[docs/ANTICHEAT.md](docs/ANTICHEAT.md) for the full writeup.

- [x] **Defined "impossible action" vocabulary** for Phase 3 streams — three built-in
      detectors in `src/NL.AntiCheat.Core`: `ImpossibleAction` (action while dead),
      `RateSpike` (too many of the same event in a sliding window), `Teleport` (impossible
      position jump via `player.x`/`y`/`z`). Tunable via `AnomalyThresholds`.
- [x] **Feed anomaly signals through the same `RuleEngine` pipeline** —
      `AnomalyDetectingEventSource` wraps any `IGameEventSource` and yields ordinary
      `anomalyImpossibleAction` / `anomalyRateSpike` / `anomalyTeleport` `GameEvent`s;
      `RuleEngine` / `NlServerHost` unchanged. `NL.Server --anti-cheat` turns the wrap on.
- [x] **Working sample** — `samples/events/anti-cheat-sample.ndjson` +
      `samples/configs/anti-cheat.nle` (Alice clean, Eve: dead-shoot / rate spike /
      teleport); end-to-end `--replay --anti-cheat` demo verified
- [x] Optional NDJSON `ts` (Unix ms) on generic events for deterministic rate/teleport
      windows under `--replay`; unit tests for detectors + host integration
- [ ] Optional later: more detectors (e.g. damage without being aimed-at), per-game Minecraft
      log-derived position/alive signals — not required for Phase 5's core sample scope

## Vertical slice — Session Host + join gate (post Phase 5)

Glue so the Phase 0–5 engines work as one Minecraft-first product loop. See
[docs/MINECRAFT_LIVE.md](docs/MINECRAFT_LIVE.md).

- [x] **Join eligibility inside NLServer** — `SpJoinGate` + `NlServerHost` join hook;
      Deny/Hold → Block (kick); Allow falls through to `.nle` rules
- [x] **Shared `%LOCALAPPDATA%\NL` paths** — `NlPaths` (profiles, moderation log, join
      requirements, session profile); Moderation Console + Session Host + Server CLI align
- [x] **`NL.SessionHost`** — WinForms Start/Stop for one session profile (config/log/RCON/
      join gate/anti-cheat/anomaly auto-mod); opens Config Editor / Moderation Console
- [x] **Anomaly auto-mod** — `--anomaly-auto-mod` / Session Host checkbox: severity≥2
      anomaly Blocks → graylist hold on SP profile
- [x] **Minecraft fidelity** — Paper `[Not Secure]` chat, more death templates, synthetic
      `respawn` after death when the player next chats/advances, `player.alive` props
- [x] **Dogfood docs** — `docs/MINECRAFT_LIVE.md`, `docs/DOGFOOD_NOTES.md`,
      `scripts/seed-banned-eve.ps1`, `scripts/publish.ps1`

## BeamNG.drive bridge (generic NDJSON title)

Second-title path without a new C# `--game` adapter. See [docs/BEAMNG.md](docs/BEAMNG.md).

- [x] Lua `NL_BeamNGBridge` mod → `%LOCALAPPDATA%\NL\beamng-events.ndjson`
- [x] Sample `beamng.nle` / `beamng-sample.ndjson` + Session Host “Load BeamNG freeroam defaults”
- [x] `--beamng-cmd` / `BeamNgUdpActionSink` (SCBN1 warn/recover/kick)
- [x] BeamMP join/leave hooks + docs (`beamng-mod/.../BEAMMP.md`); join gate off for solo
- [x] Live freeroam operator path: `bridge.json` thresholds, recover/UDP harden, BeamNgFreeroam
      anomaly profile, `NL_Kick` BeamMP server plugin + kick queue, dogfood defaults in
      [docs/DOGFOOD_BEAMNG.md](docs/DOGFOOD_BEAMNG.md) / [docs/BEAMNG.md](docs/BEAMNG.md)

## Phase A — Universal game integration spec ✓

Formal contract so **any game** integrates without new C# adapters. See
[docs/NL_INTEGRATION_SPEC.md](docs/NL_INTEGRATION_SPEC.md).

- [x] **NL Integration Protocol v1** — event + action NDJSON envelopes, `"nl": 1`, standard verbs
      (`warn`, `kick`, `recover`, `tell`, `mute`, `despawn`, `custom`)
- [x] **Transports in NL.Server** — file (existing), `tcp://` event listen, `ws://…/nl/v1`
      bidirectional (events in + actions out with `--nl-action auto`), `--nl-action tcp://` split channel
- [x] **Core types** — `NlActionEnvelope`, `NlStandardActions`, `NlSourceUri`, `GenericJsonLineParser`
      nl-version validation
- [x] **Reference bridges** — `integrations/` (Python, Node, Lua, Unity, Unreal, Paper stub, log tail)
- [x] **CLI** — `--source tcp://|ws://`, `--nl-action auto|tcp://`
- [x] **Smoke script** — `scripts/nl-integration-smoke.ps1`
- [x] **Tests** — protocol parsing, TCP round-trip, action envelope serialization
- [x] **Phase B — Session bus** — see [docs/NL_SESSION_BUS.md](docs/NL_SESSION_BUS.md)
- [x] **`NL.SessionHost.Web`** — cross-platform ASP.NET dashboard + REST API (HTTP 27020, WS 27021)
- [x] **`SessionHostService`** — shared session runner + log buffer (WinForms + web)
- [x] **Bus token auth** — WebSocket `?token=` validation when `useSessionBus` / `BusToken` set
- [x] **WinForms bus fields** — `nlActionEndpoint`, `useSessionBus`, network source paths
- [x] **Bridge templates** — Godot, Rust, .NET (`integrations/`)
- [x] **Smoke script** — `scripts/nl-session-bus-smoke.ps1`
- [x] **Tests** — `SessionHostService`, bus helper, WebSocket token rejection

## Phase C — Cross-platform operator tooling ✓

Headless NL on Linux + web moderation console. See [docs/NL_HEADLESS_LINUX.md](docs/NL_HEADLESS_LINUX.md).

- [x] **`NL_DATA_ROOT`** — override shared data directory (Docker / Linux hosts)
- [x] **`NL.Moderation.Web`** — cross-platform moderation dashboard (HTTP 27030) + REST API
- [x] **Headless `NL.Server`** — documented Linux CLI path + `scripts/run-headless-linux.sh`
- [x] **Linux publish** — `scripts/publish-linux.sh` (`artifacts/publish-linux/`)
- [x] **Docker** — `docker/Dockerfile` + `docker/docker-compose.yml` (session host + moderation)
- [x] **Health endpoints** — `GET /health` on web operator apps
- [x] **Tests** — `NlPaths` override, `ModerationHostState`

## Phase D — Networked NL Session Server ✓

Hosted session product shape: remote bridges, connect-time join gate, server-side rules. See
[docs/NL_SESSION_SERVER.md](docs/NL_SESSION_SERVER.md).

- [x] **`NlJoinAdmissionService`** — pre-connect join eligibility (`POST /api/v1/session/admit`)
- [x] **Session manifest** — `GET /api/v1/session/manifest` (bridge URL, admit URL, moderation URL)
- [x] **`NL_PUBLIC_*` env** — public URL overrides for Docker/cloud manifests
- [x] **Unified session server** — `NL.SessionHost.Web` exposes session + moderation APIs
- [x] **Operator UI** — remote manifest panel + `/moderation.html` on session server
- [x] **Bridge admit flow** — Python `--admit-url`; sample checks before `playerJoin`
- [x] **Docker** — `docker/docker-compose.session-server.yml`
- [x] **Smoke script** — `scripts/nl-session-server-smoke.ps1`
- [x] **Tests** — join admission, manifest URL builder

## Phase E — Demo security & secrets ✓

Make the session server safe to expose as a public demo. See
[docs/NL_DEMO_SECURITY.md](docs/NL_DEMO_SECURITY.md) and [`.env.example`](.env.example).

- [x] **`NL_PUBLIC_MODE`** — requires fixed `NL_BUS_TOKEN` + `NL_OPERATOR_KEY` at startup
- [x] **Operator auth middleware** — protects session control + moderation write endpoints
- [x] **Public read endpoints** — `/health`, manifest, admit, audit reads stay open
- [x] **Secret redaction** — bridge token hidden from manifest/bus/status unless operator-authenticated
- [x] **CORS tightening** — `NL_CORS_ORIGINS` / `NL_PUBLIC_HTTP` in public mode; permissive local dev
- [x] **`.env.example`** — documents all demo deployment secrets and URL overrides
- [x] **Web UI operator key** — dashboard stores `X-NL-Operator-Key` in browser session for writes
- [x] **Tests** — settings validation, auth, path guards, manifest redaction

## Phase F — CI/CD & container deploy ✓

Automated build/test, container publishing, and a one-command public demo stack. See
[docs/NL_DEPLOY.md](docs/NL_DEPLOY.md).

- [x] **GitHub Actions CI** — cross-platform build, full test suite, Docker smoke on PR/push
- [x] **GHCR publish** — `session-server` image on push to `main` + version tags
- [x] **Demo compose stack** — Caddy TLS + session server + persistent `nl-demo-data` volume
- [x] **Reverse proxy** — HTTP dashboard on `/`, WebSocket bridge on `/nl/v1`
- [x] **`NL_PUBLIC_*` wiring** — demo compose sets HTTPS/WSS manifest URLs automatically
- [x] **Docker healthchecks** — `/health` in Dockerfile + compose
- [x] **Deploy scripts** — `scripts/deploy-demo.sh` / `deploy-demo.ps1`
- [x] **Sample configs in image** — `.nle` fixtures bundled for container defaults
- [x] **Docs** — `docs/NL_DEPLOY.md`, `docker/.env.demo.example`

## Phase G — Hosted demo loop ✓

Turnkey live demo for public visitors: auto-started session, looping sample events, periodic
data reset. See [docs/NL_DEMO.md](docs/NL_DEMO.md).

- [x] **`NL_DEMO_MODE`** — `NlDemoHostedService` auto-applies demo profile and starts session
- [x] **Demo profile** — `samples/configs/demo.nle` + session bus defaults (join gate off)
- [x] **Demo reset** — `NlDemoReset` clears moderation + SP stores; configurable interval
- [x] **Demo bridge sidecar** — `docker/demo-bridge/` runs `nl_bridge.py --loop` in compose
- [x] **`--loop` bridge mode** — Python reference bridge repeats sample events with reconnect
- [x] **All-interface listeners** — WS/TCP bind fix for Docker bridge networking
- [x] **Demo status API** — `GET /api/v1/demo/status` (public read)
- [x] **Dashboard banner** — live demo indicator on session server UI
- [x] **Local/CI compose** — `docker/docker-compose.demo-local.yml` + `scripts/nl-demo-smoke.sh`
- [x] **Tests** — demo settings, reset, listen-host bind helpers
- [x] **CI** — Phase G smoke job in GitHub Actions

## Phase H — Spectator vs operator UX ✓

Split public demo viewing from operator control. See [docs/NL_SPECTATOR.md](docs/NL_SPECTATOR.md).

- [x] **Spectator landing** — `/` read-only page with live stats and decision feed
- [x] **Operator console** — `/operator.html` for session control + secrets (gated by operator key)
- [x] **Spectator API** — `/api/v1/spectator/status`, `/decisions`, `/scenarios`, `/trigger`
- [x] **Live decision feed** — polls automatic moderation records (Allow/Block/Warn)
- [x] **Try-a-rule panel** — rate-limited preset triggers for visitors
- [x] **Public session redaction** — unauthenticated `/api/v1/session` omits paths, log, secrets
- [x] **Internal event injection** — spectator triggers via loopback WebSocket to session bus
- [x] **Nav + styling** — shared nav across watch / operator / moderation pages
- [x] **Tests** — rate limiter, scenarios, spectator service, settings
- [x] **Docs** — `docs/NL_SPECTATOR.md`, security index updated

## Phase 6+ — Long-term / high-risk ideas (documented only, not scheduled)

These involve real money, KYC/identity verification, blockchain, and gambling-adjacent
mechanics, which carry legal and regulatory risk well beyond a prototype. Kept here as
reference to nl.txt, deliberately not broken into build steps yet:

- [ ] SPt points economy (predictions, polls)
- [ ] SrC blockchain trading cards + marketplace
- [ ] StreamerBids (live third-party-escrow auctions)
- [ ] Mobile companion app for admins/mods
- [ ] Cross-platform social integration (Twitch/Discord/Steam/Xbox/PlayStation/Epic/etc.)

---

When picking up work, update the checkboxes above rather than creating a separate progress
file, so this stays the single source of truth for "what's actually built."
