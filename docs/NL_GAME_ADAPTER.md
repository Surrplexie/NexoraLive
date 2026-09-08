# NL Game Adapter Contract (Phase T0)

Standard way to onboard a **new game** into NexoraLive without editing `Program.cs` or
SessionHost core logic.

**Related:** [NL_INTEGRATION_SPEC.md](NL_INTEGRATION_SPEC.md), [NL_GAME_EXPANSION_PLAN.md](NL_GAME_EXPANSION_PLAN.md),
[NL_FORK_GAME_IMAGES.md](NL_FORK_GAME_IMAGES.md), `integrations/_template/`.

---

## Contract overview

```text
integrations/<gameId>/
  adapter.manifest.json   ← machine-readable IGameForkAdapter
  README.md / CHECKLIST.md
  sidecar/nl_sidecar.py   ← or native plugin

docker/fork-<gameId>/Dockerfile
samples/configs/<gameId>.nle
samples/fork/catalog.json          ← catalog row
scripts/build-fork-images.ps1      ← image key
scripts/nl-dogfood-flow-<gameId>.ps1
```

| Surface | Purpose |
|---------|---------|
| **`IGameForkAdapter`** | C# contract: events, actions, connect scheme, health, image paths |
| **`adapter.manifest.json`** | Same contract as JSON — discovered under `integrations/*/`. |
| **`IForkRuntime`** | In-process propose-then-commit enforcement (Phase P) — implement *or* use a bridge sidecar |
| **Integration Spec v1** | NDJSON event/action envelopes on the session bus |

### Required session events (minimum)

`sessionStart`, `playerJoin`, `playerLeave`, `playerChat`

Titles may add more (`shoot`, `crash`, `entityDamage`, …). Every required event must appear in the `.nle` template.

### Required actions (minimum)

`warn`, `kick` — plus any verbs your rules emit (`tell`, `recover`, …). See Integration Spec v1.

### Connect URL scheme

Manifest `connectScheme` + optional `playerConnectPort` → NL Client / session manifest, e.g.:

| Game | Example |
|------|---------|
| Minecraft | `minecraft://host:25565` |
| Example (T0) | `example://host:27000` |
| BeamNG sidecar | `beamng-sidecar://…` |

### Health / readiness

| Type | Meaning |
|------|---------|
| `file` | Status JSON path; `readyField` must be `true` (default `sessionStarted`) |
| `http` | HTTP path must return `readyHttpStatus` (default 200) |
| `none` | Always ready (dev only) |

Orchestrator and validate scripts use this for readiness probes.

---

## Scaffold a new game

```powershell
powershell -File scripts/nl-game-adapter-scaffold.ps1 `
  -GameId rimworld `
  -DisplayName "RimWorld" `
  -ConnectScheme rimworld `
  -Port 25565 `
  -SteamAppId 294100
```

Then:

1. Register the image key in `scripts/build-fork-images.ps1` if the scaffold warned you.
2. Replace the hello-fork stand-in Dockerfile with a real dedicated server + NL sidecar/plugin.
3. Validate:

```powershell
powershell -File scripts/nl-game-adapter-validate.ps1 -GameId rimworld
powershell -File scripts/nl-game-adapter-validate.ps1 -All
```

---

## Checklist (CI gate)

`GameForkAdapterChecklist` / `nl-game-adapter-validate.ps1` require:

1. Catalog row in `samples/fork/catalog.json`
2. Dockerfile at manifest path + key in `build-fork-images.ps1`
3. `integrations/<game>/adapter.manifest.json`
4. `.nle` template with all `requiredEvents`
5. Dogfood script path exists
6. README/CHECKLIST or sidecar stub
7. `warn` + `kick` in `requiredActions`
8. Non-empty `connectScheme`
9. Health probe configured
10. Optional `nativePlugin` folder (Harmony mod / Paper plugin) when declared

Unit tests: `tests/NL.Fork.Core.Tests/GameForkAdapterTests.cs` (filter `GameForkAdapter`).

CI: `.github/workflows/ci.yml` job **adapter-validate**.

---

## Reference adapters

| gameId | Integration dir |
|--------|-----------------|
| `hello-fork` | `integrations/hello-fork/` |
| `minecraft` | `integrations/minecraft/` |
| `minecraft-paper` | `integrations/minecraft-paper/` |
| `beamng` | `integrations/beamng/` |
| `example-game` | `integrations/example-game/` (filled template) |
| `rimworld` | `integrations/rimworld/` (Phase T1) |
| `kenshi` | `integrations/kenshi/` (Phase T1) |

Template (placeholders): `integrations/_template/`.

---

## C# API

```csharp
var registry = GameForkAdapterRegistry.FromRepo(repoRoot);
var adapter = registry.GetRequired("example-game");
ForkGameProfiles.AdapterRegistry = registry;
var profile = ForkGameProfiles.Resolve("example-game"); // uses manifest for unknown ids
var report = GameForkAdapterChecklist.Evaluate(repoRoot, adapter);
```

Built-in profiles (minecraft / beamng / hello-fork) remain authoritative; **new** titles resolve from manifests without switching on `gameId` in core code.
