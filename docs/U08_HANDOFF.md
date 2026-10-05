# U08 — Building Grid and Blueprint Foundation

## Scope and preconditions
Work continued from `a7696f28c7ebb7c9e2d2a4b10f536009367b1cb8 U07: implement Tier 1 character presentation`. Before implementation the commit existed, U07 was DONE, `NEXT_TASK` selected U08 and U08 was TODO. The unchanged U07 gate passed: 130 core EditMode, 25 PostgreSQL integration, 6 full PlayMode, 29 U06, 4 U07 EditMode, 4 U07 PlayMode and Windows Mono Development build.

U08 implements only the first manual building-placement vertical slice. Resources, inventories, worker AI, production, wall/door/window tools, rooms, roofs, additional floors, damage/fire, global-map placement and full gameplay UI were not implemented.

## Domain structure and identity
`Game.Domain.Buildings` contains engine-free definition and instance data:

- `BuildingDefinitionId` is a validated lowercase ASCII content key such as `primitive_shelter`.
- `BuildingDefinition` contains display name, category, rectangular footprint, placement mode and overlap rule.
- `GridCoordinate` stores integer X/Z plus an explicit level for future floors/basements.
- `BuildingOrientation` stores North/East/South/West rather than Quaternion.
- `Building` owns the placed instance `StableEntityId`, definition ID, coordinate, orientation and `ConstructionState`.
- `BuildingState` is a detached data-only capture/restore boundary. Restore retains the same stable ID.
- `BuildingRegistry` is an ordinary session-owned canonical index and rejects duplicate IDs.

Definition identity and entity identity are intentionally unrelated. No prefab, GameObject, Transform, collider, renderer, ScriptableObject reference, Unity instance ID, object name or hierarchy path enters canonical state. Preview does not construct a `Building`, so it has no `StableEntityId`.

The existing U04 `SaveSnapshot` version 1 and PostgreSQL migration remain unchanged. U08 proves capture/restore of all building fields, including identity and construction state, but a world-level save DTO/schema migration remains a later explicit persistence task.

## Definition model and current buildables
`BuildingPresentationCatalog.asset` is the data-driven authoring/presentation adapter. It creates pure `BuildingDefinition` values and maps stable definition keys to project-owned U05A wrappers:

- `primitive_shelter`: Shelter, Grid, 4x3 cells, no overlap;
- `storage_basket`: Storage, Grid, 2x1 cells, no overlap.

The Storage Basket has no inventory behavior in U08. Both assets remain placeholders that can be replaced without changing placed building state. New grid definitions do not require a new C# building subclass.

## Placement service, grid, footprint and bounds
`BuildingPlacementService` belongs to Simulation and owns application rules. `Evaluate` rotates the rectangular footprint, enumerates every cell, rejects cells outside `BuildingGridBounds`, rejects occupied cells for non-overlap definitions and returns an explicit `PlacementEvaluation` with `PlacementFailureReason`. `Confirm` evaluates again, creates a new Domain building, reserves logical occupancy and registers the aggregate.

`BuildingOccupancyGrid` is the deterministic authority. Physics colliders are presentation/raycast aids only. The current scene uses a configurable 1 m grid, origin `(0,0,0)` and local level-zero bounds from -45 through 45 on X/Z. Presentation offsets prefab centers over their occupied rectangular cells, including rotated dimensions. `BuildingPlacementMode` already names Grid, Free and Edge; U08 supports Grid and rejects the other modes explicitly.

## Input mode and pointer integration
The existing `LocalGameplay.inputactions` asset gained one small Building map:

- B: toggle default Primitive Shelter placement;
- left mouse: confirm;
- right mouse or Escape: cancel;
- R: rotate clockwise.

Q/E remain exclusive camera-yaw bindings. `LocalGameplayInputSource` exposes building intents; no `KeyCode` checks exist in the controller. `LocalInteractionMode` is a session-owned non-singleton mode boundary. While building placement is active, `SelectionProbe` ignores primary-click selection, but `RtsCameraController` remains enabled and continues to use unscaled time.

`BuildingPlacementController` reuses `WorldPointerRaycaster`, so the U05 pointer-over-UI boundary remains authoritative. A valid ground update requires the raycast hit to have `BuildableGround`. There is no parallel camera/raycast pipeline.

## Preview, confirm and cancellation
Starting placement instantiates an unbound visual clone. All preview colliders are disabled, its hierarchy uses Ignore Raycast and no Domain aggregate, registry entry, occupancy or stable ID is created. Renderers use one reused `MaterialPropertyBlock` with green/red `_BaseColor`; shared source materials are not modified and no per-frame material instance is created.

The preview snaps to the grid, follows valid buildable-ground points and recomputes validity after R rotation. Invalid ground, bounds or occupancy displays red and cannot confirm. Cancel destroys only the preview, exits the building interaction mode and creates no canonical state.

Confirm creates Planned Domain state only after a valid second evaluation. Because U09 resources and U10 workers do not exist, the U08 controller then calls the explicit `MarkCompleted` transition and spawns the finished presentation. The transition is not permanently encoded as the only Domain state.

## Presentation spawning and respawn
`BuildingPresentationSpawner` receives an existing `Building`, resolves its prefab by definition ID, converts grid data to world position/rotation and binds a `BuildingPresenter`. It never invents Domain state. `BuildingPresentationRegistry` maps stable IDs to active presenters separately from `BuildingRegistry` and rejects simultaneous duplicate views.

Destroying/unbinding a presenter releases only the presentation mapping. Tests cover `Building X -> view -> unbind/destroy -> Building X remains -> new view`, with the same stable ID after respawn.

## Scene and generator integration
`LocalSceneCompositionRoot` explicitly constructs building registry, occupancy, placement service, presentation registry, spawner, interaction mode and controller alongside the existing U07 character flow. `LocalGameplay` remains the only build scene and its ground now has `BuildableGround`.

`U08BuildingFoundation` creates/validates the catalog, attaches presenter/root colliders to the two project-owned wrappers, rebuilds LocalGameplay and can capture a GPU validation image. Inherited U05A/U07 generators were made forward-compatible: if the U08 catalog exists, wrapper regeneration reapplies the U08 presenter/collider foundation instead of silently removing it.

## Automated tests
Eight U08 EditMode tests cover definition-key validation and separation from instance ID, rotated footprints, bounds, logical occupancy/overlap rejection, planned-to-completed capture/restore, duplicate-ID rejection, data-driven placeholder definitions and same-ID presentation respawn.

Three U08 PlayMode tests cover LocalGameplay composition with both U07 characters/camera intact, preview-without-identity plus cancellation, rotation, confirm/completion, presenter binding and overlap rejection. The inherited U05 input test now asserts the Building map and R binding while retaining Camera and Pointer bindings.

## Manual and visual validation
A GPU-rendered LocalGameplay validation image was generated at `Logs/U08-building-validation.png` and inspected. It shows the Primitive Shelter plus green and red preview examples on the ground without pink shaders. The final full PlayMode suite validates that LocalGameplay loads, the RTS camera composition remains present, the two Tier 1 characters still spawn, preview/cancel works, R rotation changes orientation, confirm creates one completed building, logical overlap blocks the second placement and mode returns to selection. Final searched logs contain no new compile, missing-script/reference, assertion, runtime-exception or shader-error diagnostic.

## Validation commands and results
Initial unchanged baseline:

```powershell
./Tools/Verify-U07.ps1 -ManagedTestCluster
```

Final U08 gate:

```powershell
./Tools/Verify-U08.ps1 -ManagedTestCluster
```

The verifier runs `U08BuildingFoundation.Build`, the complete inherited U07 chain, focused U08 EditMode and focused U08 PlayMode suites. Effective Unity commands include `-executeMethod Game.Infrastructure.Editor.U08BuildingFoundation.Build`, `-runTests -testPlatform EditMode -testCategory U08`, `-runTests -testPlatform PlayMode -testCategory U08`, the full non-PostgreSQL EditMode suite, real PostgreSQL integration, all PlayMode tests, U05A validation, U06/U07 focused suites and `-buildTarget Win64 -executeMethod U00Build.WindowsDevelopment`.

Final results on 2026-10-05:

- core EditMode excluding PostgreSQL: 138 passed, 0 failed, 0 skipped in 1.4111585 seconds;
- real PostgreSQL integration: 25 passed, 0 failed, 0 skipped in 15.275334 seconds;
- full PlayMode: 9 passed, 0 failed, 0 skipped in 0.5383939 seconds;
- focused U06 EditMode: 29 passed, 0 failed, 0 skipped;
- focused U07 EditMode: 4 passed, 0 failed, 0 skipped;
- focused U07 PlayMode: 4 passed, 0 failed, 0 skipped;
- focused U08 EditMode: 8 passed, 0 failed, 0 skipped in 0.1122532 seconds;
- focused U08 PlayMode: 3 passed, 0 failed, 0 skipped in 0.3466401 seconds;
- U05A asset generation/validation: success;
- Windows x64 Mono Development: success, errors=0, warnings=2; `Serenity.exe` 667136 bytes;
- Domain/Simulation UnityEngine dependency check and project asmdef cycle check: passed as part of core EditMode;
- PostgreSQL/Npgsql boundary: unchanged and all 25 integration tests passed.

The first two final-gate attempts exposed that inherited U05A wrapper regeneration removed newly attached U08 prefab components. The generator was corrected to reapply U08 foundation when the building catalog exists; the recorded final gate passed after that correction.

## Known limitations and deferred work
Only grid placement exists. Free placement and edge/socket wall placement are declared but unsupported. There is no grid overlay, slope/foundation analysis, uneven-terrain solution, room detection, walls/doors/windows tool, furniture behavior, roof logic, additional floor activation, basement excavation, saved player blueprint template, resource cost, hauling, worker scheduling, construction animation, damage/fire/collapse, removal tool or building gameplay UI.

U09 is next and has not started. It owns resources, inventories, storage behavior and real construction costs. U10/U11 own worker execution. U24 owns damage/fire/destruction. Future persistence work must add building states through an explicit versioned world-save schema rather than mutating U04 snapshot version 1 silently.
