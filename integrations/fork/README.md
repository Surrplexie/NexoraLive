# Fork runtime integration (Phase P)

Implement `IForkRuntime` from `NL.Fork.Core` inside your game server process.

## Contract

1. **Propose** — player action → validate with `ForkStateValidator` → build `SessionEvent`
2. **Decide** — send to NL session bus (`ForkNlBridgeClient`) or embedded `RuleEngine`
3. **Commit** — only if Allow; on Block run `ForkActionApplicator` (kick, strip weapon, …)

## Reference implementations

| Path | Use |
|------|-----|
| `src/NL.Fork.Core/HelloForkRuntime.cs` | Minimal in-process sim |
| `src/NL.Fork.Runtime/` | WebSocket client executable |
| `integrations/python/nl_bridge.py` | Legacy external bridge (Phase 3) |

## Migrating Minecraft / BeamNG

**Minecraft (Paper/Purpur):** See [`integrations/minecraft/paper/`](../../integrations/minecraft/paper/) — full WebSocket fork plugin + `nl-fork-minecraft-paper` Docker image.

**Minecraft (sidecar):** `nl-fork-minecraft` C# runtime with `minecraft.nle` event vocabulary for orchestrator smoke tests.

**BeamNG:** `nl-fork-beamng` sidecar + existing `NL_BeamNGBridge` Lua mod on the host. See [docs/NL_FORK_GAME_IMAGES.md](../../docs/NL_FORK_GAME_IMAGES.md).

Today's log/UDP bridges **keep working** — fork runtime is the NL-hosted path.
