# RimWorld Together on VPS (real Direct Connect)

The NL Docker rimworld fork defaults to a **C# sidecar** that only speaks:

`NL-RIMWORLD/1 together-ready`

**RimWorld Together** (Workshop `3005289691`) needs the real **Together dedicated server** on TCP **25555**. This doc is the operator path for Path A (`play.20062006.xyz`).

NL does **not** redistribute RimWorld or Together binaries — this downloads Together’s official server zip from GitHub onto **your** VPS.

---

## Conflict on port 25555

Only one listener can own `:25555`:

| Process | Role |
|---------|------|
| `nl-fork-*` (sidecar) | NL banner / dogfood stub — Together times out |
| Together `Server-linux-x64` | Real Direct Connect |

`nl-vps-together-up.sh` **stops** `nl-fork-*` containers, then starts Together. Leave **NL session-host** running so admit / rules still work. Do **not** Operator-Start a new rimworld fork while Together holds 25555 (or Stop the fork first).

---

## One-shot on the VPS

```bash
ssh ubuntu@40.160.88.114
cd /opt/NexoraLive
sudo git pull --ff-only
sudo chmod +x scripts/nl-vps-together-up.sh scripts/nl-vps-together-down.sh
sudo bash scripts/nl-vps-together-up.sh
```

Expect: `OK: Together PID …` and connect hint.

```bash
# prove listen
sudo ss -tlnp | grep 25555
tail -f /opt/rimworld-together/together.log
```

Stop later:

```bash
sudo bash /opt/NexoraLive/scripts/nl-vps-together-down.sh
```

---

## Client (your PC)

1. Workshop mod **RimWorld Together** + **Harmony** enabled (load order: Harmony → Core → Together).
2. Prefer client build matching server release (script default **26.8.31.1**). Update the Workshop mod if the server rejects version.
3. Direct Connect:
   - **IP:** `play.20062006.xyz` (no `:port` in the IP box)
   - **Port:** `25555`
4. First joiner may create the shared world (Together normal flow).

Optional: still run NL Client admit first for ownership dogfood; Together does not replace admit.

---

## Version / reinstall

```bash
sudo NL_TOGETHER_FORCE_REINSTALL=1 NL_TOGETHER_RELEASE=26.8.31.1 \
  bash /opt/NexoraLive/scripts/nl-vps-together-up.sh
```

Or pin URL:

```bash
sudo NL_TOGETHER_URL='https://github.com/RimWorld-Together/Rimworld-Together/releases/download/26.8.31.1/Server-linux-x64.zip' \
  NL_TOGETHER_FORCE_REINSTALL=1 \
  bash /opt/NexoraLive/scripts/nl-vps-together-up.sh
```

---

## Back to NL sidecar-only

```bash
sudo bash /opt/NexoraLive/scripts/nl-vps-together-down.sh
# Operator → Start session (fork ON) → sidecar binds 25555 again
```

---

## Honest limits

- This gives **native Together play** on the public port.
- Full **in-game NL rule cancel** still needs the NL Harmony bridge wired into that Together world (separate step).
- Sidecar `--serve` and Together cannot share `:25555` at once.
