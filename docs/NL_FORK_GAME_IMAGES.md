# NL Real Game Fork Images (Phase P)

NL-hosted fork images for **Minecraft Java** and **BeamNG.drive**, extending the hello-fork runtime with game-specific event vocabularies and Docker orchestrator integration.

Bridge paths (log tail, Lua mod) **keep working** — these images are the NL-hosted fork alternative.

## Images

| Image | Game | Purpose |
|-------|------|---------|
| `nl-fork-hello:latest` | Demo | Reference FPS-like runtime |
| `nl-fork-minecraft:latest` | Minecraft | C# sidecar sim + `minecraft.nle` vocabulary |
| `nl-fork-minecraft-paper:latest` | Minecraft | **Real Paper 1.21.1** + NL bridge plugin |
| `nl-fork-beamng:latest` | BeamNG | C# sidecar + `beamng.nle` (pairs with host game + Lua mod) |

## Build

```powershell
# From repo root
docker build -f docker/fork-hello/Dockerfile -t nl-fork-hello:latest .
docker build -f docker/fork-minecraft/Dockerfile -t nl-fork-minecraft:latest .
docker build -f docker/fork-beamng/Dockerfile -t nl-fork-beamng:latest .

# Paper plugin + dedicated server (requires Docker; downloads Paper via itzg/minecraft-server)
docker build -f docker/fork-minecraft-paper/Dockerfile -t nl-fork-minecraft-paper:latest .
```

Paper plugin only (Maven):

```powershell
mvn -f integrations/minecraft/paper/pom.xml package
# → integrations/minecraft/paper/target/nl-bridge-1.0.0.jar
```

## Orchestrator wiring

Catalog entries in `samples/fork/catalog.json` map `gameId` → `dockerImage`:

- `minecraft@1.0` → `nl-fork-minecraft:latest`
- `minecraft-paper@1.0` → `nl-fork-minecraft-paper:latest` (port **25565**)
- `beamng@1.0` → `nl-fork-beamng:latest`

When `CreateSession(gameId: "minecraft", …)` runs, orchestrator resolves the image via `ForkGameProfiles` and passes `--game minecraft` to the container.

```powershell
$env:NL_FORK_ORCHESTRATOR_ENABLED = "true"
$env:NL_FORK_ORCHESTRATOR_MODE = "docker"
Copy-Item samples/fork/catalog.json "$env:LOCALAPPDATA/NL/fork-catalog/catalog.json"
dotnet run --project src/NL.SessionHost.Web
```

Connect manifest exposes:

- Minecraft Paper: `minecraft://127.0.0.1:25565`
- Sidecars: `docker://nl-fork-{sessionId}` or relay-masked URL (Phase S)

## Runtime profiles

```powershell
# Local embedded (no session bus)
dotnet run --project src/NL.Fork.Runtime -- --game minecraft --config samples/configs/minecraft.nle
dotnet run --project src/NL.Fork.Runtime -- --game beamng --config samples/configs/beamng.nle

# Remote session bus + demo loop
dotnet run --project src/NL.Fork.Runtime -- --game minecraft --url ws://127.0.0.1:27021/nl/v1?token=... --loop
```

Env: `NL_FORK_GAME=minecraft|beamng|hello`

## Minecraft Paper plugin

Full **propose-then-commit** fork runtime in Java:

| Bukkit event | NL event |
|--------------|----------|
| `PlayerJoinEvent` | `playerJoin` (+ admit API pre-check) |
| `PlayerQuitEvent` | `playerLeave` |
| `AsyncPlayerChatEvent` | `playerChat` |
| `EntityDamageByEntityEvent` | `entityDamage` |

On Block: cancel event / kick player. WebSocket client in `NlWebSocketBridge.java`.

Config: `plugins/NLBridge/config.yml` or env `NL_FORK_WS_URL`, `NL_FORK_STATUS`.

## BeamNG sidecar model

BeamNG cannot run headless in Docker today. The **`nl-fork-beamng`** container runs the NL fork sidecar (move/crash/airtime/rollover/boundary against `beamng.nle`). The streamer's BeamNG client uses the existing **`NL_BeamNGBridge`** Lua mod pointed at the sidecar UDP/WS path.

For full in-game enforcement, point the Lua mod's event sink at the session bus WebSocket (same contract as Paper).

## Tests & smoke

```powershell
dotnet test tests/NL.Fork.Core.Tests
powershell -File scripts/nl-fork-game-images-smoke.ps1
```

## Related

- [NL_FORK_RUNTIME.md](NL_FORK_RUNTIME.md) — hello-fork architecture
- [NL_FORK_ORCHESTRATOR.md](NL_FORK_ORCHESTRATOR.md) — create/destroy lifecycle
- [MINECRAFT_LIVE.md](MINECRAFT_LIVE.md) — bridge path (still valid)
- [BEAMNG.md](BEAMNG.md) — Lua bridge path
