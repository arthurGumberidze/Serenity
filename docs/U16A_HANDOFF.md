# U16A HANDOFF — Debug Gameplay Controls / Usability Patch

Date: 2026-10-07
Status: DONE for implementation and automated validation; U17 is not started and awaits review. Interactive manual clicks are not claimed because the Windows Computer Use helper failed before exposing an application window.

## 1. Summary

U16A is a small reuse-first patch over U05/U08/U10/U11/U15/U16. It removes hardcoded demo research labor, adds selection-driven research commands and truthful Assigned/Active reporting, exposes existing building definitions through a research-aware debug selector, and wires the one `GameClock` to conflict-free Input System time controls. It adds no new research, placement, job, production or time authority.

## 2. Files added

- `Assets/Game/Simulation/Research/ResearchWorkerStatus.cs` and metadata.
- `Assets/Game/Presentation/Buildings/BuildingDebugSelector.cs` and metadata.
- `Assets/Game/Presentation/Input/TimeDebugOverlay.cs` and metadata.
- `Assets/Game/Tests/Editor/U16ADebugGameplayControlTests.cs` and metadata.
- `Assets/Game/Tests/PlayMode/U16ADebugGameplayControlPlayModeTests.cs` and metadata.
- `docs/U16A_HANDOFF.md`.

## 3. Files modified

Composition, runtime/input/presentation: `LocalSceneCompositionRoot.cs`, `Tier1AiRuntimeDriver.cs`, `Tier1AiDebugOverlay.cs`, `BuildingPlacementController.cs`, `LocalGameplayInputSource.cs`, `LocalGameplay.inputactions`, `ResearchDebugOverlay.cs`.

Simulation: `ResearchService.cs`, `ConstructionFundingService.cs`.

Regressions: `U05CameraAndInputTests.cs`, `BuildingPlacementPlayModeTests.cs`, `ProductionPlayModeTests.cs`, `ResearchPlayModeTests.cs`, `ResourceGameplayPlayModeTests.cs`, `WorkGroupPlayModeTests.cs`.

Protocol: `PROJECT_STATE.md`, `NEXT_TASK.md`, `TASK_GRAPH.md`, `DECISIONS.md`, `ARCHITECTURE.md`.

## 4. Research assignment

LocalGameplay no longer calls `AssignWorker` for Aren/Mira. The player selects one or more existing Tier 1 presenters and uses `Assign selected to Research` or `Remove selected from Research`. `ResearchService.AssignWorkers`/`UnassignWorkers` are the minimal stable-ID, distinct, ordered batch API; repeated assignment is a no-op.

## 5. Assigned

Assigned is exactly `ResearchService.AssignedWorkers`. The panel lists up to three character display names plus short stable IDs and reports the canonical count.

## 6. Active now

`ResearchWorkerStatusQuery` intersects Assigned IDs with authoritative `Tier1AiAgentRegistry` state. A worker is Active only while `CurrentAction == UtilityActionKind.Research` and `Phase == AiActionPhase.Researching`, and only while a project is active. Thus Active is always less than or equal to Assigned. Panel rate/ETA uses Active workers; a running project with zero Active workers explains that progress is paused.

## 7. Building selector

`B` raises the existing placement controller's debug-menu hook. `BuildingDebugSelector` enumerates `BuildingPresentationCatalog.Entries`, creates no second catalog and delegates an unlocked choice to `BuildingPlacementController.TryStartPlacement`.

## 8. Building IDs

The current catalog yields `primitive_shelter`, `storage_basket` and `primitive_crop_plot`. Availability follows U16 starting knowledge and completed technologies; U16's restricted starting knowledge was not rebalanced.

## 9. Lock reasons

For each unavailable definition the selector resolves the U16 technologies whose typed building unlock targets that exact ID, then shows a reason such as `LOCKED - requires primitive_agriculture`. The placement controller independently checks `ConstructionFundingService.IsBuildingUnlocked` and records `LockedByResearch`, so a direct programmatic request cannot bypass the gate.

## 10. Placing primitive_crop_plot

Complete `controlled_fire`, `fiber_working`, `stone_tools` and then `primitive_agriculture`; press `B`; choose Primitive Crop Plot; move the existing ghost; left-click confirms; `R` rotates; Esc/right-click cancels. Placement, occupancy, construction funding and U15 crop-site activation remain the existing U08/U09/U15 path.

## 11. Time controls

- Space: pause/resume.
- Minus: next slower speed.
- Equals or Plus: next faster speed.
- Ordered speeds: x1, x2, x3, x5, x10; stepping clamps at both ends.

## 12. U11 hotkey conflict

Digits 1–9 were deliberately not bound by the Time map. Existing U11 work-group recall/assignment shortcuts are unchanged. `U05CameraAndInputTests` asserts the Time map has no digit bindings.

## 13. Pause semantics

`TimeDebugOverlay` calls `Tier1AiRuntimeDriver.SetPaused`/`SetSpeed`; the driver changes the single `GameClock`. While paused, the clock returns zero delta and the existing driver publishes no effective time to the centralized Tier 1 scheduler or Tier 2/Tier 3/production/research consumers. Tests prove clock tick, AI update count, research hash and production hash remain unchanged, then advance after resume without a duplicate catch-up path.

## 14. Time overlay

The development-only panel shows calendar year/day/hour/minute, speed, paused state and clickable Pause/Resume, Slower and Faster buttons plus the actual hotkey legend.

## 15. Layout

The selected-character inspector moved below the top-left resource area. Time sits bottom-left; the building selector sits bottom-center; research remains central. At the validation resolution of 1600x900 the critical controls have distinct blocking regions. This is intentionally not the U27 production HUD.

## 16. Click-through

Research, building and time surfaces implement `IWorldPointerUiBlocker`. Placement confirmation now checks the same `WorldPointerRaycaster` UI boundary. PlayMode tests verify a time-panel click neither confirms the building behind it nor clears selection; the expanded U11 regression verifies all relevant overlays register their regions.

## 17. Tests added

Four U16A EditMode tests cover idempotent assignment/unassignment, Assigned versus Active, catalog-backed building locks/reasons/unlock, and speed sequence/no digit conflict. Three U16A PlayMode tests cover no bootstrap assignment plus selection commands, locked/unlocked crop placement through the real controller, and pause/resume across clock/AI/research/production plus UI click-through.

## 18. Exact focused/full results

- `Logs/U16A-focused-edit-final.xml`: 4 passed, 0 failed, 0 skipped.
- `Logs/U16A-focused-play-final.xml`: 3/3.
- `Logs/U16A-full-edit.xml`: 237/237 non-PostgreSQL EditMode.
- `Logs/U16A-full-play-passed.xml`: 42/42 PlayMode.

Unity was invoked with the pinned editor through a timeout-aware `Start-Process -Wait` wrapper. The semantic commands were:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile '<log>' -runTests -testPlatform EditMode -testCategory U16A -testResults '<xml>'
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile '<log>' -runTests -testPlatform PlayMode -testCategory U16A -testResults '<xml>'
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\U16A-full-edit.log' -runTests -testPlatform EditMode -testResults 'C:\serenity_game\Logs\U16A-full-edit.xml'
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\U16A-full-play-passed.log' -runTests -testPlatform PlayMode -testResults 'C:\serenity_game\Logs\U16A-full-play-passed.xml'
```

## 19. Regressions

- U05 exact class filter: 5/5 EditMode.
- U08/U10/U11/U15/U16 combined category filter: 63/63 EditMode.
- U08/U10/U16 combined category filter: 13/13 PlayMode.
- U09: 4/4 PlayMode; U11: 10/10 PlayMode; U15: 3/3 PlayMode.
- The full 42/42 PlayMode run also contains every U08/U10/U11/U15/U16 PlayMode fixture.

Commands used `-testFilter Game.Tests.U05CameraAndInputTests`, `-testCategory 'U08;U10;U11;U15;U16'`, `-testCategory 'U08;U10;U16'` and separate `-testCategory U09`, `U11`, `U15` runs with the same editor/project/result pattern above.

## 20. GPU and manual validation

GPU command (no `-nographics`):

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\U16A-gpu-play.log' -runTests -testPlatform PlayMode -testCategory U16A -testResults 'C:\serenity_game\Logs\U16A-gpu-play.xml'
```

Result: 3/3 on Direct3D 11, Intel Arc Graphics. Both Windows Computer Use initialization attempts failed before a target window existed (`windows sandbox failed: helper_unknown_error`, then kernel reset). Therefore no manual-click claim is made. Headless and GPU tests cover the requested runtime control/placement/pause/click-through paths.

## 21. Windows build

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\U16A-build.log' -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment
```

Result: `Succeeded; errors=0; warnings=2`. `Builds/Windows/Serenity_Data/globalgamemanagers` was refreshed by the successful Windows x64 Mono Development build.

## 22. Console/log inspection

Successful test/build logs contain no C# compiler error, failed assertion, missing script, runtime exception or shader failure. Unity emitted the existing package asmref notices, a non-blocking licensing access-token message despite resolving the entitlement, two Entities SerializationWeaver function-pointer notices, and two BuildReport warnings without a new gameplay warning. The build exited 0.

## 23. Documentation

`PROJECT_STATE.md`, `NEXT_TASK.md`, `TASK_GRAPH.md`, `DECISIONS.md` and `ARCHITECTURE.md` record U16A, the player-controlled assignment model, Assigned/Active distinction, disposable selector, actual hotkeys, one-clock pause coordination and U17 review boundary.

## 24. Diff checks

`git diff --check` passed before staging (line-ending conversion notices only). `git diff --cached --check` and the staged review are recorded after staging in the final handoff response.

## 25. Commit

Required message: `U16A: add debug gameplay controls`. The resulting hash is reported in the final response.

## 26. Known limitations

- Interactive manual clicks were unavailable because the Windows Computer Use helper failed twice; they are not claimed.
- The controls are debug IMGUI and intentionally temporary until U27.
- U16 starting knowledge remains only `basic_survival` and `gathering`; shelter/storage/crop are initially locked until their canonical technologies complete.
- The crop plot keeps its U15 placeholder visual; no art or balance work was added.

## 27. Definition of Done

All implementation, automated regression, GPU and Windows build items are satisfied. The manual-click protocol item is explicitly substituted with the requested headless/GPU evidence because the helper was unavailable. U17 is not started. Review U16A before authorizing the next task.
