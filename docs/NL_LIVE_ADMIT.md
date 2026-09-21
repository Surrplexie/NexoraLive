# NL Live Admit — full cutover (Path A)

Get from “mock Steam64 works” to **live Steam ownership + stable RimWorld admit** on `play.20062006.xyz`. Twitch follower floor is a separate gate at the end.

**Already proven (2026-09-21):** `sp-live-1` + real Steam64 `76561199353783794` → join `Completed` / Allow with Steam app **`294100`**. Session log: [PATH_A_SESSION_LOG.md](PATH_A_SESSION_LOG.md).

---

## Definition of done

| Gate | Pass |
|------|------|
| A. Public URLs | Manifest `httpBaseUrl` = `https://play.20062006.xyz`; fork = `rimworld://play.20062006.xyz:25555` |
| B. Live Steam | `GET /api/v1/identity/settings` → `mode: Live`, `steamConfigured: true` |
| C. Correct app id | Session profile `platformAppId` = **`294100`** (never `hello-fork`) |
| D. Live join | Real Steam64 owns RimWorld → `step: Completed` |
| E. Mock dead | Mock Steam64 `76561198000000001` → ownership **deny** |
| F. Twitch (optional GA) | OAuth linked + `NL_FLEET_MIN_TWITCH_FOLLOWERS=50` (only after E) |

A–E = **live admit fully** for Path A. F is public social GA.

---

## Part 1 — Box env (SSH)

```bash
ssh ubuntu@40.160.88.114   # or your user
cd /opt/NexoraLive
```

### 1.1 Fleet env

```bash
sudo nano docker/vps-production-fleet.env
```

Must have (no quotes):

```bash
NL_PUBLIC_BASE_URL=https://play.20062006.xyz
NL_PUBLIC_HTTP=https://play.20062006.xyz
NL_PUBLIC_WS=wss://play.20062006.xyz/nl/v1
NL_PUBLIC_HOST=play.20062006.xyz
NL_FORK_PUBLIC_CONNECT_HOST=play.20062006.xyz

STEAM_WEB_API_KEY=YOUR_REAL_KEY
NL_OWNERSHIP_MODE=live
NL_SOCIAL_MODE=live
NL_FLEET_MIN_TWITCH_FOLLOWERS=0
```

Leave Twitch client id/secret empty until Part 4. Keep followers at **0**.

### 1.2 Compose must not force mock

```bash
grep -n 'OWNERSHIP\|SOCIAL\|GA_ALLOW\|GA_REQUIRE_LIVE' docker/docker-compose.vps-production.yml
```

Under `session-host` → `environment:` you want:

```yaml
NL_OWNERSHIP_MODE: "live"
NL_SOCIAL_MODE: "live"
NL_GA_REQUIRE_LIVE_IDENTITY: "true"
NL_GA_ALLOW_MOCK_IDENTITY: "false"
```

### 1.3 Recreate

```bash
sudo docker compose -f docker/docker-compose.vps-production.yml --env-file docker/.env.vps \
  up -d --force-recreate session-host
```

### 1.4 Prove

```bash
curl -fsS https://play.20062006.xyz/api/v1/identity/settings
# mode Live, steamConfigured true

curl -fsS https://play.20062006.xyz/api/v1/t2-lite/status
# ready true, rimworld://play.20062006.xyz:25555
```

Optional redacted env check:

```bash
sudo docker inspect "$(sudo docker compose -f docker/docker-compose.vps-production.yml --env-file docker/.env.vps ps -q session-host)" \
  --format '{{range .Config.Env}}{{println .}}{{end}}' \
  | grep -E '^(NL_OWNERSHIP_MODE|NL_SOCIAL_MODE|NL_FLEET_MIN|STEAM_WEB_API_KEY|NL_PUBLIC_HTTP|NL_FORK_PUBLIC)=' \
  | sed 's/\(STEAM_WEB_API_KEY=\).*/\1SET/'
```

---

## Part 2 — Profile = RimWorld app `294100`

Stale `hello-fork` in `platformAppId` causes `Steam app hello-fork not in library` even when `gameId` is `rimworld`.

### 2.1 On operator page (preferred)

1. Open `https://play.20062006.xyz/operator.html` → paste operator key.
2. Set **Game id** to `rimworld` (field default after deploy).
3. Click **Load dogfood profile** (now posts `{ gameId: "rimworld" }` after the UI fix lands).
4. Or console:

```javascript
await fetch('/api/v1/dogfood/setup', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json', ...window.NlAuth.authHeaders() },
  body: JSON.stringify({ gameId: 'rimworld' })
}).then(r => r.json()).then(r =>
  console.log(r.status.profile.gameId, r.status.profile.platformAppId))
```

Expect: `rimworld 294100`.

5. Fix form after dogfood load:
   - Streamer = `surrplexie-7c0056`
   - Game id = `rimworld`
   - Join gate **on**, Fork orchestrator **on**
6. **Save profile** → **Start session**  
   Do not click Load dogfood without `rimworld` in the Game id field on an old image.

### 2.2 Deploy the app-id fix (once)

Repo fix (operator Save syncs Steam app id from game id; Load dogfood defaults to rimworld):

- `BusHostState.MergeOperatorProfile`
- `wwwroot/app.js` / `operator.html`
- `DogfoodSetup.ResolveSteamAppId`

On VPS after that lands on `main`:

```bash
cd /opt/NexoraLive
sudo git pull --ff-only
sudo bash scripts/nl-vps-deploy.sh
```

Until then, use the console `dogfood/setup` with `gameId: 'rimworld'` every time you reset the profile.

---

## Part 3 — Live admit test

Steam account must **own RimWorld** and have **Game details = Public**.

1. `https://play.20062006.xyz/identity-link.html` → Sign in with Steam → note Steam64.
2. Private window → `https://play.20062006.xyz/nl-client.html`
3. Player · refresh streamers · pick yours  
4. Player id `sp-live-1`  
5. Platform user = **real** Steam64 (not `76561198000000001`)  
6. At-own-risk checked → **Run join flow**

**Pass:** `success: true`, `step: Completed`, `forkConnectEndpoint: rimworld://play.20062006.xyz:25555`.

**Negative:** same flow with Platform user `76561198000000001` → ownership **deny**.

**Stop** on operator when done.

---

## Part 4 — Twitch live social (optional; before followers=50)

Without Twitch client id/secret, `NL_SOCIAL_MODE=live` falls back to **mock social**. That is OK for Steam-only live admit. Raising followers to 50 **without** Twitch will block fork create / hosting.

### 4.1 Twitch developer app

1. https://dev.twitch.tv/console/apps → Create  
2. OAuth Redirect URL:

```text
https://play.20062006.xyz/api/v1/social/oauth/twitch/callback
```

3. Copy Client ID + create Client Secret.

### 4.2 Env + recreate

```bash
sudo nano /opt/NexoraLive/docker/vps-production-fleet.env
```

```bash
TWITCH_CLIENT_ID=...
TWITCH_CLIENT_SECRET=...
NL_SOCIAL_MODE=live
NL_FLEET_MIN_TWITCH_FOLLOWERS=0
```

```bash
sudo docker compose -f docker/docker-compose.vps-production.yml --env-file docker/.env.vps \
  up -d --force-recreate session-host
```

### 4.3 Link + prove

1. Streamer: set Twitch channel in `/join-gate.html` if you enable require-follow.  
2. Player: `/social-link.html` → player id `sp-live-1` → Sign in with Twitch.  
3. Join again with Steam64 still filled.

When boring:

```bash
NL_FLEET_MIN_TWITCH_FOLLOWERS=50
```

Recreate session-host. Streamer must meet the floor to create forks.

---

## Part 5 — Log

Append to [PATH_A_SESSION_LOG.md](PATH_A_SESSION_LOG.md): live join id, app `294100`, mock deny if run, Twitch linked Y/N.

---

## Failures

| Symptom | Fix |
|---------|-----|
| `Steam app hello-fork not in library` | `dogfood/setup` with `rimworld`; confirm `platformAppId=294100` |
| `Steam app 294100 not in library` | Own RimWorld; Game details Public |
| Identity `mode: Mock` | Empty `STEAM_WEB_API_KEY` in container |
| OpenID wrong host | `NL_PUBLIC_BASE_URL=https://play.20062006.xyz` |
| Followers / social deny | Keep `NL_FLEET_MIN_TWITCH_FOLLOWERS=0` until Twitch OAuth works |
| Compose still mock | `environment:` overrides `env_file` — set live in compose |

---

## Still frozen

Cities / new titles / Away — until A–E stay boring. Native Together to `:25555` is optional and does not gate live admit.

See also: [NL_VPS_DEPLOY.md](NL_VPS_DEPLOY.md) · [NL_IDEAL_DEMO_GUIDE.md](NL_IDEAL_DEMO_GUIDE.md) · [NL_SOCIAL_GATE.md](NL_SOCIAL_GATE.md)
