# Production cutover deploy (Phase 10)

Final hosted stack before public launch — all dev shortcuts disabled.

```bash
cp samples/fleet/production-cutover.env.example docker/production-cutover-fleet.env
docker compose -f docker/docker-compose.production-cutover.yml up -d --build
```

Local validation:

```powershell
powershell -File scripts/nl-production-cutover-stack-up.ps1 -Validate
```

See [docs/NL_PRODUCTION_CUTOVER.md](../../docs/NL_PRODUCTION_CUTOVER.md).
