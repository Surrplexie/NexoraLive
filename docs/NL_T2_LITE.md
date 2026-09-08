# NL T2-lite — public RimWorld connect on VPS

**Audience:** operators and integrators. **Not** full T2 (TURN/game relay, multi-region join dogfood, dynamic host ports).

**Related:** [NL_GAME_EXPANSION_PLAN.md](NL_GAME_EXPANSION_PLAN.md) · [NL_VPS_DEPLOY.md](NL_VPS_DEPLOY.md) · [NL_RIMWORLD.md](NL_RIMWORLD.md) · [NL_CLIENT.md](NL_CLIENT.md)

---

## What T2-lite is

Viewers join a **RimWorld** fork from outside localhost. After admit, the NL Client manifest’s `forkConnectEndpoint` is a **public native URI**:

```
rimworld://play.yourdomain.com:25555
```

Paste that into Together / RimWorld MP (or copy from `/nl-client.html` after join). The control plane stays on HTTPS via Caddy (`play.…`); **game TCP is not** wrapped in the fleet `wss://relay-…` template.

| In scope | Out of scope (full T2) |
|----------|-------------------------|
| Public hostname in native connect URI | TURN/STUN for game packets |
| No `127.0.0.1` in production RimWorld manifests | Streamer region A / viewer region B proof |
| Firewall **25555/tcp** on the VPS | Many concurrent RimWorld forks on one IP (same port) |
| NL Client clipboard hint | Desktop app (T3) |

**Constraint:** Docker maps `-p 25555:25555`. **One** RimWorld fork at a time on that host port.

---

## Operator setup

1. `NL_FORK_PUBLIC_CONNECT_HOST=play.yourdomain.com` in `docker/vps-production-fleet.env` (`nl-vps-init-env` writes this from the play domain).
2. Firewall: `ufw allow 25555/tcp` (plus 80/443/3478 as in [NL_VPS_DEPLOY.md](NL_VPS_DEPLOY.md)).
3. Deploy stack. Fork create uses `nl-fork-rimworld` and publishes 25555 on the **VPS host**.
4. Check `GET /api/v1/t2-lite/status` → `ready: true`, `rimworldExample` not loopback.

```powershell
powershell -File scripts/nl-t2-lite-validate.ps1
```

On a live VPS (optional):

```powershell
curl -fsS https://play.yourdomain.com/api/v1/t2-lite/status
```

---

## How the URI is built

1. Orchestrator records `rimworld://127.0.0.1:25555` (container bind).
2. `PublicForkConnect.RewriteForPublic` swaps the host using `NL_FORK_PUBLIC_CONNECT_HOST` (or `NL_VPS_DOMAIN` / `NL_PUBLIC_BASE_URL` host).
3. Fleet mask **does not** replace native schemes with WSS. Other endpoints (`ws://`, `docker://`) still use the relay template.

Local dogfood without a public host keeps loopback.

---

## Player path

1. Admit via NL Client (`/nl-client.html` or CLI).
2. Copy `nativeConnectClipboard` / `forkConnectEndpoint`.
3. Launch licensed RimWorld + MP mod; connect to `play.yourdomain.com:25555`.

Join is still **NL-only** for admit. The connect string is the game’s native address, not a Steam invite to the session bus.

---

## Env

| Variable | Role |
|---------|------|
| `NL_FORK_PUBLIC_CONNECT_HOST` | Hostname (or IP) in `rimworld://…` |
| `NL_FORK_PUBLIC_CONNECT_PORT` | Optional override (default keep 25555) |

---

*T2 remaining: game-port relay, unique host ports per session, cross-region dogfood.*
