# RimWorld live dogfood — steps 1–2 (`play.20062006.xyz`)

Operator + player path for **admit (1)** then **native RimWorld connect (2)**.  
Host is PUBLIC READY. Use **live Steam** (your real Steam64), not mock.

**Probed 2026-09-22:** health OK · t2-lite ready · `sessionRunning:false` · profile may still say `hello-fork` / `load-topup-*` from load tests — **reset in Part 0**.

---

## FAQ — operator log spam `sessionStart` / `sessionEnd`

If the log repeats every ~8s:

```text
[NL-Fork] sessionStart -> Allow
[NL-Fork] sessionEnd -> Allow
```

That is the **sidecar demo loop** (`--loop`), not a crash. NL session stays `Running`; `rimworld://play…:25555` still works. **Ignore it for Step 2 admit**, or Stop → redeploy session-host/fork after the `--serve` fix lands, then Start again for a quiet log.

---

## What “do it” means

| Who | What |
|-----|------|
| You | Operator Start, NL Client join, launch RimWorld + Together |
| Agent | Can’t paste your operator key or drive Steam/RimWorld on your PC |

---

## Part 0 — Reset profile (required if load-test junk remains)

Current risk: manifest can show `gameId: hello-fork` and streamer `load-topup-*`. Fix before Start.

1. Open `https://play.20062006.xyz/operator.html`
2. Paste **operator key** → Save
3. F12 → Console → run:

```javascript
const r = await fetch('/api/v1/dogfood/setup', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json', ...window.NlAuth.authHeaders() },
  body: JSON.stringify({ gameId: 'rimworld' })
}).then(x => x.json());
console.log(r.status.profile.gameId, r.status.profile.platformAppId);
```

Expect: `rimworld` and `294100`.

4. On the operator form, set again (dogfood resets streamer):
   - **Streamer** = `surrplexie-7c0056` (or your GA streamer id)
   - **Game id** = `rimworld`
   - **Join gate** = on
   - **Fork orchestrator** = on
   - Config should resolve to `rimworld.nle`
5. **Save profile** (do **not** click Load dogfood again after this)

---

## Step 1 — Operator: start the NL RimWorld world

1. Still on `https://play.20062006.xyz/operator.html`
2. Confirm profile from Part 0
3. Click **Start session**
4. Wait ~15–30s for Docker fork
5. Check:
   - Operator / fork UI shows a fork session
   - Manifest fork connect = `rimworld://play.20062006.xyz:25555`

Quick prove (PowerShell / browser):

```text
https://play.20062006.xyz/api/v1/session/manifest
```

Want: `sessionRunning: true`, `gameId: "rimworld"`, `forkConnectEndpoint: "rimworld://play.20062006.xyz:25555"`.

**Only one RimWorld fork per VPS** (port 25555). Leave it running for Step 2.

---

## Step 2 — Player: admit (NL door), then enter the game

### 2A — Admit in the browser

Use a **private window** (avoids stale mock ids).

1. Own RimWorld on Steam (app **294100**). Game details = **Public**.
2. Optional first: `https://play.20062006.xyz/identity-link.html` → Sign in with Steam → copy Steam64.
3. Open `https://play.20062006.xyz/nl-client.html`
4. Mode = **Player**
5. **Refresh** live streamers → click `surrplexie-7c0056` (must show LIVE)
6. Fill:
   - **Player id** = `sp-rw-1` (new id is fine)
   - **NL account id** = leave blank unless linker filled it
   - **Platform user (Steam64)** = your **real** Steam64 (e.g. `76561199353783794`) — not the mock id
7. Check **At-own-risk acknowledged**
8. **Run join flow**

**Pass:**

- `success: true`, `step: "Completed"`, admit Allow  
- Clipboard / hint: `rimworld://play.20062006.xyz:25555`

**Fail cheat-sheet:**

| Message | Fix |
|---------|-----|
| SessionOffline | Step 1 Start first |
| hello-fork / wrong app | Part 0 again → `294100` |
| Steam app 294100 not in library | Own RimWorld; profile Public |
| empty platformUserId | Steam64 in **Platform user**, not NL account id |

### 2B — Enter real RimWorld (native)

1. Install / enable a multiplayer mod (**RimWorld Together** or equivalent).
2. Launch **RimWorld** (Steam).
3. In the MP mod, connect:
   - **Host:** `play.20062006.xyz`
   - **Port:** `25555`  
   or paste `rimworld://play.20062006.xyz:25555` if the mod accepts that URI.
4. You should join the **NL-hosted** session (not Steam’s public browser list).

**If Together won’t handshake:** Step 2A still counts as NL admit dogfood. Full dedicated Together needs **`RTServer`** (Together release zip) on `:25555` — not the NL sidecar banner. See [NL_RIMWORLD_TOGETHER_VPS.md](NL_RIMWORLD_TOGETHER_VPS.md). **Session 4 (2026-09-25):** native Together pass logged in [PATH_A_SESSION_LOG.md](PATH_A_SESSION_LOG.md).

### 2C — Stop

On operator → **Stop**. Fork dies; port 25555 frees.

---

## Checklist

- [ ] Part 0: `gameId=rimworld`, `platformAppId=294100`, streamer = yours  
- [ ] Step 1: Start → `sessionRunning:true` + public `rimworld://…:25555`  
- [ ] Step 2A: join Completed with real Steam64  
- [ ] Step 2B: (optional) RimWorld + Together connected  
- [ ] Stop  

Log pass in [PATH_A_SESSION_LOG.md](PATH_A_SESSION_LOG.md) when done.
