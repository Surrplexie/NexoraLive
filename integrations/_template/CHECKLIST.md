# Adapter onboarding checklist (Phase T0)

Copy to `integrations/<gameId>/CHECKLIST.md` and tick as you go.

- [ ] Legal tier chosen (`AtOwnRisk` / `Platform` / `Official`)
- [ ] `adapter.manifest.json` filled (no `{{PLACEHOLDERS}}`)
- [ ] Catalog row in `samples/fork/catalog.json`
- [ ] `docker/fork-<gameId>/Dockerfile` exists
- [ ] Image key added to `scripts/build-fork-images.ps1`
- [ ] `.nle` template at `samples/configs/<gameId>.nle`
- [ ] Sidecar / plugin emits required events
- [ ] Sidecar handles `warn` + `kick` actions
- [ ] Connect scheme documented (`<scheme>://host:port`)
- [ ] Health / readiness probe configured
- [ ] Dogfood script works (`scripts/nl-dogfood-flow-<gameId>.ps1` or shared flow with `-GameId`)
- [ ] `powershell -File scripts/nl-game-adapter-validate.ps1 -GameId <gameId>` passes
- [ ] Steam / platform app IDs in ownership matrix (when live identity is required)
- [ ] Section added to `docs/NL_FORK_GAME_IMAGES.md` (optional for experimental titles)
