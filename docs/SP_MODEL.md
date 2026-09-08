# SP Model & Join Eligibility — Phase 2

Implements nl.txt section 2 (the StreamPlayer relationship model) as a data model + a
deterministic join-eligibility evaluator, in the same "facts in, decision out" spirit as
Phase 0's `RuleEngine` — but as its own dedicated evaluator rather than routed through the
`.nle` grammar, because standing/roles/offenses are categorical account data rather than
numeric per-event properties like `GameEvent`.

There is still no real networking, no real NLServer, and no real social-platform
verification here — see [ROADMAP.md](../ROADMAP.md) Phase 3+ for that. This phase is a
model + rules exercise, exactly like Phase 0 was for gameplay rules.

## Where the code lives

- `src/NL.Core/Sp/` — the model and engine, cross-platform, no I/O:
  - `SpStanding` — `Normal` / `Graylist` / `Banned`, always scoped to one streamer.
  - `SpRole` — `Sp` (base, always present) / `Friend` / `Vip` / `Mod` / `Admin`, also scoped
    to one streamer (a SP can be a Mod for one streamer and a stranger to another).
  - `SpVerification` — `[Flags]` for `Email` / `Phone` / `TwoFactor` / `Id`.
  - `SpOffense` — one logged offense (streamer, timestamp, issuer, reason); active for
    `SpOffense.ActiveWindow` (2 years per nl.txt) via `IsActive(now)`, then passively archived.
  - `SpStreamerRelationship` — per-streamer facts about one SP: standing, follow/sub status,
    roles. `IsPrivileged` (Admin/Mod) and `BypassesSocialRequirements` (+ Vip/Friend) are
    derived helpers used by the engine.
  - `SpProfile` — the SP account itself: id, display name, account creation date,
    verification flags, offense history, and a per-streamer relationship map.
  - `JoinRequirements` — a streamer's configurable join rules (follow/sub required, min
    account age, required verification flags, max active offenses, whether graylist gets a
    Hold or an outright Deny). `JoinRequirements.None` means "anyone in Normal standing".
  - `JoinDecision` / `JoinResult` — `Allow` / `Deny` / `Hold` plus an optional human-readable
    reason, mirroring `ActionResult`'s shape from Phase 0.
  - `JoinEligibilityEngine.Evaluate(profile, streamerId, requirements, nowUtc)` — the pure
    evaluator; see its XML doc for the exact check order.
- `src/NL.SpSimulator/` — a console CLI, structured exactly like `NL.Simulator`: fixed fake
  SP profiles + a fake streamer's `JoinRequirements`, printing each attempt's decision.
  Run with:

  ```bash
  dotnet run --project src/NL.SpSimulator
  ```

- `tests/NL.Core.Tests/JoinEligibilityEngineTests.cs` — unit tests for every branch (ban,
  graylist hold vs. deny, Mod/Admin bypass, Vip/Friend social-requirement bypass, account
  age, verification, offense threshold, 2-year offense archival).

## Evaluation order

1. **Banned** → always `Deny`, no exceptions (not even Admin/Mod).
2. **Graylist** (and not Admin/Mod) → `Hold` for streamer/mod review, or `Deny` if the
   streamer's `JoinRequirements.AllowGraylistWithHold` is `false`.
3. **Admin/Mod** for this streamer → bypasses every requirement below.
4. **Follow/subscription** requirements → bypassed by Admin/Mod/Vip/Friend.
5. **Minimum account age**.
6. **Required verification flags** (bitwise; a profile must have every requested flag).
7. **Maximum active offenses** with this streamer (archived offenses beyond the 2-year
   window don't count).

## Why not extend the `.nle` grammar?

Phase 0's `.nle` rules are gated on **numeric event properties** at the moment an in-game
event happens. Join eligibility is gated on **account-level facts** (standing, roles, dates,
verification flags, offense history) that don't naturally fit `GameEvent`'s
"name + numeric properties" shape without a lot of encoding gymnastics (e.g. turning an enum
standing into a magic number). A dedicated, strongly-typed evaluator keeps both sides simple;
if a future phase wants streamers to *author* join requirements the way they author `.nle`
rules today, that's a natural Phase 2.5 (a small `join` config block + a loader that builds a
`JoinRequirements`), not a change to `RuleEngine` itself.

## What's intentionally not here

- Real social-platform API calls (follow/sub status is a plain `bool` set by the caller).
- Real identity verification (ID/2FA/etc. are just flags — no KYC provider integration).
- Any persistence — profiles and requirements are in-memory POCOs, same as `GameEvent` in
  Phase 0.
- A GUI — Phase 1's `NL.ConfigEditor` doesn't touch this yet; see ROADMAP.md for whether that's
  worth doing before Phase 3.
