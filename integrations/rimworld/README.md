# RimWorld adapter (Phase T1)

Full **mod-backed** title via the T0 contract: in-process runtime, Harmony/Together bridge,
`rimworld://` connect listener, and Docker sidecar (optional licensed dedicated).

| Item | Value |
|------|-------|
| Steam app | **294100** |
| Tier | AtOwnRisk |
| Connect | `rimworld://host:25555` |
| Image | `nl-fork-rimworld:latest` |
| Runtime | `RimWorldForkRuntime` (`--game rimworld`) |
| `.nle` | `samples/configs/rimworld.nle` |
| Sidecar | `sidecar/nl_sidecar.py` |
| Native plugin | `mod/` (Harmony / Together NL Bridge) |

## Events

| Event | Meaning |
|-------|---------|
| `sessionStart` / `sessionEnd` | Colony session lifecycle |
| `playerJoin` / `playerLeave` | Colonist / player connect |
| `playerChat` | Colony chat (caps block sample) |
| `entityDamage` | Pawn combat |
| `colonistDown` / `respawn` | Downed + rescue |
| `move` | Colonist cell move |
| `colonistDraft` | Draft / undraft |
| `buildingDestroy` | Grief — high-value deconstruct |
| `zoneEdit` | Grief — mass stockpile/zone edits |
| `animalRelease` | Grief — dumping animals |
| `trade` | High-value trade |
| `itemDestroy` | Strip / destroy valuables |

## Operator

```powershell
powershell -File scripts/nl-game-adapter-validate.ps1 -GameId rimworld
dotnet test tests/NL.Fork.Core.Tests --filter "FullyQualifiedName~RimWorld"
powershell -File scripts/nl-dogfood-flow-rimworld.ps1 -ExpectProvisioner mock
# with session host + docker orchestrator:
powershell -File scripts/nl-dogfood-flow-rimworld.ps1 -ExpectProvisioner docker -VerifyRuleEvents
powershell -File scripts/nl-live-social-dogfood-flow.ps1 -GameId rimworld -SocialMode mock
```

See [docs/NL_RIMWORLD.md](../../docs/NL_RIMWORLD.md) and [mod/README.md](mod/README.md).
