# NL Kenshi community-MP mod (Phase T1)

Paper-plugin equivalent for Kenshi. CI compiles `NL.Kenshi.Bridge` without Kenshi
DLLs. Operators with a **licensed** Kenshi install + a community multiplayer stack:

1. `dotnet build integrations/kenshi/mod/NL.Kenshi.Bridge.csproj -c Release`
2. Copy `About/` + `bin/Release/net8.0/NL.Kenshi.Bridge.dll` into the Kenshi mods folder
   (retarget against Kenshi assemblies when compiling on the operator machine).
3. Point `About/config.json` at the NL session bus (`NL_FORK_WS_URL`).
4. Host community MP on port **23386**.

NL does **not** ship Kenshi binaries. Docker `nl-fork-kenshi` runs the C# sidecar unless
`/game` is mounted (see `docker/fork-kenshi/entrypoint.sh`).
