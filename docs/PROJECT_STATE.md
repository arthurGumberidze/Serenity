# PROJECT_STATE.md

## Engine
Unity 6000.6.4f1 (12bfff696524), explicitly approved by user on 2026-10-03; URP 17.6.0.

## Current milestone
U03 DONE: deterministic calendar and biological clocks, supported speeds, active pause, calendar rollover and persistence-ready clock state are implemented in pure Domain/Simulation code.

## Last completed task
U03 - Игровое время и календарь.

## Active task
U04 - Stable IDs + Save/Load Core (next chat only; not started).

## Build status
Windows x64 / Mono / Development succeeded after U03: exit 0, zero errors and the same two existing build warnings recorded after U02. Player: Builds/Windows/Serenity.exe, 667136 bytes. Exact command and evidence: docs/U03_HANDOFF.md.

## Test status
35/35 EditMode tests passed in 1.180 s: the prior 20 checks plus 15 U03 clock/calendar tests. Coverage includes x1/x2/x3/x5/x10, pause/resume, speed switching, calendar rollover, separate biological time, persisted-state continuation, large-step equivalence, invalid input and overflow atomicity. No U03 warnings or C# compile errors. Result: docs/validation/U03-tests.xml.

## Known blockers
None for U04. Biological acceleration defaults to 400x relative to calendar time per the explicit U03 requirement and remains configurable. The older FRS value of about nine real minutes per biological year implies a different coefficient and must be reconciled during Character/Dynasty balancing; pregnancy remains a separate future gameplay timer.

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
- Canonical game time is `GameTimeState` in Domain: integer `TimeSpan` ticks for calendar and biological elapsed time plus persisted speed and pause state.
- `GameClock` in Simulation is advanced centrally with an explicit real delta. At x1, 24 real minutes equal one calendar day; supported multipliers are x1/x2/x3/x5/x10.
- Pause returns zero advancement without changing clock values or selected speed. Biological time advances independently at a configurable default 400x calendar rate.
