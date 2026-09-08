# Example Game adapter (Phase T0 reference)

Filled copy of `integrations/_template/` used to prove the onboarding checklist and
`scripts/nl-game-adapter-validate.ps1`. Replace this with a real title via:

```powershell
powershell -File scripts/nl-game-adapter-scaffold.ps1 -GameId rimworld -DisplayName "RimWorld"
```

| Item | Value |
|------|-------|
| Connect | `example://host:27000` |
| Image | `nl-fork-example-game:latest` |
| `.nle` | `samples/configs/example-game.nle` |
| Sidecar | `sidecar/nl_sidecar.py` |
