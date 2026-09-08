# NLServer + Minecraft — Phase 3 adapter notes

> **Start here for Phase 3 overall:** [`NLSERVER.md`](NLSERVER.md) — the host is now
> **game-agnostic** (`minecraft` + `generic` NDJSON for any title). This page keeps the
> Minecraft-specific vocabulary and rationale.

## The decision: Minecraft Java server, via log tailing + RCON

Picked **Minecraft: Java Edition server** (vanilla or modded/Paper) as the first *built-in*
adapter because it's:

- **Real and simple** — a genuine multiplayer game, not a mock, but with a plain-text
  console log and a documented remote-console protocol instead of a proprietary SDK.
- **Moddable** — matches nl.txt's own "NL makes mods easy by serverloading them" framing;
  Paper/Spigot plugins could later replace log-scraping with a proper event source without
  changing anything downstream of `MinecraftEventMapper`.
- **Free and cross-platform** — no license, storefront, or engine SDK needed to build or run
  this integration; only to actually play-test it live.
- **Already speaks a real remote-control protocol** — the Source/Valve RCON protocol, which
  gives us genuine remote actions (kick, tell, ban) instead of only reading events.

## Where the code lives

- `src/NL.Server.Core/` — pure logic, no I/O, fully unit-tested:
  - `MinecraftLogParser` — turns one raw console/log line into a `ParsedMinecraftEvent`
    (`MinecraftEventKind`: `PlayerJoin`, `PlayerLeave`, `PlayerChat`, `PlayerDeath`,
    `PlayerAdvancement`, `ServerStarted`, or `Unknown`). Covers a curated common subset of
    vanilla's log lines — see caveats below.
  - `PlayerSessionTracker` — pure per-player running counters (join/death/advancement counts)
    for this server session.
  - `MinecraftEventMapper` — turns a `ParsedMinecraftEvent` + tracker state into a
    `MappedGameEvent` (NL.Core's existing `GameEvent`, plus the player name kept alongside —
    see "Why GameEvent doesn't change" below).
  - `RconPacket` / `RconPacketType` — Source RCON wire-format encode/decode (pure byte
    logic — no socket).
- `src/NL.Server/` — the real I/O host:
  - `MinecraftLogTailer` — polls a growing log file like `tail -f`, yielding new lines.
  - `RconClient` — a real `TcpClient`-based RCON client (auth + exec command) built on
    `RconPacket`.
  - `Program.cs` — wires it together: loads a `.nle` config into a `RuleEngine`, tails the
    log, maps each line to a `GameEvent`, evaluates it, and — if a `RconClient` is configured
    and the decision is `Block` — sends a real `kick`/`tell` command.
- `tests/NL.Server.Core.Tests/` — parser, mapper, tracker, and RCON packet round-trip tests
  (including split/partial-buffer decoding, since a real `NetworkStream` can split packets
  arbitrarily).
- `samples/configs/minecraft.nle` — a sample config exercising every event below.

## Event vocabulary

Real events from the Minecraft server log, mapped to `GameEvent`s a `.nle` config can already
gate with the existing v0.1 grammar (numeric comparisons, `if`/`and`/`or`, `warn`):

| Event name | Fires on | Numeric properties |
|---|---|---|
| `playerJoin` | `<name> joined the game` | `player.sessionJoinCount` — this player's join count so far this run |
| `playerLeave` | `<name> left the game` | *(none)* |
| `playerChat` | `<name>: <message>` | `chat.length`, `chat.capsRatio` (0–1, letters only), `chat.isCommand` (1 if it starts with `/`) |
| `playerDeath` | any recognized death message (slain/shot/killed/blown up/fell/drowned/burned/starved/died) | `player.sessionDeathCount` |
| `playerAdvancement` | `<name> has made the advancement [...]` (or reached the goal / completed the challenge) | `player.sessionAdvancementCount` |

`ServerStarted` (the `Done (...)!` startup line) is parsed but intentionally has no `GameEvent`
mapping — it's host lifecycle, not a per-player gameplay rule target.

## Why `GameEvent` doesn't change

`GameEvent` stayed exactly "name + numeric properties" (see docs/ARCHITECTURE.md) — no player
identity field was added to it. The player's name is threaded alongside the `GameEvent` in
`MappedGameEvent`, used only by `NL.Server`'s own action-application code (which player to
`kick`/`tell`), never by `RuleEngine.Evaluate` itself. This is exactly the "little/no engine
change" the roadmap calls for: `RuleEngine` needed zero changes for a real event source.

## Real actions: what `Block` actually does here

Unlike Phase 0's mock events, some Minecraft events represent something that already
happened (a chat message was sent, a death occurred) — `Block` can't undo that. What it does,
in the current wiring:

- **`playerJoin` → `Block`**: sends a real `kick <player> <message>` — genuinely preventative,
  since it runs right after the join.
- **Every other event → `Block`**: sends a real `tell <player> <message>` — a real-time
  moderation notice, not a retroactive undo. A streamer wanting true chat filtering would
  need a plugin that can intercept chat before it's broadcast (a Paper plugin event source,
  not log-tailing) — a natural extension, not a redesign.

## Setup (to actually run this against a live server)

1. Install Java and download a Minecraft server jar (`server.jar`), accept the EULA
   (`eula.txt`), and start it once to generate `server.properties`.
2. In `server.properties`, set `enable-rcon=true`, `rcon.port=25575`, and a `rcon.password`.
3. Preferred CLI:
   `dotnet run --project src/NL.Server -- --game minecraft --config samples/configs/minecraft.nle --source "<path to>/logs/latest.log" --rcon 127.0.0.1:25575:secret`
   (use `--replay` with `samples/logs/minecraft-sample.log` for a no-server demo).

## Status

Phase 3 as a whole is **complete** — see [NLSERVER.md](NLSERVER.md). Minecraft remains a
first-class adapter; optional live `server.jar` playtests and Paper plugins are nice-to-haves,
not blockers (NDJSON covers any other title).

- [x] Vocabulary, parser/mapper, RCON framing, `--replay` sample validation
- [x] Broader death-message coverage
- [ ] Optional: run against an installed live Minecraft + RCON credentials on your machine
- [ ] Optional: Paper plugin event source instead of log scraping
