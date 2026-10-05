# UPDATE 2026-10-03 — PostgreSQL + Free Asset Pipeline

This update is designed to be merged into an already-running project.
DO NOT overwrite PROJECT_STATE.md or NEXT_TASK.md with old template values.
Preserve all DONE/IN_PROGRESS statuses.

## New task U04A — PostgreSQL Persistence Layer
Depends on: U04
Phase: MVP

Definition of Done:
- Persistence interface is separated from gameplay/domain code.
- Local PostgreSQL connection uses secrets outside Git.
- Schema migrations are versioned.
- New save session can be created.
- Stable IDs for at least Character, Dynasty, City and HistoricalEvent round-trip correctly.
- Save snapshot writes transactionally; failure rolls back.
- Integration tests use a separate test database.
- Runtime simulation remains in RAM; no per-frame/per-NPC SQL writes.

## New task U05A — Asset Pipeline + Free Asset Acquisition
Depends on: U02,U05
Phase: MVP

Definition of Done:
- Art staging/runtime folder structure exists.
- docs/ASSET_REGISTRY.md exists.
- Base free/placeholder set exists: humanoid, clothing, tree, rock, dwelling, storage, tool, weapon.
- Import normalization covers scale/pivot/material/collider/rig/animations/LOD.
- All third-party assets have traceable source/license.
- If automatic download is blocked, docs/MANUAL_ASSET_DOWNLOADS.md is generated and placeholders are used.

## Dependency changes
- U07 depends on U05,U05A,U06
- U08 depends on U04,U05,U05A
- U17 depends on U03,U04,U04A,U06
- U20 depends on U05,U05A,U06,U07
- U28 depends on U04,U04A,U06,U17
- U30 depends on U04A,U10,U13,U14,U23,U26,U29

## Persistence rules
- Runtime state in RAM is the source of truth during play.
- PostgreSQL is durable snapshot/history storage.
- Gameplay code must not contain SQL.
- Use a repository/persistence abstraction.
- Batch writes at save/autosave/checkpoint/major event.
- Never commit credentials or connection strings.
- Version schema with migrations.
- Keep the provider replaceable so a future embedded/file provider can be added.

## Asset rules
- Prefer free assets with clearly compatible licenses for MVP.
- Do not use assets with unclear licensing.
- Register every external asset in docs/ASSET_REGISTRY.md.
- Normalize all external art before Runtime use.
- Do not let art assets own gameplay state.
- If download requires login/CAPTCHA/manual license acceptance, record it in MANUAL_ASSET_DOWNLOADS.md and continue with a placeholder.
