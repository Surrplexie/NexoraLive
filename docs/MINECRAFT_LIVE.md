# Minecraft live setup + dogfood checklist

One-page guide to run the P0 vertical slice: join gate + `.nle` rules + anti-cheat +
moderation on a real (or replay) Minecraft Java server.

## Prerequisites

- .NET 8 SDK
- Vanilla or Paper Minecraft Java server with **RCON enabled**
- NL built: `dotnet build src/NL.sln`

Enable RCON in `server.properties`:

```
enable-rcon=true
rcon.port=25575
rcon.password=your-secret
```

## Shared data paths

All tools default to `%LOCALAPPDATA%\NL\`:

| File | Purpose |
|---|---|
| `sp-profiles.json` | SP standing / offenses (join gate + Moderation Console) |
| `moderation.jsonl` | Audit trail |
| `join-requirements.json` | Streamer join requirements |
| `session-profile.json` | Last Session Host profile |

Streamer id defaults to `default-streamer` everywhere.

## Fast path — Session Host (recommended)

```bash
dotnet run --project src/NL.SessionHost
```

1. Set **Streamer id** (or leave default).
2. Game = `minecraft`.
3. Config = `samples/configs/minecraft.nle` (or your own).
4. Source = your server `logs/latest.log`.
5. RCON = `127.0.0.1:25575:your-secret` (omit for dry-run).
6. Leave **Join gate** + **Anti-cheat** checked for the full loop.
7. Optional: **Anomaly auto-mod** (severity≥2 Block → graylist).
8. **Start session**. Use Tools → Moderation Console / Config Editor as needed.

For a safe first pass, leave RCON empty (dry-run) and check **Replay once** against
`samples/logs/minecraft-sample.log`.

## CLI equivalent

```bash
dotnet run --project src/NL.Server -- --game minecraft \
  --config samples/configs/minecraft.nle \
  --source "C:\path\to\logs\latest.log" \
  --rcon 127.0.0.1:25575:your-secret \
  --streamer default-streamer \
  --join-gate --anti-cheat --anomaly-auto-mod
```

## Ban → kick loop (prove join gate)

1. Open Moderation Console; streamer id must match Session Host.
2. Create profile for a test account name (exact Minecraft name), **Issue Ban**.
3. That account joins the server → NL should Block join (RCON kick or dry-run log)
   with reason from `JoinEligibilityEngine`.
4. Clear standing → next join Allow (+ normal `.nle` `playerJoin` rules).

## Dogfood checklist (one ~1h session)

Copy into `docs/DOGFOOD_NOTES.md` (or a private note) after the session:

- [ ] Session Host started without hand-built CLI flags
- [ ] Join of a banned SP was kicked / dry-run Blocked
- [ ] Capslock chat / death rules fired as expected
- [ ] False kicks? (list player + event + message)
- [ ] Missed death messages? (paste raw log lines)
- [ ] Paper `[Not Secure]` chat parsed?
- [ ] Synthetic respawn after death+chat worked for anti-cheat?
- [ ] Moderation Console showed automatic decisions
- [ ] Hotkey Daemon left running separately without conflict?
- [ ] Restart mid-session: Session Host recover OK?

## Replay fixtures (no live server)

```bash
# Join gate (seed Eve banned first — see scripts/seed-banned-eve.ps1)
dotnet run --project src/NL.Server -- --game generic --config samples/configs/generic.nle \
  --source samples/events/join-gate-sample.ndjson --replay --join-gate

# Anti-cheat sample
dotnet run --project src/NL.Server -- --game generic --config samples/configs/anti-cheat.nle \
  --source samples/events/anti-cheat-sample.ndjson --replay --anti-cheat --anomaly-auto-mod
```
