# NL Legal & Compliance Runbook — Phase 13

Operator steps to harden legal and compliance posture before public GA.

## Prerequisites

- Phase 12 scale & reliability validation passed
- Legal counsel review of terms, privacy, cookie policy, and DPA
- `NL_LAUNCH_LEGAL_VERSION` set to reviewed document date

## 1. Configure env

```env
NL_LEGAL_COMPLIANCE_ENABLED=true
NL_LEGAL_COMPLIANCE_DEV=false
NL_LEGAL_COMPLIANCE_MIN_AGE=13
NL_LEGAL_COMPLIANCE_SUBPROCESSORS=Steam Web API (Valve),Twitch API (Amazon),Your CDN
NL_LAUNCH_LEGAL_VERSION=2026-08-01
```

## 2. Deploy stack

```powershell
powershell -File scripts/nl-legal-compliance-stack-up.ps1
```

Verify public pages at `/legal-center.html` and cookie banner on `/play.html`.

## 3. Run validation

```powershell
powershell -File scripts/nl-legal-compliance-validate.ps1 `
  -BaseUrl https://play.yourdomain.com `
  -OperatorKey "<key>"
```

On VPS with `NL_LEGAL_COMPLIANCE_DEV=false`, scale gate and audit log must pass without dev shortcuts.

## 4. Streamer signup

Ensure `/ga.html` terms checkbox is enforced — registrations without `termsAccepted` return HTTP 400.

## 5. GDPR requests

Use operator-authenticated endpoints:

- `POST /api/v1/fleet/compliance/export/{playerId}`
- `DELETE /api/v1/fleet/compliance/sp/{playerId}`

Review audit log: `GET /api/v1/legal-compliance/audit/recent`

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `streamer_terms_smoke` fails | Pass `termsAccepted: true` on register |
| `compliance_audit` fails | Run GDPR export smoke or accept streamer terms |
| `scale_reliability_gate` fails | Run scale reliability validation first |
| Signup 400 without terms | Expected when legal compliance enabled |
