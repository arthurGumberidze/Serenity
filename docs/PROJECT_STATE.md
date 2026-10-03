# PROJECT_STATE.md

## Engine
Unity 6000.6.4f1 (12bfff696524), explicitly approved by user on 2026-10-03; URP 17.6.0.

## Current milestone
U01 DONE: project layer assemblies compile, their acyclic dependency graph and ownership rules are documented, and architecture checks plus the Windows Development build pass.

## Last completed task
U01 - Архитектура assemblies и слоёв.

## Active task
U02 - Пакеты и базовая конфигурация (next chat only; not started).

## Build status
Windows x64 / Mono / Development succeeded after U01 in the main workspace: exit 0, zero errors, two existing stripped debug shader warnings. Player: Builds/Windows/Serenity.exe. Exact command and evidence: docs/U01_HANDOFF.md.

## Test status
7/7 EditMode tests passed: three U00 environment checks and four U01 architecture checks. The checks verify compiled Player assemblies, approved project references, Editor-only test isolation, pure Domain/Simulation engine separation, explicit asmdefs and absence of cycles. No C# compile errors. Result: docs/validation/U01-main-tests.xml.

## Known blockers
None for U02. U01 does not validate DOTS behavior because package/configuration work is assigned to U02 and ECS implementation to U12. No visual rendering check was required for this architecture-only task.

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
