# Install NL RimWorld Harmony bridge (player PC)

NL does not compile against RimWorld `Assembly-CSharp` in CI. This script copies the
**protocol host + About/** into `Mods\NLBridge`. Live Verse patches need a later
net472+Harmony build on a machine that owns RimWorld.

```powershell
powershell -File scripts/install-nl-rimworld-bridge.ps1
```

Then edit:

`C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\NLBridge\About\config.json`

Set `websocketUrl` to the live manifest `bridgeConnectUrl` (`wss://play.20062006.xyz/nl/v1?token=…`).
Enable **NexoraLive NL Bridge** after Harmony + Together. Keep NL session Running with
**fork orchestrator off** while Together `RTServer` holds port 25555.

See [docs/NL_RIMWORLD.md](../docs/NL_RIMWORLD.md).
