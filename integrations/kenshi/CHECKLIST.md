# Kenshi checklist (T1 — full)

- [x] Legal tier (`AtOwnRisk`)
- [x] `adapter.manifest.json`
- [x] Catalog row `kenshi@1.0`
- [x] Dockerfile `docker/fork-kenshi/` (sidecar + optional `/game` licensed install)
- [x] build-fork-images key `kenshi`
- [x] `.nle` template — chat, combat, steal, raid, squad, trade, knockdown
- [x] `KenshiForkRuntime` propose-then-commit (move/respawn/squad/steal/raid/trade/item/KO)
- [x] `kenshi://` TCP connect listener on **23386**
- [x] T2-lite public host rewrite (`NL_FORK_PUBLIC_CONNECT_HOST`) for `kenshi://`
- [x] Community-MP NL Bridge (`integrations/kenshi/mod`)
- [x] Steam app **233860** in mock ownership
- [x] Sidecar emits full vocabulary + warn/kick/tell
- [x] Health probe
- [x] Dogfood script + social dogfood `-GameId kenshi`
- [x] Production dogfood `-AllGames` includes kenshi
- [x] validate script passes (including `nativePlugin`)
- [x] **Not** added to VPS `NL_GA_REQUIRED_GAMES` (Kenshi stays off the public launch line)
