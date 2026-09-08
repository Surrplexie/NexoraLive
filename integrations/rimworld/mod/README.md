# NL RimWorld Harmony / Together mod (Phase T1)

Paper-plugin equivalent for RimWorld. CI compiles `NL.RimWorld.Bridge` without RimWorld
DLLs. Operators with a **licensed** RimWorld 1.5 + Together dedicated copy:

1. `dotnet build integrations/rimworld/mod/NL.RimWorld.Bridge.csproj -c Release`
2. Copy `About/` + `bin/Release/net8.0/NL.RimWorld.Bridge.dll` into `RimWorld/Mods/NLBridge/`
   (retarget to net472 + Harmony when compiling against `Assembly-CSharp.dll`).
3. Point `About/config.json` at the NL session bus (`NL_FORK_WS_URL`).
4. Start Together dedicated on port **25555**.

NL does **not** ship RimWorld binaries. Docker `nl-fork-rimworld` runs the C# sidecar unless
`/game` is mounted (see `docker/fork-rimworld/entrypoint.sh`).
