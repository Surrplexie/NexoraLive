# NL RimWorld Harmony / Together mod (Phase T1)

Paper-plugin equivalent for RimWorld. CI compiles `NL.RimWorld.Bridge` without RimWorld
DLLs. Operators with a **licensed** RimWorld 1.5 + Together:

```powershell
powershell -File scripts/install-nl-rimworld-bridge.ps1
```

Full notes: [INSTALL.md](INSTALL.md) and [docs/NL_RIMWORLD.md](../../docs/NL_RIMWORLD.md).

1. `dotnet build integrations/rimworld/mod/NL.RimWorld.Bridge.csproj -c Release`
2. Copy `About/` + `bin/Release/net8.0/NL.RimWorld.Bridge.dll` into `RimWorld/Mods/NLBridge/`
   (retarget to net472 + Harmony when compiling against `Assembly-CSharp.dll` for live cancel).
3. Point `About/config.json` at the NL session bus (`bridgeConnectUrl` from the live manifest).
4. Together dedicated (`RTServer`) on port **25555**; NL session Running with **fork orchestrator off**.


NL does **not** ship RimWorld binaries. Docker `nl-fork-rimworld` runs the C# sidecar unless
`/game` is mounted (see `docker/fork-rimworld/entrypoint.sh`).
