# NL Game Adapter Template (Phase T0)

Copy this folder to onboard a **new** title without editing `Program.cs` or SessionHost core logic.

## Quick start

```powershell
# From repo root — scaffolds integrations/<gameId>/ + samples + catalog snippet
powershell -File scripts/nl-game-adapter-scaffold.ps1 -GameId rimworld -DisplayName "RimWorld"

# Validate checklist (CI gate)
powershell -File scripts/nl-game-adapter-validate.ps1 -GameId rimworld
```

Or copy manually:

1. Copy `integrations/_template/` → `integrations/<gameId>/`
2. Replace every `{{GAME_ID}}`, `{{DISPLAY_NAME}}`, `{{CONNECT_SCHEME}}`, `{{DOCKER_IMAGE}}`, `{{MAJOR}}`, `{{PORT}}`, `{{STEAM_APP_ID}}`
3. Add catalog row (see `catalog.snippet.json`)
4. Register Dockerfile in `scripts/build-fork-images.ps1`
5. Add `samples/configs/<gameId>.nle`
6. Run `scripts/nl-game-adapter-validate.ps1 -GameId <gameId>`

## Contract

See [docs/NL_GAME_ADAPTER.md](../../docs/NL_GAME_ADAPTER.md) and [docs/NL_INTEGRATION_SPEC.md](../../docs/NL_INTEGRATION_SPEC.md).

| Piece | File |
|-------|------|
| Manifest | `adapter.manifest.json` |
| Checklist | `CHECKLIST.md` |
| Sidecar stub | `sidecar/nl_sidecar.py` |
| Dockerfile | `Dockerfile` (also copied under `docker/fork-<gameId>/` by scaffold) |
| Catalog snippet | `catalog.snippet.json` |
| Sample `.nle` | `samples/template.nle` (scaffold writes `samples/configs/<gameId>.nle`) |
| Dogfood stub | `scripts/nl-dogfood-flow-{{GAME_ID}}.ps1` |

## Required events (minimum)

`sessionStart`, `playerJoin`, `playerLeave`, `playerChat`

## Required actions (minimum)

`warn`, `kick` — also handle `tell` / `recover` when your `.nle` blocks chat or anomalies.

## Exit criteria

`nl-game-adapter-validate.ps1 -GameId <id>` exits **0** with all checklist items green.
