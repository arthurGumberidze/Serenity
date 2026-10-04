# PROJECT_STATE.md

## Engine
Unity 6000.6.4f1 (12bfff696524), approved override D-008; URP 17.6.0; Windows x64 Mono Development. U04A adds only the pinned PostgreSQL provider/tooling described below and does not change the Editor or scripting backend.

## Current milestone / last completed task
U06 DONE — Character Domain Tier 1. A pure `Game.Domain.Characters.Character` aggregate now owns persistent identity and foundational personal state independently of Unity presentation, ECS handles and PostgreSQL.

## Active task
U07 — GameObject presentation Tier 1 is next and has NOT started. U06 stopped at domain state and tests; no character prefab binding, spawning, animation, navigation, AI, needs or tier switching was implemented.

## U06 character domain
- `Character.CreateNew` generates the existing U04 `StableEntityId`; `Character.Restore` requires and preserves a saved ID. `CharacterState` is a detached deterministic snapshot for future save/tier adapters.
- Canonical age data is one biological birth tick. Completed years use U03-compatible biological `TimeSpan` ticks and a fixed 365-day year; death records a biological tick and freezes age. No wall/calendar/Unity clock or per-NPC timer is used.
- Parentage is canonical on the child through stable-ID links that distinguish mother/father, biological/legal/adoptive and known/hidden status. Registry child queries derive the reverse relationship. Current spouse links are stable IDs and registry operations keep them symmetric.
- Name and biological sex are typed and validated. Trait, skill and profession references use validated data-definition keys. Full NPCs require four or five unique traits; skills/attributes use 0–100 values.
- Health foundation covers head, torso, left/right arms and left/right legs. Wealth and Influence are bounded 0–1000 values. Family/dynasty are optional stable-ID hooks; their gameplay remains U17/U32 scope.
- `CharacterRegistry` is a session-owned RAM index with duplicate-ID rejection, deterministic listing/lookup, derived children and spouse linking. It is not a singleton or database repository.
- U04 `SaveSnapshot` version 1 and migration 001 are unchanged. U06 adds no serializer, SQL, Npgsql dependency, scene object, prefab reference or new assembly edge.

## U05A asset foundation
- External download staging is the ignored `serenity_games_assets` tree. Only seven selected WizardHat FBXs were copied into `Assets/Game/Art/ThirdParty`, each with explicit source/license records; unknown-provenance files remain quarantined.
- Gameplay-facing content lives in Serenity-owned prefabs under `Assets/Game/Art/Prefabs`; wrappers reference vendor/source content without putting Serenity state or code into vendor packages.
- Selected Hodaart Characters 01/02 are the male/female Tier 1 placeholder candidates with valid Human Avatars and idle/walk/run coverage. A project-owned 832-triangle single-mesh Tier 2 placeholder is separate.
- Stone Age readiness includes axe, spear, torch, animated boar, tree, rock and campfire plus primitive project-owned shelter/storage placeholders. Production Stone Age clothing, final characters/buildings, crafting props, broader wildlife and missing work/combat/carry/death animation remain documented gaps.
- `StoneAgeAssetGallery` is development-only and excluded from Build Settings. Scoped editor tooling normalizes only registered files and never acts as a global AssetPostprocessor.

## U05 runtime
- Camera input actions: WASD/arrows move, screen-edge scroll, middle-mouse drag pan, wheel zoom and Q/E yaw rotation. Bindings live in `LocalGameplay.inputactions` for future rebinding.
- Navigation uses `Time.unscaledDeltaTime`, so it remains a presentation concern and is not coupled to GameClock or active simulation pause.
- Movement, zoom, edge width/speed, rotation, fixed pitch and rectangular XZ bounds are serialized settings. Camera target motion has no vertical drift.
- Cinemachine 6.6 supplies the virtual/output camera path; the input abstraction and controller do not depend on Cinemachine input callbacks.
- `WorldPointerRaycaster` provides world hit/point APIs and blocks world interaction when an EventSystem reports pointer-over-UI. `SelectionProbe` proves pointer-to-target selection with three primitive markers.
- No vendor pack is a dependency of the scene. U05A remains responsible for asset inventory/import/normalization.

## Provider and schema
- PostgreSQL detected/validated: 18.0 x64 on a private loopback test cluster.
- Npgsql 8.0.9, netstandard2.1, no ORM. NuGetForUnity 4.5.0 restores a fully pinned dependency graph from nuget.org into ignored `Assets/Packages`.
- Configuration uses local-only `DYNASTYGAME_PG_*` process environment variables. No credentials or complete connection strings are committed/logged.
- Recommended databases: `dynasty_game_dev` and separate `dynasty_game_test`; schema `serenity`.
- Migration 001 creates `save_sessions`; the migrator transactionally bootstraps `schema_migrations`, verifies version/name/SHA256 history, serializes concurrent migrators with an advisory transaction lock and rolls back failure.
- StableEntityId round-trips through PostgreSQL UUID without regeneration. Canonical save data remains the strict U04 XML payload in BYTEA. Database schema version and save-format version are distinct.
- Save is one fully parameterized atomic UPSERT. Load validates relational session/version metadata and the complete XML snapshot before `SaveCoordinator` can replace runtime state.

## Build status
Windows x64 Mono Development succeeded after U06: errors=0, warnings=2. Player output remains `Builds/Windows/Serenity.exe` (667136 bytes). Exact commands and diagnostics are in `docs/U06_HANDOFF.md`; `Tools/Verify-U06.ps1` runs every inherited gate.

## Test status
126/126 EditMode tests passed (0 failed/skipped, 1.5383799 seconds), including 29 focused U06 cases and all inherited architecture/time/persistence/camera/input/asset tests. The separate U06 category passed 29/29 (0 failed/skipped, 0.1969921 seconds). 25/25 real PostgreSQL integration tests passed (0 failed/skipped, 17.1574365 seconds). 2/2 PlayMode tests passed (0 failed/skipped, 0.22748 seconds). Exact commands and evidence are in `docs/U06_HANDOFF.md` and `Logs/`.

## Architecture facts
- Character identity and canonical personal state live only in the pure Domain aggregate. Presentation and future ECS representations bind through `StableEntityId` and are disposable projections.
- `CharacterState` capture is deterministic and detached; new creation and restore are distinct. Parent, spouse, family, dynasty and relationship references are stable IDs rather than object/view references.
- U06 did not implement an NPC-card UI despite the obsolete older task wording: the explicit U06 scope is domain-only, and presentation begins at U07. The snapshot exposes all U06 data needed by a later card/read model.
- External staging, selected third-party source and Serenity runtime prefabs are separate ownership zones. Vendor files are never gameplay state owners, and package folder layout is not a gameplay API.
- U05A importer automation is explicit and allow-listed. It normalizes registered FBXs to the one-unit/one-metre policy, creates project-owned URP material variants/wrappers and leaves unrelated vendor imports untouched.
- Game.Presentation now directly references the pinned Unity Input System and Cinemachine package assemblies; project-layer dependency directions remain unchanged.
- `LocalSceneCompositionRoot` uses explicit serialized references. Runtime code does not use scene-wide find/service-locator calls for composition.
- Camera and pointer state are disposable presentation state and never enter saves, StableEntityId mappings or PostgreSQL.
- Domain, Simulation, ECS and Presentation do not reference Npgsql; assembly dependency directions are unchanged. Infrastructure is the outer provider layer.
- PostgreSQL calls are limited to explicit setup/migration and save/checkpoint boundaries. No per-frame, per-NPC, movement, AI or continuous-state SQL exists.
- Timestamps are storage metadata. Calendar and biological ticks remain the only game time.
- Provider errors are controlled/redacted. Missing and corrupt save semantics remain explicit. Failed load never replaces active state.
- Successful load still creates a replacement GameClock. Future runtime composition/UI/DOTS consumers must rebind; the provider holds no GameClock reference.
- Future normalized Character/Dynasty/City/HistoricalEvent storage remains deferred behind separate ports/migrations.

## Known limitations
U06 provides foundations, not gameplay: needs, diseases/injuries, mortality scheduling, pregnancy/genetics, relationship events, marriage history, dynasty/succession, profession work, Wealth/Influence formulas, persistence schema integration and tier conversion remain deferred. The U05A characters/buildings remain readiness placeholders with the previously documented art/animation gaps. U04A remains synchronous/local-development-only.

## Working tree
The U06 commit is allow-listed to Character Domain code/tests, its verifier and documentation only. Pre-existing TutorialInfo edits, unselected vendor packs, `_Recovery`, unrelated ProjectSettings, reference/update files and U05A-generated working-tree noise remain uncommitted. The whole `serenity_games_assets` tree remains ignored and outside Git.
