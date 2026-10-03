# PROJECT_STATE.md

## Engine
Unity 6000.6.4f1 (12bfff696524), explicitly approved by user on 2026-10-03; URP 17.6.0.

## Current milestone
U00 DONE: URP environment initialized, Git/LFS configured, Windows Development build and clean-checkout verification passed.

## Last completed task
U00 - Среда, Unity-проект и Git.

## Active task
U01 - Архитектура assemblies и слоёв (next chat only; not started).

## Build status
Windows x64 / Mono / Development succeeded in the main workspace and clean clone of aa377bc. Both builds: exit 0, zero errors, two stripped debug shader warnings. Player: Builds/Windows/Serenity.exe.

## Test status
3/3 EditMode environment tests passed in both workspaces; no C# compile errors. Git LFS fsck passed. Exact commands, limitations and evidence: docs/U00_HANDOFF.md and docs/validation/.

## Known blockers
None for U01. No visual rendering check performed. Optional player auto-exit smoke check did not exit by itself and was stopped; startup had no logged exceptions. See handoff.

## Architecture facts
- C# primary language.
- Tier1: important/nearby interactive NPCs with GameObject presentation backed by persistent domain state.
- Tier2: simplified active-city NPCs via Unity Entities/DOTS.
- Tier3: distant population as aggregate data, no GameObject-per-person.
- Off-camera transitions deterministic; camera never rerolls world outcomes.
- Stone-age MVP first.
- U00 contains the stock URP sample and Editor-only environment/build checks. Gameplay and planned layer assemblies remain unimplemented.
