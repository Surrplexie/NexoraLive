# Distribution deploy

Phase 11 player & streamer distribution extends the production cutover stack.

## Files

| Path | Purpose |
|------|---------|
| `docker/docker-compose.distribution.yml` | Full stack with distribution flags |
| `samples/fleet/distribution.env.example` | VPS env template |
| `scripts/nl-distribution-stack-up.ps1` | Build client + start stack |
| `scripts/nl-distribution-stack-down.ps1` | Stop stack |
| `scripts/nl-distribution-validate.ps1` | End-to-end validation |
| `scripts/build-nl-client-package.ps1` | Publish NL Client zip |
| `scripts/register-nlclient-protocol.ps1` | Windows `nlclient://` handler |

## Docs

- [docs/NL_DISTRIBUTION.md](../../docs/NL_DISTRIBUTION.md)
- [docs/NL_DISTRIBUTION_RUNBOOK.md](../../docs/NL_DISTRIBUTION_RUNBOOK.md)

## Local validation

```powershell
powershell -File scripts/nl-distribution-stack-up.ps1 -Validate
```

Expected: **DISTRIBUTION VALIDATION PASSED**
