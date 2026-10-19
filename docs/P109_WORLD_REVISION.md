# P10.9 world revision — user brief, 2026-10-19

Status: definition/grid foundation implemented and tested; loaded Terrain generation, gameplay integration and visual acceptance remain pending. This is not a completed 12 km playable world.

## Authoritative reference legend

The user's later explicit explanation replaces the earlier yes/no confirmation of an incorrect legend.

| Reference marking | Meaning in gameplay |
|---|---|
| Yellow rectangles | Land suitable for agricultural fields; **not settlement territory** |
| Blue rectangles | Hunting areas with animals |
| Purple rectangles | Berry-gathering areas; may overlap hunting areas |
| Red strip | One continuous river spanning the map |
| Red circles | Lakes containing fish |
| Grey areas | All settlements, including the player's settlement |
| Green trees | Areas with a higher frequency/density of trees |
| Pink areas | Stone, iron, gold and other ore deposits |

Markings are authoring semantics, not coloured debug rectangles to render in the finished game. Preserve mixed areas and transition zones instead of treating overlapping hunting/berry designations as mutually exclusive biomes.

## Scale and persistence

- Exactly 12 × 12 logical tiles, each 1000 × 1000 metres; total 12,000 × 12,000 metres, 144 km².
- Separate Terrain chunks rather than one enlarged Terrain. Neighbour borders must use identical samples, compatible materials and connected navigation.
- New geography applies to new games only. Existing save files remain untouched. Loading an incompatible old generation must not apply its coordinates to the new generation or overwrite its slot.
- The player explicitly selected the **lower, near-central grey settlement** as the starting settlement. The reference definition uses origin (-6840, -1440) so that this settlement retains convenient near-zero starting coordinates; the map is still exactly 12,000 m on both axes.
- Canonical world generation/version, tile coordinates, resource identities and zone definitions belong to Domain/Simulation/configuration. Loaded Terrain, renderers and colliders are disposable views, not save-state.
- Use the already approved assets; no new paid/external dependency or Unity version change is authorized.

## Implemented foundation

`TiledWorldDefinition` is engine-free immutable configuration: tile identity, half-open world bounds, shared heightmap sample coordinates, overlapping activity masks, and deterministic required-tile selection retaining physical actors separately from the camera. `TiledWorldDefinitionLoader` loads the versioned JSON at `Assets/Game/Resources/World/P109TiledWorld.json`: 144 tiles, 3 settlements, agriculture/hunting/berry/forest/ore/fishing zones and a continuous north-to-south river control polyline. This resource is **not yet activated by LocalSceneCompositionRoot**; no old-generation constant or save file was changed.

Eight EditMode cases cover exact 144 km² area, every shared edge at 129/257/513 sample resolutions, unique ownership of tile borders, outside/invalid coordinates, independent physical-actor retention, overlapping zone semantics and the requested starting-settlement position. These prove coordinate contracts, not generated height equality, actual Terrain loading, navigability or finished visuals.

## Migration work still required

The current implementation is tightly coupled to a 512 m square: `ConnectedWorldContract`, discovery's hard-coded 32 × 32 grid, strategic-camera framing, a single Terrain reference in placement/ground projection, 512 m NavMesh volume, building bounds and fixed resource/district positions. Do not change only `TerrainData.size` and call the migration complete.

1. Version the new world definition and enforce the new-game/old-save boundary before attaching views.
2. Define all 144 tile coordinates and zone masks from the reference; generate heights in shared world coordinates so borders agree independently of load order.
3. Add bounded tile loading and unloading, keeping terrain/collision/navigation available for physical actors, not only for the camera. Preserve persistent state outside loaded tiles.
4. Replace single-Terrain height/placement queries and fixed bounds. Connect river/lake shorelines, crossings and routes across tiles.
5. Populate resource, hunting, fishing and agricultural zones throughout the world, with stable identities and gameplay availability matching the visible object/area.
6. Strategic overview uses the same geography and discovery state, with a lower-detail view; it must not load all high-detail vegetation or create a second simulation.
7. Test tile seams, corner/boundary crossing, loading-order determinism, resource/zone coverage, camera travel, collision/NavMesh continuity, Save/Load of the new generation and rejection of the old generation without file mutation. Build and inspect normal Player before any completion claim.

### Activation constraints

- Keep legacy geography/version explicit while bringing up the new scene projection. Do not globally change the old 512 m constants before every consumer has a layout-aware equivalent.
- A camera-centred Terrain ring alone is insufficient: residents and queued jobs may be elsewhere. Physical actors need retained terrain, and long-distance commands need continuous navigation or the established simulation-tier transition path even when intermediate visual tiles are unloaded. Do not disable remote jobs merely because only the origin and destination tiles are visible.
- Save slot IDs are currently reused across worlds. Activating v2 must isolate its default storage path/generation and reject incompatible loads before state rebind; a generation label in JSON alone does not protect old files from a later Save command.
- Height, collision, buildability, resource-site positions and strategic overview must derive from the same world definition. A low-detail overview is permitted as a view, not as an independent map or alternate authority.

## Video-repair checkpoint

The repair code changes strategic camera ownership and fog clipping, shader-level SpeedTree billboard exclusion (including base-region trees), mouse-edge panning, TPS target rotation inheritance, and feedback for LMB attempts. Three focused PlayMode checks passed before the pause. The expanded run was interrupted; after resuming, a 19/20 result exposed a legacy test asserting the defective camera-parenting relationship. That assertion was replaced with independent target ownership plus follow-position continuity after Save/Load. The final affected PlayMode suite passes 20/20; EditMode including the grid foundation passes 21/21. These are **not** evidence of a playable 12 km world, visual acceptance or physical mouse-input acceptance.

The existing `GameplayUiController.HasContextPath` edit and `P109FunctionalIntegrationPlayModeTests` were present before this repair work and must not be accidentally discarded or silently attributed to it. The Unity test runner also changed a vendor Cypress material's render queue and left an `InitTestScene` after interruption; inspect and clean up only those confirmed generated side effects.

Intermediate build `Builds/P109VideoFixes/Serenity.exe` completed successfully with 0 errors / 44 warnings and immutable-source verification. The importer-only material edit was restored; the interrupted test scene is retained recoverably under `Artifacts/P109/video-fixes-20261019`. This build remains on the old playable geography. See `P109_HANDOFF.md` for exact evidence/provenance.
