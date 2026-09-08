# Kenshi on NexoraLive (Phase T1 — full)

Second **mod-backed** title after RimWorld — onboarded through the
[T0 adapter contract](NL_GAME_ADAPTER.md). This title also has the Paper-plugin
equivalent: an NL community-MP hook catalog, a `kenshi://` connect port, and a
Docker image that runs the C# sidecar unless a licensed game tree is mounted.

Kenshi has **no official dedicated server**. NL does **not** ship Kenshi binaries.

Kenshi is **not** a public-line launch blocker (`NL_GA_REQUIRED_GAMES` stays without it).

| | |
|--|--|
| **gameId** | `kenshi` |
| **Steam app** | `233860` |
| **Tier** | AtOwnRisk |
| **Image** | `nl-fork-kenshi:latest` |
| **Connect** | `kenshi://host:23386` |
| **Runtime** | `KenshiForkRuntime` |
| **Rules** | `samples/configs/kenshi.nle` |
| **Integration** | `integrations/kenshi/` |
| **Native plugin** | `integrations/kenshi/mod/` (`NexoraLive.NLKenshiBridge`) |

## Ideal NL path (this title)

| Element | Status |
|---------|--------|
| NL hosts world | ✅ sidecar fork image (licensed `/game` when mounted) |
| Native client | ✅ Kenshi + community MP mod |
| NL app admit | ✅ ownership + social gate |
| In-fork enforcement | ✅ propose-then-commit (`KenshiForkRuntime` + hook host) |
| Connect URL | ✅ TCP banner on **23386** (`NL-KENSHI/1`) |
| Cross-platform | ❌ PC Steam |
| True SP without mod | ❌ |
| Redistribute Kenshi | ❌ operators mount a licensed install |

## Event vocabulary

| Event | Props | Sample rule |
|-------|-------|-------------|
| `playerChat` | `chat.capsRatio`, `chat.length` | Block caps spam |
| `entityDamage` | `weapon.damage` | Block extreme combat damage |
| `knockdown` / `respawn` | `player.downed`, `player.health` | KO + get-up |
| `move` | `player.x/y/z` | Character move |
| `squadOrder` | `squad.ordered`, `player.downed` | Block ordering a KO'd character |
| `buildingDestroy` | `building.value` | Block high-value grief |
| `steal` | `steal.value` | Block high-value theft |
| `raid` | `raid.severity` | Block mass raids |
| `trade` | `trade.value` | Block high-value trades |
| `itemDestroy` | `item.value` | Block stripping valuables |

## Operator quick start

```powershell
# Validate adapter checklist (includes nativePlugin)
powershell -File scripts/nl-game-adapter-validate.ps1 -GameId kenshi

# Unit / embedded enforcement + hook catalog
dotnet test tests/NL.Fork.Core.Tests --filter "FullyQualifiedName~Kenshi"

# Embedded one-shot
dotnet run --project src/NL.Fork.Runtime -- --game kenshi --config samples/configs/kenshi.nle

# Build image
powershell -File scripts/build-fork-images.ps1 -Images kenshi

# Dogfood (session host running with fork orchestrator)
powershell -File scripts/nl-dogfood-flow-kenshi.ps1 -ExpectProvisioner mock
powershell -File scripts/nl-dogfood-flow-kenshi.ps1 -ExpectProvisioner docker -VerifyRuleEvents
powershell -File scripts/nl-live-social-dogfood-flow.ps1 -GameId kenshi -SocialMode mock -SkipImageBuild
```

Streamer picks **Kenshi** from `/fork-catalog.html` → goes live → SPs admit via NL Client
with Steam ownership of **233860** → outpost rules enforce through the session bus → connect
string is `kenshi://host:23386`. T2-lite rewrites loopback when `NL_FORK_PUBLIC_CONNECT_HOST`
is set. Public VPS still publishes RimWorld **25555**, not Kenshi **23386**.

## Licensed game path

NL does **not** ship Kenshi. On a host that already owns the game:

```powershell
docker run --rm -p 23386:23386 `
  -v ${env:LOCALAPPDATA}/NL:/data `
  -v "C:\Path\To\Kenshi:/game" `
  -e NL_KENSHI_DEDICATED=1 `
  -e NL_FORK_WS_URL=ws://host.docker.internal:27021/nl/v1?token=... `
  nl-fork-kenshi:latest
```

`docker/fork-kenshi/entrypoint.sh` copies `integrations/kenshi/mod` into
`/game/mods/NLBridge` and execs a community-MP host binary when present. Otherwise the
image stays on the C# sidecar (CI / dogfood).

Hook catalog: `NlKenshiHarmonyHooks.HookMap` (community MP + FCS-style method names → NL
events). Compile against Kenshi assemblies on the operator machine for live in-game
cancel; CI validates the protocol without Kenshi DLLs.

## Production dogfood

```powershell
powershell -File scripts/nl-production-dogfood-validate.ps1 -AllGames
```

`-AllGames` smokes **hello-fork + minecraft + beamng + rimworld + kenshi**. The validation
gate requires a kenshi join only when `NL_PRODUCTION_DOGFOOD_REQUIRED_GAMES` includes
`kenshi`. Public-line / VPS `NL_GA_REQUIRED_GAMES` does **not** include Kenshi.
