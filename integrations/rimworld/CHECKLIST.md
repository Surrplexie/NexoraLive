# RimWorld checklist (T1 — full)

- [x] Legal tier (`AtOwnRisk`)
- [x] `adapter.manifest.json`
- [x] Catalog row `rimworld@1.0`
- [x] Dockerfile `docker/fork-rimworld/` (sidecar + optional `/game` dedicated)
- [x] build-fork-images key `rimworld`
- [x] `.nle` template — chat, combat, grief, draft, trade, rescue
- [x] `RimWorldForkRuntime` propose-then-commit (move/respawn/draft/trade/item/down)
- [x] `rimworld://` TCP connect listener on **25555**
- [x] T2-lite public host rewrite (`NL_FORK_PUBLIC_CONNECT_HOST`)
- [x] Harmony / Together NL Bridge (`integrations/rimworld/mod`)
- [x] Steam app **294100** in mock ownership
- [x] Sidecar emits full vocabulary + warn/kick/tell
- [x] Health probe
- [x] Dogfood script + social dogfood `-GameId rimworld`
- [x] Production dogfood `-AllGames` includes rimworld
- [x] validate script passes (including `nativePlugin`)
