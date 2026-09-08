# Ideal NL demo — operator VPS + streamer + demo fan

This is the **one loop** that matters: NL on a real URL, a streamer session on an NL-hosted world, a fan admitted through NL, native game connect (not loopback), then the session dies.

RimWorld is the **example world** (Steam 294100, port 25555). NL is still one server. Do not treat this as a RimWorld product.

**Related:** [NL_PUBLIC_LINE.md](NL_PUBLIC_LINE.md) · [NL_VPS_DEPLOY.md](NL_VPS_DEPLOY.md) · [NL_T2_LITE.md](NL_T2_LITE.md) · [NL_CLIENT.md](NL_CLIENT.md)

---

## What “done” looks like

| # | Check |
|---|--------|
| 1 | `https://play.YOURDOMAIN/health` is OK (TLS) |
| 2 | `GET /api/v1/t2-lite/status` → `ready: true`, example is `rimworld://play.YOURDOMAIN:25555` (not `127.0.0.1`) |
| 3 | Streamer is live on NL (session running, Docker fork up) |
| 4 | Fan opens **NL Client**, finds **that streamer**, join **succeeds** |
| 5 | Fan copies `rimworld://play.YOURDOMAIN:25555` (clipboard / join result) |
| 6 | Optional native: RimWorld + MP (Together) connects to `play.YOURDOMAIN:25555` |
| 7 | Dashboard / operator log shows a **Block or kick** from `.nle` (caps or grief) |
| 8 | Streamer **Stop** → fork gone |

Two people is enough: you as **operator + streamer**, a friend (or second browser) as **fan**. One PC can do both browsers; native join needs a second machine **or** the same PC with RimWorld installed.

---

## Roles

| Role | Who | Job |
|-----|-----|-----|
| **Operator** | You | VPS, DNS, secrets, firewall, start stack |
| **Streamer** | You (or a friend) | Sign up, go live, start session, `.nle` |
| **Demo fan** | Second browser / friend | Find streamer on NL, admit, connect native client |

---

## 0. Before you buy anything (local rehearsal)

On the Windows machine with Docker Desktop, from the NL repo:

```powershell
cd C:\Users\surrp\Downloads\!a\future\products\nl
powershell -File scripts/nl-public-line-ready.ps1
```

Expected: **`PUBLIC LINE READY (local)`**. That proves images + RimWorld dogfood **on this PC**. It does **not** replace the VPS.

---

# Part 1 — Operator: public host

## 1.1 Hardware and DNS

| Need | Spec |
|------|--------|
| VPS | Ubuntu 22.04+, **4 GB RAM** (8 GB better), public IPv4, SSH |
| Domain | You control DNS |
| Email | Let's Encrypt + a support contact |
| Steam Web API key | [steamcommunity.com/dev/apikey](https://steamcommunity.com/dev/apikey) — **required for live ownership**; skip only if you use the first-session mock override below |

DNS **A records**, all → VPS IPv4:

```
play.yourdomain.com
relay-us-east.yourdomain.com
relay-us-west.yourdomain.com
relay-eu-west.yourdomain.com
```

Wait 5–30 min. From Windows:

```powershell
cd C:\Users\surrp\Downloads\!a\future\products\nl
powershell -File scripts/nl-vps-dns-check.ps1 -Domain play.yourdomain.com -ExpectedIp YOUR_VPS_IP
```

## 1.2 Put **this** tree on the VPS

GitHub `Surrplexie/NexoraLive` may be **behind** this folder. Deploy the tree you actually built:

**Option A — push, then clone**

```bash
# on VPS
sudo apt update && sudo apt install -y git curl
git clone https://github.com/Surrplexie/NexoraLive.git /opt/NexoraLive
cd /opt/NexoraLive
```

**Option B — copy this repo over SSH** (if GitHub is stale)

From Windows (adjust user/host):

```powershell
scp -r C:\Users\surrp\Downloads\!a\future\products\nl\* USER@YOUR_VPS_IP:/opt/NexoraLive/
```

Then SSH in: `cd /opt/NexoraLive`.

## 1.3 Bootstrap

```bash
cd /opt/NexoraLive
bash scripts/nl-vps-bootstrap.sh
```

Prompts: play domain (`play.yourdomain.com`), base domain (`yourdomain.com`), ACME email, support email, Steam key (paste or Enter to skip).

**Save the operator key** printed at the end. You cannot recover it from the UI.

Open the RimWorld game port (bootstrap opens 80/443/3478; **25555 is required** for T2-lite):

```bash
sudo ufw allow 25555/tcp
sudo ufw status
```

If Docker was just installed and `docker` permission fails: log out/in (or `newgrp docker`) and run:

```bash
bash scripts/nl-vps-deploy.sh
```

## 1.4 First two-person session (overrides)

Stock VPS compose uses **live** Steam, **live** social, and **50 Twitch followers**. That will **fail** the first demo unless OAuth + a live stream + Steam key are already wired.

For the **first** streamer + fan pass, on the VPS edit `docker/vps-production-fleet.env` and set:

```bash
NL_FLEET_MIN_TWITCH_FOLLOWERS=0
NL_OWNERSHIP_MODE=mock
NL_SOCIAL_MODE=mock
```

In `docker/docker-compose.vps-production.yml` the session-host service still has `NL_GA_REQUIRE_LIVE_IDENTITY=true`. For this first pass, either:

- keep compose as-is and **do not** run the full public-GA validation gate, or
- temporarily set on the **session-host** environment (compose `environment:`):  
  `NL_GA_REQUIRE_LIVE_IDENTITY: "false"` and `NL_GA_ALLOW_MOCK_IDENTITY: "true"`  
  then `bash scripts/nl-vps-deploy.sh` again.

Mock Steam64 for the bundled ownership matrix: **`76561198000000001`** (owns RimWorld / 294100 in `samples/identity/mock-ownership.json`).

When this loop is boring, put **live** Steam key back, `NL_OWNERSHIP_MODE=live`, `NL_SOCIAL_MODE=live`, restore follower minimum, add Twitch/Discord redirect URIs ([NL_VPS_DEPLOY.md](NL_VPS_DEPLOY.md) OAuth table).

## 1.5 Prove the host

On the VPS:

```bash
curl -fsS https://play.yourdomain.com/health
curl -fsS https://play.yourdomain.com/api/v1/t2-lite/status
```

`t2-lite` must show `ready: true` and a **public** `rimworld://play.…:25555`.

From Windows:

```powershell
cd C:\Users\surrp\Downloads\!a\future\products\nl
powershell -File scripts/nl-vps-validate.ps1 `
  -BaseUrl https://play.yourdomain.com `
  -OperatorKey YOUR_OPERATOR_KEY
```

Expected: **`VPS PRODUCTION VALIDATION PASSED`**.

Optional (images already on VPS):

```powershell
powershell -File scripts/nl-t2-lite-validate.ps1
```

(That script checks the **local** compose file; on a live box prefer the `curl` to `/api/v1/t2-lite/status`.)

---

# Part 2 — Streamer + demo fan

Replace `play.yourdomain.com` with your domain.

## 2.1 Streamer — account

1. Open `https://play.yourdomain.com/play.html`
2. Sign up at `https://play.yourdomain.com/ga.html` (accept terms)
3. Write down **Streamer ID** from the confirmation
4. Optional: `https://play.yourdomain.com/identity-link.html` (Steam) — skip if using mock ownership
5. Optional: `https://play.yourdomain.com/login.html` — unified NL account

## 2.2 Streamer — start the NL world

1. Open `https://play.yourdomain.com/operator.html`
2. Paste **operator key** → Save key
3. **Load dogfood profile** (or set by hand):
   - Streamer = your Streamer ID
   - **Game id** = `rimworld`
   - Config = `rimworld.nle` / path that resolves to `samples/configs/rimworld.nle`
   - **Fork orchestrator** = on
   - **Join gate** = on (admit before connect)
4. **Save profile** → **Start session**
5. Confirm fork on `https://play.yourdomain.com/fork-orchestrator.html` (or operator log)

**Fast operator check (same outcome, no clicking):** from Windows, with the session host already up:

```powershell
powershell -File scripts/nl-dogfood-flow.ps1 `
  -BaseUrl https://play.yourdomain.com `
  -GameId rimworld `
  -ExpectProvisioner docker `
  -SkipImageBuild `
  -OperatorKey YOUR_OPERATOR_KEY `
  -VerifyRuleEvents
```

That script **starts and then tears down**. Use it to prove the stack; for a **held** session for a human fan, use **operator Start** and do **not** run teardown until the fan has joined.

Only **one** RimWorld fork per VPS (host port **25555**).

## 2.3 Demo fan — admit on NL (not in Steam’s server list)

Steam does **not** show an “NL” tab yet. The fan:

1. Has **RimWorld installed** on their PC (licensed). MP mod (e.g. Together) if they will actually enter the colony.
2. Opens `https://play.yourdomain.com/nl-client.html` (or `/play.html` → NL Client)
3. Mode = **Player**
4. **Refresh** live streamers → click **your streamer**
5. Player id = something unique (`sp-fan-1`)
6. Platform user = `76561198000000001` if mock; else their Steam64 after identity link
7. Check **At-own-risk acknowledged**
8. **Run join flow**

**Pass:** join result includes `forkConnectEndpoint` / native clipboard:

```text
rimworld://play.yourdomain.com:25555
```

If you see `127.0.0.1`, Part 1 T2-lite is not ready — stop and fix env/firewall.

## 2.4 Demo fan — enter the game (native)

1. Launch **RimWorld** + community MP (Together-style)
2. Connect to host `play.yourdomain.com` port **25555**  
   (or paste the `rimworld://` string if the mod accepts it)
3. They should land on the **NL-hosted** session, not a random public server

NL Client is the **door**. The game is still RimWorld.

## 2.5 Prove enforcement

On `https://play.yourdomain.com/` (watch) or operator log:

- Caps in colony chat (per `samples/configs/rimworld.nle`) → **warn + block**
- High-value `buildingDestroy` → **block / kick**

The Docker sidecar **already emits** that vocabulary in its demo loop even without Together. Native Together + Harmony is stronger “in-world” proof; the **minimum** for this guide is: join succeeded + public URI + a Block/kick in the NL log.

## 2.6 End the session

Streamer (or operator): **Stop** on `/operator.html`. Fork container should disappear. Fan can no longer connect to that world.

---

## Troubleshooting

| Symptom | What to do |
|---------|------------|
| TLS / health fail | DNS not pointing yet; wait. Check `docker compose logs caddy` |
| `t2-lite` not ready / loopback URI | `NL_FORK_PUBLIC_CONNECT_HOST=play.yourdomain.com` in fleet env; redeploy |
| Join: SessionOffline | Start session first (2.2) |
| Join: ownership denied | Mock Steam64 `76561198000000001`, or live Steam key + owned 294100 |
| Join: social / followers | First-session overrides (1.4) or go live on Twitch with OAuth |
| No connect on 25555 | `ufw allow 25555/tcp`; cloud security group; one fork only |
| Together won’t handshake | Sidecar banner is `NL-RIMWORLD/1` — real Together needs licensed `/game` on the fork. First demo can stop at URI + NL log |
| 401 on operator | Wrong operator key |

---

## After this demo is boring

1. Restore **live** Steam + social; Twitch/Discord callbacks  
2. Fan UX: **only live streamers** (hide game dropdowns)  
3. Full T2: unique ports / TURN (more than one fork per IP)  
4. Publisher “NL” in Steam/Xbox lists — not a git checkbox  

Do **not** start Cities: Skylines or another `*-mod` product folder until this loop has been run once with a real fan.
