# NL Legal & Compliance — Phase 13

Legal center, cookie consent, GDPR audit log, and streamer terms acceptance on top of the scale & reliability stack.

## Quick start (local validation)

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive

powershell -File scripts/nl-legal-compliance-stack-down.ps1
powershell -File scripts/nl-legal-compliance-stack-up.ps1 -Validate
```

Expected: **`LEGAL COMPLIANCE VALIDATION PASSED`**

## What Phase 13 adds

| Feature | Description |
|---------|-------------|
| **Legal compliance program** | `NL_LEGAL_COMPLIANCE_ENABLED` master switch |
| **Legal center** | `/legal-center.html` hub for all public legal docs |
| **Cookie policy + banner** | `/cookie-policy.html` + `legal-consent.js` on public pages |
| **Subprocessors + DPA** | `/subprocessors.html`, `/dpa.html` |
| **GDPR audit log** | Records export/delete and streamer terms acceptance |
| **Streamer terms gate** | GA signup requires `termsAccepted=true` when enabled |
| **Scale reliability gate** | Requires scale validation upstream |
| **Validation API** | `GET/POST /api/v1/legal-compliance/validation` |
| **Ops UI** | `/legal-compliance-ops.html` |

Compose: [`docker/docker-compose.legal-compliance.yml`](../docker/docker-compose.legal-compliance.yml)

## Public pages

| URL | Purpose |
|-----|---------|
| `/legal-center.html` | Legal document hub |
| `/terms.html` | Terms of Service |
| `/privacy.html` | Privacy Policy |
| `/cookie-policy.html` | Cookie policy |
| `/subprocessors.html` | Third-party processor list |
| `/dpa.html` | Data Processing Addendum |
| `/legal-compliance-ops.html` | Operator validation console |

## Environment

```env
NL_LEGAL_COMPLIANCE_ENABLED=true
NL_LEGAL_COMPLIANCE_DEV=true              # local validation only
NL_LEGAL_COMPLIANCE_MIN_AGE=13
NL_LAUNCH_LEGAL_VERSION=2026-08-01
NL_SCALE_RELIABILITY_ENABLED=true
```

See [`samples/fleet/legal-compliance.env.example`](../samples/fleet/legal-compliance.env.example).

## Validation checks

- Legal compliance + scale reliability programs enabled
- Scale reliability gate passed
- Terms, privacy, legal center, cookie, subprocessors, DPA published
- GDPR export/delete enabled + 730-day retention
- Partnership legal gate enabled
- GDPR export smoke + streamer terms smoke
- Compliance audit log entries recorded

```powershell
powershell -File scripts/nl-legal-compliance-validate.ps1 -OperatorKey "<key>"
```

## Phase 13 exit criteria

- [x] Legal compliance compose stack (extends scale)
- [x] Legal center + cookie/subprocessor/DPA pages
- [x] Cookie consent banner on public pages
- [x] GDPR audit log + streamer terms gate
- [x] Validation API + script
- [x] Ops UI + runbook
- [ ] Counsel review + signed DPA on VPS (operator deploy)

Next: **Phase 14** — public GA launch checklist & operator runbook. See [`docs/NL_PUBLIC_GA_LAUNCH.md`](NL_PUBLIC_GA_LAUNCH.md).

See also: [NL_LEGAL_COMPLIANCE_RUNBOOK.md](NL_LEGAL_COMPLIANCE_RUNBOOK.md) · [NL_SCALE_RELIABILITY.md](NL_SCALE_RELIABILITY.md)
