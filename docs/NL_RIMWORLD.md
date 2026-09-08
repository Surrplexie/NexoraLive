# RimWorld on NexoraLive (Phase T1 — full)

First **new major title** after Minecraft / BeamNG — onboarded through the
[T0 adapter contract](NL_GAME_ADAPTER.md). This title now has the Paper-plugin
equivalent: an NL Harmony / Together bridge, a `rimworld://` connect port, and a
Docker image that runs the C# sidecar unless a licensed game tree is mounted.

| | |
|--|--|
| **gameId** | `rimworld` |
| **Steam app** | `294100` |
| **Tier** | AtOwnRisk |
| **Image** | `nl-fork-rimworld:latest` |
| **Connect** | `rimworld://host:25555` |
| **Runtime** | `RimWorldForkRuntime` |
| **Rules** | `samples/configs/rimworld.nle` |
| **Integration** | `integrations/rimworld/` |
| **Native plugin** | `integrations/rimworld/mod/` (`NexoraLive.NLBridge`) |

## Ideal NL path (this title)

| Element | Status |
|---------|--------|
| NL hosts world | ✅ sidecar fork image (Together dedicated when `/game` is mounted) |
| Native client | ✅ RimWorld + MP mod (e.g. Together) |
| NL app admit | ✅ ownership + social gate |
| In-fork enforcement | ✅ propose-then-commit (`RimWorldForkRuntime` + Harmony host) |
| Connect URL | ✅ TCP banner on **25555** (`NL-RIMWORLD/1`) |
| Cross-platform | ❌ PC Steam |
| True SP without mod | ❌ |
| Redistribute RimWorld | ❌ operators mount a licensed install |

## Event vocabulary

| Event | Props | Sample rule |
|-------|-------|-------------|
| `playerChat` | `chat.capsRatio`, `chat.length` | Block caps spam |
| `entityDamage` | `weapon.damage` | Block extreme pawn damage |
| `colonistDown` / `respawn` | `player.downed`, `player.health` | Rescue after down |
| `move` | `player.x/y/z` | Colony cell move (teleport check) |
| `colonistDraft` | `colonist.drafted`, `player.downed` | Block drafting downed pawns |
| `buildingDestroy` | `building.value` | Block high-value grief |
| `zoneEdit` | `zone.tiles` | Block mass zone edits |
| `animalRelease` | `animal.count` | Block dumping animals |
| `trade` | `trade.value` | Block high-value trades |
| `itemDestroy` | `item.value` | Block stripping valuables |

## Operator quick start

```powershell
# Validate adapter checklist (includes nativePlugin)
powershell -File scripts/nl-game-adapter-validate.ps1 -GameId rimworld

# Unit / embedded enforcement + Harmony bridge
dotnet test tests/NL.Fork.Core.Tests --filter "FullyQualifiedName~RimWorld"

# Embedded one-shot
dotnet run --project src/NL.Fork.Runtime -- --game rimworld --config samples/configs/rimworld.nle

# Build image
powershell -File scripts/build-fork-images.ps1 -Images rimworld

# Dogfood (session host running with fork orchestrator)
powershell -File scripts/nl-dogfood-flow-rimworld.ps1 -ExpectProvisioner mock
powershell -File scripts/nl-dogfood-flow-rimworld.ps1 -ExpectProvisioner docker -VerifyRuleEvents
powershell -File scripts/nl-live-social-dogfood-flow.ps1 -GameId rimworld -SocialMode mock -SkipImageBuild
```

Streamer picks **RimWorld** from `/fork-catalog.html` → goes live → SPs admit via NL Client
with Steam ownership of **294100** → colony rules enforce through the session bus → connect
string is `rimworld://host:25555` (on VPS: `rimworld://play.yourdomain.com:25555` — [NL_T2_LITE.md](NL_T2_LITE.md)).

## Dedicated Together path (licensed game)

NL does **not** ship RimWorld. On a host that already owns the game:

```powershell
docker run --rm -p 25555:25555 `
  -v ${env:LOCALAPPDATA}/NL:/data `
  -v "C:\Path\To\RimWorld:/game" `
  -e NL_RIMWORLD_DEDICATED=1 `
  -e NL_FORK_WS_URL=ws://host.docker.internal:27021/nl/v1?token=... `
  nl-fork-rimworld:latest
```

`docker/fork-rimworld/entrypoint.sh` copies `integrations/rimworld/mod` into
`/game/Mods/NLBridge` and execs the dedicated binary when present. Otherwise the
image stays on the C# sidecar (CI / dogfood).

Harmony hook catalog: `NlRimWorldHarmonyHooks.HookMap` (Together + vanilla Verse/RimWorld
method names → NL events). Compile against `Assembly-CSharp` + Harmony on the operator
machine for live in-game cancel; CI validates the protocol without RimWorld DLLs.

## Production dogfood

```powershell
powershell -File scripts/nl-production-dogfood-validate.ps1 -AllGames
```

`-AllGames` now smokes **hello-fork + minecraft + beamng + rimworld**. The validation gate
requires a rimworld join only when `NL_PRODUCTION_DOGFOOD_REQUIRED_GAMES` includes `rimworld`.
