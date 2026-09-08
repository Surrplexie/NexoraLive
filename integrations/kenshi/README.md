# Kenshi adapter (Phase T1)

Full **mod-backed** title via the T0 contract: in-process runtime, community-MP hook catalog,
`kenshi://` connect listener, and Docker sidecar (optional licensed game tree). Kenshi has
**no official dedicated server**.

| Item | Value |
|------|-------|
| Steam app | **233860** |
| Tier | AtOwnRisk |
| Connect | `kenshi://host:23386` |
| Image | `nl-fork-kenshi:latest` |
| Runtime | `KenshiForkRuntime` (`--game kenshi`) |
| `.nle` | `samples/configs/kenshi.nle` |
| Sidecar | `sidecar/nl_sidecar.py` |
| Native plugin | `mod/` (community MP NL Bridge) |

## Events

| Event | Meaning |
|-------|---------|
| `sessionStart` / `sessionEnd` | Outpost session lifecycle |
| `playerJoin` / `playerLeave` | Character / player connect |
| `playerChat` | Squad chat (caps block sample) |
| `entityDamage` | Combat hits |
| `knockdown` / `respawn` | KO + get-up |
| `move` | Character move |
| `squadOrder` | Squad follow / attack order |
| `buildingDestroy` | Grief — high-value outpost deconstruct |
| `steal` | Theft of valuables |
| `raid` | Mass raid / faction attack |
| `trade` | High-value trade |
| `itemDestroy` | Strip / destroy valuables |

## Operator

```powershell
powershell -File scripts/nl-game-adapter-validate.ps1 -GameId kenshi
dotnet test tests/NL.Fork.Core.Tests --filter "FullyQualifiedName~Kenshi"
powershell -File scripts/nl-dogfood-flow-kenshi.ps1 -ExpectProvisioner mock
# with session host + docker orchestrator:
powershell -File scripts/nl-dogfood-flow-kenshi.ps1 -ExpectProvisioner docker -VerifyRuleEvents
powershell -File scripts/nl-live-social-dogfood-flow.ps1 -GameId kenshi -SocialMode mock
```

See [docs/NL_KENSHI.md](../../docs/NL_KENSHI.md) and [mod/README.md](mod/README.md).
