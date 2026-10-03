# DECISIONS.md

## D-001 Engine
Unity 6.3 LTS (6000.3), C#, Windows PC. URP for MVP.

## D-002 Hybrid simulation
Tier1 = permanent important/nearby NPC domain objects + GameObject presentation as needed.
Tier2 = Entities/DOTS simplified agents in active city.
Tier3 = statistical city population stored as aggregate data.
Permanent named persons preserve StableEntityId across tiers.

## D-003 Time
Normal day at x1 = 24 real minutes. Biological age uses separate accelerated clock. Pregnancy target ~30-60 real minutes at x1; childhood several real hours; a generation roughly 8-12 real hours. Death from old age always exists.

## D-004 Off-camera simulation
World sections collapse to aggregate snapshots. Snapshots preserve composition, health, inventories/supply, morale, orders, production, relevant timers and RNG state/seed. Returning reconstructs detail without rerolling.

## D-005 Wealth/Influence
No full salary/market/inflation simulation. Wealth = property + business income + state rewards. Influence = office + dynasty + wealth + achievements + events.

## D-006 Non-negotiable game pillars
War; building; third-person control of monarch/captain; dynasty/family/inheritance; weather gameplay effects.

## D-007 Content architecture
ScriptableObject for immutable/authoring definitions. Runtime state is serializable models/DTOs with explicit save schema version.

## D-008 User-approved Editor version (2026-10-03)
The user explicitly requested keeping the installed Unity version and continuing U00. Target Unity 6000.6.4f1 (12bfff696524), overriding the original 6000.3 LTS requirement. Use the bundled URP blank template (17.2.1), URP 17.6.0, Windows x64 Mono Development builds. Do not upgrade the Editor automatically. No gameplay or U01 architecture is included.

## D-009 Assembly boundaries and composition (2026-10-03)
Runtime dependencies are one-way: Domain has none; Simulation references Domain; ECS and Presentation reference Domain plus Simulation; Infrastructure references all four as the outer composition layer. Tests reference all runtime layers but compile for Editor only. Domain and Simulation use `noEngineReferences` and must expose pure C# contracts. All project runtime asmdefs disable implicit auto-reference and unsafe code. New dependency edges require an architecture update and test change.

Canonical mutable game state belongs to Domain models and is changed through Simulation orchestration. Presentation and ECS are disposable projections; stable IDs bridge them to state, while Unity instance IDs and ECS entity handles remain transient. Infrastructure implements persistence/configuration/scene adapters and constructs the application. Saves occur only at a synchronization barrier after commands and ECS jobs commit. Package installation and concrete gameplay services remain assigned to their later tasks.

## D-010 Package and baseline configuration (2026-10-03)
The Unity 6000.6.4f1 package profile is pinned directly in Packages/manifest.json and fully resolved in Packages/packages-lock.json: Entities 6.6.0, Burst 2.0.0, Collections 6.6.0, Mathematics 1.4.0, Cinemachine 6.6.0, Addressables 2.11.2, Input System 1.20.0, AI Navigation 2.0.12, URP 17.6.0 and Unity Test Framework 1.8.0. Jobs and parts of the Burst/Mathematics surface are supplied by Unity 6.6 engine modules; no obsolete standalone Jobs package is added. Entities Graphics is deferred until U12 chooses a rendering need.

Package installation alone does not add new project-layer dependency edges. An asmdef references a package assembly only when the task introducing that API requires it. Addressables groups/catalogs and ECS systems are likewise created with their owning feature tasks. Windows x64 Mono Development, Linear color space, URP, Input System-only input, Force Text serialization and Visible Meta Files are the committed baseline. Existing Enter Play Mode settings disable domain and scene reload, so later bootstrap code must reset static state explicitly.

## D-011 Deterministic game clocks (2026-10-03)
Canonical time state is a serializable Domain object containing integer `TimeSpan` ticks for calendar and biological elapsed time, the selected supported speed and pause state. Simulation owns one central `GameClock` and advances it from an explicit elapsed-real-time input; the clock does not read `UnityEngine.Time`, set `Time.timeScale` or require per-entity update loops. A session driver, future DOTS system or offline aggregate runner can therefore supply the same input contract.

At x1, 24 real minutes advance one 24-hour calendar day. Speed is restricted to x1, x2, x3, x5 and x10. Active pause preserves the selected speed and all state while producing zero calendar and biological delta. Checked integer arithmetic rejects overflow before either clock is mutated, so large-step and split-step results are identical within the supported range.

Biological elapsed time is a separate stored clock, not an age derived from calendar time. Its default multiplier is 400x calendar time, configurable through pure Simulation settings. This follows the explicit U03 range of roughly 300–500x. The older FRS target of about nine real minutes per biological year does not numerically match that range, so exact life-stage balance is deferred to Character/Dynasty work. Pregnancy is explicitly a separate gameplay timer and is not implemented by U03.
