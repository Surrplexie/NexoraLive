# Launch ops deploy (Phase 9)

Full public launch stack: multi-game production + status page + legal + hardening + backups.

```bash
cp samples/fleet/launch-ops.env.example docker/launch-ops-fleet.env
docker compose -f docker/docker-compose.launch-ops.yml up -d --build
```

Local validation:

```powershell
powershell -File scripts/nl-launch-ops-stack-up.ps1 -Validate
```

See [docs/NL_LAUNCH_OPS.md](../../docs/NL_LAUNCH_OPS.md).
