# BeamNG.drive adapter

- **Host Lua bridge:** `beamng-mod/NL_BeamNGBridge/`
- **Fork sidecar image:** `nl-fork-beamng:latest`
- **Docs:** [docs/BEAMNG.md](../../docs/BEAMNG.md)

Connect scheme: `beamng-sidecar://` (pairs with host game + UDP kick).

```powershell
powershell -File scripts/nl-game-adapter-validate.ps1 -GameId beamng
```
