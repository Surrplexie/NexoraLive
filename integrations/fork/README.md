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

**Minecraft (Paper/Purpur):** Plugin hooks `EntityDamageByEntityEvent` → `TryShootAsync` pattern;
connect plugin to `ws://session-host:27021/nl/v1?token=…` instead of RCON-only.

**BeamNG:** Move from file-tail NDJSON to in-mod call into a sidecar running `NL.Fork.Runtime`,
or embed C# fork host via future native bridge.

Today's log/UDP bridges **keep working** — fork runtime is the NL-hosted path.
