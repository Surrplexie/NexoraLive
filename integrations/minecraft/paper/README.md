# NL Paper plugin — fork runtime (Phase P)

Full **propose-then-commit** integration for Minecraft Java on NL-hosted Paper servers.

## Build

```powershell
mvn -f integrations/minecraft/paper/pom.xml package
```

Output: `integrations/minecraft/paper/target/nl-bridge-1.0.0.jar`

## Docker (Paper + plugin)

```powershell
docker build -f docker/fork-minecraft-paper/Dockerfile -t nl-fork-minecraft-paper:latest .
```

Set `NL_FORK_WS_URL` and `NL_FORK_STATUS=/data/fork-status.json` when running under the fork orchestrator.

## Manual install

Copy the JAR to your Paper server's `plugins/` folder. Edit `plugins/NLBridge/config.yml`:

```yaml
websocketUrl: "ws://127.0.0.1:27021/nl/v1?token=YOUR_BUS_TOKEN"
admitUrl: "http://127.0.0.1:27020/api/v1/session/admit"
gameId: "minecraft"
enforceJoinGate: true
```

## Events

| Bukkit | NL event | Block behavior |
|--------|----------|----------------|
| Join (+ admit API) | `playerJoin` | kick |
| Quit | `playerLeave` | — |
| Chat | `playerChat` | cancel message |
| PvP damage | `entityDamage` | cancel damage / kick |

See [docs/NL_FORK_GAME_IMAGES.md](../../../docs/NL_FORK_GAME_IMAGES.md).
