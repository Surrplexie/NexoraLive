# NL Fork Catalog (Phase N)

NL-maintained **major-version snapshots** of partner / at-own-risk game forks. Streamers pick
`gameId@major` from the catalog; the system refuses unknown or deprecated majors at session
start and admit time.

## Quick start

```powershell
$env:NL_FORK_CATALOG_ENABLED = "true"
New-Item -ItemType Directory -Force "$env:LOCALAPPDATA/NL/fork-catalog" | Out-Null
Copy-Item samples/fork/catalog.json "$env:LOCALAPPDATA/NL/fork-catalog/catalog.json"
```

Open **Fork catalog** UI: `http://127.0.0.1:27020/fork-catalog.html`

Select `gameA@1.0` → **Apply to session profile** → start session from Operator console.

## Major-only policy

| Allowed | Rejected |
|---------|----------|
| `1.0`, `2.0`, `3` → `3.0` | `1.2`, `1.4`, `2.1` |

Patch releases roll into the current major container image — catalog rows are **never** per-patch.

## Partnership tiers

| Tier | UI label | Legal copy |
|------|----------|------------|
| `Official` | Official | Publisher-approved NL session |
| `Platform` | Platform | Platform-opted title |
| `AtOwnRisk` | At own risk | Not endorsed; no progress transfer |

Tier and `noProgressTransfer` appear on the session manifest for join UX.

## Storage governance

`maxMajorsPerGame` (default **3**, env `NL_FORK_CATALOG_MAX_MAJORS`) caps active majors per
`gameId`. Registering a new major when over quota **auto-deprecates** the oldest active row.

Deprecated majors:
- Hidden from the default game picker
- Rejected at session start and admit (`CatalogEnforced`)

## Mod slot model

Verified mods live in `modHub` with SHA-256 hashes. Streamers attach mod ids via the catalog
UI; `ForkModSlotResolver` builds a `ForkModManifest` baked into the fork instance — never
pushed to SP clients.

## REST API

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/fork/catalog/settings` | Public catalog config |
| GET | `/api/v1/fork/catalog/entries` | Active catalog rows |
| GET | `/api/v1/fork/catalog/mod-hub` | Verified mod hub |
| GET | `/api/v1/fork/catalog/entries/{gameId}/{major}` | Single entry |
| POST | `/api/v1/fork/catalog/register` | Register major (operator) |
| POST | `/api/v1/fork/catalog/select` | Apply game+major+mods to profile |

## Session profile fields (Phase N)

| Field | Purpose |
|-------|---------|
| `catalogEnforced` | Reject unknown/deprecated majors |
| `gameId` / `gameMajorVersion` | Selected catalog entry |
| `attachedModIds` | Hub mod ids for fork bake-in |
| `partnershipTier` | Shown at join |
| `noProgressTransfer` | Ephemeral session banner |
| `catalogLegalNotice` | Tier-specific copy |

## Environment variables

| Variable | Default | Description |
|----------|---------|-------------|
| `NL_FORK_CATALOG_ENABLED` | off | Enforce catalog at start/admit |
| `NL_FORK_CATALOG_ROOT` | `%LOCALAPPDATA%/NL/fork-catalog` | Store root |
| `NL_FORK_CATALOG_MANIFEST` | `{root}/catalog.json` | Manifest path |
| `NL_FORK_CATALOG_MAX_MAJORS` | `3` | Quota per gameId |
| `NL_SAMPLES_ROOT` | auto-detect | Resolve `defaultNleTemplate` paths |

## Smoke test

```powershell
./scripts/nl-fork-catalog-smoke.ps1
```

## Exit criteria (ROADMAP)

Operator registers `gameA@1.0` → streamer selects it in web UI → system refuses unknown or
deprecated majors at start and admit.
