# NL Public GA Launch Runbook — Phase 14

Operator steps for the final public GA go-live gate.

## Prerequisites

- Phase 13 legal & compliance validation passed
- Counsel-reviewed legal documents published
- Support inbox monitored (`NL_PUBLIC_GA_SUPPORT_CONTACT`)
- Recent fleet backup verified

## 1. Configure env

```env
NL_PUBLIC_GA_LAUNCH_ENABLED=true
NL_PUBLIC_GA_LAUNCH_DEV=false
NL_PUBLIC_GA_LAUNCH_VERSION=2026-08-01
NL_PUBLIC_GA_SUPPORT_CONTACT=support@yourdomain.com
NL_LEGAL_COMPLIANCE_ENABLED=true
NL_LEGAL_COMPLIANCE_DEV=false
```

## 2. Deploy stack

```powershell
powershell -File scripts/nl-public-ga-launch-stack-up.ps1
```

Verify:

- `/ga-launch-checklist.html` — checklist loads
- `/play.html`, `/download.html`, `/status.html` — public pages reachable
- `/public-ga-launch-ops.html` — operator console

## 3. Pre-launch operator steps

1. Run fleet backup: `POST /api/v1/launch-ops/backup/run` (operator key)
2. Review checklist at `/ga-launch-checklist.html`
3. Record signoff: `POST /api/v1/public-ga-launch/signoff`

## 4. Run validation

```powershell
powershell -File scripts/nl-public-ga-launch-validate.ps1 `
  -BaseUrl https://play.yourdomain.com `
  -OperatorKey "<key>"
```

On VPS with `NL_PUBLIC_GA_LAUNCH_DEV=false`, legal gate, backup, and signoff must pass without dev shortcuts.

Expected: **`PUBLIC GA LAUNCH VALIDATION PASSED`**

## 5. Go live

After validation passes:

1. Announce GA on status page and public channels
2. Monitor `/status.html` and alerting
3. Keep support contact responsive for first 72 hours

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `legal_compliance_gate` fails | Run legal compliance validation first |
| `operator_signoff` fails | POST signoff from ops UI or validate script |
| `recent_backup` fails | Run `POST /api/v1/launch-ops/backup/run` |
| `support_contact` fails | Set `NL_PUBLIC_GA_SUPPORT_CONTACT` in env |
| `all_programs_enabled` fails | Enable all upstream phase env flags |
