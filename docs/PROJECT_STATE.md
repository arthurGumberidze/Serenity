# PROJECT_STATE.md

## Engine
Unity 6000.6.4f1 (12bfff696524), approved override D-008; URP 17.6.0; Windows x64 Mono Development.

## Current milestone / last completed task
U08 DONE — Building grid and blueprint foundation. LocalGameplay now supports a data-driven Primitive Shelter placement vertical slice with preview, snapping, rotation, deterministic logical occupancy, confirm/cancel, completed building projection and stable-ID respawn.

## Active task
U09 — Resources, inventories and storage is next and has NOT started.

## U08 building foundation
- `BuildingDefinitionId` is a validated lowercase content key and is distinct from every placed instance's GUID-backed `StableEntityId`.
- `Building`, `BuildingState`, `GridCoordinate`, quarter-turn `BuildingOrientation`, rectangular `BuildingFootprint` and `ConstructionState` are pure Domain types with no Unity references.
- `BuildingPlacementService` evaluates the rotated footprint against local build bounds and a logical `BuildingOccupancyGrid`; physics overlap is not canonical occupancy.
- `BuildingRegistry` owns canonical session instances and rejects duplicate stable IDs. Capture/restore preserves ID, definition, coordinate, orientation and planned/completed state.
- `BuildingPresentationCatalog` is a ScriptableObject authoring adapter for `primitive_shelter` (4x3) and `storage_basket` (2x1). Prefabs and catalog object identity are not saved state.
- `BuildingPresentationSpawner`, `BuildingPresenter` and `BuildingPresentationRegistry` are disposable projections. Despawn/respawn retains the same aggregate and ID.
- `BuildingPlacementController` is one scene-level Update. It reuses U05 `WorldPointerRaycaster` and its UI block, accepts `BuildableGround`, snaps to a configurable 1 m grid, rotates by R, confirms with left click and cancels with right click/Escape. B toggles the default shelter preview.
- Preview clones have no Domain aggregate or StableEntityId, disable colliders, use Ignore Raycast and display valid/invalid state through MaterialPropertyBlock tint without changing source materials.
- `LocalInteractionMode` prevents selection from consuming build clicks; the unscaled RTS camera remains active. Q/E continue to rotate the camera.
- Confirm creates Planned state and immediately completes it for the U08 resource-free slice. U09 must replace this with real resource requirements; worker AI remains deferred.

## Preserved architecture and scope
- `Game.Domain` and `Game.Simulation` remain `noEngineReferences`; building position uses integer grid data and rotation uses an enum, not Vector3/Quaternion/Transform.
- U04 `SaveSnapshot` version 1 and PostgreSQL schema are unchanged. `BuildingState` is the explicit extractable state boundary for a later world-save schema task.
- LocalGameplay remains the sole enabled player scene and still spawns the two U07 Tier 1 demo characters.
- No resource economy, inventory behavior, worker construction AI, production, rooms, doors/windows, walls, multi-floor construction, destruction/fire, global map or gameplay UI was implemented.

## Validation status
`Tools/Verify-U08.ps1 -ManagedTestCluster` passed on 2026-10-05:

- 138/138 core EditMode tests passed;
- 25/25 real PostgreSQL integration tests passed;
- 9/9 full PlayMode tests passed;
- 29/29 focused U06 tests passed;
- 4/4 focused U07 EditMode and 4/4 focused U07 PlayMode tests passed;
- 8/8 focused U08 EditMode and 3/3 focused U08 PlayMode tests passed;
- U05A generation/validation passed and preserves the U08 presenter/collider additions during inherited regeneration;
- Windows x64 Mono Development build succeeded with errors=0 and 2 inherited warnings; `Serenity.exe` is 667136 bytes;
- GPU validation image confirmed the Primitive Shelter and green/red preview colors on LocalGameplay ground; automated PlayMode checks cover camera/characters, preview/cancel, rotation, confirm and overlap rejection;
- logs contain no new compiler error, failed assertion, missing-script/reference diagnostic, runtime exception or shader error.

## Next action
Start only U09 from `docs/NEXT_TASK.md`. U09 has not been implemented.
