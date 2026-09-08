# Hello Fork adapter (Phase P / T0)

Reference in-process fork runtime. See `src/NL.Fork.Core/HelloForkRuntime.cs` and
`src/NL.Fork.Runtime/`.

Connect scheme: `hello://` (sidecar / embedded; no native client port).

```powershell
powershell -File scripts/nl-game-adapter-validate.ps1 -GameId hello-fork
powershell -File scripts/nl-fork-smoke.ps1
```
