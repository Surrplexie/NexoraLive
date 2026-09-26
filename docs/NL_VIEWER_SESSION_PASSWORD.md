# Viewer session password (NL door)

Together’s `Configs/PasswordConfig.json` is **not** the SP/viewer gate. On 26.8.31.1 it
mismatches the in-game prompt and is the **player account / server** layer. NexoraLive
viewers must be gated at **admit**.

## What each password is

| Layer | Who | File / UI |
|-------|-----|-----------|
| **NL session password** | SPs / viewers | Join gate → `sessionPassword` → NL Client field |
| Together `PasswordConfig` | RimWorld Together handshake | Leave `""` on Path A |
| Together user account | Your `ByteSizedKai` login | Created on first join — not NL |

Streamer Direct Connect (you) can skip NL Client. Viewers **must** pass NL Client (ownership + session password) before NL hands them `rimworld://play…:25555`.

## Operator (after this code is on the VPS)

1. `cd /opt/NexoraLive && sudo git pull --ff-only && sudo bash scripts/nl-vps-deploy.sh`
2. `https://play.20062006.xyz/join-gate.html` → operator key
3. **Viewer session password** — set a secret, **Save requirements**
4. Operator: join gate **on**. Do **not** Start a rimworld Docker fork while `RTServer` owns `:25555`.
5. Together stays `Password: ""` and `EnableServerBrowser: false` in `ServerConfig.json`

## Viewer

1. `https://play.20062006.xyz/nl-client.html`
2. Steam64 + **Session password** + at-own-risk → **Run join flow**
3. Wrong/missing → admit deny `Session password required.`
4. Success → Together Direct Connect `play.20062006.xyz` / `25555` (no Together password)

CLI:

```powershell
$env:NL_CLIENT_SESSION_URL = "https://play.20062006.xyz"
dotnet run --project src/NL.Client -- join `
  --player sp-fan-1 --streamer surrplexie-7c0056 `
  --platform-user 76561199353783794 `
  --session-password THE_SECRET --ack
```

## Note

Anyone who already knows host:port can still TCP to Together. NL password blocks the **published** join path. Optional extra: enable `ufw` and allow `:25555` only from known IPs.
