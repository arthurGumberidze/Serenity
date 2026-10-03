# PROJECT_STATE.md

## Engine
Unity 6000.6.4f1 (12bfff696524), explicitly approved by user on 2026-10-03; URP 17.6.0.

## Current milestone
U02 DONE: the Unity hybrid-stack packages are pinned, their complete resolved graph is committed, baseline project settings are documented, and restore/tests/build pass from a clean checkout.

## Last completed task
U02 - Пакеты и базовая конфигурация.

## Active task
U03 - Игровое время и календарь (next chat only; not started).

## Build status
Windows x64 / Mono / Development succeeded after U02 from a clean detached worktree: exit 0, zero errors, two existing stripped debug shader warnings. Clean player: Builds/Windows/Serenity.exe, 667136 bytes. Exact commands and evidence: docs/U02_HANDOFF.md.

## Test status
20/20 EditMode tests passed from a clean checkout: three U00 environment checks, four U01 architecture checks, twelve U02 package/configuration checks and one Addressables package test. U02 verifies direct pinned versions, installed paths, required Player assemblies/APIs, Mono, Linear color space, Force Text and Input System-only mode. No C# compile errors. Result: docs/validation/U02-clean-tests.xml.

## Known blockers
None for U03. U02 installs and validates the DOTS toolchain but does not implement ECS behavior; that remains U12. No gameplay or visual implementation was part of U02.

## Architecture facts
- C# primary language.
- Tier1: important/nearby interactive NPCs with GameObject presentation backed by persistent domain state.
- Tier2: simplified active-city NPCs via Unity Entities/DOTS.
- Tier3: distant population as aggregate data, no GameObject-per-person.
- Off-camera transitions deterministic; camera never rerolls world outcomes.
- Stone-age MVP first.
- Runtime boundaries are Game.Domain, Game.Simulation, Game.ECS, Game.Presentation and Game.Infrastructure; Game.Tests is Editor-only.
- Domain has no project dependency. Simulation depends only on Domain. ECS and Presentation depend on Domain + Simulation. Infrastructure is the outer composition layer and may reference all four.
- Domain and Simulation compile without UnityEngine/UnityEditor references. No gameplay services are implemented yet.
- Infrastructure will own composition, persistence adapters and scene loading; Simulation owns orchestration/ticks; Domain owns canonical state and rules.
- Direct package pins and baseline settings are recorded in docs/PACKAGES.md; package API references are added only with the task that first uses them.
