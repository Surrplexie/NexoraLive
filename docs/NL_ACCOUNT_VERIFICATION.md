# NL Account Verification

Email and TOTP two-factor authentication for NL identity accounts. Verified flags sync to `SpProfile.Verification` at admit time and are checked by `JoinEligibilityEngine`.

## Quick start (mock email)

```powershell
$env:NL_VERIFICATION_ENABLED = "true"
$env:NL_VERIFICATION_DEV_EXPOSE_CODES = "true"
$env:NL_EMAIL_MODE = "mock"
```

1. Create an NL account at `/identity-link.html`
2. Open `/account-verify.html` with the account id
3. Send email code (mock mode logs to console and returns `devCode` in API response)
4. Confirm email, then enroll 2FA with an authenticator app
5. On `/join-gate.html`, enable **Require email verification** and/or **Require 2FA**
6. Test admit from account-verify page with `nlAccountId` + `twoFactorCode` when 2FA is required

## Architecture

```
Account verify UI → AccountVerificationService
                 → NlIdentityAccount (email, TOTP secret)
                 → SpProfile.Verification at admit (via NlAccountId)
                 → JoinEligibilityEngine RequiredVerification check
                 → Optional TOTP step-up when 2FA required
```

| Component | Role |
|-----------|------|
| `AccountVerificationService` | Email codes, TOTP enroll/verify, flag computation |
| `TotpHelper` | RFC 6238 TOTP (30s, 6 digits) |
| `MockEmailSender` / `SmtpEmailSender` | Dev vs production email delivery |
| `JsonEmailVerificationChallengeStore` | Short-lived email codes |

## Environment variables

| Variable | Default | Description |
|----------|---------|-------------|
| `NL_VERIFICATION_ENABLED` | on | Enable verification API + admit sync |
| `NL_EMAIL_MODE` | `mock` | `mock` or `smtp` |
| `NL_VERIFICATION_DEV_EXPOSE_CODES` | off | Return email codes in API response (local only) |
| `NL_EMAIL_SMTP_HOST` | — | SMTP host when `NL_EMAIL_MODE=smtp` |
| `NL_EMAIL_SMTP_PORT` | `587` | SMTP port |
| `NL_EMAIL_SMTP_USER` | — | SMTP username |
| `NL_EMAIL_SMTP_PASSWORD` | — | SMTP password |
| `NL_EMAIL_FROM` | — | From address |

See [`samples/identity/account-verification.env.example`](../samples/identity/account-verification.env.example).

## REST API

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/identity/verification/{accountId}` | Current verification status |
| POST | `/api/v1/identity/verification/email/request` | Send email verification code |
| POST | `/api/v1/identity/verification/email/confirm` | Confirm email with code |
| POST | `/api/v1/identity/verification/2fa/enroll/start` | Start TOTP enrollment |
| POST | `/api/v1/identity/verification/2fa/enroll/confirm` | Confirm TOTP enrollment |
| POST | `/api/v1/identity/verification/2fa/verify` | Test TOTP code |
| DELETE | `/api/v1/identity/verification/2fa` | Disable 2FA (requires code) |

Admit body extensions (`POST /api/v1/session/admit`):

```json
{
  "playerId": "my-sp",
  "nlAccountId": "abc123",
  "twoFactorCode": "123456"
}
```

`twoFactorCode` is required when join requirements include `TwoFactor` and the account has 2FA enabled.

## SpVerification flags

| Flag | How earned |
|------|------------|
| `Email` | Confirm email verification code |
| `TwoFactor` | Complete TOTP enrollment |
| `Phone` | Not implemented (future) |
| `Id` | Not implemented (KYC future) |

## Validation

```powershell
powershell -File scripts/nl-account-verification-validate.ps1
```

Expected: **`ACCOUNT VERIFICATION VALIDATION PASSED`**
