# U28 Handoff — Deterministic Event System

## Status

U28 is DONE after all mandatory gates. U29 is the next unblocked task and has not started.

## Result

- Added one engine-independent, centralized event framework for standalone events and persisted multi-node chains.
- Added immutable validated definitions, stable typed targets/IDs, deterministic integer weighted selection/no-event bucket, exact canonical-tick cooldowns, typed player commands, deterministic AI resolution and invalid-target policies.
- Added keyed deterministic instance/transition selection, exactly-once node/effect markers, a 32-transition immediate budget, duplicate-active suppression and bounded 128-entry history with archive count/checksum.
- Added four representative Stone Age definitions: a three-node player family chain, a standalone player family event, an off-camera AI injury using U18 and an AI narrative event.
- Integrated one scheduler into normal `LocalSceneCompositionRoot`, snapshot/XML/PostgreSQL metadata v14, `SaveCoordinator`, U27 Events UI, Character inspector and notification feed.
- Loading v1-v13 clears U28 state at the restored tick. V14 detached restore validates identity, graph, targets, choices, markers, cooldowns and seed before commit and never reapplies outcomes.

## Main files

- `Assets/Game/Domain/Events/EventModels.cs`
- `Assets/Game/Simulation/Events/EventService.cs`
- `Assets/Game/Simulation/Events/CanonicalEventAdapters.cs`
- `Assets/Game/Domain/SaveSnapshot.cs`
- `Assets/Game/Simulation/SaveCoordinator.cs`
- `Assets/Game/Infrastructure/SaveXmlCodec.cs`
- `Assets/Game/Infrastructure/LocalSceneCompositionRoot.cs`
- `Assets/Game/Presentation/UI/GameplayUiContext.cs`
- `Assets/Game/Presentation/UI/GameplayUiController.cs`
- `Assets/Game/Tests/Editor/EventSystemTests.cs`
- `Assets/Game/Tests/PlayMode/EventSystemPlayModeTests.cs`
- `Tools/Verify-U28.ps1`

## Determinism evidence

The U28 EditMode suite compares canonical state hashes rather than presentation state. The same seed/state converges for direct catch-up versus incremental steps, reversed definition registration and reversed target enumeration. Snapshot v14 round-trip preserves the complete event state hash. Repeated advancement at the same tick is hash-stable, and another seed is proven capable of changing the weighted result. The scale case processes 100 AI targets across 50 checkpoints with no active-queue growth beyond policy and bounded history.

## Validation evidence

Final command:

```powershell
& .\Tools\Verify-U28.ps1 -ProjectPath C:\serenity_game -FullRegression -GpuValidation -BuildPlayer
```

- Compile/import: passed.
- U28: 20/20 EditMode, 4/4 headless PlayMode and 4/4 Direct3D GPU PlayMode.
- Required dependency regressions: 72/72 EditMode and 12/12 PlayMode.
- Persistence: 35/35.
- Full suites: 391/391 non-PostgreSQL EditMode and 86/86 PlayMode.
- PostgreSQL: all 25 integration tests discovered; 0 passed, 0 failed, 25 skipped because `DYNASTYGAME_PG_TEST_DATABASE` was absent. V14 codec and metadata whitelist unit paths passed.
- Windows x64 Mono Development build: succeeded; errors=0, two inherited BuildReport warnings; `Builds/Windows/Serenity.exe` exists.
- Every verifier log had zero compiler warnings, compiler errors, unhandled exceptions, null-reference exceptions, failed assertions and duplicate-AudioListener diagnostics. The two BuildReport warnings are the established inherited build-summary warnings.
- `git diff --check`: passed with only Git line-ending notices.

## Persistence compatibility

`SaveSnapshot.EventVersion` and `CurrentVersion` are 14. The strict event payload contains seed/checkpoint/sequence, active chain continuation, pending choices, targets, markers, cooldowns, bounded history and archive summary. Versions 1-13 remain readable. The PostgreSQL relational schema is unchanged because the payload remains opaque XML.

## Known limits

U28 provides Character selectors and U18 relationship/health adapters; additional typed target kinds need adapters to their existing authorities. The event history is intentionally bounded and is not the full historical chronicle. A manual native Player click-through session was unavailable and is not claimed; headless and GPU PlayMode exercised the same UI choices, input blocker, save/load and notification paths.

## Boundary for U29

U29 may measure and optimize the existing centralized scheduler as part of the formal 100→300→500→1000 gate. It must preserve U28 stable ordering, keyed RNG, exactly-once semantics, bounded transitions/history and canonical service boundaries. U29 was not started here.
