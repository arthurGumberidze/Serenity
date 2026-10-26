# DECISIONS.md

## D-190 WM15 foliage repair reuses mesh LODs without changing world density (2026-10-24)

Rendered build-e isolation measured GPU means of 81.875ms for the normal scene,
75.894ms with terrain basemaps, 77.414ms without shadows, 20.448ms without foliage,
and 74.052ms without skinned characters. These 10s/30s smoke runs are diagnostic only.
Their original wrapper reports retain a cross-host Get-FileHash failure; independent
SHA-256 and focus checks are recorded in `render-e-integrity.json`. The wrapper hash
implementation now uses .NET and passes both Windows PowerShell and PowerShell 7 probes.

The current compact world reuses the already reviewed WM05 mesh-only variants of the
same four Meadow/Birch models. This is asset reuse, not activation of the proposed V2
generation. LOD0 still determines exact normalization and trunk placement. Distinct
mesh stages use no wind, dithering, overlapping cross-fades or billboard stages. Tree
count, canonical source IDs, material mapping, colliders and world layout remain intact.
This supersedes D-156's temporary LOD0-only rendering choice, subject to rendered checks.

Terrain tiles omit only material layers whose sampled weights are all exactly zero.
Every positive weight keeps its original material and value. Thirteen of sixteen compact
tiles use fewer than five layers. Height, alphamap and basemap resolution, draw distance,
render scale, shadow quality, population/tier mix and performance thresholds stay unchanged.

## D-189 WM15 repair separates current contracts, defects and measurement evidence (2026-10-24)

Original night artifacts are immutable. Repair selects exact failed NUnit fullnames and
separately repeats cleanup/protection regressions and failed rendered population gates.
Valid completed performance evidence above the threshold is FAIL; missing/incomplete
evidence is ERROR. Owned-process cleanup diagnostics cannot replace NUnit's assertion
verdict, and unresolved owned processes block the next runtime launch. Only the exact
root-level generated TestResults.xml is excluded from protected-save inventory; real save
changes, deletions and additions remain failures.

Historical assertions must follow approved current contracts: ten nearby starter residents,
authored expanded catalogs, current generation/profile, temporary CarryLoad inventories,
worker-driven construction and admitted sources on canonical tiled ground. This does not
permit weakening conservation, identity, navigation, role/capability or save-roundtrip checks.
Actual integration defects are repaired in runtime code: explicit research assignment must
survive NeedsOnly policy, replacement presenters must remove stale scheduler state, scene
unload must tolerate destroyed cameras, and manual movement must accept TerrainCollider.
Starter outcrops register finite ecology state using the existing Production/Ecology services.
Strategic resource commands route to DirectWorkCommandService in the same physical world.
Placement cancellation records its rendered frame so later handlers of the same click cannot
create a movement order. Selected residents use the modern context-command path; an explicitly
selected developer group with no individual selection retains physical-only group verdicts.
Tiled presentation caches source components and only announces actual tile-visibility changes;
canonical simulation callbacks still refresh resource state, and save-load clears the view cache.

Unsupported Mono thread allocation counters report unavailable and -1, never measured zero.
Render isolation modes require explicit diagnostic smoke configuration and are rejected by
the mandatory gate validator. They cannot qualify a performance acceptance result. Density,
tier mix, rendered resolution, 33.33ms p95 and 3 x (60s + 300s) durations remain gate constraints.

## WM15 runner decision — independent evidence and bounded process ownership (2026-10-24)

The user requested an unattended local runner that continues after failures and reports concise progress.
Each test fixture is an independent Unity invocation with a timeout and durable result; assertion failures,
malformed/missing XML and crashes cannot stop later fixtures. Only missing-XML infrastructure failures are
retried. Build-dependent Player checks are explicitly BLOCKED if a fresh provenanced build is unavailable.
Process termination traverses only descendants of the process created by the runner, using a native snapshot
and creation-time checks; it does not terminate another Editor or an unrelated Player. A named project mutex
prevents duplicate runners, and a held UnityLockfile blocks launch without closing the existing Editor.

This reuses the existing build, U31 smoke and U29 performance authorities. No art, new Unity package or second
gameplay authority is introduced. Save overrides are isolated and protected inputs are SHA-256 checked before
and after. Automatic success is called AUTOMATED_PASS; user visual acceptance remains PENDING. Current Player
evidence is labelled compact-world evidence because the proposed V2 profile is not activated by this tooling.

## D-188 WM14 profiles authored scale before changing content (2026-10-24)

WM14 keeps the existing tree/detail counts, terrain draw distances, 650 interactive-collider cap and wildlife
tier budgets because captured 100/300/500 population evidence already meets the 30 FPS minimum with bounded
memory. Content density is therefore not reduced to manufacture a faster benchmark. The reusable short profile
records exact hybrid tier counts, frame percentiles, available CPU timing, memory deltas, scheduler work, tree
LOD/collider facts and wildlife tiers in JSON. Its five-second Editor samples are a regression smoke, not a
replacement for WM15 Player/GPU and visual acceptance.

An explicitly authored benchmark population temporarily suppresses automatic camera-distance tier migration;
the suppression defaults off and is restored on benchmark teardown. This isolates measurement intent without
creating another simulation authority. Character visual LOD uses horizontal XZ distance because tactical-camera
altitude is not world separation; the existing eight-presenter cap still bounds Animator/NavMesh cost. Detailed
visible residents retain the D-157 idle/work animation behavior, while lightweight residents disable Animator
evaluation. World Map V2 remains inactive and no package or asset is added.

## D-187 WM13 isolates World Map V2 persistence behind an inactive profile (2026-10-23)

Save format v30 belongs only to the explicit `p109-world-map-v2-proposed` profile. The active compact-world
profile deliberately remains v29 with save generation `p109-stone-age-buildings-v1` and the unchanged
`StoneAgeBuildingsV1/Saves` path. The V2 profile uses its generation ID as the save-generation ID and writes
under `WorldMapV2Proposed/Saves`. `LoadReference()` and the main menu still select the current profile, so
declaring the V2 profile is not world activation. Unknown generation/profile combinations fail closed.

The existing save authorities already cover mutable resource/fish stock and ecology baseline, buildings,
work/clearance, revealed discovery cells and event history. Hidden deposits are immutable undiscovered plan
entries until a later discovery feature creates a canonical mutation path; WM13 does not invent mutable
shadow state for them. WM12 adds no mutable buildability state. Therefore v30 adds only the missing WM08
`WildlifeWorldState`: individuals, lifecycle cursor/birth sequence and per-species/region hunting pressure.

`SaveCoordinator` rejects a mismatched save generation, connected-world generation or missing V2 wildlife
payload before clock replacement or service restoration. The XML codec remains strict and includes the new
section only at v30; the opaque PostgreSQL store accepts the same version. Startup compatibility diagnostics
show expected and actual generations. Separate-path SHA-256 tests and the protected repository manifest prove
that V2 writes do not overwrite current saves. No visual asset or package is required for a persistence-only
boundary.

## D-186 WM12 keeps buildability as policy over existing placement and work authorities (2026-10-23)

`WorldMapBuildabilityDefinition` retains the authored V2 zone, slope, grid and blocker taxonomy, while one
pure `WorldMapBuildabilityEvaluator` combines it with the shared height/hydrology and POI contracts. Matching
restricted masks are hard red for ordinary construction; overlapping allowed masks use their point-local
strictest slope ceiling, and ordinary land outside a named mask uses the specified 18-degree limit. Exact
POI circle-versus-cell checks are data-derived, so protection does not depend on a streamed collider being
loaded. Unknown physical blockers fail closed. A mask marked `requiresClearing` does not fabricate an amber
result: amber exists only when a real bound, non-depleted canonical source is present.

`BuildingPlacementService` remains the occupancy/grid/territory authority and `P102WorldPlacementSurface`
remains its physical adapter. The adapter never clears a source during preview or confirm. It passes only
stable source IDs to the existing construction transaction, and `ConstructionClearanceCoordinator` creates
normal exclusive Chop/Mine/Gather work. Depletion stays in `ProductionService`; output and construction
materials continue through physical inventory/haul paths before Build begins. The coordinator repeats the
authored allow-list check on registration and load derivation, making a bypass attempt a fail-closed error.

WM12 adds no mutable buildability state or save field. Existing construction buildings, buffers, work orders,
resource sites and occupancy remain canonical, so current save/load reconstruction is sufficient. The policy
is selected only when `TiledWorldDefinition.MapData.Buildability` exists; the active compact generation has no
such data and keeps its legacy behavior. Existing work indicators, staged building visuals, stone axe and
tree/rock presenters are sufficient; no art, material, VFX or package was added.

## D-185 WM11 derives discovery from existing revealed cells and event history (2026-10-23)

`WorldMapPoiPlanner` is the sole immutable POI projection. It preserves the nine authored coordinates,
regions, purposes and protection radii, derives stable IDs from world seed/generation/POI ID, and computes a
dry slope-bounded last-mile route from the existing WM09 network to each protection circle. WM09 remains the
navigation authority; the approach plan proves local terrain reachability and introduces no second pathfinder,
agent scheduler or mutable route state.

`ConnectedWorldService` revealed cells remain the canonical discovery state. `WorldMapPoiDiscoveryAdapter`
owns no collection: it derives discovery from those saved cells and uses existing `EventService` active
instances/history as the exactly-once record. Each POI has one explicit player event targeted by stable ID.
The existing connected-world/event XML payloads therefore provide round-trip persistence without a save
version bump; WM11 only permits the already-defined proposed generation ID in connected-world state. WM13
still owns generation activation, compatibility policy and broader world-version persistence.

POI GameObjects, colliders and imported prefab compositions are streamed disposable presentation. The
protection component performs exact circle-versus-placement-footprint checks and always rejects automatic
clearance. The asset catalog references only existing project content; stone circle, grove and glade are
compositions of imported prefabs rather than generated models. No ScriptableObject owns mutable discovery,
no package is added and the compact-world branch never constructs the V2 POI plan.

## D-184 WM10 extends the population/New Game plan without creating a settlement authority (2026-10-23)

The authored player-start metadata, `build_start_core` and three expansion directions load into immutable
`WorldMapStartTerritoryDefinition`. `WorldMapStartTerritoryPlanner` is a pure seeded derivation owned by the
existing `TiledWorldPopulationPlan`; it validates dry/slope/height/buildability footprints, stable starter
object identity, ten resident positions, the complete start-resource guarantee and ordered corridors. The
core uses the authored 8-degree construction limit. Expansion corridors use the already accepted 30-degree
WM09 footpath limit because they express future access, not immediately placeable building foundations.

Prepared territory is one shared exclusion contract for WM05 vegetation and WM06 resources. Resource nodes
also keep a two-metre safety buffer around the 8 m corridors so a valid seeded plan cannot later fail its
blocker audit. The start-to-river contract references the existing WM09 route/crossing and existing tiled
NavMesh; WM10 introduces no navigation manager, mutable settlement state or save payload.

`NewGameBootstrap.Create` optionally receives the population plan and publishes the same immutable territory
beside its existing ten characters. Calls without that input retain the exact legacy result. All starter
buildings, resource visuals and nature coverage reuse current catalogs, and no composition root selects the
proposed generation. Activation and persistence remain later gated work.

## D-183 WM09 derives walkable road geometry without creating a second navigation authority (2026-10-23)

`WorldMapRoadDefinition` is immutable authored data and `WorldMapRoadNetworkPlanner` is the pure deterministic
projection authority. It densifies all seven canonical splines against the shared height/water sampler, moves
incidental water samples to a dry bank clearance, clamps the physical longitudinal profile to the authored
road/path grades and derives the eight-region reachability matrix plus crossing/chokepoint link plans. These
values are neither mutable simulation nor save state.

The existing per-tile `NavMeshSurface` remains authoritative. V2 route renderers receive narrow generated
collider ribbons as bake input; a ribbon segment is omitted whenever its interior touches water and it is not
inside one authored crossing span. Exactly three crossing links plus the non-duplicate ridge-pass and marsh-
causeway links bridge constrained geometry. This reconciles canonical road splines that touch/cut WM03 water
without inventing a fourth crossing or making decorative meshes authoritative. V2 water itself is a Not
Walkable collider source.

Presentation reuses existing imported assets and the established earth-route material. Imported decorative
mesh colliders are disabled on instantiated bridge/ford/pass props because at least one vendor collision mesh
is not readable by runtime NavMesh building; route ribbons and links own physical navigation instead. No
package import, activation switch, compact-world mutation or alternate pathfinding manager is introduced.

## D-182 WM08 keeps one canonical wildlife population across all simulation tiers (2026-10-23)

`WorldMapWildlifeDefinition` is immutable authored data loaded from the canonical package. A pure seeded
`WorldMapWildlifePlanner` derives a connected food/rest/water/shelter graph, region carrying capacities and
initial individuals. Species suitability comes from each definition's `suitableBiomes`; one shared alias table
reconciles regional package vocabulary without hard-coding species rules. Zone multipliers weight allocation
but do not exclude compatible regions. Small populations may use the high side of their authored range when
necessary to put at least one individual in every compatible starting habitat.

`WildlifePopulationService` is the sole mutable wildlife authority. It owns identity, sex, birth day, habitat,
position and current simulation tier, and performs daily adjacent migration, bounded reproduction and hunting-
pressure decay. Tier1 presentation and Tier2 ECS entities are projections reconciled from that state; Tier2 is
materialized in the already existing `Tier2Runtime.World`, and Tier3 deliberately owns no Unity object. Tier
changes never mint an identity. State capture/restore is a Domain boundary for later WM13 persistence work;
WM08 does not introduce a parallel save payload or activate the proposed generation.

The asset catalog references existing project content. Exact deer, boar and waterfowl models are used; absent
hare/fox/wolf/bear runtime models use scaled kitty/dog/boar visual fallbacks without changing canonical species.
The exact Toon Farm duck is static because its imported Animator has no controller; the other entries retain
their imported animation controllers. This preserves asset-first scope without importing or generating art.

## D-181 WM07 models fish as canonical production sites with calendar-based carrying-capacity recovery (2026-10-23)

The 24 authored fish sources are immutable `WorldMapFishPopulationDefinition` values connected to the WM03
hydrology graph. `TiledWorldPopulationPlanner` is their only placement and stable-ID authority: a production
point is resolved to dry/buildable shore with a local dry approach, while a separate disposable visual point
lies in its lake or on `river_main`. Lake habitat indexes scale a shared authored daily growth rate; capacities
remain the authored 48/42 units. Terrain, water meshes, fish particles and colliders own no population state.

`ProductionSite.RemainingAmount` remains the mutable stock authority. A typed `NaturalRecoveryPolicy` extends
the existing `ResourceEcologyService` with bounded integer logistic growth per crossed calendar day. Recovery
is clamped to `SourceCapacity`; a minimum breeding/recovery unit lets a fully depleted population resume. The
ecology calendar baseline restores from the already persisted `ProductionSaveState.LastCalendarTick`, avoiding
a save-version change and preventing the saved day from recovering twice. No second fish scheduler, inventory,
save payload or per-school `Update` is introduced.

The V2 presenter reuses the already audited PNB `FX_Fish_Small_01` prefab from
`WorldMapResourceArtCatalog`. The legacy definition carries no recovery policy and continues through its exact
existing fish branch. WM07 does not activate `p109-world-map-v2-proposed`; generation activation remains a
later gated task.

## D-180 WM05 separates deterministic vegetation identity from streamed Terrain presentation (2026-10-23)

`WorldMapVegetationDefinition` is immutable definition data parsed from the authored JSON. A pure
`WorldMapVegetationPlanner` combines class densities, zone multipliers, clearings, route/POI/build exclusions,
WM02 height/water and WM04 biome suitability. Overlapping masks are partitioned by density priority before
the authored relative targets are normalized into the global performance ranges. A final avalanche separates
salted deterministic coordinate streams; global spatial hashing enforces the 4.5–8 m interactive-tree spacing.
Stable IDs are independent of Unity objects and tile load/read order.

The plan is canonical immutable runtime data but not resource ecology: later WM06 may bind depletion state to
interactive stable IDs without making Terrain instances authoritative. `WorldMapVegetationTerrainProjector`
maps trees/shrubs to Terrain tree instances and ground/low props to Terrain details. `TiledTerrainWorld` owns
only disposable streamed presentation and centrally selected nearby capsule proxies, capped by the authored
650 limit; strategic mode selects none. No mass object owns an `Update`.

The asset-first audit selected the already approved PNB Meadow Forest source. Four project-owned prefab
variants retain three mesh LODs and existing stable materials, remove source colliders and remove billboard/card
LOD; existing shrubs and details are referenced directly. This is preferable to packs whose mass prefabs lack
LOD or retain per-instance colliders. No external content or package was added. WM05 neither activates the
proposed generation nor changes compact-world presentation/save authority.

## D-179 WM04 owns biome intent in Domain data and projects one deterministic Terrain mask (2026-10-23)

`WorldMapBiomeDefinition` is the single immutable authority for the eight authored region polygons, their
biome/soil/elevation/gameplay tags and ecotone widths. Compatibility region bounds are derived from it rather
than parsed as a second source. `WorldMapTerrainPaintSampler` remains pure Domain code and combines polygon
distance with the shared WM02 height sampler and WM03 water-distance query. Height, broad slope, moisture,
soil and deterministic microvariation influence seven normalized semantic weights. Edge-aligned global samples
make adjacent Terrain tiles consume the exact same border values; Terrain and strategic colors are disposable
presentations of that mask.

The loaded-asset audit selected the seven already admitted official `TerrainDemoScene_URP` layers previously
used by P9.3: Grass_A, Grass_Moss_A, Cliff_Mossy_E, Pebbles_B, Grass_Soil_A, Heather_A and Tidal_Pools_B.
Together they provide diffuse, normal and mask coverage for the seven WM04 roles more completely than the
legacy five PNB Meadow Forest layers. They are exposed through a V2-only `StarterNatureCatalog` array; the
legacy array and paint formula remain unchanged. No substitute textures/materials/shaders or package dependency
are added, and no V2 activation, scene authority or save-state authority is introduced.

## D-178 WM03 hydrology is an immutable graph evaluated by the shared terrain authority (2026-10-23)

`WorldMapHydrologyDefinition` is definition data over the canonical `WORLD_MAP_WATER.json`, not mutable world
state or a second water manager. Its main river and tributaries retain width-bearing splines, depth/bank ranges,
stable connections and explicit surface-level policies. Main-river level follows the terrain reference, lakes
use their fixed authored surface, and each tributary linearly descends between its connected water objects.
The loader fails closed if a tributary runs uphill, its endpoint does not spatially meet the named water body,
or any of the exactly four lakes lacks a graph path to the main river.

`TiledWorldHeightSampler` remains the single pure height/water/buildability evaluator. It carves channels and
polygonal basins, raises only the bounded bank band needed to remain dry, and reapplies water interiors after
late terrain masks so a pass cannot fill a stream. `TiledTerrainWorld` is still disposable presentation: the
legacy branch retains the old constant-level mesh, while only definitions carrying V2 `MapData` project the
canonical varying-width courses and 8/17/36 m surfaces. No scene, activation path, save schema or legacy
`p109-compact-world-v1` behavior changes in WM03.

The loaded-asset audit found multiple water presentations: PNB Meadow Forest lake/river materials and normal
maps, Synty and Slavic modular river pieces, TerrainDemo water, Low Poly Nature water and project-owned older
shore meshes. Their prefab geometry is fixed and cannot express the canonical polygon shorelines, continuous
widths and multi-level drainage without becoming the map shape authority. WM03 reuses the existing
`StarterWater` adapter plus the admitted PNB `_DistortionMap` already exposed as
`StarterNatureCatalog.WaterRipples`; only mesh topology is generated from canonical hydrology data. This keeps
imported visual detail without adding a new shader, material, texture, package or competing water system.

## D-177 WM02 applies authored relief through the existing global height sampler (2026-10-23)

`TiledWorldHeightSampler` remains the sole pure height/buildability authority. A definition with WM01
`WorldMapDataDefinition` selects a V2 data-driven path; a definition without it follows the previous compact
formula unchanged. The V2 path converts Unity coordinates back to the canonical design grid before every
sample, so tile load order and local Terrain origins cannot affect a shared border. Unity Terrain remains a
disposable projection and reads its heightmap resolution and vertical range from the same terrain contract.

Authored noise bands are frequency-weighted before applying regional anchors, so the slope target is met by
local relief rather than large areas clamped to minimum/maximum height. The ridge and marsh masks follow the
general anchors; the pass and start plateau are deliberately last, preserving traversable authored surfaces.
The checked-in slope audit uses a fixed world lattice and the JSON targets: ≤12° must be 45–58%, ≤18° must be
at least 38%, and >32° must be at most 9%. This is terrain-only. River/lake carving, distinct water levels,
dry banks and drainage remain WM03; no hydrology placeholder is promoted here.

WM02 does not activate `p109-world-map-v2-proposed`, change scene composition, create save state or reinterpret
legacy slots. The final lightweight gate is 20/20 EditMode over WM02, WM01 loader contracts and the affected
legacy height tests. Broad tests, Player build and visual acceptance remain WM15 work.

## D-176 WM01 loads World Map V2 through a separate fail-closed contract path (2026-10-23)

The current compact world remains the only active reference: `LoadReference()` still accepts only
`p109-compact-world-v1`. WM01 introduces `LoadProposedWorldMap()` for the isolated
`p109-world-map-v2-proposed` package and does not connect it to scene composition, Terrain generation or save
selection. `TiledWorldDefinition` receives optional immutable `WorldMapDataDefinition`; it is null for legacy
and its generation must equal the enclosing tiled definition when present.

Authored JSON stays canonical and is copied verbatim into a dedicated Resources folder. Infrastructure owns a
small dependency-free strict JSON reader because Unity `JsonUtility` cannot faithfully bind the package's
nested coordinate arrays and a new package dependency is not justified. Domain owns only immutable geometry,
transform and mask values. The loader validates every common header and manifest entry, then validates typed
geometry, layer indexes and cross-references before constructing a definition. Invalid packages never return a
partial runtime object. Height and vegetation masks are included now because WM02 and WM05 depend on those
contracts; applying them to Terrain or population remains outside WM01.

## D-175 World-map design uses a canonical 0…1000 grid and a versioned runtime projection (2026-10-23)

The completed design package at 'docs/world_map/' is versioned as 'serenity-world-map-v1'. Authoring and
documentation use X=0…1000 west-to-east and Z=0…1000 south-to-north. A single explicit transform preserves
the current compact-world runtime origin: unityX=designX-570 and unityZ=designZ-120. Technical images are
generated from the same in-memory data as the JSON layers; the separate image-generated illustration is
labelled supplemental and never overrides coordinates.

The future implementation is isolated as 'p109-world-map-v2-proposed'. It must not mutate or reuse
'p109-compact-world-v1' saves. The northwestern start, eight regions, four connected lakes, river, routes,
resources, wildlife habitats, POIs and buildability masks become runtime authority only after a separately
authorized data-contract/implementation task. Existing TiledWorldDefinition, height/terrain projection,
Production/ResourceEcology, building placement/clearance, tiering and SaveCoordinator remain the extension
points; no parallel world, inventory, wildlife or save manager is authorized by the design.

## D-158 Compact world and reviewed art libraries (2026-10-21)

The user supersedes the 12 km footprint with an exact 1000 x 1000 m new-game world.
Four by four 250 m Terrain projections retain the approved relative geography at 1/12
horizontal scale; models keep human scale and banks retain walkable metre-scale transitions.
The extra tutorial pond is adapted locally, not shrunk into the camp. All sixteen terrain
tiles remain loaded/render-eligible, avoiding exposed streaming borders at tactical zoom.
Runtime Terrain collider transforms are synchronized before NavMesh volume collection;
otherwise distant tiles can be collected at their creation origin during bootstrap.
All 44 fishing banks are checked against the real runtime NavMesh, not only height queries.
Generation `p109-compact-world-v1` and `Serenity/P109/CompactWorldV1/Saves` isolate new saves.
Previous generations are not migrated, modified or deleted. Fog cells become 25 m.

Each lake has four stable canonical fishing sources with dry work/output positions and
separate water-side visual positions. One scene scheduler animates the disposable schools.
Every catch creates a WorldPile; existing priority and capacity-aware hauling owns transport.
The compact-world fishing recipe takes 20 game minutes before modifiers and outputs four fish.

The user authorizes imports of the local archives. Only audited art is admitted: no vendor
gameplay/editor scripts, DLLs, settings, scenes or package changes. Existing GUID/path assets
win over duplicate vendor content. Imported prefabs are stripped of vendor MonoBehaviours;
source archives remain intact. Reviewed gameplay projections reference selected art through
CompactWorldArtCatalog, while production identity and save-state remain in Domain/Simulation.
Classic City Builder is not adopted as a replacement engine. Unused art stays a library,
not a claim of implemented mechanics; the follow-up plan is P109_COMPACT_WORLD_HANDOFF.md.

## D-157 Worksite V2 uses physical outputs and worker-built structures (2026-10-21)

The user's latest instruction authorizes implementation now, not merely a diagnostic plan.
Player residents have no personal stock: `CarryLoad` is a bounded, temporary transport owner,
not a backpack or funding source. Production commits outputs once to canonical world piles at
the source. Explicit queued work and critical needs precede idle cleanup; source/destination
reservations limit pickup to the minimum of available goods, carrier capacity and free storage.
Cancellation/failure/restored orphan loads return cargo to ground without destroying quantity.
The legacy domain catalog and default construction contract remain available for historical tests;
normal Worksite V2 composition explicitly enables ground outputs and construction requiring labor.

Construction pays its declared cost at placement, creates a planned structure and queues a Build
order. Canonical required/accumulated labor ticks survive save/load; a scaffold and progressive
mesh visibility project that state. Existing completed structures remain completed. Optional XML
labor fields read older snapshots with zero defaults; the new generation and normal save directory
prevent silent migration of old worlds. No old save file is written or deleted.

Visible detailed residents retain animation evaluation through idle transitions. Work clips are
looped by their suffix without rebuilding a valid humanoid mapping. Tools attach to the active
visual skeleton. Decorative starter outcrops receive their own deterministic source identity;
fish remain a canonical source approached from dry shore, with a small centralized cosmetic school.
Normal player army creation does not preload supplies; accepted map departure prepares expedition
supplies. New external packs are researched only. Art packages do not implement command routing,
storage reservations, construction work or save-state ownership.

## D-156 Synty starter art remains a disposable resource projection (2026-10-20)

The user explicitly requested one playable starter area using the admitted Synty assets,
not a vendor demo or a new terrain generator. Project-owned derived prefabs retain LOD0
meshes and the vendor's leaf/trunk material mapping, with wind/jitter/dithering disabled.
This bounded slice favors stable silhouettes over unverified billboard transitions.
The approved vendor assets are not edited by gameplay integration. Canonical resource roots,
StableEntityIds, quantities and save generation remain unchanged. Separate physical trunks
and selection triggers preserve walking and clicking. Streamed copies must not expand the
retired tree formation or overwrite normalized world scale. The lake uses imported ripple
texture on the existing authority-clipped water mesh, not an unconstrained vendor plane.
An editor authoring method regenerates the derived assets; the scoped build uses only enabled
gameplay scenes and reports its GUID/errors/warnings, without claiming U00's source fingerprint.
Native Player verification is required; automated tests alone do not close P10.9. Old saves
remain hash-verified and read-only; tests/manual play use separate Artifacts directories.

Close native water review rejected the first pale/angular pond. Increase presentation terrain
sampling uniformly to 257 (129 -> 257), paint/basemap to 128, preserving pure heights and tile
seams. Add a fifth shore layer and bounded bank dressing from the same approved package.
The twelve extra pond trees are canonical sources, not unclickable decoration; small ground
stones/grass are explicitly decorative. Opt-in development camera bookmarks enable visual
inspection when the native tool cannot hold MMB; they do not count as native input acceptance.

Acceptance note (2026-10-21): the user explicitly confirmed Home returning to camp, Space
pausing and smooth held-MMB camera movement. Record this as human acceptance of those three
checks only, separate from agent-exercised routes and automated tests. No architecture,
gameplay code, save files or build artifacts changed in this documentation-only follow-up.

## D-155 User-approved reversible Synty Meadow Forest import (2026-10-20)

The user explicitly approved importing their downloaded package into the main project rather
than a separate sandbox project. This overrides the earlier no-import scope for this package
only. Preserve the pre-import playable-patch commit `c732c96` and unrelated dirty materials;
do not use a destructive reset as rollback. A later rollback should remove/revert only the
import-specific commit after checking subsequent edits.

The archive is inspected without executing its contents. Only Meadow Forest and shared Core
content plus their bundled URP replacements are admitted under `Assets/PolygonNatureBiomes`.
The unrelated biome overlays are excluded. Keep Unity 6000.6.4f1, package dependencies,
startup/build scenes and save directories unchanged. Vendor demo geometry is art, not a new
resource, world-generation or persistence authority. Connecting it to the playable area needs
the existing stable-ID presentation binding and separate gameplay/visual verification.
Third-party availability does not establish publication rights; no licence entitlement is
asserted. Import success does not close the interrupted manual Player check or P10.9.

Compatibility adaptations are limited to this imported folder: use the included URP variants,
remove five duplicate depth-texture declarations now supplied by URP 17, and convert the 31
remaining built-in materials with scoped standard/particle conversion. Preserve GUIDs and
prefab references. The diagnostic build appends the vendor demo to its in-memory scene list
to exercise imported shader/mesh build paths; it does not save that list to EditorBuildSettings
or replace the previously built playable candidate. Six vendor floating-point `pow` warnings
remain distinct from shader compilation errors and are not claimed as visual acceptance.
The final admission gates passed: 9/9 EditMode, 5/5 PlayMode and the diagnostic Windows
build (0 errors / 34 warnings). Protected files and all original scenes remain hash-identical.

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

## D-012 Stable identity and checkpoint persistence (2026-10-03)
U04 uses immutable `StableEntityId`, a non-empty GUID value object. Explicit NewId creates identity only for new domain records; Parse restores the saved value. Wire representation is lowercase 32-digit GUID N format; empty/default IDs are invalid. IDs belong to domain records, never views, names, array indices or transient ECS handles.

`SaveSnapshot` version 1 is immutable and contains session identity, all four U03 time fields and the biological multiplier needed for deterministic continuation with non-default settings. No wall-clock metadata or speculative character records are added. Slot identity is independent of session identity. Simulation owns synchronous `ISaveStore` and `SaveCoordinator`; Infrastructure owns a strict XML codec and local file provider. XML uses framework APIs without new packages, preserves 64-bit ticks and validates required/duplicate/unknown fields. Unsupported versions throw NotSupportedException; malformed or invalid data throws InvalidDataException. No migration is silently attempted.

Save/load runs on the simulation owner thread at a committed tick barrier. Capture copies mutable time state. Load constructs and validates a replacement GameClock before committing the clock/session pair; callers must rebind after successful Load. Storage never mutates runtime objects. The file provider flushes a same-directory temporary file then uses File.Replace (existing slot) or File.Move (new slot); it does not delete the old file or fall back to a non-atomic overwrite. Storage errors propagate. One writer per directory is required. Power-loss durability across all filesystems is not guaranteed. PostgreSQL and asynchronous orchestration remain U04A/later work.

## D-013 PostgreSQL durable persistence (2026-10-04)
U04A implements the existing `ISaveStore` boundary with Npgsql 8.0.9, selected because its .NET Standard 2.1 target compiles in Unity 6000.6.4f1 with the Windows Mono Development backend. NuGetForUnity 4.5.0 and its CLI restore the pinned package graph from nuget.org into an ignored directory; no provider binaries are committed and no ORM is introduced. Only Infrastructure and Editor tests explicitly reference Npgsql. Domain, Simulation, ECS and Presentation remain provider-free.

PostgreSQL is local durable snapshot/history storage. Active simulation remains in RAM. The adapter is synchronous and called only at a committed save/checkpoint barrier, never per frame, movement, HP change, NPC or AI tick. Configuration comes from `DYNASTYGAME_PG_*` process environment variables; only loopback hosts are accepted in U04A. Development and integration databases are separate (`dynasty_game_dev` and `dynasty_game_test`). Credentials and restored packages are excluded from Git, and public errors redact provider details and expose only operation plus SQLSTATE.

Schema `serenity` uses a checksummed `schema_migrations` ledger and one initial `save_sessions` table. Migration application takes a PostgreSQL advisory transaction lock; schema changes and ledger inserts share one transaction. Version/name/checksum drift, gaps and future database versions fail explicitly. Save uses one parameterized UPSERT, retaining creation time and atomically replacing the committed XML payload. Load checks redundant session UUID and save format metadata before returning a snapshot. PostgreSQL schema migration version and `SaveSnapshot.FormatVersion` are independent. StableEntityId maps exactly to PostgreSQL UUID and back.

The XML `SaveSnapshot` remains the canonical U04 payload rather than prematurely normalizing the future world model. Future Character, Dynasty, City and HistoricalEvent repositories will use separate persistence ports/tables and the same identity mapping. Timestamps are persistence metadata, not game time. The provider holds no runtime or GameClock reference; successful load still requires consumer rebinding as described by D-012.

## D-014 Local navigation and input boundary (2026-10-04)
U05 uses one dedicated `LocalGameplay` scene built entirely from project-owned Unity primitives. Vendor asset packs are not scene dependencies and remain U05A scope. Infrastructure provides one explicit serialized `LocalSceneCompositionRoot`; Presentation owns the camera, raw input adaptation, pointer raycasting and debug selection. No scene-wide lookup or service locator is used for runtime composition.

The local Input System asset has only Camera and Pointer maps. Camera bindings are WASD/arrows movement, configurable normalized edge scrolling, middle-mouse drag pan, wheel zoom and Q/E yaw. Pointer bindings expose position and primary/secondary click for future selection/orders. The input component emits state/intent and knows no Domain, simulation, persistence or PostgreSQL details. This boundary can later coexist with a separate U22 third-person map without sharing physical-input callbacks with gameplay rules.

RTS navigation operates in presentation time using `Time.unscaledDeltaTime`; active simulation pause therefore does not freeze camera movement. A yawed target rig is clamped to configurable rectangular XZ bounds. Zoom moves a fixed-pitch Cinemachine camera mount between configured distances and never advances or reads GameClock. Cinemachine drives camera presentation but does not own gameplay input. Camera distance and selection highlight are disposable view state, not canonical save data.

Pointer conversion is an explicit `TryGetWorldHit` / `TryGetWorldPoint` service over the output Camera and physics layers. If an EventSystem exists and reports pointer-over-UI, world interaction is blocked; U05 does not create an EventSystem because the scene contains no UI. The selectable markers and highlight are only an integration probe, not the future character/RTS selection system.

## D-015 Third-party asset ownership and normalization boundary (2026-10-04)
Asset downloads outside Unity live in the ignored `serenity_games_assets` staging tree. A file crosses into `Assets` only after its source, license, commercial-use status, visual role and concrete runtime need are recorded. Unknown provenance is `LICENSE_REVIEW_REQUIRED` and forbids runtime selection; a filename, package resemblance or generator metadata is never license evidence. Existing Unity vendor packages remain in place and are not treated as implicitly approved merely because they import.

Third-party source and Serenity runtime content are separate ownership zones. Vendor/source models, textures, materials and animation may be referenced, but gameplay-facing prefabs belong under `Assets/Game/Art/Prefabs`. Canonical state, StableEntityId, gameplay components and save ownership must not be placed in or coupled to a vendor hierarchy. Replacing a source package may change a wrapper's presentation dependencies without changing Domain or Simulation APIs.

One Unity unit equals one metre. Staging-derived FBX normalization is performed by an explicit allow-listed editor command, never by a global AssetPostprocessor. Model import settings handle scale when Serenity owns the selected imported copy; non-destructive wrapper transforms handle already-imported vendor content. Ground/contact and attachment pivots are solved at wrapper roots unless a source correction is clearly safer. Serenity-specific URP materials are separate from vendor originals.

The U05A gallery is development-only and excluded from Build Settings. `LocalGameplay` remains the only enabled scene. Addressables labels/groups, authored LOD production and final tier art are deferred until loading and presentation tasks establish an actual need. Placeholder classification is asset-readiness metadata and never implements the simulation tier system.

## D-016 Character aggregate and kinship ownership (2026-10-04)
Persistent people are pure `Game.Domain.Characters.Character` aggregates. `StableEntityId` is assigned only by `CreateNew`; `Restore` requires and preserves the saved identity. A detached `CharacterState` captures every U06 field in deterministic collection order and is the boundary for future save codecs and tier projections. U06 deliberately does not expand `SaveSnapshot` version 1 or add SQL; world-level character persistence requires an explicit later schema/version task.

Biological birth tick is the only canonical age source. Completed age uses 365-day years over U03 biological `TimeSpan` ticks; alive characters use the supplied current biological tick, while dead characters stop at their recorded biological death tick. Calendar time, wall-clock time and per-character timers are excluded.

Parentage is stored canonically on the child as stable-ID `ParentLink` values. A link distinguishes mother/father role, biological/legal/adoptive kind and whether the relationship is known to the character, allowing hidden biological and different legal fathers without duplicating mutable child lists. `CharacterRegistry.GetChildren` derives children from those links. Current spouse links are stable IDs and are made symmetric by the session-owned registry; historical marriages remain future dynasty work.

Traits, skills and professions use canonical data-definition keys rather than ScriptableObject or UI strings. Full NPC state requires four or five unique traits; skill and basic attribute values use 0–100 bounds. Body-health foundation covers head, torso, both arms and both legs without implementing injury/disease gameplay. Wealth and Influence are distinct 0–1000 domain values. Family and dynasty are optional `StableEntityId` hooks, not speculative aggregates.

`CharacterRegistry` is an ordinary in-memory session index, not a singleton or PostgreSQL repository. It rejects duplicate active character IDs, provides deterministic lookup/listing and derives children. Presentation, ECS and storage adapters may project the aggregate but cannot own its identity or canonical state. No assembly dependency edge changes in U06.

## D-017 Tier 1 character presentation ownership and pause behavior (2026-10-05)
Tier 1 characters are disposable GameObject projections of existing `Game.Domain.Characters.Character` aggregates. `CharacterPresenter.Bind` receives the aggregate and a session-owned `CharacterPresentationRegistry`; it exposes the aggregate's existing `StableEntityId` and never generates, serializes or replaces identity. The registry rejects simultaneous duplicate views for one character and is cleared by explicit unbind, spawner despawn or presenter destruction. Destroying a view does not remove the character from `CharacterRegistry`.

`CharacterPresentationCatalog` is a presentation ScriptableObject mapping the current deterministic Male/Female descriptors to Serenity-owned wrappers. Hodaart Characters 01/02 remain temporary U05A-approved placeholders; replacing their catalog entries or wrapper visual children must not change Domain, saves, simulation or identity. `CharacterPresentationSpawner` materializes only a supplied existing aggregate and receives world transform separately.

The project-owned Tier 1 Animator controller starts in Idle and exposes `Speed` and `Moving`; U07 uses `Speed` for idle/walk/run presentation only and adds no navigation or AI. Animator evaluation is presentation state. `SetSimulationPaused` freezes/resumes the Animator locally without changing global `Time.timeScale`; camera navigation therefore remains active and no biological/calendar time is advanced by the presenter. A later session pause coordinator may fan this explicit call out to active presenters.

## D-018 Building placement, occupancy and projection boundary (2026-10-05)
Placed structures are pure `Game.Domain.Buildings.Building` aggregates. A lowercase validated `BuildingDefinitionId` identifies content, while each placed instance owns an unrelated GUID-backed `StableEntityId`. `BuildingState` stores only definition ID, integer grid coordinate including an explicit level, quarter-turn orientation and construction state. `CreateNew` generates identity only after valid confirm; `Restore` preserves it. The U04 `SaveSnapshot` version-1 wire format and PostgreSQL schema remain unchanged; U08 provides a detached data-only state boundary for a later world-save schema migration.

Definitions are authored in one `BuildingPresentationCatalog` ScriptableObject, but the catalog and prefabs are not canonical mutable state. The catalog creates engine-free `BuildingDefinition` values with category, rectangular footprint, placement mode and overlap rule. U08 implements the grid mode only; Free and Edge are explicit future modes. Primitive Shelter and Storage Basket are current project-owned U05A placeholders, not hardcoded building subclasses or inventory implementations.

`BuildingPlacementService` owns deterministic bounds and logical occupancy rules in Simulation. Presentation converts pointer hits from the existing U05 raycaster into configurable one-metre grid coordinates. The rectangular footprint rotates at 90-degree increments, all occupied cells must be inside local bounds, and non-overlap definitions reserve cells atomically before entering the domain registry. Physics colliders support pointer/visual interaction but are not canonical occupancy.

The Building input map uses B to toggle the default shelter preview, left click to confirm, right click or Escape to cancel and R to rotate; Q/E remain camera rotation. A session-owned `LocalInteractionMode` prevents selection from consuming placement clicks while leaving the camera enabled. Preview clones are unbound, collider-disabled, ignore raycasts, receive green/red MaterialPropertyBlock tint and never receive an ID or registry entry. Confirm creates one planned aggregate, explicitly completes it for the resource-free U08 vertical slice, reserves occupancy and spawns a bound projection. Resource consumption and worker construction are U09/U10 work.

`BuildingPresentationRegistry` and `BuildingRegistry` are separate session-owned indexes. Destroying or unbinding a `BuildingPresenter` removes only the view mapping; respawn binds the same aggregate and stable ID. No singleton, SQL, Npgsql, GameObject, Transform, Vector3 or Quaternion enters Domain/Simulation building state.

## D-019 Canonical bulk resources and atomic construction funding (2026-10-05)
U09 models MVP resources as integer bulk units. `ResourceId` is a validated lowercase content key; `ResourceQuantity` is a nonnegative 64-bit value and `ResourceAmount` is a positive `(id, quantity)` pair. Definitions are immutable pure data produced from `ResourceCatalogAsset`; mutable quantities exist only in `ResourceInventory`. World piles, characters and building storage use one `InventoryOwner` key containing an owner kind and `StableEntityId`, so presenters and scene objects cannot become canonical inventory state.

Transfers are exact and atomic: the source must contain the whole amount and the destination must accept the whole amount under capacity/category rules before either inventory changes. Failed transfers conserve both inventories. Settlement totals are a read-only projection over selected storage inventories, never a second mutable stockpile. `ResourceInventoryState` and pile identity/coordinate provide detached save-ready boundaries, but U09 deliberately does not change the U04 version-1 time snapshot or PostgreSQL schema.

Construction confirmation is one Simulation transaction. Spatial validity and affordability are evaluated separately. Confirm reserves occupancy and registers the building, creates any storage inventory, deducts exact resource costs, invokes presentation, and completes the building. Any exception refunds deductions and removes storage, registry and occupancy state. Insufficient funding mutates nothing and creates no persistent building identity. U09 consumes resources immediately on successful confirm; worker delivery/reservations and staged progress remain U10/U11 work.

## D-020 Centralized Tier 1 Utility AI and logistics claims (2026-10-05)
Tier 1 AI is session-owned simulation state keyed by the existing Character `StableEntityId`, not a component-owned copy of character state. One `Tier1AiScheduler` advances bounded needs, utility decisions and action state machines from explicit U03 calendar deltas. Stable-ID ordering, deterministic phase staggering, fixed batch caps and finite normalized scores make equal-state decisions predictable. There is no per-character AI `Update`; a single Presentation runtime driver connects the scheduler to Unity frames and passes zero simulation delta while paused.

Utility selection and execution are separate. U10 implements only Idle, Rest and autonomous Haul, with a switch margin and non-interruptible carrying phase. Haul work is discovered from U09 registries, selected by distance with stable-ID tie-breaks and protected by session-ephemeral quantity/capacity claims. Claims do not own resources: pickup and dropoff use only `ResourceTransferService`, so cancellation before pickup leaves stock at source and cancellation after pickup leaves stock in the character inventory. Work groups, player priorities, construction labor and production remain U11/U15 concerns.

Movement is an application boundary. Simulation emits `MoveTo(WorldPosition)` through `ITier1MovementDriver`; Presentation implements it with AI Navigation 2.0.12 `NavMeshAgent`, reports Idle/Moving/Arrived/Failed and drives the existing U07 animation parameters. NavMesh paths, task/claim execution and presentation objects are not serialized. U10 leaves the U04 snapshot/SQL schema unchanged and records dynamic obstacle/rebake and durable in-flight task semantics as future explicit work.

## D-021 Read-only canonical development selection (2026-10-05)
U10 development inspection extends the existing U05 pointer raycast and `SelectionProbe`; it does not create a gameplay selection model or U27 UI. The probe may retain disposable presenter references for the currently hit Character, WorldResourcePile or Building, while all displayed identity, quantities, capacities, action phases and claims are read on demand from their bound Domain/Simulation objects and registries.

Debug snapshots are short-lived immutable render/test values, never canonical storage. `HaulClaimRegistry` exposes deterministic read-only query results, and the resource overlay recomputes location totals from `ResourceInventoryRegistry` for every draw. UI code cannot transfer resources, create claims or retain an independently mutable balance. Player work groups/orders remain U11 scope.

FRS supplies no exact MVP costs or capacities. Current `BALANCE_TBD` authoring values are Primitive Shelter = 8 wood logs + 3 plant fiber, Storage Basket = 3 wood logs + 5 plant fiber, and basket capacity = 40 bulk units. LocalGameplay's 30 wood, 20 fiber, 10 stone and 5 hide piles plus two capacity-8 character inventories are `DEV_BOOTSTRAP_ONLY`, not production new-game balance. Individual durable items, spoilage, weapon condition, quality, off-map aggregation and production chains remain U15/later scope.

## D-022 Work-group identity, manual-order lifecycle and AI precedence (2026-10-05)
Work groups are canonical session-domain data with their own GUID-backed `WorkGroupId`; their members and optional commander are existing character `StableEntityId` references. A character may belong to at most one group. Membership is deterministic and survives disposable presenter loss, while durable group persistence remains a later explicit snapshot/schema migration.

Player work uses typed session-local `JobId` values and explicit Queued, Assigned, Active, Completed, Cancelled and Failed states. Move targets are world positions; resource and actor targets use stable IDs. `WorkManager` owns creation, assignment, priority, status queries and exclusive target reservations. Group commands create deterministic per-member orders rather than a shared mutable presenter task. Eligibility is a replaceable Simulation policy hook; the U11 default requires a living registered character.

Manual orders participate in the one centralized U10 scheduler. Non-interruptible carrying wins first, critical Energy (`<= 0.10`) wins next, then manual/group work, then autonomous utility. Manual work may preempt an interruptible autonomous action; critical rest requeues unfinished manual work. Manual Haul uses the existing quantity/capacity claims and only U09 atomic transfers, so cancellation or failure releases reservations without manufacturing, deleting or teleporting stock.

Shift multi-selection, Ctrl+number group assignment, number-key recall and the work overlay are development command/input surfaces. They may issue Simulation commands and show immutable snapshots but do not own group, job, inventory or AI state. U27 remains responsible for production gameplay UI and persistence of key bindings.

## D-023 Tier 2 projection ownership and explicit DOTS stepping (2026-10-05)
U12 represents a character in Tier 2 as an ECS projection, never as a second independent `Character`. `Tier2StableIdentity` is the unmanaged 16-byte encoding of the existing Domain `StableEntityId`; the materializer generates no identity and rejects duplicates. ECS `Entity` handles and the boundary-side lookup index are session-local. Domain and Simulation retain no `Unity.Entities` dependency.

The U12 component set is deliberately narrow: stable identity, biological/lifecycle facts, position, lightweight movement and explicit simulation progress. The five components total 92 bytes before ECS chunk overhead and contain no managed references. Family, relationships, full health, traits, skills, professions, inventories, U10 utility execution and U11 group/job authority are not copied into ECS. Domain remains authoritative for durable/unprojected state; while a Tier 2 projection is active, ECS is authoritative only for its supported mutable position/movement/progress fields until extraction after all tracked jobs complete.

`Tier2Runtime` owns a private ECS `World`, a materialization/extraction boundary and one `Tier2SimulationSystem`. The system schedules a parallel Burst `IJobEntity` and receives calendar/biological deltas solely through a singleton command populated from U03 `GameTimeAdvance`. Zero deltas are a complete no-op, so active pause cannot advance movement or progress. The runtime does not read wall time, `Time.time`, `Time.deltaTime` or `Time.timeScale`.

U12 intentionally does not attach this private world to the default player loop, create a runtime MonoBehaviour, render entities or transition real Tier 1 characters. U13 must coordinate one active representation, complete jobs at transfer barriers and reuse `Tier2TransferState` rather than inventing another identity/state path. Tier 3 aggregation and deterministic off-camera rules remain U13/U14.

## D-024 Transactional tier lifecycle and representation authority (2026-10-06)
`TierManager` is the single session-owned coordinator for named-character Tier 1, Tier 2 and Tier 3 representations. It lives in engine-free Simulation and calls one adapter per tier, preserving the existing dependency graph. A managed character has exactly one declared active representation. Same-tier transitions are no-ops; re-entrant transitions are rejected. A target is validated before source release, the source state is captured at an explicit barrier, and target failure triggers source rematerialization from the captured state.

Authority is field-scoped. `Character` remains canonical for identity, name, sex, birth/death, parents, spouse, family/dynasty, traits, skills, health, profession, Wealth/Influence and relationships in every tier. U09 character inventory and U11 work-group/order state remain in their existing stable-ID registries and are never copied into ECS or Tier 3. `CharacterRuntimeState` is the transition DTO for position, lightweight velocity/movement, processed time/step progress, coarse location key and current U10 Hunger/Energy continuity. Tier 2 owns its projected position/movement/progress only while active; extraction completes tracked jobs before the entity is destroyed.

Tier 1 demotion stops and discards the NavMesh path because it is presentation-only. Scheduler unregister cancels Tier 1 execution, releases U10 claims and requeues an active U11 manual order while preserving its assignment. Resources already carried remain in the canonical character inventory. Returning to Tier 1 creates one presenter through the existing deterministic sex-to-prefab catalog and rebinds one centralized AI agent with retained needs; no appearance or character data is rerolled.

Tier 3 named-person state is a pure C# `Tier3CharacterRecord` keyed by the same `StableEntityId`. It contains restoration/runtime continuity and a location key, not a copied biography or a GameObject/ECS Entity. U13 does not advance Tier 3 records. U14 owns deterministic off-camera progression and any controlled RNG state.

Automatic representation choice is a separate `TierDistancePolicy`, not a source of truth. Its thresholds define hysteresis bands, minimum residency is counted in deterministic evaluations, stable-ID iteration is bounded, and transitions per evaluation are capped. Camera position selects a representation only; it never changes canonical character results. Default distances and budgets are `BALANCE_TBD` implementation settings pending U29 profiling.

## D-025 Development UI blocks world pointer input before selection (2026-10-06)
All development command/read panels participate in the existing `WorldPointerRaycaster` UI boundary through registered GUI-space rectangles. Pointer coordinates are converted once from Input System screen space to IMGUI space before physics. The raycaster checks UI before any world raycast, and `SelectionProbe` treats a UI-blocked click as consumed rather than as an empty-world miss; therefore it never clears selection because a debug control was pressed.

The U11 work panel routes its visible buttons through the existing `LocalGameplayInputSource.PrimaryClicked` event and resolves the clicked command from the same rectangles used for drawing. Selection remains the existing disposable Presentation state, and work groups/orders remain Simulation/Domain state. EventSystem UI remains supported by the same raycaster boundary. No second selection authority, workaround selection cache or post-error restoration path is introduced.

## D-026 Deterministic named-character off-camera catch-up (2026-10-06)
U14 uses one engine-free `OffCameraSimulationService` with an explicit saveable world seed and a one-calendar-day fixed step. The service accepts absolute U03 calendar/biological ticks, never wall time, frame delta, camera state or Unity random state. Partial-day calls commit the exact target tick but perform a fixed step only when an absolute day boundary is crossed, so one 24-hour call and twenty-four one-hour calls are identical.

Random samples are counter-keyed by `(world seed, StableEntityId, absolute fixed-step index, stream ID)`. There is no shared mutable RNG cursor; input collection order, neighboring characters and materialization history cannot shift another character's samples. U14's `AbstractActivityProgress` and deterministic accumulator are generic audited long-horizon continuity only. They intentionally do not manufacture resources, execute jobs, change health/family or implement U28 events. The deterministic state hash covers seed, identity, location and U14 continuity fields in stable-ID order and excludes GameObject/ECS handles, wall time and dictionary order.

`CharacterRuntimeState` remains the one U13 transition DTO and now carries the last committed absolute calendar/biological ticks, completed remote steps, deterministic accumulator and abstract activity progress. Tier 1 and Tier 2 adapters retain those fields unchanged. The Tier 3 adapter validates and catches up before materialization and again before capture/promotion; automatic runtime batching traverses active Tier 3 records at most once per fixed day and skips same-day frame calls in O(1). This makes Tier3→Tier1/Tier2 promotion an exact latest-state barrier and makes Tier1/Tier2→Tier3 begin from the carried checkpoint without double time.

The existing `Character`, inventory registry and work registry remain canonical. U14 does not run Tier 1 Utility AI, NavMesh, physical hauling or production while remote; Hunger/Energy, physical resources, group membership and assigned work are preserved. Biological age continues to derive from U06 birth tick evaluated against the U03 biological clock; U14 does not add a second age or implement natural-death policy, disease or injury. Durable world-save/schema integration is deferred, but every U14 continuation field is detached and constructor-restorable, and split-session tests prove restart determinism without serializing an RNG object.

## D-027 Tier-aware RTS manual Move command (2026-10-06)
Right-click movement reuses the existing U05 `Pointer/SecondaryClick` and `WorldPointerRaycaster`; there is no second raycast, camera projection or UI-block authority. One Presentation coordinator distinguishes building-placement cancellation from ground Move, resolves selected characters or the active U11 work group, and submits only world intent. EventSystem and registered IMGUI rectangles block the command before physics.

`ManualMoveCommandService` is an engine-free Simulation boundary. It creates one U11 Move `WorkOrder` per executable member in stable-ID order and assigns deterministic 1.5 m centred formation slots (`BALANCE_TBD`). Physical execution remains the U11 manual action through U10 `ITier1MovementDriver`; Presentation/selection code never calls `NavMeshAgent.SetDestination`.

Only a current Tier 1 representation may receive a physical Move job. `TierManager` implements read-only `ICharacterTierLookup`; Tier 2/Tier 3 members produce explicit `TierUnavailable` results with no job and no hidden promotion, remote pathfinding or U14 mutation. Existing open work produces `AlreadyHasWork`. This preserves U13 representation authority and U14 deterministic off-camera state while leaving future Tier 2/remote order execution for an explicit FRS-backed task.

## D-028 Reusable production authority and canonical food (2026-10-06)

`berries` is the one canonical U15 food resource. Production definitions are immutable engine-independent recipes with declared inputs, outputs, work ticks, enabled state and compatible site kind. Mutable source, crop and task progress belongs to stable-ID domain objects coordinated by one `ProductionService`; no recipe, vendor prefab, presenter or ScriptableObject owns mutable stock. This permits later input chains such as ore -> metal -> weapon to reuse the same catalog and commit rules without implementing those U16+ contents now.

Production creates output for exactly one canonical U09 owner. A Tier 1 batch prefers its character inventory, then a valid completed building storage if the character cannot accept the whole batch. Physical delivery uses the existing U09 transfer service; if storage becomes full, stock remains in the character inventory. Aggregate tiers may deposit after completion, but never create a second balance. Recipe inputs and outputs are all-or-nothing at the batch boundary; natural source capacity decrements exactly once.

## D-029 Production reservations, tiers and deterministic time (2026-10-06)

One active `ProductionTask` exclusively reserves one `ProductionSite` and one worker. Cancellation releases both claims; completion releases them only after the batch commit. Active task IDs, worker/site IDs and accumulated/required work are persisted, and restore reconstructs claims only after validating unique IDs and matching site/recipe relationships. Reservations are session-owned maps, not database locks.

Tier 1 physically moves, works and delivers through the centralized U10 scheduler and movement bridge. Tier 2 accumulates `PendingWorkTicks` in the existing Burst ECS pass, then crosses a completed-job barrier and commits sorted stable IDs through `ProductionService`. Tier 3 supplies its sorted stable IDs directly to the same fixed-step service. All aggregate production, crop growth and food consumption advance on absolute U03 calendar ticks in one-day steps; traversal input is deduplicated/sorted, hashes exclude representation handles, and U15 introduces no random stream. Tier changes and time chunking therefore do not alter the economic result.

## D-030 Primitive farming and production persistence v2 (2026-10-06)

The first cultivated site is `primitive_crop_plot`, a U08 `BuildingDefinition` whose placed stable ID is also its production-site ID. U08 occupancy and U09 construction/activation remain the only placement path. Its abstract deterministic lifecycle is Unplanted -> planted work -> fixed growth -> Ready -> harvest work -> Unplanted. Soil, seeds, seasons, weather, irrigation and crop disease remain explicitly deferred. The Storage Basket prefab is a temporary project-owned placeholder visual, not crop state.

`SaveSnapshot.CurrentVersion` is 2. The production payload stores absolute production tick, food remainder/consumed/shortage, targets, sites, active tasks, produced totals and all canonical inventory states. XML decoding remains strict and capped at 4 MiB; legacy version 1 time-only saves remain readable with no production payload. PostgreSQL stores and validates the versioned XML as before, so no SQL schema migration or realtime database authority was introduced.

## D-031 Canonical research authority and typed unlock availability (2026-10-06)

One session-owned, engine-independent `ResearchService` is the canonical settlement research authority. Definitions use stable `TechnologyId` values, explicit prerequisite IDs, tick costs, Stone Age era metadata and typed building/production-recipe/capability unlocks. `TechnologyCatalog` fails fast on duplicate or unresolved IDs, self-dependency, cycles and unlocks that do not resolve against the real U08/U15 catalogs.

Research never removes, clones or mutates building/recipe definitions. U08 construction and U15 production accept a read-only `IResearchUnlockView` and decide availability at the action boundary. A programmatic locked construction request returns `LockedByResearch`; an isolated system fixture with no injected research view preserves its intentional unlocked sandbox behavior. Unlock state is derived from completed technologies rather than persisted as redundant truth.

## D-032 Research labor uses absolute deterministic checkpoints across tiers (2026-10-06)

Research progress is integer game-time work with a fixed-point rate-per-thousand remainder per assigned stable worker. The default character rate is a small explicit hook over Intelligence and optional `research` skill; no wall clock, coroutine or random stream contributes. Completion computes the reproducible absolute tick and commits a technology once. A lower stale checkpoint from a tier transition or teardown is an idempotent no-op, while forward calls consume only their unprocessed interval.

Tier 1 adds Research to U10's centralized Utility AI; Tier 2 and Tier 3 submit stable IDs after their existing batch barriers to the same service. There is no research MonoBehaviour per worker/project, no GameObject in Tier 2 and no off-camera copy. One active settlement project is the deliberate MVP scope; queueing and physical libraries/universities are deferred.

## D-033 Research persistence version 3 and Stone Age completion (2026-10-06)

`SaveSnapshot.CurrentVersion` is 3 because research adds durable completed IDs, active project/progress, assignments, fractional remainders and the last processed absolute tick. Versions 1 and 2 remain strictly readable; missing research payload means no research state to restore. PostgreSQL continues to validate and store the opaque versioned XML, accepts metadata versions 1–3 and requires no SQL migration or realtime query path.

The current catalog is Stone Age only. `stone_age_mastery` depends on all required late branches and unlocks `stone_age_completed`; `ResearchService.StoneAgeCompleted` exposes that capstone without starting a later era. Post-Stone-Age content remains U38.

## D-034 Player-controlled research and disposable debug controls (2026-10-07)

Research assignment is an explicit player/debug command over the canonical `ResearchService`; LocalGameplay composition does not implicitly assign demo characters. Batch assignment is idempotent and stable-ID ordered. Assigned means present in `ResearchService.AssignedWorkers`. Active means that same assigned ID currently has authoritative Tier 1 AI action `Research` in phase `Researching`; visual state and assignment alone are insufficient. The research panel derives its rate and ETA from Active workers and explains a zero-active stall.

The U16A building selector is development-only until U27. It enumerates the existing `BuildingPresentationCatalog`, derives availability and unlock reasons from the live U16 catalog/service, and passes the selected `BuildingDefinitionId` to the existing U08 controller. The controller itself enforces `LockedByResearch`, so UI state is explanatory rather than authoritative. Current U16 starting knowledge remains unchanged; shelter, storage and crop availability follow their canonical technologies.

Time input remains in the Unity Input System. Space toggles pause, Minus steps slower and Equals/Plus steps faster through x1/x2/x3/x5/x10. Digits 1–9 remain exclusively U11 work-group recall keys. `Tier1AiRuntimeDriver` is the debug command coordinator for pause/speed and mutates the one `GameClock`; its zero-delta path is the shared pause barrier for AI, tier batches, production and research. No second time service or final U27 HUD was introduced.

## D-035 Named agnatic primogeniture policy (2026-10-07)

The first succession policy is `agnatic_primogeniture_v1`. This follows FR-MON-002/003: only living male members of the ruling dynasty can inherit under the base law. Eligibility is separate from traversal: an ineligible or dead child is skipped, but living male descendants of that branch keep its primogeniture position. Branches sort by biological birth tick and then canonical `StableEntityId` text; no list insertion, scene spawn, GameObject or random order participates. Naming the policy leaves a clean replacement seam for later law changes without embedding gender rules in `DynastyState`.

## D-036 Event-driven exactly-once succession and extinction (2026-10-07)

`SuccessionService` is the single authoritative boundary from U06 `Character.MarkDead` to ruler succession. It queries canonical characters through stable IDs and updates `DynastyState`, `PlayerRealmState` and `GameRunState` in one transaction result. Each deceased monarch ID is persisted beside exactly one ordered history entry. Reprocessing the same death is a no-op; it cannot create another history entry or reroll the heir. No valid heir sets the dynasty Extinct and the run to GameOver/DynastyExtinction; no unrelated NPC or generated replacement is permitted.

## D-037 Version 4 character and dynasty persistence (2026-10-07)

`SaveSnapshot` version 4 persists complete detached `CharacterState` records together with dynasty identity/members/rule/status/history/processed markers, the player ruler ID and game-run state. Family relationships remain owned by Character rather than duplicated into a dynasty graph. Load validates duplicate/missing/self/cyclic/asymmetric references before mutating the active registry and preserves existing canonical Character objects when identities match. Versions 1–3 remain readable. PostgreSQL continues storing the strict XML payload as opaque bytes, so no relational migration is added.

## D-038 Canonical batched health and capability authority (2026-10-07)

`HealthAndFamilyService` owns condition/event/pregnancy progress by `StableEntityId` and mutates the existing Character aggregate. Effective work, movement, research, combat and fertility capability is derived from base age/life plus active data-driven conditions. U10/U15/U16 consume that query; no tier, presenter, MonoBehaviour or ECS projection owns copied health truth or a per-character update.

## D-039 Deterministic simulation-derived identity and birth (2026-10-07)

Manual entity creation keeps `StableEntityId.NewId()`. Simulation-derived condition/event/pregnancy/child IDs use the narrow `FromDeterministicHash` factory over world seed, purpose, canonical parent/target IDs and absolute ticks. Birth creates one canonical Character, writes U06 parent links, enters the active U17 dynasty when applicable and publishes only a materialization notification. This makes save/restart, collection order and tier routes identity-equivalent.

## D-040 Health death and family events reuse existing authorities (2026-10-07)

Fatal conditions call `SuccessionService.ProcessDeath`; they do not duplicate life/succession state. Family events update existing bounded Character relationships and persist deterministic event markers. U18 remains a narrow foundation rather than the U28 event engine, medical economy, epidemiology or genetics system.

## D-041 Version 5 health and family persistence (2026-10-07)

Snapshot v5 adds health/family world seed and checkpoints, condition lifecycle/death markers, relationship-event markers and pregnancy/birth markers. Canonical children, parent links and relationship results remain in U17's character/dynasty payload. Versions 1 through 4 load with no health payload. PostgreSQL continues storing opaque XML and needs no relational migration.

## D-042 Canonical keyed daily weather and derived seasons (2026-10-08)

Season is derived from the existing U03 calendar and is never independently mutable. The initial configurable 365-day mapping begins Spring/Summer/Autumn/Winter on days 1/92/183/274. `WeatherService` derives a day from `(world seed, climate profile ID, absolute calendar day, stream ID)` with U14's keyed RNG; it keeps no sequence cursor or daily history. Camera, scene, tier, render frame and collection traversal cannot reroll weather.

Temperature is integer deci-Celsius. One validated `ClimateProfile` owns four seasonal temperature/weight/growth definitions and a complete, unique weather-effect table for anomaly, precipitation range, wind range and gameplay modifiers. The temperate profile is current content, while the catalog rejects duplicate profile IDs. Rain is converted to snow at or below the configured threshold and configured snow is converted to rain above it; no accumulation, moisture or physical climate simulation is introduced.

## D-043 Weather modifiers cross existing production, movement and health authorities (2026-10-08)

U15 recipes now declare only `Outdoor`/`Sheltered` exposure and optional seasonal-wild-source metadata. The existing `ProductionService` multiplies base crop progress by seasonal/weather crop rate, and worker capability by outdoor-weather and optional wild-food rates. Aggregate workers consume raw elapsed time even when effective work is slowed, preserving Tier1/Tier2/Tier3 equivalence and output ownership. Winter has a zero seasonal crop rate; Ready crops and stored food remain intact.

Movement reads one weather interval modifier: Tier 1 applies it through the existing NavMesh movement bridge and Tier 2 applies it in the batched ECS job. U19 adds no per-character update. Severe outdoor cold/heat calls the U18 authority with `cold_stress` or `heat_stress`; a stable-ID/day checkpoint makes exposure exactly once. Because canonical indoor/outdoor location does not yet exist, exposure is limited to active outdoor production workers and aggregate Tier2/Tier3 workers rather than inventing shelter or clothing state.

## D-044 Version 6 weather persistence and disposable presentation (2026-10-08)

Snapshot v6 persists world seed, climate profile ID and sorted last-processed exposure day per character. Daily weather and season remain derived, so load on the same day cannot reroll and no redundant history is stored. Versions 1-5 remain readable with no weather payload. PostgreSQL accepts v6 opaque XML metadata and needs no schema migration or realtime weather query.

Environment fog/ambient/directional-light changes and the debug overlay are projections only. They refresh centrally, own no canonical state and are not saved. The debug panel shows current state/modifiers and a deterministic next-day preview, registers with the U16A pointer blocker and exposes no persistent forced-weather override.

## D-045 Stable-ID local combat authority and tactical sides (2026-10-08)

`CombatService` is the only mutable U20 encounter authority. It references canonical Characters by `StableEntityId`, owns deterministic participant/target/cooldown/morale/retreat/outcome state and advances from the existing absolute game clock in one central batch. `CombatSideId` is a narrow encounter allegiance used to distinguish two hostile sides; it is not a U21 army, captain or strategic-faction aggregate. Presentation, NavMesh agents and selected GameObjects cannot own combat truth.

## D-046 Deterministic melee reuses U18 death and pins Tier 1 (2026-10-08)

U20 resolves melee as deterministic always-hit attacks with immutable weapon profiles, stable-ID ordering and a fixed impact-to-injury ladder. All persistent injury, incapacity and death mutation goes through `HealthAndFamilyService.ApplyCombatInjury`; fatal resolution therefore continues through U17 `SuccessionService` exactly once. While an encounter is active, combat owns the character's external activity and blocks Tier1 demotion. This keeps physical movement local without inventing U21/U23 aggregate battle state.

## D-047 Combat snapshot v7 and disposable tactical controls (2026-10-08)

Snapshot v7 persists detached combat encounters, participants, targets, attack sequence/cooldown, morale/retreat, attack history and outcome. Restore follows canonical Character/dynasty and U18 health restore, validates every cross-reference and does not replay attacks or deaths. Versions 1-6 remain readable; PostgreSQL stores v7 as the existing opaque validated XML with no schema migration. Right-click Attack and the IMGUI combat panel are adapters over the service and existing pointer blocker; final UX remains U27.

## D-048 Military units reference canonical Characters and preserve roster history (2026-10-08)

`MilitaryUnitService` owns unit identity, owner/side, membership history, captain, representation, order, abstract-location hook, summary and encounter link. A roster member is only an existing `Character.StableEntityId`; the unit never copies biography, health, dynasty, inventory, presenter or ECS state. Death does not delete membership history. Explicit removal marks the historical record removed, and one Character may be active in at most one unit.

## D-049 Captain eligibility and fallback are deterministic (2026-10-08)

A captain must be an active roster member whose canonical Character is alive, adult and combat-capable through U18. Assignment rejects missing, dead, child, incapacitated and non-member candidates. If a captain becomes invalid, the existing eligible member with greatest Influence wins; stable-ID ordinal breaks ties. If none exists, the unit is Leaderless. U18/U17 remain the death and monarch-succession authorities, and fallback creates no Character.

## D-050 Unit representation composes existing tiers and U20 (2026-10-08)

Detailed/Aggregate is unit representation, not Character tier identity. `MilitaryUnitRepresentationService` performs rollback-safe transitions through the one `TierManager`: living non-incapacitated members use Tier1 for Detailed and the requested existing Tier2/Tier3 adapter for Aggregate. Active U20 participants block aggregation. `MilitaryUnitCombatService` only supplies U20-eligible members and later derives summary/status from U20 plus canonical Character/U18 state; it does not resolve attacks or aggregate outcomes. U23 owns all abstract combat.

## D-051 Version 8 military persistence and disposable controls (2026-10-08)

Snapshot v8 stores stable unit/side/captain IDs, sorted roster history, status, representation, abstract location, order, morale/cohesion summary, active U20 encounter link and revision markers. Restore validates Characters, unique active membership, captain eligibility and encounter cross-references after combat restore. Versions 1-7 remain readable. PostgreSQL continues opaque XML storage and requires no relational migration.

The LocalGameplay unit overlay is development-only. It issues commands to Simulation, reuses U11 Move and the existing pointer blocker, and owns no persistent state. Final army UI remains U27.

## D-052 Stable-ID player control and exclusive movement ownership (2026-10-08)

`PlayerControlService` is the engine-independent authority for the temporary RTS/ThirdPerson ownership mode. It stores only the existing Character `StableEntityId`, admits the current monarch, first eligible heir or player-unit captain, pins that ID in Tier1 and never creates a person, presenter or copied gameplay state. Direct control composes with the existing combat transition guard and external-activity owner rather than replacing them.

The existing `NavMeshMovementDriver` has an explicit `Autonomous`/`DirectPlayer` authority. Direct movement uses the same `NavMeshAgent.Move`; there is no second CharacterController or Transform writer. On entry the current Utility action is cancelled through its existing contract. A U11 manual Move becomes `Assigned` again, while assignment/group metadata is retained. U20 continues logical combat but its movement requests are gated while player control owns locomotion. On exit the scheduler may resume valid work through normal selection.

## D-053 Reusable TPS presentation and mode-scoped input (2026-10-08)

One scene-level TPS Cinemachine rig and target are created once and rebound to the existing Character presenter. Camera priorities switch while the RTS controller and transform are retained for exact in-session return. Camera state is disposable and not a gameplay/save authority.

The Unity Input System owns all bindings. Camera/Pointer/Building maps are disabled in TPS, the ThirdPerson map is enabled, and global Time plus Mode maps remain active. T toggles, Escape exits, WASD moves camera-relative, pointer delta looks and Shift runs. Cursor locking is presentation-only and skipped in batch mode. U22 deliberately adds no second damage or attack input path.

## D-054 Version 9 player-control persistence and safe fallback (2026-10-08)

Snapshot v9 adds only `PlayerControlSaveState`: mode and optional controlled Character stable ID. Load re-enters through normal eligibility/materialization/arbitration. A missing, dead, incapacitated, hostile or otherwise invalid identity falls back to RTS; no GameObject or camera reference is restored. Versions 1–8 remain readable. PostgreSQL continues storing the validated XML opaquely and requires no relational migration or realtime role.

## D-055 U20 encounter state is the canonical cross-mode battle (2026-10-08)

U23 extends `CombatEncounterState` instead of introducing a second aggregate-combat authority. The existing stable encounter ID is also the `BattleId`; one record owns mode, U21 side-unit links, participants, targets, cooldowns, attack sequences, morale, retreat, attacks, outcome and compact battle events. Physical mode continues through U20 movement and position boundaries. Abstract mode changes only how the same state advances. A mode transition therefore cannot reroll health, replace a Character or reset combat progress.

## D-056 Abstract combat uses shared rules and a fixed one-minute step (2026-10-08)

`CombatRules` contains the impact, injury and morale/retreat constants used by both modes. Abstract battles advance from their last committed tick to the target `GameClock` tick in exact one-game-minute steps, respect persisted weapon intervals and choose targets from a stable hostile ordering while retaining valid assigned/current targets. No render delta, Transform, NavMesh or `UnityEngine.Random` participates. Direct catch-up, step-by-step advancement and reversed registration order must produce the same state hash.

Physical/abstract convergence is a distribution gate rather than an identity-equality claim. The accepted U23 evidence is 100 paired starts with 0.0% winner delta, mean casualty delta 1 and mean duration delta 3 minutes. Exact abstract state and transfers are guaranteed; future physical Transform/NavMesh paths and the identity of future casualties across alternate modes are not.

## D-057 Battle transitions compose U21/U22 and snapshot v10 (2026-10-08)

`BattleResolutionService` changes representations through U21's rollback-safe service. Physical units are Detailed and active fighters are Tier1-pinned; abstract units are Aggregate and use their existing Tier2/Tier3 canonical projections. A directly controlled U22 participant blocks Physical→Abstract. Returning to Physical rematerializes the same living, capable Characters from retained U13 runtime state; dead members remain absent and incapacitated members remain canonical but unmaterialized. Exact logical position state is retained, while exact future centimetre-level path continuity is not promised.

Snapshot v10 adds mode, optional unit IDs and compact started/mode-changed/side-broken/captain-death/finished events to the existing combat payload. Attack history remains the detailed casualty evidence. Versions 1–9 default to Physical/no unit links when those fields are absent. PostgreSQL keeps the opaque XML design and requires no schema or realtime-query change.

## D-058 Canonical structural state and retained ruins (2026-10-08)

U24 extends the existing `Building` rather than introducing destroyed, captured or combat copies. The canonical record owns data-driven max/current integrity, structural status, `CombatSideId` owner, fire/checkpoint state, processed damage IDs and compact history. One engine-independent `BuildingDamageService` applies Generic, Combat and Fire damage using integer definition multipliers. Stable event IDs make repeated delivery idempotent; integrity is bounded and destruction commits once.

A destroyed building remains in `BuildingRegistry` and keeps its grid occupancy as ruins. This prevents free overlap and preserves a stable future repair/reconstruction target without implementing repair. Destruction cancels uncommitted site work and makes production/crop progression non-operational. Storage contents move atomically from the building inventory into deterministic WorldPiles at its position; an explicit spill marker makes retries and load continuation non-duplicating.

## D-059 Central fixed-step fire and battle-validated in-place capture (2026-10-08)

Fire is a building state, never a ParticleSystem. The centralized service processes active fires in stable-ID order at a fixed one-game-hour step from an absolute persisted checkpoint. Fixed-point settings own base damage/decay plus U19 rain, snow and heat modifiers. Stable building/tick-derived damage IDs make direct catch-up, incremental advancement, collection reorder and save/load continuation converge without a new RNG stream. U24 deliberately omits spatial propagation, firefighting and smoke/evacuation systems.

Capture mutates owner on the same intact, definition-capturable building only after a supplied canonical U20/U23 battle finishes with the hostile new side victorious. Identity, definition, position, integrity, fire and storage remain unchanged. Active production is cancelled for deterministic ownership revalidation, while the intact captured site may later operate even if its new owner lacks the technology required to place another one. Physical and abstract combat call the same building-damage API; no siege or territory authority is introduced.

## D-060 Building snapshot v11 and disposable U24 presentation (2026-10-08)

Snapshot v11 appends a sorted detached `BuildingWorldSaveState` with structural, owner, fire/checkpoint, processed-event, destruction/spill and history fields. Inventories/WorldPiles and production tasks remain in their existing snapshots. Restore validates the graph before replacement and does not replay destruction, spill or fire intervals. Versions 1–10 remain readable; PostgreSQL continues opaque XML storage and needs no relational migration.

The development overlay and presenter are projections only. The click-blocked panel submits service commands; material-property tint and a lightweight burning ParticleSystem read canonical state. They own no damage, capture, storage or production truth and are not U27 final UI.

## D-061 Stable-ID world graph and canonical settlement references (2026-10-09)

U25 adds an engine-independent validated graph of `WorldLocationId`, routes and `SettlementId`. Settlements reference existing Character and Building stable IDs, U21 units retain their canonical identity/location/order, and U20/U23 encounter IDs remain battles. Local and global presentation never create copies of those authorities. The initial six-node/two-settlement graph is fixed authored data, not procedural generation or a territory system.

## D-062 Absolute-tick travel and rollback-safe representation transitions (2026-10-09)

Global travel is a persisted logical order resolved by absolute canonical game ticks in arrival-tick/unit-ID order. Exactly-once arrival updates the existing U21 abstract location; deterministic hostile co-location starts the existing U23 Abstract battle. Local↔Global mode changes commit mode last and compensate U22 control, U23 battle mode, U21 representation and U13 tier changes on failure. No per-army or per-character global Update exists.

## D-063 Snapshot v12 and additive disposable map presentation (2026-10-09)

Snapshot v12 appends validated graph, settlement, mode and in-progress travel state; v1-v11 remain readable and PostgreSQL needs no relational migration. `GlobalMap.unity` is an additive presentation scene. `LocalGameplay` and its composition root stay loaded off-camera so the existing clock and simulation services remain authoritative. Schematic markers, map camera and controls are disposable U25 development UX; U27 owns final interface design.

## D-064 U30 requires an actual external eight-hour soak (2026-10-09)

U30 is not complete when only a short soak passes or an eight-hour runner exists. The mandatory gate is one real uninterrupted or checkpoint-resumed run whose measured wall-clock duration is at least eight hours and whose structured result reports no critical exception, invariant failure, watchdog failure or runaway-memory signal. The task implementation first prepares and commits the harness as IN_PROGRESS. The night orchestrator then runs PowerShell/Unity without an active Codex session, validates the structured result, and invokes a fresh Codex finalizer to update documentation and create the U30 DONE commit. This avoids spending Codex usage on idle polling while retaining auditable evidence.

## D-065 Pre-U31 FRS compliance patch (2026-10-09)

U31 remains an integration and release gate rather than a feature-development task. Confirmed FRS-to-roadmap gaps are assigned to U30A after the U30 reliability gate: a canonical new-game bootstrap with ten starting player characters and a monarch; normal player save/load access; deterministic AI discovery; AI construction over existing building/resource authorities; minimal Stone Age military supply over canonical inventories; and TPS aiming/attack through the existing U20 combat, weapon, injury and cooldown rules. U30A must also preserve continued sandbox play after military victory. U26, U27 and U28 still own their natural prerequisites—AI state scoping/construction, save/load and work UI, and event chains/cooldowns—so U30A verifies and closes only remaining cross-system gaps.

## D-066 Detect Codex CLI automation flags before task execution (2026-10-09)

The night orchestrator must not hard-code one historical approval flag. On Windows it prefers the npm `codex.cmd` shim to avoid PowerShell Execution Policy blocking `codex.ps1`, reads the installed `codex exec --help`, selects standalone `--approve-for-me` when supported and retains `--sandbox workspace-write --ask-for-approval never` only as a legacy fallback. In CLI 0.160.0, `--approve-for-me` already includes the workspace-write preset and conflicts with a separate `--sandbox`. A no-agent preflight therefore exercises the complete real argument combination in a captured child process until a deliberately missing output-schema file stops execution locally; captured stderr avoids PowerShell `NativeCommandError` under stop-on-error semantics. Real agents similarly capture their native exit code with only the invocation scope set to non-terminating stderr handling, then restore global fail-fast behavior. `--help` alone is insufficient because it can bypass argument-conflict validation. Future CLI drift must fail before any implementation, commit or long soak begins.

## D-067 Strategic AI composes owner-scoped canonical authorities (2026-10-09)

An AI state is a stable-reference orchestration record, not a second game model. Player and AI share canonical Characters, Buildings, military units, settlements and battles. Each state owns a separate instance of the existing research, inventory, production and world-pile authorities. `AiStrategicScheduler` handles multiple states in stable ID order on absolute decision ticks and submits every action through U09/U15/U16/U21/U23/U24/U25 services. There is no `AiResearchService`, generated resource grant, teleported army, forced battle result or per-NPC strategic `Update`.

## D-068 Atomic settlement capture and common defeat rule (2026-10-09)

The capture transaction first validates the finished hostile U23 winner and every U24 building in the settlement. Only after all buildings accept capture are their owners changed; U25 settlement ownership is synchronized last. Any rejection leaves all building and settlement owners unchanged. The MVP defeat rule is identical for every capture caller: a side is defeated when no canonical U25 settlement has that `CombatSideId`. Final victory presentation and continued-sandbox policy remain U27/U30A/U31 work.

## D-069 Multi-dynasty health routing and remote Tier3 bootstrap (2026-10-09)

One health service may bind one succession authority per dynasty ID and routes fatal events by the canonical Character dynasty. This generalizes U17 without copying health or Character state and keeps player/AI histories independent. The normal rival's residents are deterministic seed-derived Characters materialized in Tier3 at a remote logical position; explicit U25 location entry, not active-scene camera distance, is the path back to Tier1.

## D-070 Snapshot v13 and deterministic strategic identities (2026-10-09)

Snapshot v13 stores each AI faction's strategic checkpoint, targets, orders, bounded history, sequence counters, defeat state, dynasty, research, production/inventories and world piles. Existing sections still own travel, units, battles and buildings. Restore rejects identity, ownership and target mismatches before applying faction state. AI-created building and unit identities are derived from world seed, state ID, purpose and decision sequence; direct catch-up and stepped execution therefore converge without replaying a decision. Versions 1–12 remain readable and PostgreSQL remains opaque XML.

## D-071 One UI Toolkit projection and explicit developer mode (2026-10-09)

U27 uses one programmatic UI Toolkit document with a runtime theme and a 1600×900 scale reference. It owns transient panel/confirmation choices only; all displayed values are rebuilt from canonical services and all commands call existing boundaries. Legacy IMGUI debug behaviours are disabled by default and F10 is the only developer-surface toggle. Stable IDs, forced damage/health/combat actions, hashes and AI scoring never enter player mode.

## D-072 Local slot metadata and post-load rebinding (2026-10-09)

The always-available player save path remains `SaveCoordinator` plus `FileSaveStore`; U27 adds an optional `ISaveSlotCatalog` capability and a three-slot adapter, not another persistence authority or schema. Corrupt metadata is redacted. A successful load adopts the coordinator's replacement clock, rebinds the centralized runtime driver, clears selection and reconciles disposable Local/Global/UI presentation exactly once. Tier3 absolute cursors are derived projection state and are rebased to the restored clock so a load to an earlier tick cannot make the next batch advance backwards.

## D-073 Player UI blocking and bounded refresh (2026-10-09)

UI Toolkit regions join the existing `WorldPointerRaycaster` blocker before physics, preserving the U11/U08 no-click-through contract for selection, Move and placement. Text-field focus suppresses U27 time/build/group hotkeys. The UI computes a compact cross-service state hash at 4 Hz and rebuilds only changed projections; it never gives each Character, unit or panel an Update loop. Local/Global, RTS/TPS, panel and Save/Load changes reuse one guarded subscription set.

## D-074 One event authority with canonical outcome adapters (2026-10-09)

U28 owns one engine-independent `EventService` for eligibility, offer selection, chain state, player/AI resolution, cooldown and event history. Immutable definitions and typed stable target references live in Domain; Infrastructure only composes targets/content; U27 is a projection/command adapter. Outcomes cross narrow `IEventEffectExecutor` boundaries into existing authorities. The shipped relationship and health outcomes call U18 exactly once; U28 does not duplicate Character, health, death, succession or relationship state.

## D-075 Keyed weighted selection and active-scope suppression (2026-10-09)

At every due canonical checkpoint, all eligible definition/target candidates are stably ordered and one candidate is sampled per owner/target scope. Positive definition weights are relative integer weights; a configurable `noEventWeight` is an explicit additional bucket and totals use checked arithmetic. U14 keyed RNG includes world seed, stable definition/target identity, absolute tick, purpose stream and occurrence/transition sequence, so neither registration order nor a mutable cursor affects results. Cooldown starts at offer and uses definition + owner + ordered targets; an unresolved instance also blocks another offer for the same definition/scope, bounding pending growth.

## D-076 Persisted chains are exactly-once bounded state machines (2026-10-09)

One deterministic `EventInstanceId` remains the chain/root identity across nodes. Active state persists current node, due tick, sequence, selected choice, resolved-node records and applied `node:index` effect markers. Player commands are typed; AI uses applicable integer scores and choice-ID tie-breaks. Effects commit before resolution markers, delayed nodes return to canonical checkpoints, and terminal instances move to history. Catalog validation rejects immediate cycles, while a hard 32-resolution per-instance budget converts any excessive immediate path to a controlled Invalid result.

## D-077 Event history is bounded detail plus deterministic archive summary (2026-10-09)

U28 retains the newest 128 terminal event entries in stable resolution/instance order. Evicted entries increment an archive count and update a deterministic checksum. Active/unresolved instances are retained separately and never evicted. This bounds the v14 payload while preserving replay/audit signals, but it is not presented as the complete 2000-year chronicle requested by later information-system work.

## D-078 Snapshot v14 and U27 event projection (2026-10-09)

Snapshot v14 appends event seed/checkpoint/sequence, active chain state, cooldowns, markers, bounded history and archive summary to strict XML. Restore validates a detached candidate after canonical services are restored and never reapplies outcomes. Loading v1-v13 resets event state at the restored tick; PostgreSQL keeps its opaque payload schema and accepts v14 metadata. U27 adds one Events panel, pending HUD count, Character-history projection and notification adaptation; it stores no canonical event state, reuses the existing blocker/hash refresh/subscriptions and suppresses restored-history notification spam.

## D-079 U29 performance evidence is offline, repeatable and fail-closed (2026-10-11)

The mandatory U29 result is a visible Windows Player matrix at 100/300/500/1000 Characters with an exact 25/50/25 tier mix, 60 seconds warm-up, 300 seconds measurement and three repeats. The local PowerShell runner makes no Codex/API calls, keeps the machine awake, maintains Player foreground and writes raw frames, summaries, screenshots and logs into a new output directory. Missing repeats/artifacts, background samples, population drift, identity/tier mismatch, save/load mismatch, runaway memory or worst-repeat p95 above 33.33 ms fail closed. A smoke or performance-only iteration cannot complete U29.

## D-080 Canonical simulation is decoupled from bounded disposable presentation refresh (2026-10-11)

Tier1 AI and all canonical services continue to advance on every normal frame. NavMesh movement/Animator-parameter projection is sampled at a nominal 20 Hz and distributed round-robin across three target-rate frame slices instead of batching every presenter on one frame; when the frame rate falls, presentation cadence degrades without accumulating catch-up debt. Unchanged native properties are cached, destroyed-agent cleanup is sampled at 1 Hz, and the hash-gated U27 HUD automatically refreshes at 1 Hz while commands still refresh immediately. These are presentation scheduling boundaries, not benchmark-only shortcuts: population, AI work, path motion, camera, UI, tiers and save/load stay enabled. This removes redundant main-thread native calls and periodic frame spikes while preserving deterministic state and responsive player-visible behavior.

## D-081 Tier1 skeleton animation uses a bounded shared presentation budget (2026-10-11)

Tier1 presentation must not leave hundreds of visible `Animator` components in Unity's automatic per-frame loop. `CharacterPresenter` evaluates its initial pose, switches its Animator to manual scheduling and uses `CullCompletely`; the scene-level Tier1 driver advances at most 16 skeletons per frame in stable round-robin order with elapsed-time compensation. NavMesh movement and canonical AI remain independently scheduled, and direct control refreshes its single skeleton immediately. Autonomous NavMesh agents use low-quality local avoidance because collision avoidance is disposable presentation rather than canonical state. The cap is normal population-scaled LOD, applies outside benchmarks and never changes StableEntityId, tier, decision, movement target or save truth.

## D-082 Tier1 movement presentation is event-driven with bounded reconciliation (2026-10-11)

The sustained matrix showed that even distributed 20 Hz polling and a 16-skeleton cap lose margin after long sequential CPU/GPU load. Autonomous `MoveTo` and `Stop` now update Animator movement parameters immediately, while pause/time-speed commands still force immediate all-agent reconciliation. Only unchanged weather/speed projection is sampled round-robin at 2 Hz with no catch-up debt. Idle AI entries do not reread an engine Transform because they cannot move; active actions and direct control retain the canonical position path. Manual skeleton work is capped at 8 presenters per frame. These are normal disposable-presentation LOD rules, not benchmark detection: decision/need/action cadence, NavMesh motion, StableEntityId, tier and save state remain unchanged. D-082 supersedes the 20 Hz/three-slice and 16-skeleton numeric settings in D-080/D-081 while retaining their architectural boundary.

## D-083 Locomotion animation is continuous only while a presenter moves (2026-10-11)

Sparse manual skeleton stepping is not an acceptable optimization for locomotion because NavMesh translation then appears as foot sliding. A presenter with movement speed above the controller threshold remains in Unity's continuous Animator loop. When movement stops, the existing 0.15-second locomotion-to-idle transition is allowed a 0.2-second unscaled grace period; the shared Tier1 scheduler then disables that Animator and retains the evaluated idle pose. Direct control uses the same rule. Idle animation is disposable and frozen, while walking/running animation is smooth, so CPU cost scales with active movers rather than every visible Tier1 character. D-083 supersedes the manual-update behavior in D-081/D-082 but retains their shared retirement scheduler and canonical/presentation separation.

## D-084 Sustained diagnostics are separate from acceptance evidence (2026-10-12)

The failed full matrix cannot be replaced by a healthy short control. An opt-in `-u29Diagnostics` observer records completed-frame callback sections, available native profiler markers, collection-count changes and one-second process CPU samples, with an explicit simulation-frame alignment check. Recorder names/categories/units are discovered in the actual Player; missing/non-emitted values are -1, never invented zero-cost evidence. A 64 KiB allocation self-test qualifies per-thread allocation readings. Buffers are reserved before measurement and files are written afterwards. Nested/multithread timings must not be summed, collection frequency is not pause duration, and CPU frequency/temperature are not inferred. The dedicated offline runner uses one 180+900-second population-1000 capture and reports `DIAGNOSTIC_COMPLETE`, not a gate PASS. Its observer overhead is not silently added to the default final matrix. D-083 locomotion and all canonical behavior/acceptance thresholds remain unchanged.

## D-085 Batch component access and prepare only due work (2026-10-12)

Tier2 pending-production consumption resolves its ECS dependency barrier and a fresh ComponentLookup once per batch; it retains the existing sorted managed identity index and consumes each positive pending-work value exactly once. It does not reduce ticks, skip residents, add an authoritative state copy or keep a lookup across structural changes. Weather validates the complete outdoor roster before mutations but sorts only residents whose individual exposure checkpoints are behind the requested day, preserving same-day late arrivals and restore/catch-up semantics. Its immutable seed/profile/day function memoizes the last WeatherState without persisting the cache. Production still advances its canonical checkpoint on every call, but prepares the abstract-worker roster only when an existing fixed production step is due. These optimizations apply to normal gameplay, not just benchmarks. D-083 locomotion remains unchanged.

## D-086 Diagnostic capture completion is independent of performance verdict (2026-10-12)

A deliberately diagnostic single-population run must finish writing detailed evidence when p95 fails. The generic reporter can explicitly return without throwing on its verdict for that workflow; it still writes FAIL, and the ordinary matrix keeps strict failure behavior. The wrapper attempts detailed-report recovery even after a runner error, then propagates integrity/runtime failures. Invalid/unverified per-thread allocation values are omitted instead of displayed as zero; repeat table results include reporter blockers, so a smoke harness success cannot be mislabeled as a performance PASS. No acceptance threshold changes.

## D-087 Triggered native traces are offline, bounded and diagnostic-only (2026-10-12)

When selected markers cannot attribute a sustained failure, an explicit diagnostic option waits for at least 12 over-budget frames in a 60-frame window after ten measurement seconds, captures eight seconds through Unity's binary profiler and exits. Timeout without a complete capture is an error. Full-profile overhead invalidates performance acceptance, so config validation rejects this option for mandatory gates. A headless pinned Editor exports native per-thread inclusive/exclusive markers, omitting partial boundary frames and retaining call-path context. Clearing Profiler.logFile flushes the saved trace. Unity profiler API reference: https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/ProfilerEditor/Public/ProfilerAPI.bindings.cs .

## D-088 U29 may be checkpointed as PARTIAL without weakening its gate (2026-10-12)

The user explicitly chose to preserve the completed U29 infrastructure and safe optimizations, record the unresolved p95 debt, and prepare U30 in a separate chat rather than spend more runs now. PARTIAL is not DONE: p95 <=33.33 ms at 1000 Characters remains unproven and U31 must not interpret the checkpoint as a passed dependency. U30 may proceed as an explicit sequencing exception because its eight-hour reliability soak can collect additional long-duration performance evidence. It must not rewrite U29 thresholds or silently close the debt. The rejected four-worker experiment is not adopted into runtime or runner defaults.

## D-089 U30 is segmented, fail-soft and finalized from external evidence (2026-10-12)

The official reliability gate runs outside Codex through `Tools/Run-Soak-U30.ps1`. The runner targets at least the requested real duration and the requested productive simulation duration, splits work into restartable Player segments, writes append-only heartbeat/performance/incident/segment evidence, and uses bounded backoff. A recoverable gameplay exception, invariant failure, invalid checkpoint, crash or watchdog restart permanently makes the aggregate verdict FAIL but does not discard later observation. Immediate stop is reserved for exhausted safe recovery or evidence integrity. U30 remains IN_PROGRESS after harness preparation; only a fresh finalizer may mark DONE after auditing an actual result with requested and actual duration at least 28800 seconds.

## D-090 Exact checkpoint identity and repeatable logical outcome use separate hashes (2026-10-12)

Every checkpoint stores snapshot-v14 XML plus all representation-neutral Tier runtime fields, SHA-256 hashes and an exact canonical hash. Resume verifies the bundle, restores canonical time/state first, then transactionally restores exactly one tier projection per StableEntityId; births added during a prior segment are seeded into the canonical registry before transactional load. The cross-process `final_state_hash` separately normalizes legacy GUID identities and frame-scheduled presentation interpolation so same-seed/config runs compare logical outcomes before U30A supplies deterministic New Game identities. The normalized hash never substitutes for exact checkpoint verification.

## D-091 The offline soak process owns a temporary Windows sleep guard (2026-10-12)

The runner calls `SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED)` for its process lifetime. This prevents system sleep during an unattended night run without changing the user's persistent power plan; display power-off remains allowed, and the request is cleared explicitly on normal completion or automatically when the process exits.

## D-092 Failed official U30 evidence does not close reliability or memory gates (2026-10-12)

The external seed-3030 run in `C:\serenity_u30_soak_20261012_run3` is audited FAIL after 4,364/28,800 seconds. Weather exposure to a dead Character causes an exception storm and five watchdog attempts. U30 remains IN_PROGRESS and U30A/U31 remain unstarted. A completed aggregate JSON is not proof that its counters include aborted Player segments: this run retains 182,677 gameplay exceptions while aggregate critical errors are six and performance/coverage are zero. Future U30 remediation must preserve aborted-segment evidence and the actual first gameplay failure, maintain trustworthy step recovery, qualify memory readings and reconcile early-stop conditions with the original prompt. This entry records an audit boundary, not an implemented fix or a relaxed gate; the failed artifacts must remain immutable and a fresh passing eight-hour run is still mandatory.

## D-093 U30 recovery discards partial steps and retains live aborted-attempt evidence (2026-10-12)

Weather exposure treats a canonical dead resident as ineligible for a new condition while advancing that resident's exactly-once day cursor; unknown identities remain errors and HealthAndFamilyService remains the mutation authority. A callback exception may occur after the clock or earlier services mutate, so the U30 Player records it and exits the attempt without checkpointing that partial state. PowerShell imports complete JSONL records while the Player runs, captures the original failure/reproduction, and aggregates atomic progress evidence even after watchdog termination. Repeated failures continue with bounded backoff to the real deadline while a verified checkpoint or deterministic fresh bootstrap is safe; attempt count alone is not an immediate-stop condition. Raw frames rotate, per-segment sampling is bounded, working-set availability is a positive PASS gate and unqualified allocation samples are unavailable rather than zero. The checkpoint-5 regression crossed the original Y1 D35 failure with zero incidents. This changes U30 evidence/recovery behavior without weakening its eight-hour acceptance gate.

## D-094 New Game identities derive from seed, role and stable index (2026-10-12)

Normal New Game has one immutable `NewGameConfiguration` and derives the session, ten player Characters, six AI Characters, dynasties, settlements, starting buildings, resource piles and other canonical bootstrap identities from world seed, semantic role and stable index. The player roster is always ten and includes a valid monarch, spouse and two heirs. Starting the same seed/config produces the same logical initial state; a different seed changes it. Scene reload is the composition reset boundary, and the one-use requested seed prevents duplicate roots, UI documents or subscriptions. Player-created entities after bootstrap may still use ordinary generated IDs.

## D-095 Discovery and victory are canonical campaign facts (2026-10-12)

`CampaignProgressService` owns rival discovery, discovery tick/marker, victory tick/marker and audit sequence. Discovery occurs exactly once when a player unit reaches a rival settlement or another canonical shared-contact path is reported; presentation merely hides or reveals rival detail. Victory occurs only after a finished hostile battle won by the player has been processed through the existing U26 settlement-capture transaction and the shared no-settlement defeat rule confirms the AI side is defeated. Victory is a banner/event state, not a clock stop, so sandbox play and Save/Load continue.

## D-096 Stone Age military supply is conserved inventory state (2026-10-12)

U21 military units own a persisted supply checkpoint state, but berries remain canonical quantities in existing resource inventories. Resupply transfers berries atomically from an eligible owner inventory into the unit ledger; rollback preserves conservation. U25 travel and U20 combat consume configured amounts at stable exactly-once checkpoints. Shortage applies fixed-point travel, combat and morale modifiers through those existing authorities. Representation changes never refill or duplicate supply, and v1-v14 snapshots load with an explicit empty/uninitialized default that preserves legacy behavior.

## D-097 TPS attack is an input adapter over U20 (2026-10-12)

The ThirdPerson Attack action is a press-only left-mouse command. `ThirdPersonControlController` resolves the aimed canonical target and delegates to `CombatService.TryManualAttack`; U20 remains the sole authority for encounter membership, hostile-side validation, range, weapon profile, cooldown, damage, injury, morale and battle outcome. Pause rejects the command, holding the button does not create repeated attacks, and physical/abstract/direct-control transitions preserve the same unit, Character and battle identities.

## D-098 Snapshot v15 appends supply and campaign state (2026-10-12)

Snapshot v15 strictly serializes per-unit supply/checkpoints and the campaign discovery/victory audit. Restore validates detached state before committing and never replays discovery or victory notifications. V1-v14 decode with safe empty campaign and uninitialized supply defaults; PostgreSQL remains an opaque snapshot store. The U30 resume composition must bind `CampaignProgressService` to the v15 coordinator, while the isolated U30A Windows build path is a verifier option and does not change the normal release path.

## D-099 U31 acceptance adapters command the normal composition and own no gameplay truth (2026-10-12)

U31 automation uses the public U27 UI, the normal `LocalSceneCompositionRoot`, real Input System actions and the established U09/U15/U16/U20-U28 services. Its command-line Player smoke is attached only for `-u31Smoke`, records assertions/screenshots, and may request UI New Game/Save/Load; it stores no canonical state and is absent from ordinary startup behavior. Long logical intervals may be accelerated through the existing GameClock/runtime and production commands, while battle outcome, capture, defeat and victory remain owned by U20/U23/U26/U30A. U31 evidence is isolated from U29/U30 evidence and cannot satisfy either external gate.

## D-100 A structured soak PASS remains subject to behavioral audit (2026-10-12)

Run4's duration, exact snapshot integrity and restart continuity pass, but its persisted AI history exhibits the invalid-target loop prohibited by the original U30 contract. Reference-validity checks do not establish liveness. Keep U30 IN_PROGRESS and preserve original PASS artifacts without rewriting them. The correction belongs to U26/U25 progression and U30 observation, not an alternative gameplay authority or a relaxed gate. Planned process restarts bound memory conclusions to observed segment length; invocation counters and normalized outcome hashes must not be promoted to sustained-workload or exact-identity evidence. This records an acceptance boundary, not an implemented fix.

## D-101 AI arrival is a persisted hold, not implicit victory

At the target location U26 completes the travel order and retains one AwaitBattle intent with its ordered target. It does not issue a self-route or capture without a canonical victory. Reconciliation accepts travel/hold intent and reads the order's target rather than a transient display objective; later U25 hostile arrival can therefore still lead to U23/U26 capture. U30 observes repeated rejected travel decisions and stale travel orders. A new occupation rule for undefended settlements requires separate gameplay design and is not invented by the reliability harness.

## D-102 U30 evidence schema 2 is exact and explicitly scoped

The CLI seed is resolved before NewGame bootstrap. Checkpoints hash canonical XML plus every tier continuation field using an explicitly versioned binary encoding; floating-point values additionally use exact base64 numeric bits because Unity JSON doubles are not bit-exact. Schema-1 soak bundles remain immutable historical evidence, not eligible schema-2 resumes. Gameplay save format remains v15. Outcome SHA preserves XML identity/ownership/hierarchy and excludes frame-dependent presentation; it is not a claim that an unmeasured repeated run is deterministic.

Heartbeats reference the last verified checkpoint hash, explicitly not a fresh live-world hash. This removes full snapshot serialization from each heartbeat. CPU uses floating-point seconds; profiler values average valid frame observations; segment GC is cumulative and the startup delta is excluded. Process-segment memory, native shutdown diagnostics and invocation versus observed workload coverage are separate evidence. Source/input fingerprint, build GUID, executable/project DLL SHA and runner SHA identify new artifacts, including builds made from an uncommitted worktree.

## D-103 Health history stays canonical while hot queries use a derived active index

Recovered/fatal records are not silently deleted to improve memory graphs. An active-condition index is maintained on apply/recover/advance and rebuilt from validated canonical state on restore. Tick and capability work no longer traverses recovered history. This fixes repeated hot-path history scanning, not the separate long-term history-retention policy; snapshot size and retained-condition count remain visible and archival/schema work stays explicit.

## D-104 Technical integration is not visual product acceptance

The requested development plan preserves existing systems and introduces explicit product acceptance for Russian UI, first launch/main menu, appearance creation, a coherent semi-realistic authored Stone Age region and the first playable hour. Green unit/integration tests alone do not approve art or usability. `PRODUCT_ROADMAP_RU.md` is a proposed delivery order and does not activate unrelated tasks or approve purchases. U29/U30/U31 statuses and the prohibition on a new eight-hour run remain unchanged.

## D-105 Late outdoor entrants cannot receive backdated weather illness (2026-10-13)

The seed-correct short Player control exposed a late outdoor roster entry after the health clock had advanced. A missing exposure cursor previously replayed days from zero and attempted ApplyCondition before HealthFamily.LastCalendarTick, causing a deterministic exception. Weather now retires unobserved historical days older than the health authority's watermark without inventing retroactive illness. Current/future due days still use normal deterministic exposure and health commands. This also avoids treating a newly introduced newborn/worker as observed outdoors before its first roster entry. Direct backdated health mutation remains rejected; weather checkpoint/idempotence and restore rules remain intact. The failed short control is retained as evidence, not rewritten into PASS.

## D-106 Undefended occupation is approved product scope, not implemented (2026-10-13)

The user approves the same rule for player and AI: defenders require combat; an undefended settlement can be occupied without combat; arriving defenders can interrupt occupation; completion changes ownership and evaluates the common state-defeat rule. PRODUCT_ROADMAP_RU P7 records implementation and acceptance work. Duration, defender eligibility, interruption progress and competing armies remain explicit design parameters. D-101 still describes the current implementation; no fabricated battle result or harness-only capture shortcut is authorized. This documentation change neither implements occupation nor starts another eight-hour run. The user requested a launch command for their own use, not an agent-launched run.

## D-107 Final performance qualification follows first-era feature completion (2026-10-13)

By explicit user decision, product directions P1 through P7 now execute sequentially before final performance and endurance qualification. Every stage still runs proportional focused tests and a build; this decision defers only the expensive final optimization, U29 matrix, native-memory/history closure, continuous-process endurance, new eight-hour U30 and U31 finalization to P8. Existing failures and acceptance thresholds remain recorded and cannot be reclassified as passes. Each product direction runs in a separate Codex chat from the committed result of its predecessor; chats must not edit the shared checkout concurrently. P3 visual and P6 usability acceptance remain user decisions even when an autonomous chain prepares provisional implementations.

## D-108 Russian localization is a presentation boundary (2026-10-13)

`ru-RU` is the default shipped locale. Stable player-facing keys live in `RussianTextCatalog`; content/event/enum identifiers and save v15 remain canonical English IDs so persistence, deterministic hashes and service contracts do not change. Supplied fictional person/dynasty names are transliterated only when displayed. Missing translations log once and degrade to a neutral Russian fallback, while unexpected exceptions are logged technically and replaced with a safe Russian player message. Legacy diagnostic overlays are also translated because they are visible game surfaces, but stable IDs may remain visible there by design. No external localization package or Asset Store dependency is added.

## D-109 Startup reconstructs identity before canonical load and preferences remain external (2026-10-13)

The separate `MainMenu` scene may inspect only validated slot metadata. A usable metadata record includes the world seed already present in canonical v6+ save sections; Continue/Load passes that seed through the existing one-use New Game launch state so `LocalGameplay` reconstructs the same deterministic StableEntityIds before `SaveCoordinator.Load`. A corrupt, missing or older seedless slot is explained and left untouched. No failed load falls back to a new game.

Volume, display, quality, UI scale, camera speed and primary key overrides live in a versioned atomic user-preferences JSON file outside snapshot v15. Exit dirty detection compares the canonical serialized snapshot against the last successful Save/Load fingerprint only when queried. The menu, preference file and fingerprint do not become gameplay authorities.

## D-110 P3 establishes a reversible shore-camp reference, not final art (2026-10-13)

The first visual baseline is a warm stylized-PBR/mid-poly camp on a forest clearing beside a curved shore. Water and sand form the left boundary; an open central lawn preserves construction and RTS readability; trees and rocks frame rather than occlude the camp; the fire, shelter and storage form the close-view focus. Clear daylight uses warm directional light and long readable shadows; rain changes fog/light, emits downward screen-readable drops and supplies its own ambience. One metre remains one Unity unit.

Only approved local wrappers are used: Hodaart Characters 01/02, WizardHat tree/rock/campfire and axe/spear, plus project-owned primitive shelter/storage and generated P3 meshes/materials/audio. Vendor files are not edited and no package is imported from license-review-required staging. The Hodaart/primitive content remains grade-C placeholder art; this decision constrains P4/P5 composition and palette but does not claim final visual acceptance or authorize a purchase.

Canonical Characters and buildings remain Domain/Simulation objects with deterministic `StableEntityId`. `StoneAgeReferenceCampPresentation` may attach disposable equipment, materials, particles, light and audio to their presenters, but owns no alternate character/building state. Weather preview used by visual validation is presentation-only and cannot mutate `WeatherService`. The P3 scene builder is idempotent so this reference can be replaced without a migration of canonical gameplay state.

## D-111 Appearance is canonical profile data; unavailable modular art is not fabricated (2026-10-13)

Every persistent Character has an `AppearanceState` keyed by the same `StableEntityId` and containing stable profile/body/face/skin/hair/beard/hair-colour/clothing IDs. `CharacterAppearanceService` is the canonical authority; GameObjects, preview and tier adapters are disposable projections. Identity, sex, family, dynasty and succession stay in their existing authorities. Snapshot v16 appends appearance state; v15 migration deterministically assigns a sex-compatible default. A saved unknown profile is retained canonically and presentation emits one warning before using the configured sex-compatible visual fallback.

The approved runtime catalog provides two whole Hodaart wrapper models and no verified modular variants. P4 therefore exposes two integrated compatible profiles and read-only stable component IDs. Randomize selects only among those approved profiles, deterministically from seed and sequence. Independent face/hair/beard/clothing controls are deferred until authored compatible assets exist; P4 does not imply that swapping those components is currently supported. No license-review asset or new dependency is admitted.

## D-112 The P5 region is an authored projection over existing authorities (2026-10-13)

P5 extends the idempotent P3 scene recipe rather than creating a parallel world model. Scene anchors and project-owned meshes describe the visible water boundary, routes, clearings, resource views and rival territory. `ProductionService`, `GlobalMapService`, buildings, Characters and `WeatherService` remain canonical. The first four deterministic `player_source` indices are preserved for save compatibility; additional finite nodes receive new indices. Save/Load rebinds visible source presenters to restored sites by `StableEntityId`.

Water is explicitly non-walkable, approved wrapper/resource colliders remain obstacles, and construction uses the existing research/funding/occupancy path on the authored clearings. Seasonal tint/snow is a renderer projection only. Local player and rival anchors correspond to stable global location IDs; P5 does not replace the existing schematic global-map authority. Only registered P3 wrappers and project-owned generated assets are used, no dependency or vendor source is added, and the result remains a reversible provisional visual baseline pending user acceptance.

## D-113 First-hour guidance observes canonical proof and owns no tutorial state (2026-10-13)

P6 guidance is a disposable Presentation projection. It accumulates only whether the player has observed or caused canonical facts: a selected Character, a Work job, operational shelter/storage viewed in the settlement panel, newly produced food, an active/completed non-starting research project, resolved danger/event evidence and player scouting travel/contact. It cannot grant resources, create jobs, advance research, resolve events or issue travel. Those changes remain commands to the existing Simulation services.

The route is visible by default but can be hidden and restored for the current session. Its visibility/progress is not serialized into the world snapshot or user settings, so Save/Load never gains a second tutorial authority and older saves need no migration. Starting shelter/storage may satisfy that stage only after the player opens the settlement explanation; starting berries do not satisfy food until canonical production records new output. Manual UX acceptance remains separate from automated proof.

## D-114 Undefended occupation is a timed canonical claim with defender-first ordering (2026-10-13)

P7 implements D-106 symmetrically in `GlobalMapService`. A claimant must be an idle aggregate military unit physically at the hostile settlement with at least one combat-capable member. Residents do not become an implicit militia; only an aggregate owner-side unit with at least one combat-capable member is a defender. If defenders and attackers coexist, U25/U23 starts a real battle and no occupation is created. If a single hostile side is present without a defender, the stable lowest `MilitaryUnitId` becomes claimant for a 24-game-hour occupation.

Arrival/contact and battle creation are evaluated before occupation completion at the same tick. A defender arriving exactly at completion therefore interrupts the claim and starts combat before ownership can change. A missing, moved, engaged, disbanded or combat-incapable claimant, an owner change, or a competing hostile side interrupts the claim. Stone Age interruption always resets progress; later retention rules require a new explicit state model. Multiple claimants on the same side do not multiply speed, and different hostile sides cannot begin a claim while contested.

Completion delegates one prevalidated ownership transaction to `SettlementCaptureService`/`BuildingDamageService`, records one terminal occupation event, removes the active claim, then applies the same `StateDefeatRule` for player and AI. No battle or victory is fabricated. Snapshot v17 persists active occupations, bounded event history and campaign occupation provenance; v1-v16 remain readable. The UI projects progress, remaining time, interruption/completion notifications, defeat reason, pregnancy/child age and adulthood feedback without owning gameplay state. P7 acceptance instrumentation exists only under `-p7Smoke`.

## D-115 P8 visual rollout is staged before final qualification (2026-10-14)

P8 is split into P8.1–P8.6. P8.1 establishes a compact, reversible stylized-natural URP/PBR benchmark containing shore, land, forest, stone, calm daylight/atmosphere and one detailed customizable Character. P8.2 may propagate only the accepted map language. The approved continuation is P8.3 working groups and simple jobs, P8.4 full appearance editor, P8.5 polished interface and P8.6 integration/performance/endurance/release qualification. This prevents an unaccepted visual direction from being multiplied across the complete region while preserving the existing P3/P5 canonical locations and systems. U29, U30 and U31 retain their recorded statuses and gates; only P8.6 may close them.

## D-116 UMA is a replaceable presentation provider; AppearanceState remains canonical (2026-10-14)

UMA 3.1f3 is pinned to the upstream release commit and admitted under MIT for P8 character rendering. The package/runtime plus the official non-demo races, slots, overlays, textures, wardrobe, animation/DNA/pose and URP support content are retained locally even though the resulting asset is large; the user explicitly accepts that size. `AppearanceState` and `StableEntityId` remain the only persistent character appearance truth. The benchmark maps that stable profile to UMA through reflection and can fall back to the project-owned presenter without changing saved state, family, dynasty, health or simulation authority.

The official Unity URP Terrain Sample remains the approved terrain reference, but is not imported in P8.1 because Asset Store acquisition requires the user's authenticated Unity session. Its approximately 2.8 GB size is accepted and is not the blocker. P8.1 therefore uses deterministic project-owned benchmark meshes/materials/textures; P8.2 may replace that presentation after authenticated acquisition without a save migration.

## D-117 P8.2 extends the P5 projection and resource catalog without a parallel map authority (2026-10-14)

P8.2 remains an authored Presentation pass over the existing P5/P7 services. A serialized finite polygon validates scene placement and drives custom land/shore/water meshes, but it is not a second `GlobalMapService`, construction grid or save payload. Existing `player_source` indices 0–15 remain stable; iron/gold use new indices 16–23 and ordinary `ProductionSite`/recipe/resource definitions. Local and global resource markers retain canonical site IDs and own only hit testing, highlight and navigation actions.

The P8.2 generator deliberately keeps the legacy P3 ground/shore/water object names as compatibility handles while replacing their meshes/materials. Production catalog construction conditionally adds iron/gold recipes only when those resource definitions exist, preserving isolated legacy-domain fixtures. The official Terrain Sample remains deferred; all new P8.2 geometry and materials are project-owned and replaceable without a save migration. The next approved slice is P8.3 working groups/simple jobs; P8.4–P8.6 and the U29/U30/U31 gates remain outside P8.2.

## D-118 P8 continuation follows gameplay control before appearance and UI polish (2026-10-14)

After P8.2, the sequence is P8.3 working groups and simple jobs, P8.4 full appearance editor, P8.5 polished interface and P8.6 integration/qualification. P8.3 owns the direct flow «click an object → action → priority», assignable profession groups and the explicit «Прямо сейчас» priority. It must extend the canonical U11 scheduler/reservation/job state and Save/Load rather than introducing UI-owned work state or a per-NPC `Update` loop.

P8.4 owns expanded appearance editing over canonical `AppearanceState`; P8.5 owns the cohesive final interface and outstanding user-facing acceptance; P8.6 alone owns final cross-system integration, the complete U29 matrix, U30 endurance/native-memory/history closure and U31 release qualification. This decision corrects the superseded post-P8.2 handoff wording that assigned P8.3 to character-art rollout and P8.4 to building/effect art; it does not alter completed P8.1/P8.2 implementation or evidence.

## D-119 Direct work is a canonical role order with guarded immediate interruption (2026-10-14)

P8.3 extends the existing U11 `WorkManager`; UI and map markers never own assignments. Nine deterministic `WorkGroupRole` groups describe gatherers, builders, haulers, crafters, farmers/gatherers, scientists, scouts, warriors and caregivers. Membership remains exclusive and explicitly reassignable. A direct order stores a stable target plus position, role group, priority, assignee, lifecycle and claim state. Candidate selection is deterministic: nearest target distance, then suitability from profession/skill/attributes, then `StableEntityId`. Exclusive object reservations reject competing live orders.

`Immediate` is the canonical value displayed as «Прямо сейчас». It may preempt only a Tier1 scheduler action that reports interruptible and only when the Character is not under direct/external control or critical-rest protection. Carrying and production work/delivery phases remain non-interruptible. The interrupted order is released back to the queue without duplicating an assignee or target claim. Normal/high queued work can remain attached to off-camera members and execute after materialization; one central scheduler/tick reevaluates queues, and no per-NPC `Update` is introduced.

Explicit gather/farm/craft orders reserve the selected canonical `ProductionSite`, bypassing automatic stock-target selection but not capability, inputs, storage, site availability or production claims. Other simple duties use the same central movement/action boundary. Snapshot v18 appends work groups/orders and rebuilds exclusive reservations with strict duplicate validation; v1-v17 stay readable. P8.4 may consume these groups but must not move appearance authority out of `AppearanceState`.

## D-120 Detailed appearance is canonical bounded data; UMA remains a selective projection (2026-10-14)

P8.4 extends `AppearanceState` with stable eye-colour, age and ordered integer morph parameters in the closed 0–1000 range. Snapshot v19 stores these values; v1–v18 remain readable, and v18 appearance records receive explicit adult/brown-eye/no-morph defaults rather than inferred vendor state. The editor, deterministic randomize/reset, New Game, tiers and Save/Load all exchange this same state. UMA recipes, generated meshes, DNA names, materials and GameObjects remain disposable Presentation details and never become canonical identity, family, dynasty, health or simulation data.

Only compatible content actually present in the admitted UMA package is exposed: two human races, three skin tones, four hair choices, male beard choices, five hair colours, five eye colours, three age treatments and nine supported morphs. Clothing remains one verified compatible set and the UI says so; P8.4 does not fabricate a modular Stone Age wardrobe. Detailed UMA projection is created only for states carrying the complete editor parameter set, which covers the ruler/key-character path. Compact generated defaults for ordinary mass NPCs keep the existing wrapper fallback and avoid a large population of UMA generation coroutines. A failed or missing UMA projection leaves canonical state intact and shows the sex-compatible fallback.

Preview and world use the same projection. UMA creates render children asynchronously, so the projection reapplies the owning layer after mesh generation; this is required for isolated render-texture cameras and does not change world collision. Generated UMA colliders remain disabled and rigidbodies non-colliding. The preview starts at a full-body framing appropriate to UMA 3.0 while retaining player rotate/zoom. P8.5 owns interface restyling; P8.6 alone owns U29/U30/U31 qualification.

## D-121 P8.5 is one replaceable presentation language, not a new gameplay authority (2026-10-14)

Main Menu, ruler creation, local RTS panels, global map controls and TPS HUD share one UI Toolkit palette and component vocabulary based on the accepted P8 natural stone/moss/bark/firelight direction. Responsive layout uses the rendered safe area and actual element bounds rather than hard-coded screen hit boxes. Normal, hover, focus, selected, disabled and danger states remain distinguishable by border, text and shape as well as colour; long Russian labels wrap and panel content scrolls. The supported acceptance floor is 1280×720 with 125% UI scale.

The shared style helper and USS contain presentation constants only. Existing named elements, button text and public test hooks remain stable, and all commands continue to route to the same Simulation/Infrastructure services. UI selection, folding, modal and notification state is disposable; no save, appearance, work, event, tier, map or combat authority moved into Presentation. The IMGUI global-resource surface is visually aligned but still delegates to canonical work/map services.

Automated contrast/layout/navigation evidence and visible screenshots are necessary but do not decide subjective art or usability acceptance. P3/P5 visual taste, P6 first-hour usability and P7 campaign pacing remain explicit human acceptance items. P8.5 does not run or close U29/U30/U31; P8.6 alone owns those unchanged performance, endurance and release gates.

## D-122 P8.6 may be checkpointed without relabelling omitted qualification as success (2026-10-14)

The user explicitly stopped the in-progress replacement U29 matrix and then requested completion without additional performance, endurance, connected, regression or Player testing. The implemented technical-launch, exact-tier/save and bounded Tier1 presentation improvements are retained because their focused automated tests and Windows build already passed and the full-length 1000-Character diagnostic showed materially improved frame latency. No acceptance threshold, workload or validator is weakened.

This user-directed scope reduction closes the active work only as a PARTIAL checkpoint. A diagnostic smoke is not a U29 acceptance matrix; historical or segmented endurance is not a fresh continuous U30 pass; existing integration tests are not an unblocked U31 release gate. Accordingly P8/P8.6 remain PARTIAL, U29 remains PARTIAL, U30 remains IN_PROGRESS and U31 remains BLOCKED. `NEXT_TASK.md` has no active task; any future resumption begins with the missing qualification recorded in `P86_HANDOFF.md`.

## D-123 P9 adopts the official Unity Terrain source through a reversible benchmark first (2026-10-14)

The user-authenticated Unity URP Terrain Sample 1.0.3 (Asset Store 213197) is admitted as an approved runtime environment source under its Unity Asset Store/Companion terms. Import is intentionally selective: PBR Terrain layers/textures, trees, rocks, foliage, water and their required render dependencies are retained; demo scenes, sample behaviours, UI, camera/lighting/volume rigs, authored demo Terrain data and proposed package-version changes are excluded. Current URP/Input System/test/visual-scripting versions stay authoritative. Unity's built-in Terrain module 1.0.0 is the only package addition.

P9.1 is an isolated 96 m Terrain benchmark inside normal `LocalGameplay`, not a parallel world authority. It demonstrates the source at RTS and ground viewpoints while the P3/P5/P8.2 map remains intact. `P91RealisticWorldBenchmark` and its generator own replaceable Presentation only; canonical territories, production sites, building placement, navigation contracts, weather, characters and saves remain unchanged. This makes visual inspection and package compatibility independently reversible.

P9.2 is the only task allowed to migrate gameplay anchors to the accepted Terrain and remove the superseded visual map. Removal is gated by focused and affected tests, NavMesh/construction/selection/weather/Save-Load proof, Windows build and visible RTS/TPS Player evidence. P9 does not resume or alter the checkpointed P8.6/U29/U30/U31 qualification gates.

## D-124 P9.2 makes physical Terrain the sole visible local-world projection without moving authority (2026-10-14)

The accepted official Terrain source is composed into one project-owned 128 m Unity Terrain for normal `LocalGameplay`. It carries four PBR Terrain layers, water, vegetation, rocks, route/buildable shaping and the physical `TerrainCollider`; Unity's built-in Terrain Physics module 1.0.0 is enabled so that collider is retained in Player builds. Vendor assets remain unedited and the generated TerrainData/material/scene recipe remains replaceable Presentation.

The six canonical locations, resource indices 0–23, established stable IDs and finite source amounts are preserved exactly. Building and Character presenters may resolve Terrain height for rendered placement, but canonical coordinates and snapshot payloads are not rewritten. Weather/season projection may tint the Terrain material, while `WeatherService` remains authoritative. Navigation, construction, production, map ownership and Save/Load continue through their existing services.

The P9.2 builder may remove the obsolete visible P3/P5/P8.1/P8.2/P9.1 scene roots only after validating the complete successor Terrain, anchors, routes, water, resources and density. Historical generators and source assets stay in the repository for reversibility. This decision closes only P9.2; it does not change P8.6/U29/U30/U31 status.

## D-125 P9 follows the user-approved six-stage visual sequence (2026-10-14)

The P9.2 implementation remains DONE, but its first handoff incorrectly described P9.3 as settlement/Character polish. The user reaffirmed automatic continuation of the agreed six-stage plan. Therefore P9.3 is the populated-biome pass across the whole accessible local region; P9.4 covers settlement, resources and interactions; P9.5 covers atmosphere and the six-location global map; P9.6 covers integrated gameplay and visual acceptance. `NEXT_TASK.md` and `TASK_GRAPH.md` are the stage gates. Each stage gets a separate local chat only after its predecessor reports an honest DONE, commits with its task prefix, leaves a clean tree and points `NEXT_TASK.md` to the exact successor. No unapproved Asset Store import, changed canonical authority or requalification of P8.6/U29/U30/U31 is implied. This decision changes sequencing documentation only, not P9.2 gameplay code or verification results.

## D-126 P9.3 biome density is deterministic disposable presentation (2026-10-14)

P9.3 enriches the single P9.2 physical Terrain without adding a world, resource or save authority. Seven named zones describe six visible biome kinds, and a fixed seed plus quantized layout hash makes the authored scatter reproducible. The accepted density floor is 170 additional trees, 90 additional rocks, 900 additional ground-cover instances and at least 92% sampled accessible-land coverage. Large trees and rocks retain official LODGroups and physical collision; small ground cover and the five visual satellites around each canonical resource disable collision and shadow cost so they do not create hidden navigation or per-object simulation work.

The original 24 clickable resource roots, indices and `StableEntityId` bindings remain the only production targets; satellites are decoration. The six existing `WorldLocationId` values remain unchanged. The `east_hills` presentation anchor may move from an isolated steep-slope point to the adjacent NavMesh-accessible shelf, because its transform is disposable local presentation and global-map identity remains canonical. Weather, construction, work orders and Save/Load continue through their existing Domain/Simulation services. P9.3 uses only the already approved official Unity URP Terrain Sample 1.0.3; settlement, building, work-site and Character art remains P9.4 scope.

## D-127 P9.4 settlement art and work indicators remain disposable presentation (2026-10-15)

P9.4 replaces the primitive shelter/storage/crop visuals with project-owned multi-part meshes and materials derived only from the approved Unity URP Terrain Sample and Serenity-authored texture/mesh data. Six static worksite affordances and four detailed camp-supply presentations improve readability but own no inventory, production, building or save state. Existing root presenters, colliders, prefab GUIDs where applicable, resource IDs and building definitions remain the binding boundary. The crop plot receives its own prefab instead of aliasing storage.

Open work orders are projected by one bounded scene-level `P94WorkOrderIndicatorSystem` refreshing at 4 Hz with a 24-indicator cap. It reads `WorkManager` and uses `JobId`, target position, lifecycle and priority without creating per-NPC `Update` loops or serializing presentation state. Save/Load continues to restore canonical group/order/building/resource identity; indicators are recreated from that state. Static gear attached to the UMA/fallback wrapper root was explicitly rejected after Player visual inspection because it did not follow the skeleton; optional Character polish is deferred rather than shipping a misleading artifact or adding an unapproved asset.

P9.4 does not alter weather, global-map or atmosphere authority. The Player evidence pauses simulation and suppresses the existing aggregate crowd draw only for the clean TPS screenshot after the functional RTS/order frame; production gameplay is unchanged. P9.5 owns atmosphere and global-map alignment. P8.6/U29/U30/U31 status is unchanged.

## D-128 P9.5 shares atmosphere definitions, not mutable world state (2026-10-15)

P9.5 introduces one project-owned `P95NaturalAtmosphereProfile` containing only visual definitions for solar light, ambient/sky/fog/water treatment and the six canonical map affordances. Local and global controllers evaluate that profile from the existing `GameClock` tick and `WeatherService` result. Rain and snow remain centralized scene-level particle projections; no per-NPC update, new clock, weather roll, climate state or snapshot payload is introduced. The P9.2 Terrain and water renderers may receive disposable property-block/material treatment, while Terrain collision, NavMesh and canonical seasonal effects remain in their established boundaries.

The additive GlobalMap presentation keeps `GlobalMapService`, routes, resources, travel and campaign state authoritative. Runtime island relief, river, natural markers and Russian labels are rebuilt from unchanged `WorldLocationId` values; their deterministic definition hash is `84BE8C806F7B2B61`. A map-specific night readability floor is presentation contrast only and does not change canonical time or weather. When the additive map unloads, the local projection explicitly re-evaluates the same canonical clock/weather and restores its camera/RenderSettings on the next presentation frame. Visual QA rejected the first over-zoomed/dark frame, then the second frame's buried/oversized markers; the accepted frame fits the island and exposes all six labelled locations.

No external asset or dependency is added. The existing P9.4 limitations are not reclassified: static wrapper-root clothing remains rejected and the centralized lightweight aggregate crowd draw may remain visible in RTS. P9.6 owns integrated route and subjective acceptance. P8.6/U29/U30/U31 status is unchanged.

## D-129 P9.6 separates technical integration proof from subjective visual acceptance (2026-10-15)

P9.6 uses one normal Player route to exercise Main Menu, New Game and the edited ruler, local RTS/TPS, the additive six-location global map and return, finite-resource work/building commands, atmosphere states and Save/Load. Its automation may stage inputs and capture evidence, but it does not replace or bypass the production services. `StableEntityId`, appearance, work orders, resources, buildings, `GameClock`, `WeatherService`, `GlobalMapService` and snapshot authority remain in their existing Domain/Simulation boundaries. The smoke result deliberately records separate `technical_passed` and `subjective_visual_acceptance` fields so a functional route cannot silently become an art approval.

The exact 1280×720 and 1600×900 Player runs technically pass. Their inspected images do not meet the requested final realistic Stone Age Character bar: the approved UMA inventory supplies modern demo clothing, mass/fallback Hodaart Characters remain visibly low-poly, and the centralized aggregate crowd path can render a diagnostic capsule. The smoke must not disable, crop or conceal those known limitations. A separate transient UI Toolkit text-clipping defect is visible after TPS/Load at 1600×900. No unapproved Character pack is imported merely to turn the result green.

P9.6 therefore remains PARTIAL until the user approves/provides a skeleton-compatible realistic Stone Age wardrobe plus coherent mass-NPC/LOD art, or explicitly accepts the placeholders, and until the high-resolution UI clipping/aggregate visualization is fixed or explicitly accepted. This checkpoint does not change P9.2–P9.5 presentation ownership, save formats, gameplay authority or the P8.6/U29/U30/U31 qualification statuses.

## D-130 P9.6 closes visual gaps inside the admitted UMA and bounded presentation boundary (2026-10-15)

User authorization allowed acquiring needed assets, but no new external package was necessary. The already registered MIT UMA-3.1F3 inventory contains compatible Human 3.0 slots, wrapped trousers and a leather-only overlay. Serenity-owned male/female wardrobe recipes compose those assets with a brown hide tint and bare feet. A Serenity-owned clean prefab is generated from the admitted UMA source and removes only the unavailable sample walker; vendor files remain untouched. Runtime acceptance verifies the actual active chest recipe and missing-script-free instance rather than trusting a requested-slot flag.

Detailed UMA generation is bounded to the existing eight-Character high-detail budget. The centralized aggregate renderer remains one draw path, but its capsule is replaced by a generated human-scale torso/head/limb/tunic mesh and non-metallic hide material; no per-NPC GameObject, coroutine or `Update` is introduced. Save/Load explicitly schedules UI Toolkit refresh/repaint passes so the 1600×900 text-clipping failure is no longer present in the captured post-Load frame.

After visual inspection at both supported evidence sizes, `subjective_visual_acceptance` may be true independently of the technical flags. P9/P9.6 are DONE only because both fields and the wardrobe/clean-prefab assertions pass. This decision does not relax or resume P8.6/U29/U30/U31 and does not move canonical Character, work, resource, weather, clock, map or save authority into Presentation.

## D-131 The explicit P10 contract supersedes conflicting product details without weakening architecture (2026-10-16)

The user's P10 instruction and the answers in `1.docx` are the product authority where they are more specific than the FRS. P10 therefore requires one connected world at least ten times the current playable land, a same-world strategic view, reliable click/context control, nine resource types and universal accessible-resource affordances, industrial extraction, unified original soft-stylized Characters, constrained family presentation, military-only direct targeting, the explicit capture protocol and persistent diagnostic codes. Unaffected FRS requirements remain active.

This does not move rules into MonoBehaviours or permit per-NPC loops. Canonical state remains keyed by `StableEntityId` in Domain/Simulation, definitions remain data-driven, and Tier 1/Tier 2/Tier 3 continue to be replaceable representations. P10.1 records requirements and evidence only; no P10.2+ gameplay feature is implemented by this decision.

## D-132 P10 uses one world authority and a universal resource-affordance audit (2026-10-16)

The current 128 m Terrain and additive schematic GlobalMap are not the P10 world model. P10.2 will provide a ≥512×512 m Terrain footprint and automatically prove ≥163,840 m² of connected accessible dry land, using 2 m sampling and explicit water/slope/NavMesh criteria. Strategic mode is a camera/LOD/overlay over that same coordinate space and identities, not a second canonical map. The six current `WorldLocationId` values remain append-only anchors within the expanded layout.

Every renderer classified as a resource-like object on accessible land must resolve to a canonical affordance or an explicit unavailable reason. Decorative objects may not visually promise a resource interaction; a scene audit enforces this. Small sources use 20–50 units and deterministic 15-day free-space respawn, while industrial deposits require built extractors. Seven current catalog types are retained, `meat` and `fish` are appended, and animal sources also provide existing `hide`. Seven workers must produce exactly seven times a controlled one-worker operation before travel/haul effects.

## D-133 P10 saves intentionally fork from the v19 P9 world (2026-10-16)

The current `SaveSnapshot.CurrentVersion` is 19. The first P10 task that commits connected-world state increments the schema to v20 or later, persists `worldGenerationId = p10-connected-world-v1` and writes to a distinct P10 slot namespace. Formats v1–v19 are shown only as read-only P9 archive metadata and are excluded from Continue; there is no automatic migration or overwrite. An explicit future import experiment may be designed separately, but failed or lossy inference is never part of ordinary Load.

This incompatibility is user-approved because the P10 world, ecology, housing, capture and cheat state change the game contract. P9 files remain physically untouched until the user explicitly deletes or archives them. The first cheat use permanently persists `cheatsUsed=true` and disables achievements for that P10 save.

## D-134 P10 freezes new external provenance and exposes the Character production gap (2026-10-16)

New P10 work may use only the already approved Unity URP Terrain Sample 1.0.3 and UMA 3.1f3 as external sources, plus Serenity-authored assets. Existing Hodaart/WizardHat content remains registered historical runtime/placeholder material, but it is not a blanket approval for the new P10 target style. Any other Asset Store/package/content source requires separate user approval before import.

The present Character boundary has two detailed UMA profiles, one Stone Age wardrobe family, a clean prefab, an eight-character detail budget, one procedural humanoid mass LOD and only Idle/Walking/Running in the active controller. It lacks accepted mass variety, military/profession clothing, a complete LOD chain, animals/fish and the work/combat/domestic animation set. P10.5 must first prove a small Player benchmark from approved/project-owned sources; if that cannot meet the target, it reports exact missing categories and waits for approval instead of silently adding a dependency.

## D-135 P10 stages are sequential and legacy qualification statuses remain independent (2026-10-16)

P10 runs strictly P10.1 through P10.10. P10.2 owns the connected world; P10.3 control/queues; P10.4 ecology and industry; P10.5 Character/animation production; P10.6 home/family/save fork; P10.7 military/capture; P10.8 diagnostics; P10.9 integration/performance; and P10.10 final product acceptance. Each stage needs its own tests, build, normal Player evidence, handoff and task-prefixed commit before the next starts.

P10 evidence may reveal or reduce old debt but cannot silently relabel P8.6, U29, U30 or U31. They remain PARTIAL, PARTIAL, IN_PROGRESS and BLOCKED unless their original gates are completed or the user makes an explicit status decision. The P10.9 p95 ≤33.33 ms requirement at 1920×1080 is a new feature-complete product gate, not retroactive proof for U29 or U30.

## D-136 P10.2 uses one physical world and changes strategic scale through the camera (2026-10-16)

The authoritative spatial projection is the 512×512 m `LocalGameplay` Terrain/NavMesh. Local RTS/TPS and strategic overview are camera modes over that same loaded scene; the old additive `GlobalMap` scene is no longer entered by the runtime bridge. The strategic overlay may darken unrevealed cells and label known locations, but it must render the real Terrain and existing presenters rather than a second schematic geography. The six established location IDs and 24 resource identities are retained; new locations are append-only graph nodes.

Terrain, authored water/mountain blockers and route geometry are disposable Presentation/navigation inputs. `GlobalMapService`, building/resource/Character authorities and stable IDs remain canonical. Construction accepts ordinary accessible Terrain and rejects only explicit terrain blockers or existing occupancy/funding/rule failures. The connected-world builder may regenerate project-owned Terrain/material/scene composition, but it does not edit vendor source or introduce a new dependency.

## D-137 P10 fog and scouting are canonical v20 state in an isolated P10 save namespace (2026-10-16)

`ConnectedWorldService` is an engine-free Simulation authority over deterministic 16 m fog cells and assigned-scout completion checkpoints. Physical presence and completed scouting both reveal the same canonical cell set; `P102StrategicWorldView` is only its read projection. There is no per-cell or per-scout MonoBehaviour update.

Snapshot v20 appends `worldGenerationId = p10-connected-world-v1`, revealed cells and scouting assignments. The ordinary file facade writes under `Serenity/P10/Saves`; historical P9 files remain untouched. The codec can still validate/read v1–v19 payloads for explicit legacy tooling, but P10 does not infer a connected world into old slots or silently overwrite them.

## D-138 P10.2 records real frame time without claiming the P10.9 gate (2026-10-16)

The P10.2 Player smoke records actual unscaled frame samples around local and strategic views and reports percentiles without thresholding them. The final 1920×1080 evidence measured p50 35.664 ms, p95 128.342 ms, p99 313.966 ms and max 784.861 ms. This does not satisfy the future p95 ≤33.33 ms feature-complete requirement and is retained as explicit P10.9 optimization debt.

P10.2 remains complete because its Definition of Done requires measured evidence and assigns the minimum-performance gate to P10.9. The known baseline `rain_snow_seasons=false` and `fps_displayed=false` flags likewise remain visible rather than being reclassified by the connected-world stage.

## D-139 P10.3 commands are canonical queues over stable identities (2026-10-16)

Context actions are evaluated by pure Domain rules and materialized as canonical `WorkOrder` values in Simulation. The presentation menu never owns mutable job state. Every resident queue has a persisted ordinal: a normal command receives a front ordinal and safely interrupts/requeues interruptible work; Shift receives a tail ordinal; an active carrying/non-interruptible phase and critical hunger/rest remain ahead until safe. Snapshot v21 appends queue cursors/ordinals, while legacy v18–v20 work payloads derive deterministic order from job IDs. Selection is likewise retained as `StableEntityId` values and only rebinds disposable Tier1 presenters.

The local runtime starts in `NeedsOnly` autonomous mode: residents idle unless hunger or energy requires attention, or a player command is queued. Existing production/haul/research utility logic remains available behind the explicit `LegacyUtility` mode for compatibility and future automation UI, but it is not enabled implicitly. Natural-source commands reserve canonical production sites, haul uses real inventories/capacity and building orders complete canonical buildings; no core action rule lives only in a MonoBehaviour and no per-NPC `Update` was added.

P10.3 is not DONE until its mouse-driven visible Player route is observed. A Windows firewall/security prompt is not an authorization to change firewall state and may not be operated by automation; if it blocks the Player, the task stays PARTIAL until a human dismisses the prompt and the exact route is captured. This does not advance P10.4 or alter P8.6/U29/U30/U31.

## D-140 Context UI dispatch and UI Load preserve stable selection (2026-10-16)

The visible P10.3 Player route exposed two presentation-boundary defects that focused headless coverage had not exercised. A contextual menu click could reach `SelectionProbe` after the UI handler and clear the live selection before command dispatch; the UI Load confirmation also explicitly cleared selection after the snapshot service had restored stable IDs. Context-menu execution now captures the `StableEntityId` roster when the menu opens, restores that roster after UI dispatch, and shields world selection through the following UI event frame. The UI Load path trusts the canonical snapshot/selection restoration and no longer clears it.

This is a presentation/input correction, not a new command or save authority. `ContextCommandService`, `WorkManager`, snapshot v21 and stable IDs remain canonical. A visible ordinary Move/Guard and UI Save/Load route plus focused regressions verify these boundaries. The task remains PARTIAL because the available Computer Use API cannot physically hold Shift across a mouse click or sustain a drag across multiple rendered frames, and the normal UI has no state-forcing controls for needs/protected carrying or Tier transitions. Automated coverage for those rules is retained, but it is not relabelled as completion of the required visible route. P10.4 does not start, and P8.6/U29/U30/U31 remain unchanged.

## D-141 P10.4 may proceed while P10.3 visible acceptance remains deferred (2026-10-16)

The user explicitly instructed the project to continue with P10.4 and later stages while acknowledging that P10.3 is still PARTIAL. This is a narrow sequencing exception to D-135/D-139/D-140: the committed P10.3 implementation, automated coverage, build provenance and observed Player subset are a permitted foundation for P10.4, but the missing physical Shift-click/Shift-command, slow multi-frame drag, needs/protected-carry resume, Tier1→Tier2→Tier1 and remaining target/action matrix are not accepted or reclassified.

`NEXT_TASK.md` therefore advances to P10.4 without changing the P10.3 row from PARTIAL. Later work must preserve the recorded P10.3 behavior and evidence boundary. P10.9 integration and P10.10 final product acceptance inherit the deferred route as a mandatory gate and cannot become DONE until it is completed or the user makes a separate explicit acceptance decision. This handoff changes documentation and sequencing only; it implements no P10.4 feature, adds no dependency and does not alter P8.6/U29/U30/U31.

## D-142 P10.4 separates renewable small nodes from facility-gated industrial zones (2026-10-17)

Small natural resources remain `ProductionSite` authorities keyed by `StableEntityId`; `ResourceEcologyService` owns depletion timing and deterministic biome candidate selection. Every small node has 20–50 canonical units. Exhaustion removes its presentation, and the service may refill and move it only after 15 game days at a free candidate. A blocked candidate never overlaps existing resource/building affordances and instead advances a persisted attempt cursor. Animals use one multi-output recipe for hide+meat and water uses a fish recipe. Multiple workers may reserve the same finite source, while atomic completion prevents over-harvesting the final unit; this permits the required controlled seven-worker linearity without per-NPC update loops.

Large deposits are not inflated natural nodes. `IndustrialExtractionService` owns data-driven facility definitions and zone state: biome/facility kind, cost, research gate, storage link, seven-worker cap, cycle output, remaining/capacity, fractional progress and optional restoration. Construction consumes one real canonical funding inventory and requires a real storage building. Industrial output enters a canonical world-pile inventory and is hauled through the existing logistics authority. Restorative mining is mine-only, research-gated, capacity-bounded and slow; restoration and extraction remain visible in the same remaining/capacity state.

Snapshot v22 appends natural lifecycle and industrial state inside the existing production payload. v21 is still explicitly readable and omits those appended fields. Presentation binds existing authored resource-like objects to canonical sources, exposes all outputs/quantities and reads strategic markers through the existing connected-world fog service. No new external asset/package or parallel economy authority is introduced. P10.4 remains PARTIAL until the build-provenance and complete normal-Player evidence gates in `P104_HANDOFF.md` pass; P10.3's D-141 debt is unchanged.

## D-143 P10.5 may proceed while P10.4 acceptance remains deferred (2026-10-18)

The user explicitly authorized continuing to P10.5 while acknowledging that P10.4 remains PARTIAL. This is a narrow sequencing exception to the normal P10 order, not acceptance of P10.4 and not permission to weaken its tests or evidence gates. The committed P10.4 implementation and focused coverage may serve as P10.5's technical foundation, but the missing build provenance, affected U15 Tier1 physical-walk pass, complete saved normal visible D3D11 resource/industry route, clean Player exit and honest final log scan remain open.

`NEXT_TASK.md` therefore advances to P10.5 without changing the P10.4 row from PARTIAL. P10.9 integration and P10.10 final product acceptance inherit every blocker listed in `P104_HANDOFF.md`; they also retain the P10.3 manual route debt under D-141 and P10.2's measured 1920×1080 p95 128.342 ms performance debt. P10.5 must not represent any of those items as passed. This handoff changes documentation and sequencing only: it runs no Unity tests/build/Player, adds no dependency, and implements no P10.5 feature.

## D-144 P10.5 uses canonical appearance with bounded detailed and instanced far projections (2026-10-18)

`CharacterAppearanceService` remains the single mutable appearance authority keyed by `StableEntityId`. Normal New Game now derives deterministic skin, hair/beard, hair/eye colour and UMA morph variation for every canonical starter while preserving the ruler editor's exact state. `CharacterPresentationSpawner` may materialize at most eight detailed UMA residents; `Tier1AiRuntimeDriver` applies a camera-distance/hysteresis policy that keeps direct, selected and nearest residents detailed and projects farther residents through one instanced project-owned humanoid mesh. Both paths read the same `AppearanceState`, including body scale and clothing ID, and neither creates save truth.

Locomotion playback rate is derived from measured walk/run speed rather than a fixed cycle. Existing centralized work/combat authorities produce a bounded `CharacterAnimationIntent`; `CharacterPresenter` adds the corresponding procedural pose without a per-resident `Update`. Civilian, work and military clothing roles are data-driven IDs, but only the existing civilian UMA recipe has approved visual content. Missing work/military recipes deliberately fall back with a diagnostic and the military ID is retained as P10.7's hook rather than fabricated art.

One scene-level `StableEnvironmentLodController` enforces cross-faded mesh stages and disables billboard stages, so trees do not rotate toward the camera. This benchmark imports no external source beyond the already approved UMA 3.1f3 and Terrain Sample 1.0.3. It remains PARTIAL because final profession/military wardrobe, authored Character LOD meshes, age-group coverage and authored work/combat/sleep clips require separately approved compatible sources and uninterrupted visible Player acceptance. The inherited P10.2 route `LineRenderer` bands observed during the rerun are unrelated presentation debt and are not modified by P10.5.

## D-145 P10.5 completes with user-authorized Quaternius CC0 role content and bounded multi-stage LOD (2026-10-18)

The user explicitly authorized independent selection and import of free assets with a clear licence for the remaining P10 stages. P10.5 selects Quaternius CC0 Universal Base Characters, Modular Character Outfits Fantasy and Universal Animation Library 1/2. Source URLs, retained licence/readme files and hashes live under `Assets/Game/Art/ThirdParty/Quaternius/P105`. This approval is specific to clearly licensed free content and does not weaken the general dependency/provenance rule.

Civilian presentation remains UMA. Worker and military presentation uses a complete Quaternius outfit plus project-generated head and eye meshes derived from the Universal Base Character sources and rebound to the outfit skeleton. A role visual owns exactly one Humanoid `Animator`; neither renderer hierarchy nor animation state becomes save truth. Normal New Game assigns role clothing and three deterministic age groups through canonical `AppearanceState`, retaining the existing stable-ID and snapshot boundary.

The project-owned action controller consumes centralized carry/gather/chop/mine/build/fish/hunt/combat/sleep intent and plays authored clips, with procedural posing retained only as a compatibility fallback. Character distance presentation is detailed skinned content, instanced mid mesh at 24/32 m hysteresis, a lower-detail instanced far mesh at 64 m, and culling at 120 m. `Serenity/CharacterLod` is an Always Included shader because Player shader stripping otherwise removed the instanced stages. The scene-level environment LOD policy remains unchanged.

P10.5 is accepted by the provenance-stamped Windows build and the isolated 1920×1080 D3D11 smoke JSON that proves normal New Game, role visuals and heads, authored chop/combat, three age groups, mid/far LOD and Save/Load/Tier continuity. The P10.2 beige route bands are explicitly out of scope: they are overlapping 2.1 m `LineRenderer` routes and remain P10.9 presentation debt. P10.3 and P10.4 retain their independent PARTIAL statuses and gates. P10.6 becomes the only active implementation task.

## D-146 P10.6 uses one canonical domestic authority and snapshot v23 (2026-10-18)

`DomesticService` is the engine-free authority for homes, closed/habitable indoor bounds, resident access, assigned beds, occupancy, two independent consent decisions and completed family interactions. All durable links use `StableEntityId`; GameObjects and UI never own these rules. Sleep evaluates the live building registry and succeeds only for a living resident assigned to a free bed inside a completed, operational, eligible home. Tier1 moves to that bed before energy recovery, releases occupancy on completion/cancel and uses the existing P10.3 action cancellation path so interruptible manual work returns to its canonical queue while protected carry/control remains protected.

Adult relationships reuse U18 rather than creating a second pregnancy model. Both living Characters must be adults, mutual spouses, independently consenting, sufficiently related and fertility-eligible. A successful interaction applies the existing U18 relationship event and starts the existing deterministic 45-minute pregnancy. Presentation is an implicit fade modal with an explicit «Пропустить» action; no explicit content and no P10.8 family diagnostic code are introduced.

Snapshot v23 appends strict `domestic` and representation-neutral `characterRuntime` payloads to the complete v22 state. Runtime entries preserve tier, position, hunger/energy and off-camera continuity; existing appearance/role/age, work queues, U18 pregnancy and connected-world sections remain their established authorities. Gameplay accepts only v20+ saves carrying `p10-connected-world-v1`; the normal P10 directory is `Serenity/P10/Saves`. P9 v1–19 files are enumerated from the legacy directory only as read-only metadata and cannot be continued, migrated, deleted or overwritten through P10 UI.

P10.6 imports no external content. Focused and affected tests, Windows build and an isolated normal Main Menu → UI buttons → Save/Load 1920×1080 D3D11 Player route pass. The broad suite still exposes independent historical isolation/geography/GPU-runner failures listed in `P106_HANDOFF.md`; none is hidden or used to change P10.3/P10.4, P8.6, U29, U30 or U31 status. P10.7 becomes the sole active task.

## D-147 P10.7 upgrades existing residents and persists one deterministic flag-capture workflow (2026-10-18)

Military training is a ticked engine-free service over the existing `CharacterRegistry`, building/resource authorities, P10.5 appearance service and `MilitaryUnitService`. A course consumes its data-defined cost transactionally, retains the trainee's `StableEntityId` and object identity, then writes combat skill, military clothing and roster membership on completion. Recruit, Soldier, Veteran and Elite use increasing duration/cost/skill definitions; repeated training advances one level and never spawns or substitutes a Character.

Direct attack is a separate command policy over the existing physical military/work/combat/building-damage services. Every attacker must be trained and active in one same-side detailed unit. Enemy military, military barracks and area orders may proceed; a civilian can never be a direct target, friendly and non-military buildings are rejected, and path failure has an explicit reason. Issuing `Напасть` first creates the existing physical move job and resolves combat/damage only after proximity—strategic overview changes camera/UI scale, not world authority or teleportation.

`SettlementFlagCaptureService` is the persisted P10.7 capture workflow. It reads canonical building/unit/Character/map state and requires simultaneous ownership of the settlement main building and current operational barracks; destroyed barracks deterministically falls through to the next operational barracks, while no operational barracks removes the second flag. Progress uses shared calendar ticks: five game hours equals five real minutes at x1, clock speed scales it, pause advances nothing, defender contest freezes it, and defender recapture resets it. Completion uses the established building/map ownership mutation, so attached inventories/stores retain their stable identities under the new owner, then applies a saved-seed deterministic soldier join/death resolution once. Snapshot v24 appends courses, direct orders, flag clocks, seed and completed outcome; reload is idempotent.

No P10.8 code, new package, per-NPC `Update`, mutable ScriptableObject save state or alternate Character authority was introduced. Task-scoped/affected tests, the provenance-stamped build and the ordinary visible 1920×1080 D3D11 Player route are the acceptance evidence. Broad suite failures remain recorded independently in `P107_HANDOFF.md` and do not relabel P10.3/P10.4, P8.6, U29, U30 or U31. P10.8 becomes the sole active task.

## D-148 P10.8 diagnostics are transactional adapters over canonical authorities and snapshot v25 (2026-10-18)

`DiagnosticService` is the sole engine-free command orchestrator for the explicit paused-game codes and family diagnostic. It owns only irreversible cheat evidence plus a monotonic identity sequence; technology, inventory, Character, appearance, tier, domestic, pregnancy, training and military roster state remain in their established services. `serenity` completes the current research catalog. `res_serenity` selects an operational player-owned common storage accepting every current resource, atomically expands that inventory's capacity and adds exactly 10,000 of every catalog entry; any exception restores its previous quantities and capacity. `army_serenity` creates one adult with a deterministic sequence/rank `StableEntityId` for every current training rank and registers each through the canonical training completion path. The separate family action creates a canonical adult pair, mutual consent/relationship, indoor bed and invokes the existing P10.6 interaction/pregnancy path.

Snapshot v25 appends `cheatsUsed` and the diagnostic sequence. Successful execution marks the party only after its transaction commits; invalid input and missing/rolled-back prerequisites do not. Restore ORs the flag and takes the maximum sequence, so an in-session load of an earlier clean checkpoint cannot re-enable achievements or reuse an ID. Save metadata and normal UI expose the disabled-achievement state. v24 is explicitly readable without inferred cheat evidence and malformed v25 diagnostics are rejected. Character/tier restore reconciles stable identities so diagnostic additions round-trip without creating a presentation-owned authority.

P10.8 is accepted by focused/affected suites, the provenance-stamped Windows build and the ordinary visible 1920×1080 D3D11 Main Menu→New Game→Pause→Codes→Save/Load route recorded in `P108_HANDOFF.md`. No external asset or package was added. P10.3/P10.4 debt and P8.6/U29/U30/U31 statuses are unchanged; the correctly rendered U31 diagnostic still reproduces two independent failures and remains BLOCKED. The screenshot-inclusive P10.8 frame sample is not the P10.9 p95 gate. P10.9 becomes the sole active task.

## D-149 P10.9 retains a partial checkpoint until physical-input and Full-HD performance gates pass (2026-10-18)

P10.9 may improve authored presentation and acceptance instrumentation without creating a second gameplay authority. Connected routes are therefore corrected only as disposable scene presentation: narrower Terrain-conforming, endpoint-trimmed, shadow-free lines over the existing district graph. Four existing stable resource formations are repositioned near the starter settlement without changing IDs or canonical quantities. The feature-complete performance profile extends the established U29 harness through explicit arguments and exact 1920×1080 validation; U29's independent fixed 1600×900 contract and status are unchanged.

The checkpoint cannot be DONE. Two fully focused mandatory-duration runs fail p95 at 60.618/75.126 ms, although identity, Save/Load and memory stability pass. The available computer-control surface exposes no native Unity Player window, so automated tests and build-contained smoke controllers are not relabelled as proof of physical Shift-click, Shift-command, slow drag or the remaining resource/industry route. P10.9 remains the only active task and `NEXT_TASK.md` remains on P10.9; P10.10 must not start. P10.3/P10.4 and P8.6/U29/U30/U31 retain their previous statuses.

## D-150 P10.9 video repairs and approved tiled-world revision (2026-10-19)

Strategic mode temporarily owns the physical Camera, including disabling/restoring its CinemachineBrain and viewport. Fog is projected from world cells and clipped to the map viewport, not painted over the entire UI. Tree LOD filtering recognizes shader-driven `EFFECT_BILLBOARD` MeshRenderers as well as BillboardRenderer components, removes billboard-only stages and extends the last real mesh without duplicating it in adjacent cross-fades. This applies to both P9.2 base trees and P9.3 canopy trees. Pointer-edge motion no longer pans the RTS camera; held middle-button drag and wheel zoom remain. TPS follows an independent target so character rotation cannot feed back into camera yaw. LMB emits a short presentation-only swing and feedback even when it misses; actual damage remains exclusively subject to CombatService range/cooldown/opponent rules, and aiming skips self colliders but not walls.

The user superseded the 512 m single-Terrain geography with 12 × 12 tiles of 1000 × 1000 m, and explicitly chose new-game-only adoption with old save files preserved and not migrated. The corrected legend is agricultural yellow, hunting blue, berries purple, settlements grey, river red strip, fishing lakes red circles, tree concentrations green, ores pink. The earlier settlement/resource rectangle interpretation is invalid. `P109_WORLD_REVISION.md` records the implementation boundary and acceptance work. Recording this decision does not claim that the tiled world is already implemented. P10.9 remains PARTIAL; P10.10 and legacy debt statuses are unchanged. Performance profiling remains deferred per the user's prior stop instruction.

## D-151 P10.9 Stage 1 uses one deterministic tiled-world authority and generation-isolated saves (2026-10-19)

The active new-game world is a 12 × 12 array of separate 1000 m Unity Terrains generated from one pure world-coordinate sampler. Terrain height, river/lake depressions, water exclusion, slope/buildability, ground projection and the strategic overview all derive from that authority, so neighbouring heightmap borders use identical samples and Unity Terrain objects remain disposable presentation/collision projections. A bounded camera-centred 3 × 3 ring owns visuals. Collision and per-tile NavMesh retention are planned separately for every physical actor and every tile crossed by an active command; a manual move prepares the whole corridor before its WorkOrder is created.

Fog uses a 48 × 48 layout over the same 12 km bounds, while the canonical global graph uses the three approved settlement zones plus connected world-scale landmarks/routes. Strategic mode temporarily substitutes a coarse unlit land/water projection and retains the established Camera/Brain ownership rules. Stage 1 deliberately defines settlement, river and lake geometry/contracts but does not claim final biome/resource/settlement population or visual polish; those are P10.9 Stage 2.

`p109-tiled-world-v2` is an explicit save-generation boundary. Current slots live under `Serenity/P109/TiledV2/Saves`; the previous `Serenity/P10/Saves` directory is never written by the new flow and is enumerated only as incompatible legacy metadata. Generation mismatch is rejected before clock/service rebind, and gameplay refuses to overwrite a mismatched current-catalog slot. No old save is migrated, deleted or reinterpreted. P10.9 stays PARTIAL, performance profiling remains deferred, and P10.10 does not start.

## D-152 P10.9 Stage 2 separates canonical world population from streamed presentation (2026-10-19)

The `TiledWorldPopulationPlanner` is the engine-free deterministic placement authority for the `p109-tiled-world-v2` generation. It guarantees a baseline of two wood, one stone, one berry and one fibre node per tile, then adds the approved forest, berry, hunting, ore and fishing concentrations. Every node remains a 20–50 unit `ProductionSite` with a generation-scoped deterministic `StableEntityId`; every approved extraction region adds the matching existing data-driven industrial definition, inventory and world pile. No GameObject, Terrain instance or ScriptableObject owns mutable resource state.

`P109TiledWorldPopulationPresenter` materializes only the current visual tiles and destroys only their views on unload. It reuses the approved P9.3 resource formations as presentation-scaled clusters where possible, generates small missing animal/fish/industry markers, and rebinds after Save/Load. `ResourceEcologyService` explicitly rebinds registrations to restored production-site instances before restoring lifecycle cursors, so respawn remains attached to canonical loaded state rather than stale objects.

Terrain colour, agricultural flattening, local river/lake meshes, fields, settlement clusters and strategic zone overlays are disposable projections of the same tiled definition/height sampler. The strategic UI draws discovery-gated zone extents over the canonical camera view so those overlays remain readable even when coarse overview geometry is visually flat. The superseded broad `P10.2 Physical Route` LineRenderer objects are disabled; they are not converted into a second path or navigation authority. Existing P10 saves remain preserved and unmigrated. Stage 2 completion does not relabel P10.9 as DONE, does not start P10.10, does not resolve inherited P10.3/P10.4 manual acceptance debt, and does not resume the user-deferred performance benchmark.

## D-154 P10.9 playable-patch harvesting and scenery ownership (2026-10-20)

The user requested one usable starter area and a personally exercised Windows Player before expanding the world further. New games use `p109-playable-patch-v1` and `Serenity/P109/PlayablePatch/Saves`; tests and manual review override this with an Artifacts-only directory. Previous P10, TiledV2 and legacy saves are neither migrated nor overwritten. The patch starts in daylight, grants stone tools only through its explicit starting-research option, and keeps the initial player roster physical independently of camera distance.

Runtime-grounded or cloned scenery must not participate in scene-space static batching or baked occlusion. A scene-build processor clears those flags on the processed scene only. Tree meshes use static alpha-clipped Lit materials instead of SpeedTree deformation, retain mesh LODs, and share the ground/tile visibility policy. Authored layout hashing retains original positions. Starter trees, including wood-formation satellites, have individual deterministic canonical sources; a trigger selection volume covers their visible crown without becoming a navigation obstacle.

The new-world production catalog treats one chop/mining order as one complete finite source. Its duration is 30/40/50/60 game minutes for wood/stone/iron/gold before existing modifiers. Completion atomically transfers the remaining canonical quantity once, exclusively reserves that source, and exhausts it. The previous catalog remains unchanged. Work progress is visible before completion; mesh-only transient tree-fall/rock-break feedback owns no inventory, collider or identity and is not replayed when loading an already depleted source.

RMB chooses the ordinary available context action; Ctrl+RMB opens the full menu and Shift retains queue semantics. Mouse-driven camera panning requires the middle button; Home returns to the starter view. Accelerated time now scales NavMesh acceleration and turning with speed, while the physical movement timeout permits traversal at x10. These choices do not waive needs/protected-work rules.

The five proposed external assets were researched, not acquired. Environment art and procedural terrain tooling are separate decisions: neither replaces canonical resource IDs, commands or save state. No package/dependency, purchase, world-generator migration or deferred performance benchmark is authorized by this comparison alone. P10.9 remains PARTIAL until the recorded acceptance gates pass; P10.10 and inherited P10.3/P10.4 debt are unchanged.

## D-153 P10.9 video follow-up keeps authored layout and runtime terrain projection separate (2026-10-19)

P9.2/P9.3 trees retain their authored X/Z, rotation and stable references, but their runtime Y is projected once onto the active tiled height sampler. P9.3 snapshots authored tree positions before projection so its deterministic scene-layout hash still audits the source composition rather than the disposable runtime view. The tree LOD controller hides legacy trees whenever their tiled ground is not rendered, including strategic mode; on return, the same tree objects reappear. No camera-dependent repositioning or per-tree Update loop is introduced.

The tactical character cull radius must exceed the 160 m RTS zoom and its diagonal view; it is 384 m. Reapplying an unchanged crowd-detail tier no longer restarts an active NavMesh path. Context-menu group Move uses deterministic separated formation destinations while retaining each member's canonical WorkOrder and existing critical-need/protected-phase delay rules. The new `map_serenity` code reveals every canonical discovery cell through `ConnectedWorldService`, is idempotent, participates in diagnostic rollback/cheat evidence, and does not change save generation or migrate old P10 files. P10.9 remains PARTIAL; performance profiling remains deferred.

## D-159 Screenshot feedback command authority and presentation fixes (2026-10-21)

The user explicitly overrides the earlier critical-need/protected-haul delay for an ordinary
RMB work command: replace the current action and assigned queue, release claims and drop
physical cargo without loss. Immediate work is evaluated before needs; Shift appends and
keeps ordinary scheduling. Disabled commands do not cancel work. Health eligibility and
external control ownership remain guards; this is not a second simulation or inventory.
Queued/active work still persists through the existing WorkManager and snapshot schema.

Navigation owns every character root, including UMA. Used Quaternius action clips are
imported with body-based orientation, feet-based vertical reference and baked root transforms;
the source FBX root had caused Carry to face backwards. A shared presentation loop positions
cargo between the animated hands. Foot picking is a nonblocking trigger; lightweight
navigation updates elevation instead of keeping a stale Y on slopes.

P10.2 primitive water/mountains and their navigation volumes do not coexist with the compact
tiled-world authority. Their original scene assets remain unchanged. BuildingPlacementService
accepts an injected full-footprint environmental validator, which is also applied at commit;
Unity terrain/physics queries remain in Presentation rather than pure simulation code.
The foundation height tolerance is serialized configuration. No Terrain flattening is added.

Fish are shared project-owned 3D meshes with phase-separated movement and tail animation,
not the imported particle mask on horizontal quads. The 44 sources, stock quantities,
shore work positions and ground catch outputs retain existing stable IDs and ownership.

The 25-building prompt is an audit/architecture approval gate, not permission to implement
all buildings at once. The proposal extends existing definitions, construction, production,
hauling, domestic and save systems; Campfire/cooking belong to the first agreed slice.
No new asset dependency, download or import occurs. The user takes over manual Player QA;
we provide automated evidence, a separate build and a reproducible checklist. Old saves
remain untouched; no push or P10.10/performance work is authorized by this request.

## D-160 Building capabilities remain immutable definition data and validate as one graph (2026-10-21)

The user approved the B-F sequence in `P109_STONE_AGE_BUILDINGS_PLAN.md`. B1 extends the existing
`BuildingDefinition`; it does not introduce a parallel building instance or manager. Optional
engine-free specifications describe construction, placement/form, housing, workplace slots,
storage, production recipes, maintenance/demolition and upgrade options. Era, descriptive UI keys
and technology prerequisites are definition data. The historical constructor maps to the new
specifications so earlier tests and systems remain source-compatible while authored catalog entries
store construction work/stage and placement tolerances as serialized values.

`BuildingDefinitionCatalog` is the validation boundary for the complete set. It rejects duplicate
building IDs, missing resource/recipe/technology references, missing or self upgrade targets and
cycles in the upgrade graph. ScriptableObjects remain static authoring inputs; no mutable building
or save state is stored in them. B1 changes no save format or gameplay inventory. B2 owns persistent
construction buffers/stages and the new-generation boundary.

## D-161 Construction sites use the shared inventory registry and snapshot v26 (2026-10-21)

A building under construction owns one `ConstructionBuffer` inventory keyed by its existing stable
building ID. Delivery is an ordinary exact transfer between existing inventory owners. Materials
remain in that buffer until a stage transaction removes the complete deterministic stage slice; only
then does immutable save progress record them as installed. Installed accounting is not spendable
stock, so no second inventory or duplicated resource authority exists. Construction work is blocked
until every material stage is installed. Restore validates the buffer and derives readiness from its
actual contents rather than persisting transient claims.

Snapshot v26 persists settlement identity, phase, stage cursor, material contract/progress and the
existing work cursor. Construction-buffer contents already round-trip in `ProductionSaveState` with
all other registry owners. Historical v1-v25 files remain codec-readable for metadata/diagnostics,
but the live B2 rules require save generation `p109-stone-age-buildings-v1` in the new
`Serenity/P109/StoneAgeBuildingsV1/Saves` directory. `CompactWorldV1` is legacy read-only input;
there is no in-place migration, resource-ID rewrite or terrain-generation-ID change.

## D-162 Placement shapes persist exact cells; verification scales with slice risk (2026-10-22)

The existing placement service remains the single authority. Structures derive their rotated
rectangle; zones accept one bounded, unique, same-level, cardinally connected authored cell set;
segments preserve a unique ordered cardinal path so turns are deterministic. Placement evaluates
the same cells for bounds, occupancy, environment and owned territory, and exposes a sorted free
perimeter for approach rules. Shoreline and groundwater requirements fail closed unless an injected
world rule authority explicitly accepts them. The starter settlement territory is explicit data,
not a second building grid.

Arbitrary zone/segment geometry cannot be reconstructed from anchor and definition alone, so
snapshot v27 appends exact placement cells to `BuildingState`. Restore reserves those cells; v26 and
older structures retain footprint-derived fallback behavior. Settlement assignment is one-way and
idempotent for the same ID. No Unity instance ID or mutable ScriptableObject becomes canonical.

The user requested shorter feedback cycles after B3. C1-C3 therefore use targeted tests for changed
rules and directly affected connections, without repeating green unrelated suites. A broad
regression and separate Player build occur after C4, then at the C5 and F1 control points, or earlier
only when a concrete save/resource-loss risk justifies it. Compilation, old-save readability,
protected-resource checks and honest reporting remain mandatory.

## D-163 Construction funding reserves intent; ownership changes only through hauling (2026-10-22)

In LocalGameplay, confirming a construction plan no longer consumes its cost. It creates ordinary
high-priority `WorkOrder` haul batches whose destination is the building's existing shared-registry
`ConstructionBuffer`. Open construction deliveries count as source reservations during later funding
evaluation, while canonical ownership changes only through `ResourceTransferService` pickup and
drop-off. Material readiness commits complete stages after delivery; build work may wait at the site
but cannot progress before every material stage is installed.

Cancellation terminates the site's open haul/build orders and converts all delivered material—both
buffered and recorded as installed—back into canonical world piles before removing the unfinished
building, attached storage and occupancy. In-flight cargo uses the established Tier1 cancellation
drop path. Buffer contents, material progress and work orders already fit snapshot v27, so C1 does
not introduce another manager, inventory, save version or migration.

## D-164 Base-building visuals follow canonical stages; construction outranks unpicked cleanup (2026-10-22)

The first playable set is authored in the existing `BuildingPresentationCatalog`: Primitive Shelter,
Stone Age Hut, Campfire, Storage Basket and Stone Tool Workshop. Housing, storage and research
requirements remain immutable definition data. Project-owned primitive prefabs use explicit stage
markers read by `BuildingPresenter`; canonical `Building.ConstructionStageIndex` is the only progress
authority, while older prefabs retain the combined-renderer fallback. No mutable progress is stored
in prefabs or ScriptableObjects and no external asset dependency is added.

Autonomous cleanup may already hold source claims when a player confirms construction. A confirmed
high-priority construction transaction may release only matching `ReservedAtSource` claims required
by its deductions before creating delivery orders. It never revokes `Carrying` claims or moves stock:
ownership still changes only through the shared transfer/haul path. This prevents a valid plan from
deadlocking behind speculative cleanup reservations without introducing a second reservation system.

## D-165 Building production is a U15 site with physical edge buffers (2026-10-22)

A productive building is represented by one existing `ProductionSite` per recipe, using a deterministic
site ID and an explicit stable owner-building ID. Recipes in the same building share a definition-owned
worker cap and two inventories in the canonical registry: `ProductionInput` and `ProductionOutput`.
These are logistics endpoints, not hidden stock or a second inventory system. Raw material/fuel reaches
the input through normal `WorkOrder` hauling and finished output returns to operational storage through
the same claim/transfer path.

Queue state, pause and worker limit live in production state; recipe definitions, buffer capacity and
slots remain immutable building/catalog data. A batch commits only when every input can be consumed and
all outputs fit simultaneously. If input is absent or output fills after work starts, completed progress
waits and retry cannot duplicate output. Snapshot v28 appends the new site fields while keeping v1-v27
readable and preserving active tasks and buffer inventories in their existing sections.

## D-166 Cooked resources are the single edible contract across simulation tiers (2026-10-22)

Raw berries, fish and meat are production inputs, not meals. `FoodConsumptionService` owns the
ordered cooked-resource set and is the only service that removes a meal: first from operational
building storage, then from explicitly registered character inventories. Tier1 `Eat` must acquire one
meal before applying hunger recovery; failure leaves hunger unchanged. Tier2/Tier3 consume through
the same service's aggregate tick, and LocalGameplay subtracts materialized Tier1 residents from that
population so a person cannot be charged both physically and abstractly.

New Game seeds a small cooked reserve rather than permitting raw-food fallback: eight cooked berries
in player storage and forty on the abstract rival chief. This is bootstrap content, not an alternate
food rule. Production buffers and world piles remain inedible until normal hauling/cooking places
finished food in an eligible inventory. No new save fields or manager are required.

## D-167 Feedback mechanics extend canonical work, needs and combat state (2026-10-22)

A natural source admits no more active gather tasks than its remaining portions. Accepted workers own
separate production tasks and outputs; worker count never multiplies one shared task's speed. Indoor
presence is presentation derived from an arrived `Working` phase plus canonical bed/workplace ownership:
the presenter hides inside enclosed buildings, but Campfire work is explicitly outdoor and visible.

Mood belongs to `Tier1Needs` and snapshot v29. It changes from bed/ground sleep, cooked meals and favorite
work; at or below 20% it blocks ordinary work but not survival recovery. Favorites are persisted as one
reserved `favorite.<skill>` marker in the existing skill collection, so later skill growth cannot silently
change the person's preference and older saves can fall back to their highest known skill. Skill levels
modify the existing production, construction, carry and combat calculations and grow through their
canonical completion points. Existing barracks training is the first formal training path; future schools
must award levels through `CharacterSkillRules`, not a second progression store.

Combatant state owns weapon durability (0–100) in v29. Each armed resolved attack consumes one point;
zero durability selects the existing unarmed profile. Arrows are presentation feedback and are not an
inventory/ammunition resource. Wildlife HP is presented from canonical hunting work remaining, so visible
damage cannot diverge from task completion. Building color blocks are cleared in the undamaged friendly
state to preserve authored material colors; damage/enemy tints remain derived presentation.

## D-168 Stage D keeps hunting products and preservation in the shared economy (2026-10-22)

An operational `hunting_camp` is a settlement-level prerequisite for reserving `hunt_animal` work.
The hunt produces one `carcass`, not independent meat/hide rewards. A `butcher_station` consumes that
single inventory unit and atomically emits five meat, two hides and three bones, so output capacity,
save/load and retry use the existing production transaction and cannot duplicate individual products.

`food_storage` is a standard `BuildingStorage` inventory accepting only `ResourceCategory.Food`.
`drying_rack` converts three raw meat to two `dried_meat`; `hide_workshop` converts two hides plus one
plant fibre to two `cured_hide`. Dried meat joins the established edible-resource set only after normal
hauling reaches eligible storage. No second decay, inventory, recipe, worker or save authority is added.
Stage-D work maps to the existing Hunting, Cooking and Crafting skills. Arrows remain non-consumable
presentation; persistent weapon durability remains the equipment cost agreed in D-167.

## D-169 Extended facilities and lifecycle remain adapters over shared authorities (2026-10-22)

E-stage buildings extend immutable `BuildingDefinition` capabilities and the existing building registry;
they do not introduce separate settlement, inventory or production truth. A resident may have at most one
home and one workplace assignment, with capacities read from the definition. Treatment and training call
the canonical health and skill services. Fishing and water production remain ordinary recipes/resources,
and shoreline/groundwater restrictions are placement predicates evaluated alongside territory ownership.

Open gates release their exact cells from the shared occupancy map and closed gates reserve them; damage,
demolition and load reconciliation use the same pathing adapter. Building wear, repair, upgrade and
demolition carry deterministic operation IDs. Repair/upgrade charge only after their work requirement is
met, upgrade preserves stable identity, and demolition spills registered cargo before removing the storage
owner and returns configured construction materials as physical piles. These services own orchestration,
not duplicate mutable building or resource state. Gate openness and facility assignment are presently
session state; F1 must either prove the intended reset-on-load behavior or add persistence before final
acceptance.

## D-170 Variable gardens and final building art remain presentation over canonical placement (2026-10-22)

Primitive Garden is the first player-authored rectangular zone: held LMB selects a connected rectangle with
an explicit 2×2 minimum and 8×8/64-cell maximum. The exact cells travel through the existing
`BuildingPlacementRequest`, occupancy grid and `Building.PlacementCells`; there is no second farm grid. A
presentation component repeats one imported crop visual per cell and recentres the instance, while cost and
work remain the current definition values pending a separate balance decision.

Final facility art references already imported assets, including CityBuilderThree tent art for Primitive
Shelter and Low Poly Farm basket art for Storage Basket. Construction stages may use simple readable foundation
geometry, but a completed building shows only its highest authored stage. Runtime selection colliders and fire
materials are project-owned adapters: imported prefab colliders are stripped from building visuals, existing
narrow resource colliders are preserved, and particle renderers receive an explicit URP material. These choices
do not make vendor prefabs authoritative for simulation, placement, inventory or save state.

## D-171 Build provenance fingerprints committed content once (2026-10-23)

The Windows build provenance guard uses the current Git `HEAD` as the content identity for every unchanged
tracked file. It additionally hashes the raw `Assets`/`Packages`/`ProjectSettings` worktree status and the
contents of every modified, staged or untracked file in that scope. Comparing this manifest before and after
`BuildPipeline.BuildPlayer` therefore still fails closed when source changes during a build and still identifies
ordinary uncommitted builds.

It must not re-hash every unchanged vendor asset. The former implementation read the complete 9.87 GB asset
tree twice for each build and exposed no progress, making healthy Unity runs look stalled. The optimized
manifest preserves immutable provenance while making build time depend on actual dirty payload rather than the
size of the imported art library.

## D-172 Presentation quality audits follow the completed authored stage (2026-10-23)

A building presentation audit evaluates the highest `BuildingConstructionStageVisual` when staged visuals
exist, otherwise the prefab root. A valid final visual must contain renderable mesh geometry, a minimum useful
vertex payload and non-error materials. The number of renderer components and a project-era mesh-name prefix
are not quality measures: an imported authored building may correctly combine the same detail into one mesh.

Settlement layout identity covers authored worksite/supply placement and stable prefab root identities. It does
not include nested FBX mesh names because Unity's Editor and Player import paths may expose those names
differently. Geometry/material validity is checked separately. Any authoring step that replaces shared starter
prefabs must refresh the serialized settlement layout hash before validation and build.

## D-173 Player feedback: explicit dismantling, no age damage and readable outdoor facilities (2026-10-23)

Calendar age must never damage buildings, including older definitions with non-zero wear settings. Combat
and fire remain the structural damage authorities. Friendly damaged buildings retain authored material colors;
the integrity readout and fire effects communicate damage instead of a uniform brown material override.

Demolition is an exclusive `WorkJobType.Demolish` in the existing WorkManager. Clicking queues work; a worker
must travel to an exterior approach before accumulating skill-scaled work. Required work is half the authored
construction work, with a ten-minute floor. Completion uses the existing exactly-once cargo/material return
and occupancy release. Cancellation leaves the building intact. WorkOrder owns progress; the XML work codec
reads an optional non-negative `progressTicks` field (missing means zero), and preserves it across reload.
No parallel job queue or inventory is introduced.

Palisade and path accept cardinal, straight held-LMB segments up to 32 cells; costs and construction time scale
with cell count. Canonical PlacementCells drive occupancy, persistence and repeated visuals. Garden remains a
single player menu option (2x2–8x8); the legacy crop ID is retained for existing authored scenes and saves.

Fishing camps are shoreline outposts and no longer require the starter settlement's 128m territory. They still
require dry, level unobstructed land, research and materials. Landing candidates derive from the canonical
height sampler, then pass the real placement predicate before a cyan shore post/outline is shown. Placement
snaps near these markers. The shoreline search accounts for the world's wide walkable bank, rather than
requiring water within four metres. Outdoor facility meshes use human-scale height limits, not a 20cm zone
collider height. Deer views switch between imported idle/walk clips, turn and follow sampled ground; they do
not advance the population or inventory simulation independently.

## D-174 Downloaded art remains a presentation library, never a second authority (2026-10-23)

The user authorized review and selective integration of `Assets/23102026`. Admission is path-scoped and inert:
vendor C#/DLLs, demo scenes, ProjectSettings and package-manager payloads are excluded. MicroVerse and Toby are
not installed because their terrain/vegetation controllers would compete with `TiledTerrainWorld`, weather and
season presentation. Modern Miami/industrial content is out of era. Bear, dogs and knights stay deferred until
canonical species, ownership, loot and era definitions exist. Winter art is deferred because the supplied
package includes URP 14 pipeline content while this project is pinned to URP 17.6.

`CompactWorldArtCatalog` may reference reviewed disposable art, textures and audio only. Woodland props have no
collider, stable ID or save state. Ambient clips read the canonical clock, hunting clips read the existing work
task, and lightning reads the existing `WeatherService`; none can mutate simulation. Notification dismissal
removes text and target from parallel bounded lists in one callback before performing focus, preserving their
index invariant.

## D-175 Construction clearance is derivable orchestration, and the HUD remains a projection (2026-10-23)

Natural colliders under a requested footprint do not become a second occupancy grid. The physical placement
surface classifies only bound, non-depleted natural production sites as clearable and returns their stable
entity IDs. Free, clearable and forbidden are presentation states over the existing canonical
placement/funding result; a forbidden result always carries a concrete surface or occupancy reason.

Confirming a clearable site creates the canonical unfinished building and construction buffer but defers
delivery creation. ConstructionClearanceCoordinator then creates normal exclusive Chop/Mine/Gather jobs
through WorkManager. Once every referenced source is canonically depleted, the existing funding service
creates ordinary haul orders and the coordinator creates one ordinary build order. It never directly removes
resources, completes production, advances construction or moves a character. On load its plan is reconstructed
from unfinished building cells, open work orders and natural-site positions, so no new save payload is needed.

The modern strategy HUD is one UI Toolkit projection over the same services. Resident cards, resource totals,
notifications, priority controls and build categories own no simulation state. The icon atlas and footprint
mesh are disposable presentation assets. One combined footprint mesh is required instead of a GameObject per
cell, and the runtime atlas is sliced in memory rather than introducing 24 separate imported textures.

## D-176 World-map resources share Production/Ecology authority and keep deposits latent (2026-10-23)

`WORLD_MAP_RESOURCES.json` is parsed into immutable resource-type and zone contracts. The V2 population planner
is the only placement authority: it combines those masks with canonical terrain, hydrology and biome samplers,
then assigns a stable identity from generation, seed, type, zone, quantized global cell and ordinal. Tile load
order, Terrain instances and prefab instances are excluded from identity.

Only visible WM06 nodes are registered with `ProductionService` and `ResourceEcologyService`. Remaining amount
and depletion persist through their existing snapshot/codecs; an explicit renewable policy prevents finite
stone/deposits from respawning. Hidden copper, iron, gold and oil retain stable identity and technology metadata
but have no presentation or early extraction recipe. Fish placement and recovery remain WM07-owned.

Resource visuals reuse audited project prefabs through a V2-only ScriptableObject lookup. The catalog contains
references, not canonical state, and missing V2 art is an error rather than permission to create a runtime
primitive. The compact-world branch and active generation remain unchanged.

### D-191 — performance handoff and save-isolation incident (2026-10-24)

The user's long 100/300/500 gates run through a dedicated performance-only offline launcher.
It builds fresh and retains the original thresholds/durations; a short smoke PASS cannot
qualify the frame budget. The normal build-f diagnostic is still above that budget.
P6 test teardown must restore the inherited SERENITY_SAVE_DIRECTORY rather than clear it.
PlayMode-c exposed this omission: later tests wrote an original slot 2702 and a new slot
2701 in StoneAgeBuildingsV1. Original hashes/evidence remain immutable; no matching backup
was located. Do not describe save protection as passed or silently replace the old baseline.

### D-192 — World Map V2 is the active runtime generation (2026-10-24)

The user's explicit activation and save-deletion decision supersedes the earlier inactive-V2 constraint.
Normal New Game selects `p109-world-map-v2-proposed` and snapshot v30 in `WorldMapV2/Saves`; the generation ID
is retained for persistence compatibility even though its historical name contains `proposed`. The menu does
not enumerate legacy saves. The old loader stays available only to regression fixtures, and old local save
directories were deleted as authorized.

The authored start contract controls the building origin and ten resident spawn positions. Prepared expansion
corridors receive a deterministic terrain shoulder so they satisfy their buildability promise for every tested
seed. Start-resource guarantees obey physical exclusions but cannot be invalidated by cosmetic paint weights.
Strategic-map locations reuse V2 start/POI coordinates rather than compact-world zone IDs.

Vegetation masks clear only construction zones that do not require clearing. Imported multi-LOD tree prefabs
are rendered through a disposable stream capped at 192 views within 210 metres; canonical vegetation remains
the seeded WM05 plan and collision remains independently capped at 650 proxies. Unity Terrain details that
require unreadable source meshes/textures are disabled in Player. V2 camera framing is applied in `Start` at
zoom 120 so the prepared clearing and surrounding forest are visible after all Awake initialization.

### D-193 — feedback-driven full V2 preparation and material repair (2026-10-24)

The user's recording supersedes D-192's 192-view vegetation stream. All sixteen authored V2 terrain/navigation
tiles and population views are prepared during scene loading and retained. Canonical vegetation is projected
through immutable spatial mesh batches, selecting LOD and frustum visibility at draw time. This trades longer
initial loading and retained memory for removal of terrain generation/navigation baking during camera movement.
It does not claim acceptance of the long 100/300/500 frame budgets.

Imported decorative POIs must not carry interactive BuildingPresenter or vendor mesh colliders. Their world
interaction belongs to the explicit domain-backed presenter. Material repair restores original albedo textures
from legacy shader fields into URP BaseMap. The user's dirty original rock material remains untouched; V2 stone
and quarry use separate authored assets. Bear is imported from the already present local archive; three other
species remain explicitly documented temporary models.

The enemy starter camp uses real canonical buildings, independent economy, its own settlement territory and
terrain-aware construction coordinates. Mountain stone extraction is a canonical industrial zone using the
existing shift/cost/technology/storage mechanics. Full worker assignment automation is not implied by its model.

Actual local-water triangle area and a native multi-point route are required evidence: overview-water tests and
startup screenshots had missed an integer division that collapsed water meshes. The diagnostic route runs in
the background explicitly, with isolated saves, so a hidden window cannot silently pause its coroutine.

The established enemy camp exposed starvation in the old strategic priority chain: immediate military travel
prevented starting research, which prevented further construction. Research project selection now belongs to
the economy tick. Civilian construction can proceed while the army's existing travel/battle/occupation order
continues. Established-camp regression verifies research and agricultural development after twelve days.

## D-194 — Settlement-first NPC military policy (2026-10-24)

User explicitly rejected immediate enemy expeditions. AiWarPolicy now supplies a seven-day minimum development
period, a one-day advance warning, a 1.25 relative strength threshold, hostile relations at -35 or below, and a
three-person civilian reserve. These are tunable policy defaults, not a timer that forces war. Neutral starts
continue economic research/construction and do not attack merely because a neighbour exists.

Actual barracks training uses MilitaryTrainingService, spends the NPC's own food/wood, changes canonical combat
skills/ranks and recruits existing residents. Enemy graduates form aggregate units at their own settlement;
player training retains its existing detailed-unit behavior. Shelter, storage, agriculture, trained soldiers and
food for both the home reserve and expedition are prerequisites. Strength estimates use living combat-capable
residents, units and combat skills; they are planning estimates, not guaranteed battle outcomes.

Faction relations and training are saved alongside the faction economy. PrepareAttack is a persisted order;
its target, army and original issue tick survive saves. Departure rechecks relations, strength, training and
supplies; invalid preparation is cancelled. The HUD gives advance warnings and the map retains the countdown.
Map diplomacy offers actual food transfers as gifts and explicit hostility; active battles also establish
hostility. This is basic state-to-state diplomacy, not the full FRS treaty/coalition system. Existing v30 faction
records without the optional extension load with neutral relations and empty training; no current saves deleted.

## D-195 — Measured vegetation rendering cost and generated character atlases (2026-10-24)

The functional full-map route exposed a GPU vegetation bottleneck: a controlled visibility comparison drops
median frame time from 96.92 ms to 13.90 ms when only vegetation drawing is disabled. Probe disabling does not
improve it. Preserve the complete vegetation plan and its canonical identity, but use a project-owned instanced
URP shader with source leaf/bark textures and cheap vertex wind instead of vendor fragment noise. Choose mesh
LOD at 55/140m and retain tree shadow casting within 250m. These changes require a fresh rendered measurement;
the ablation itself is not a performance pass and does not authorize removing the forest.

Generated UMA atlases can populate legacy _MainTex while an admitted URP material reads _BaseMap. Bridge that
texture when present; keep a valid fallback visible until generated materials are ready. Audit visible runtime
renderers plus all source flora materials, rather than diagnosing intentionally hidden construction buffers as
white scenery. The first Player audit after this change must remain explicit evidence.

## D-196 — Source-only user feedback repair and shared worker authority (2026-10-24)

The user explicitly suspends tests and rebuilding while continuing to report bugs. Implement source changes,
record them as unverified and keep WM15 IN_PROGRESS; prior test/build evidence does not validate these edits.

Industrial extraction now uses ordinary saved resident jobs and the existing industrial output inventory.
Only arrived workers contribute elapsed work; warehouse delivery uses physical haul reservations/transfers.
Do not simulate a day's production from a UI button. Finite forest tree stock uses existing Production identities
and saves; batched rendering is a projection of that depletion. Avoid camera-dependent ecology registrations,
because the ecology restore contract requires a stable registration set. Visible fish represent existing stock.

Inspection and command authority are separate. Resolve commands from canonical settlement ownership/allegiance,
never from the currently viewed map location or a blacklist of one enemy faction. Read-only foreign inspection
must not populate the single-resident command selection used by training and research.

The user rejected toon characters/editor output. This supersedes D-195's visible toon fallback: UMA remains the
body projection regardless of profession, generated atlas and legacy shader-color bindings are repaired, and
generation failure is reported explicitly instead of displaying a different character. The avatar cap follows
the 32-character detailed tier; the separate eight-view animation budget remains. Added generation/memory cost
and actual visual results remain unverified until the user permits execution. Appropriate hare/fox/wolf assets
are still required; no new Asset Store dependency is authorized by this decision.

## D-197 — User-selected resident art, crop choice and bounded map repair (2026-10-24)

Later explicit user direction supersedes D-196's uniform UMA body choice: rulers and their editor retain UMA;
ordinary residents use the already imported Polytope peasants. Isolate admitted dependencies in project-owned
assets, preserve vendor imports, retain the publisher's URP 17 NPC color-mask shader and project humanoid
animation controller. Animation throttling may freeze a resident's pose but must not swap their selected mesh
for the old generic crowd body. Measure the additional Tier1 rendering cost after execution is permitted.

The user approves White Rabbit, retires fox/wolf, and permits asset selection but requires separate approval
for paid purchases. The rabbit file is missing locally; do not claim the existing cat proxy is a rabbit model.
Explicitly retire fox/wolf in saves without relaxing unknown-species validation. Keep a subsequently assigned
hare prefab when regenerating the art catalog. No new paid dependency was acquired.

Crop choice changes the canonical site's saved recipe only when empty, operational, owned by the requesting
side and unreserved. Presentation reads canonical growth and harvest. Tool identity follows work intent and
the current humanoid hand, not a permanent demo attachment. Imported art is not itself a livestock simulation;
animal pen gameplay remains an explicit open item, not decorative livestock claimed as functioning economy.

Repair lake-outlet continuity and shore mesh clipping in the existing hydrology. Remove only the rejected
rock-spire mesh, preserving stable exploration identity. A water asset is optional and does not repair missing
geometry. The broader map-art request is analysis only: do not implement suggested sakura, palettes or regional
lighting before the user answers. The prohibition on tests/builds remains; WM15 is not DONE.

## D-198 — Approved regional art and canonical animal health (2026-10-25)

The user's thirteen answers approve strong but smoothly blended regional identities, natural stylization,
one permanent flowering grove with seasonal smaller pockets, dark realistic wetlands, readable start lighting,
weather/night, distant ridges plus a sea coast. Implement this within WM15, retaining canonical vegetation IDs,
resource nodes and gameplay bounds. Reuse admitted local art and runtime material copies; do not alter vendor
assets. The pink crowns are a stylized existing-mesh treatment, not a newly acquired sakura package. Scenic
sea/ridges are outside playable land. Geometry/navigation and shader cost require later execution evidence.

Animal HP is persistent domain state, shared by canonical wildlife and legacy hunt sources. A successful timed
bow flight plus accuracy/range/obstruction checks causes damage; presentation arrows and blood have no damage
authority. At zero HP, a hunter must approach and skin the corpse before resources are awarded. Do not derive
HP from a production progress bar, grant outputs from a living animal, resurrect harvested identities through
resource ecology, or reset HP on visual tier changes. People keep their established BodyHealth injury model.
Physical hunters use Tier1 presentation for the obstruction adapter; offscreen workers in this local service
select other tasks. A future aggregate hunting model must explicitly resolve movement/encounters, not bypass
physical hit checks. Animal save XML is an optional backward-readable extension; older executables need not
read extended snapshots.

First domestic gameplay uses an empty owned field as a sheep pasture, with one campaign founding pair to avoid
unlimited free animals through rebuilding. Persist health, home, sex and age; reproduction needs living mature
male and female animals and free capacity. A shared presenter projects the herd. This is not full husbandry:
feeding, trade, wool/milk, transfer between pens and other domestic herds remain separate work. Never describe
imported animal art alone as an implemented economy. White Rabbit remains missing; no purchase was made.

The user's explicit no-tests/no-rebuild instruction still overrides the timing of AGENTS.md validation.
Record source/diff review only, do not claim visual correctness or performance without execution, and keep
WM15 IN_PROGRESS. Scope and exact commands are in `docs/world_map/WM15_ART_DIRECTION_AND_HUNTING.md`.

## D-199 — Preview build acceptance is separate from runtime/performance acceptance (2026-10-25)

The user authorizes compilation and a Windows preview for map/hunting/pasture, while deferring full
performance tests. Require both a successful build result and zero reported build/shader errors: Unity
reported Succeeded with 12 shader errors in build-d. Reject that artifact and use corrected build-e,
0 errors / 30 warnings. Keep the preview in Builds/WorldMapArtHunting, preserve the previous build,
and retain WM15 IN_PROGRESS. Compilation does not establish gameplay, visual or performance acceptance.
See docs/world_map/WM15_PREVIEW_BUILD.md for exact commands and validation boundaries.

## D-200 — Scene approach adapters preserve domain identities (2026-10-25)

Resource work positions may be projected to reachable scene stances without changing persistent source IDs
or canonical inventory locations. Tree work targets the trunk, stone work its visible surface, fishing a
wooden pier at the actual shoreline. Barracks training owns the physical character while travelling/inside;
its duration starts only after arrival. Presentation hides the trainee, while the domain advances rank and
saved clothing. Head submersion damages existing canonical BodyHealth, with normal death/succession handling.
Asset art does not substitute for tested navigation or gameplay. Existing imported cloth is admitted into
owned prefabs/materials, preserving resident faces and rigs. No paid dependencies are acquired.

## D-201 — Loading responsiveness and terrain shader retention (2026-10-25)

Prepare the complete authored terrain/navigation before gameplay rather than during camera movement.
Pure immutable terrain/paint calculations may run on one background task per tile; Unity object creation,
colliders and navigation remain on the main thread. Cache immutable hydrology segment lengths and bounds
to avoid repeated work. A serialized instanced terrain material must retain the shader variants required
by terrains created at runtime. A successful build is insufficient: reject missing terrain, invisible
inspectors and failed placement in the actual Player. Preserve focused evidence and keep WM15 open.

## D-202 — Direct harvesting and automatic camp work are distinct (2026-10-25)

Explicit player hunting/fishing orders may reserve an available natural source before an automatic-work
camp exists. Research gates remain mandatory; automatic site selection still requires its camp. Fish is
a gathering target, wildlife a bow-hunting/skinning target rather than a military area attack. Both mouse
buttons use the same character/animal priority through decorative foliage. Regression checks cover the
policy and canopy interception; an opt-in disposable Player route verifies actual navigation and work.

## D-203 — Late feedback: physical water boundaries and owned asset admission (2026-10-25)

Water is a visual surface rather than a walkable physics floor. Navigation excludes deep submerged
terrain through merged volumes, while elevated piers/bridges retain navigation. The RTS camera uses
water level as its minimum focus ground and retains a selected elevated occupant's height until panning.
Hunt/fish intents use a neutral base animation so generic labor swings cannot fight their hand poses.
Fish outputs use a scene-supplied dry landing near the actual
pier; source IDs and saved output inventories keep their existing authorities.

Death presentation binds a real model explicitly, never an incidental temporary child. Shared character
pose scheduling owns its additive offsets and restores them only when Animator has not replaced them.
Bow drawing follows canonical shot timing; visual impact particles do not decide resource production.
External deaths between scheduler ticks immediately disable capabilities without rewriting death dates.

User-provided packages are admitted selectively into owned GUID-remapped copies, without old demo,
editor or postprocessing scripts. Missing Arctic palette dependencies receive explicit URP snow materials.
AQUAS wave textures may be reused now; this does not establish compatibility of its legacy GrabPass,
reflection or underwater pipeline. A full AQUAS port remains unimplemented. See
`docs/world_map/WM15_LATE_FEEDBACK.md` for all eleven package dispositions and validation boundaries.

## D-204 — Shore workplaces, connected defences and physical neighbour economy (2026-10-26)

Fishing camps use a continuous building recipe, not the finite natural fishing source. The canonical
ProductionOutput inventory holds at most 30 fish; ordinary haulers collect from the dry entrance.
The worker retains the workplace while its buffer is full, but needs can interrupt work. Queue-free
continuous mode and faction workplace ownership are serialized, with optional fields for older saves.
Placement samples the actual shore and rotates a dry 8×6 footprint toward water; no marker is required.

Gates occupy three cells and cut those cells out of an owned finished palisade. The wall uses individual
navigation blockers, so its old aggregate box cannot seal the opening. Imported Medieval Fortification
meshes use owned URP materials; one-metre wall sections are clipped from the source mesh rather than
compressing complete log rows. Gate leaves move independently of the permanent frame.

Wildlife escape is canonical state (threat, expiry and hit sequence), with data-driven species policy.
The presentation adapter chooses traversable routes in both visible and distant representations. Damage,
death and resource harvesting remain domain authorities. Generated crossing visuals, roads and links
are all removed together; continuous bank heights replace the sharp shore height discontinuity.

Neighbour factions use their own production/inventory services and the same work actions as player
residents. Shared natural sites retain canonical object identity; rebinding updates selection caches.
An off-camera movement adapter follows validated routes, while tier transitions preserve needs/position.
Starting materials are moved into haulable piles without minting resources. Construction delivery plans
subtract existing site buffers and ignore zero deficits. Work assignment is scoped to the owning side.
Full performance gates remain deferred by user instruction; targeted regressions and disposable Player
routes are the acceptance evidence for this repair batch.
