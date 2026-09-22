# NL Public Ready — Path A cutover

Get `https://play.20062006.xyz` from “live Steam dogfood works” to **operator-signed public ready** (Phase 14 gate on the real VPS). Not marketing GA to strangers with Twitch floor 50 — that is Part F.

Live snapshot (probed **2026-09-22**):

| Check | Result |
|-------|--------|
| `/health` | `ok`, `publicMode:true`, `hardening:true`, `demoMode:false` |
| Identity | `mode:Live`, `steamConfigured:true`, `publicBaseUrl` correct |
| T2-lite | `ready:true`, `rimworld://play.20062006.xyz:25555` |
| Public GA settings | `enabled:true`, **`devMode:false`**, support `support@20062006.xyz` |
| Public GA status | `gaOpenSignup:true`, `allProgramsEnabled:true`, legal enabled |

Sessions 1–2 + live Steam join already logged: [PATH_A_SESSION_LOG.md](PATH_A_SESSION_LOG.md) · [NL_LIVE_ADMIT.md](NL_LIVE_ADMIT.md).

---

## Definition of “public ready”

| # | Gate | Status |
|---|------|--------|
| 1 | HTTPS play host + public HTTP/WS/fork host | **Done** |
| 2 | Live Steam ownership + RimWorld `294100` admit | **Done** |
| 3 | Public pages up (`/play`, `/download`, `/status`, `/ga-launch-checklist`, `/legal-center`, `/ga`) | Confirm in Part A |
| 4 | Fleet backup + operator GA signoff + validation **PASSED** | **You — Part B** (needs operator key) |
| 5 | Profile always `platformAppId=294100` (no hello-fork trap) | Deploy UI/code fix if not pulled — Part C |
| 6 | Mock Steam64 deny once | Optional Part D |
| 7 | Twitch OAuth + followers=50 | **Deferred** (social GA) — [NL_LIVE_ADMIT.md](NL_LIVE_ADMIT.md) Part 4 |
| 8 | Cities / new titles | **Frozen** |

**Public ready = 1–4 done.** 5–6 harden. 7–8 later.

---

## Part A — Public pages (2 min, no key)

From Windows PowerShell:

```powershell
$base = 'https://play.20062006.xyz'
@(
  '/health',
  '/api/v1/identity/settings',
  '/api/v1/t2-lite/status',
  '/api/v1/public-ga-launch/settings',
  '/api/v1/public-ga-launch/status',
  '/play.html',
  '/download.html',
  '/status.html',
  '/ga-launch-checklist.html',
  '/legal-center.html',
  '/ga.html',
  '/nl-client.html',
  '/public-ga-launch-ops.html'
) | ForEach-Object {
  $u = $base + $_
  try {
    $r = Invoke-WebRequest -Uri $u -UseBasicParsing -TimeoutSec 30
    "{0} {1}" -f $r.StatusCode, $_
  } catch {
    "FAIL $_"
  }
}
```

All should be **200**. Identity `mode` must stay **Live**.

Or one script:

```powershell
cd C:\Users\surrp\Downloads\!a\future\products\nl
powershell -File scripts/nl-public-ready-cutover.ps1 -BaseUrl https://play.20062006.xyz -PagesOnly
```

---

## Part B — Backup + signoff + validation (needs operator key)

Failure you hit (`LEGAL COMPLIANCE VALIDATION FAILED`) means the **scale / production_ready** gate is still open. Legal pages and GDPR smoke are fine; Phase 14 still requires a recorded **~100 mock-fork load test** so `live-production` → `production_ready` is true. Attesting flags alone cannot set that.

1. Get key (VPS, do not paste into chat):

```bash
grep '^NL_OPERATOR_KEY=' /opt/NexoraLive/docker/vps-production-fleet.env
```

2. Full cutover (pages + backup + signoff + **scale load test** + legal + GA):

```bat
cd /d C:\Users\surrp\Downloads\!a\future\products\nl
powershell -NoProfile -File scripts\nl-public-ready-cutover.ps1 -BaseUrl "https://play.20062006.xyz" -OperatorKey "YOUR_OPERATOR_KEY"
```

Takes several minutes. Spins ~100 `hello-fork` sessions and leaves them up so `production_ready` sticks. On a tight 8GB box you can try `-ConcurrentSessions 100` (default).

3. Wanted: **`PUBLIC READY (VPS)`**

If scale OOMs the VPS, stop leftover forks then retry lower concurrency only if the gate still accepts (needs ≥100 for production_ready):

```bash
# on VPS after a failed / heavy run
sudo docker ps -q --filter name=nl-fork | xargs -r sudo docker rm -f
```

UI alt after a successful load test: `https://play.20062006.xyz/public-ga-launch-ops.html`

---

## Part C — Deploy hello-fork / app-id fix (once)

If Save profile still leaves `platformAppId=hello-fork` when Game id is `rimworld`, pull the fix onto the VPS:

```bash
cd /opt/NexoraLive
sudo git pull --ff-only
sudo bash scripts/nl-vps-deploy.sh
```

Until then, every profile reset:

```javascript
await fetch('/api/v1/dogfood/setup', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json', ...window.NlAuth.authHeaders() },
  body: JSON.stringify({ gameId: 'rimworld' })
}).then(r => r.json()).then(r =>
  console.log(r.status.profile.gameId, r.status.profile.platformAppId))
```

Expect `rimworld 294100`. Then set streamer `surrplexie-7c0056`, join gate on, Save → Start.

Repo changes live under `products/nl` (MergeOperatorProfile + operator Load dogfood → rimworld). Push to `Surrplexie/NexoraLive` before `git pull` on the box if that remote is the VPS source of truth.

---

## Part D — Optional harden (10 min)

1. **Mock deny:** join with Platform user `76561198000000001` → ownership deny.  
2. **Live join again:** real Steam64 → Completed.  
3. **Native Together (optional):** paste `rimworld://play.20062006.xyz:25555`.  

Log in [PATH_A_SESSION_LOG.md](PATH_A_SESSION_LOG.md).

---

## Part E — Still not required for public ready

| Item | When |
|------|------|
| Twitch/Discord OAuth redirect URIs | Before raising followers |
| `NL_FLEET_MIN_TWITCH_FOLLOWERS=50` | After Twitch live social works |
| Apex Worker marketing | Separate from `play.` |
| Cities / Kenshi public | Frozen until live admit stays boring |

Keep `NL_FLEET_MIN_TWITCH_FOLLOWERS=0` until Part 4 of [NL_LIVE_ADMIT.md](NL_LIVE_ADMIT.md).

---

## Failures

| Symptom | Fix |
|---------|-----|
| `operator_signoff` fail | POST signoff with correct `X-NL-Operator-Key` |
| `recent_backup` fail | `POST /api/v1/launch-ops/backup/run` first |
| `legal_compliance_gate` fail | Run `nl-legal-compliance-validate.ps1` against the same BaseUrl |
| Identity `Mock` | `STEAM_WEB_API_KEY` missing in container |
| `hello-fork` deny | Part C — set `platformAppId=294100` |
| Validation wants announcement | Leave `launchAnnouncementReady` false unless you are announcing |

---

## After public ready

1. Mark checklist in [NL_PUBLIC_LINE.md](NL_PUBLIC_LINE.md)  
2. Daily log: “PUBLIC READY (VPS)” + date  
3. Do **not** open Cities  
4. Optional: Twitch OAuth when you want social floor  

See also: [NL_PUBLIC_GA_LAUNCH_RUNBOOK.md](NL_PUBLIC_GA_LAUNCH_RUNBOOK.md) · [NL_VPS_DEPLOY.md](NL_VPS_DEPLOY.md) · [NL_LIVE_ADMIT.md](NL_LIVE_ADMIT.md)
