# TASK_GRAPH.md

UI01 radical gameplay interface redesign (2026-10-31): **DONE / user approved**. The optimized
monthly Ollama build has passed the user's retest and no longer blocks development. UI01 replaces the active
task with a design-first rearchitecture of the player flow over the existing canonical services. Required
review artifacts cover screen/navigation structure, HUD information hierarchy, resident/building/settlement
cards, construction, research, dynasty, events, army, world map and future diplomacy. The user approved the
specification and revision 7 prototype. The interactive prototype and twenty
1920x1080/1280x720 renders passed static syntax, browser navigation, console-error and horizontal-overflow
checks. No Unity runtime file changed and no Player build is claimed. General optimization and final polish
remain deferred; unrelated dirty WM15/terrain/vendor work is preserved. User revision 1 moves navigation to
the bottom, adds direct worker-group and military entries, combines Realm with diplomacy and adds a clickable
local mini-map; overall visual approval remains open.
User revision 2 keeps the world visible behind a compact lower construction catalog and adds a persistent
resident tracker whose selection opens and focuses any resident; overall visual approval remains open.
User revision 3 supersedes that per-resident tracker with group-first drill-down suitable for 1000–2000
residents and makes research a dependency tree grouped by construction, defense, farming, crafting and
medicine; overall visual approval remains open.
User revision 4 adds mandatory implementation work: profession-aligned groups are exclusive work eligibility,
urgent priority never recruits another group and only requeues eligible members' interrupted work second;
starting and new adult/recruited residents auto-join by profession, while the player retains create/delete/edit
control. Profession and group names remain aligned and no resident may remain ungrouped.
User revision 5 approves the information architecture but pauses implementation for a final color review. The
prototype now uses a stronger Black and Gold shell while preserving natural world colors and semantic success,
warning and danger colors. At that revision, UI02 remained gated on palette acceptance.
User revision 6 quiets the palette and defines typography: normal UI uses near-black, bone, gray-beige and one
desaturated gold accent; research categories no longer form a color rainbow; semantic hues are small and muted.
Segoe UI represents dense Cyrillic UI text and Georgia is limited to large headings. Twenty regenerated renders,
interaction checks and the prototype contrast targets pass. At that revision, UI02 remained gated on final
visual acceptance.
User revision 7 removes internal rules and design rationale from player-facing copy. Exclusive scheduling,
urgent-group ownership and requeue ordering remain Domain behavior/tests, while the visible group workspace shows
only actionable settings and state. Browser verification now rejects leakage of those internal phrases. UI02
is the next unblocked implementation task and receives the approved UI01 baseline through a separate chat.

WM16 neighbour/Ollama cumulative-stall optimization (2026-10-31): **DONE / user retest accepted**. Active work has a dedicated sorted
index; completed history is O(1)-trimmed to 256; terminal production tasks are removed immediately; audit
resident discovery is throttled/cached. Ollama automatically leaves CPU capacity for Unity and records request
duration. Tests passed 60/60 plus a real three-month scene route; monthly simulation cost decreased
6.79 -> 6.72 -> 5.24 s. The 10,000-job gate retained 256 entries in 45.56 ms. Evidence:
`docs/validation/NeighbourAiOptimization.md`.

WM16 neighbour progression repair (2026-10-31): **DONE / awaiting user retest**. Repaired neighbour-specific
work assignment, production/logistics, critical-need recovery and crop-plot concurrency. Monthly local-only
Ollama planning was observed for 730 game days: 25/25 accepted plans, all plan sequences correlated with
canonical strategic execution, all six residents productive, and zero failed-meal loops in 42,288 audit rows.
The exact hourly 14-day route passed cooking, research, new building and operational completion; production
passed 13/13. Windows build passed with 0 errors / 0 warnings. Evidence:
`docs/validation/NeighbourProgressionRepair.md`.

WM16 user follow-up (2026-10-30): **DONE / awaiting user retest**. Menu, complete snow/map readiness, progress bar,
concurrent upgrade navigation, delayed configurable AI cadence, execution audit, and livestock selection
exception repair. 71 domain/UI/strategy/work tests passed; final runtime completed five concurrent upgrades,
58 footprints and menu return. Windows build passed (0 errors / 0 warnings), native Player exited 0.


WM16 optional ruler strategy AI (2026-10-30): **DONE / awaiting user retest**. Added deterministic-only,
local Ollama and cloud-first/Ollama-fallback modes. External models return one validated aggregate yearly plan,
never resident-by-resident commands or direct world mutations. Annual/trade/war/crisis/dialogue/manual triggers,
pause suppression, timeouts, strict JSON/content-ID validation, nested schema-v1 persistence and bounded journal
are connected to the existing U26 scheduler. Delivery compile passed; U26 29/29 and P2 5/5 EditMode tests passed;
real `qwen3.5:4b` JSON smoke passed at 37.8 s; Windows Development build succeeded with 0 errors / 52 inherited
warnings. Evidence: `docs/validation/RulerStrategyAI.md`; operation: `docs/AI_RULER_STRATEGY.md`.

WM15 screenshot/video repair (2026-10-30): **DONE / awaiting user retest**. Addressed building materials,
physical navigation, preloading, automatic upgrades/construction, housing/sleep, exact-pier fishing and
snow preparation/trails. Validation: 56/56 domain + 7/7 roles + 1/1 actual gameplay; Windows build passed
with 0 errors / 0 warnings and Player exited 0. Full HD running snow-camera average is approximately
31 FPS; general simulation/per-population optimization remains deferred. Evidence: `docs/validation/WM15RetestRepair.md`.

WM15 existing-asset expansion (2026-10-30): **DONE / awaiting user retest**. Added level-II
building chains with resource-funded builder work, separate communal house/storage/crafting hall, hut4 →
existing stone house6, rank-dependent military presentation, deformable alpine-foot snow and corrected lake
screen sampling. Persistent runner validated 104 domain/content cases, 26 strategic cases, 4 final content
cases and 1 rendered route. Final immutable Windows build succeeded with 0 errors / 0 warnings;
actual Player passed 21/21 checks, exited 0 and its close lake/snow images were reviewed.
Evidence: `docs/validation/WM15AssetExpansion.md`; configuration: `docs/world_map/WM15_ASSET_EXPANSION.md`.
The broader WM15 population/performance gates remain deferred.

U00 persistent Unity test/build runner (2026-10-29): **DONE**. One Unity 6000.6.4f1 Editor process owns a
file-backed, domain-reload-safe command queue for incremental compilation, asynchronous EditMode tests and
Windows Development builds. Test XML, per-command logs, JSON exit status and build provenance are mandatory;
zero selected tests fail closed. One PID passed compile, focused 1/1 EditMode and Development-build validation.
Evidence: `docs/validation/PersistentUnityRunner.md`. This infrastructure does not change WM15 gameplay,
water presentation, acceptance status or the delivered retest Player.

WM15 resident map cache/warmup (2026-10-29): **DONE / awaiting user retest**. Initial gameplay remains bounded,
then all 16 terrain/population tiles are prepared in the background, retained and reused instead of being
rebuilt during camera travel. Immutable presentation data is cached per generation/seed/version; incomplete
first preparation shows one bottom progress percentage and a complete cache suppresses it on later launches.
Focused EditMode passed 6/6, the full-residency PlayMode route passed 1/1, Windows Development build
`Builds/WM15MapWarmup/Serenity.exe` succeeded with 0 errors / 82 warnings, and actual Player smoke exited 0
with cache hit 16/16, 24 valid lake landings and worst warm camera refresh 5.4 ms (previously 1205.7 ms).
Evidence: `docs/validation/WM15MapWarmup.md`. Deferred 100/300/500 population gates remain outside this task.

WM15 Player world-creation repair (2026-10-29): **DONE / awaiting user retest**. The approved
`FX/ProceduralWater` shader is retained in Player builds and enforced by build preflight. Steep authored POIs
keep their landmark position and receive a deterministic road-connected, dry, slope-safe arrival stance within
a bounded 64 m fallback radius. Focused POI tests passed 4/4. An actual Player reached
`WM15_BOOT gameplay ready` without the shader or POI exception. The final Windows Development rebuild at
`Builds/WM15WaterFinal/Serenity.exe` succeeded with 0 errors / 85 warnings. Evidence:
`docs/validation/WM15WaterReview5.md`.

WM15 ordinary-lake/marsh follow-up (2026-10-29): **DONE within accepted visual scope**. Mountain and
mountain lake remain frozen. Ordinary lake density is increased; the wetland shader now evaluates its broad
continuous region from world coordinates as well as mesh vertex colour, producing the intended clean-river to
dark-olive marsh transition without material bands. The user approved both rendered views. Focused EditMode
validation passed 16/16; Windows Development build `Builds/WM15WaterFinal/Serenity.exe` succeeded with 0
errors / 85 warnings. Evidence: `docs/validation/WM15WaterReview5.md`.

WM15 living buildings (2026-10-29): **DONE within agreed scope**. The complete contract is recorded in
`docs/world_map/WM15_LIVING_BUILDINGS_SPEC.md`; building upgrade requirements/mechanics remain explicitly
paused rather than omitted. Final gate: 86/86 focused EditMode tests passed; Windows Development build
`Builds/WM15LivingBuildings/Serenity.exe` succeeded with 0 errors / 85 warnings. Side-aware gates with
neutral detention, opposing doctor shifts, horse rider speed and non-healing military promotion close the
last identified gaps. Exact evidence: `docs/validation/WM15LivingBuildings.md`. Overall WM15 remains
IN_PROGRESS only for its separate visual/water acceptance and deferred performance work.

WM15 ordinary-lake/marsh follow-up (2026-10-28): **IN_PROGRESS / source-only correction, render permission
pending**. The accepted mountain and mountain lake are frozen. Ordinary lakes now use higher water-colour
blend, absorption and minimum opacity. The wetland core is dark/opaque again and retains a broad continuous
river transition without material bands. Static review only; Unity, tests, capture and Player build were not
run by explicit user instruction.

WM15 library audit (2026-10-28): **IN_PROGRESS / analysis deliverable complete**. Full current
Assets/archive inventory and prioritized reuse assessment are in docs/world_map/WM15_ASSET_LIBRARY_REVIEW.md
and WM15_ASSET_LIBRARY_INVENTORY.md. No implementation/import/delete/test/build; no change to visual
acceptance or remaining WM15 gates. Exact commands/results: docs/validation/WM15LibraryAudit.md.

WM15 environment/water review (2026-10-28): **IN_PROGRESS / visual approval pending**. Generated lake ice is
removed. Procedural Water Shader URP supplies profiled water without changing canonical hydrology; lake water
is denser and its clean-to-marsh change is a continuous per-vertex shader blend. The playable alpine terrain
now composites the existing StampIT base with selectively admitted Terrain Asset Pack `Ridge05`, `Ridge06`
and `Mountain06` heightmaps. Snow Mountain and Winter Mountains & Stamps remain visual supplements. The final
hidden Editor review capture exited 0 and the four PNGs are in `Artifacts/WM15EnvironmentReview3/`. No tests
or Player build were run by explicit user request.


WM15 winter mountain review (2026-10-28): **IN_PROGRESS / superseded by four-view review**. StampIT alpine
height data and Winter Environment URP rock, snow, vegetation, props, weather and distant scenery are
integrated. Hidden Editor art capture exited 0 and produced
`Artifacts/WM15MountainReview/winter-environment-final.png`. No tests or Player build were run by explicit
user request; those remain blocked until the user approves the screenshot.


WM15 current feedback (2026-10-27): **IN_PROGRESS / build-batch-c and Player-a verified**.
Focused EditMode passed 9/9. Final build: exit 0, 0 errors / 30 warnings; Player-a: 32/32, exit 0,
no gameplay runtime errors. Manual-pier fishing, six lake places plus river support, mixed growing
livestock, crop actions/labels, navigation, wildlife orientation/habitat, winter/petals and lighting are
implemented. User visual acceptance, slow initial loading and full population gates remain open. See
`docs/world_map/WM15_CURRENT_FEEDBACK.md`.


WM15 evening feedback (2026-10-27): **IN_PROGRESS / build-i and Player-g verified**.
Eleven focused Editor checks passed. Final build: 0 errors / 82 warnings; Player-g: 31/31, exit 0,
no gameplay runtime errors. All four lakes accept camp construction; preview worst call 41.7 ms.
Earlier Player-c validated crops/pasture/hunting/skinning/chopping. General building menus are deferred
by user; questions delivered. User visual acceptance, initial loading and full population gates remain open.
See `docs/world_map/WM15_EVENING_FEEDBACK.md`.


WM15 afternoon feedback (2026-10-26): **IN_PROGRESS / build-f and Player-d verified**.
19 distinct focused Editor checks passed. Build-f: 0 errors / 82 warnings. Player-d: 47/47, exit 0, no runtime
errors. Shore preview worst call 43.0 ms; full gates deferred. Pier, cultivation, rotation,
lighting, shore search, animations and selected 26102026 assets are integrated; user-log startup and boar
height fixes included. See `docs/world_map/WM15_AFTERNOON_FEEDBACK.md`. Deferred full gates unchanged.

WM15 asset replacement (2026-10-26): **IN_PROGRESS / replacement candidate verified**. Existing imported
models and 52 selectively admitted local source files replace the active primitive props; remaining
search list is in `docs/world_map/WM15_ASSET_REPLACEMENTS.md`. Focused Editor checks 17/17 pass;
Windows WorldMapAssetReplacement build-b has 0 errors / 81 warnings. Player-b passed 29/29 with no runtime errors. Evidence in
`docs/validation/WM15Replacement/`. Full performance gates remain deferred; user acceptance pending.

WM15 asset coverage audit (2026-10-26): **IN_PROGRESS / analysis delivered**. All 11 handmade categories
mapped to local imported assets and downloaded archives; 45 folders, 44 outer archives, 60 nested Unity
packages indexed. Most props already exist; rod/static fish/bones and All Nature VFX URP are available
for selective admission. Remaining searches narrowed to work animations, animated fish, distinct raw
materials and impact VFX. No game changes or new runtime validation. See
`docs/world_map/WM15_IMPORTED_ASSET_COVERAGE.md`. Prior build evidence remains unchanged.

WM15 morning feedback (2026-10-26): **IN_PROGRESS / repair candidate verified**.
Build-e: `Builds/WorldMapMorningFix/Serenity.exe`, 0 errors / 77 warnings. 41 distinct focused tests;
Player-d 31/31 plus saved-game Player-e 6/6, no runtime exceptions. The neighbour completed a physically
funded building after load while off camera. Fishing camp capacity/haul/resume, gates, fleeing, smooth
shores and removal of automatic crossings/snow props verified. Temporary-model inventory delivered;
user asset selection, art acceptance/loading and deferred full performance gates remain next work.
See `docs/world_map/WM15_MORNING_FEEDBACK.md` and `docs/validation/WM15MorningFeedback/commands.md`.

WM15 late feedback (2026-10-26): **IN_PROGRESS / build-c and focused route verified**.
`Builds/WorldMapLateFix/Serenity.exe`: 0 errors / 71 warnings, immutable-source provenance verified.
Player-c: 51/51 PASS, exit 0, no runtime errors; four distinct focused regressions passed. Current
batch repairs resource/death errors, animal corpse pose, tools/bow/fishing, deep-water navigation,
fishing camp work and admits selected content from eleven local packages. Full AQUAS not ported.
Evidence: `docs/world_map/WM15_LATE_FEEDBACK.md`. User visual acceptance, loading optimization,
White Rabbit/full husbandry and full performance gates remain open. Preserve inherited imports.

WM15 October 25 feedback: **IN_PROGRESS / build-f and focused route verified**.
Builds/WorldMapOctoberFix/Serenity.exe: 0 errors / 69 warnings. Player-f: 38/38 PASS, exit 0,
no runtime errors. Eight distinct targeted regressions passed. Final visual acceptance, fine shoreline
polish, loading duration and earlier outstanding content remain open. See
`docs/world_map/WM15_OCTOBER_FEEDBACK.md` for exact build/runtime evidence.
No full 100/300/500 performance suites. Preserve inherited imports and earlier task history.

WM15 preview checkpoint (2026-10-25): **IN_PROGRESS / compiled and built**. Build-e succeeded with 0 errors,
30 warnings after C# and HLSL repairs. Deliver Builds/WorldMapArtHunting/Serenity.exe for manual inspection.
No tests or gameplay/visual acceptance claimed; full performance gates remain deferred. See
`docs/world_map/WM15_PREVIEW_BUILD.md`. Earlier no-execution status below is superseded by user authorization.

WM15 art/health feedback (2026-10-25): **IN_PROGRESS / source only**. All art-direction answers received and
implemented as a first regional appearance pass, with saved animal HP, bow impacts/death/skinning and a first
sheep pasture. See `docs/world_map/WM15_ART_DIRECTION_AND_HUNTING.md`. No compilation, tests, Unity/Player run,
rebuild or new visual acceptance. White Rabbit and full husbandry remain open. Do not move WM15 to DONE.

WM15 content feedback (2026-10-24): **IN_PROGRESS / uncompiled source checkpoint**. User-selected residents,
work tools, crop selection/rows, water outlet/shore repairs, removal of the photographed monolith and fox/wolf
retirement are recorded in `docs/world_map/WM15_CONTENT_SOURCE_REPAIR.md`. White Rabbit admission and
livestock pens remain open. Point 4 has analysis/questions only; regional art changes await answers.
Tests, Unity/Player execution and rebuilding remain prohibited. No advancement to DONE or a new task.

WM15 follow-up feedback (2026-10-24): **IN_PROGRESS / source edits only**. Quarry work/haul, forest-tree
interaction, visible stock-backed fish, UMA consistency and foreign-estate command isolation are being repaired.
User explicitly forbids tests and rebuilding for now. No execution evidence for this follow-up; old build-l and
route-h apply only to the preceding commit. Animal model replacement awaits appropriate approved art.
See `docs/world_map/WM15_FOLLOWUP_SOURCE_REPAIR.md`; do not advance WM15 to DONE.


WM15 user feedback repair (2026-10-24): **IN_PROGRESS — acceptance remains open**. The rejected activation
build is superseded by `Builds/WorldMapV2Feedback/Serenity.exe`. Repairs cover full terrain retention,
batched vegetation, actual local water, wildlife selection/separation, POI localization/colliders, restored
scenery textures, the mountain quarry and a real enemy starter settlement. Established enemy research and
construction now continue during military travel. The later user-requested settlement-first strategy adds real
training, relations/strength/supply checks and persisted advance attack warnings; edit-l passed 30/30 and
build-l succeeded (0 errors / 54 warnings). Native route-h passed 35 points / 0 errors, including enemy growth
and training without a neutral opening raid. Seven-species pointer selection passed play-d. Pan median/P95 are
32.271/37.861 ms, so full performance acceptance remains open. Evidence and exact commands:
`docs/world_map/WM15_FEEDBACK_REPAIR.md`. Long 100/300/500 gates, initial-loading optimization, the three
temporary fauna models and user visual acceptance remain open; this is not WM15 DONE.

WM15 V2 activation (2026-10-24): **IN_PROGRESS**. The user approved activating
`p109-world-map-v2-proposed` and deleting legacy saves. Normal New Game, save profile, start territory,
residents, resources, roads, POI and strategic locations now use V2. Legacy presentation is hidden and the
menu no longer reads old saves. Bounded streamed vegetation plus the post-Awake zoom make the authored clearing
and forest edge visible in the Windows Player. Activation/vegetation EditMode passed 9/9, affected PlayMode
passed 3/3, the final build passed, and native Player smoke exited 0. Evidence:
`Artifacts/WM15Activation/world-map-v2-release-candidate.png` and `Builds/WorldMapV2/Serenity.exe`.
The full rendered 100/300/500 gates still await the user's offline run, so WM15 is not DONE. PostgreSQL stays
PARTIAL without a test DB. The earlier save-protection incident remains historical; the user authorized
deletion of all old local saves.

User-requested support deliverable (2026-10-24): resilient WM15 offline runner DONE. This is runner tooling,
not completion of WM15. The final game test/build/performance/visual gates remain pending. The runner passed
81 fault-injection assertions in Windows PowerShell and 62 in PowerShell 7, including repeated continuation after independent failures; its real-project
dry run planned 172 fixtures and 182 steps. WM14's work and stage status are unchanged by this deliverable.

Statuses: TODO / IN_PROGRESS / PARTIAL / DONE / BLOCKED

WM14 World Map V2 optimization and profiling (2026-10-24): DONE. Reproducible short D3D11 Editor gates at
100/300/500 preserve exact 25/50/25 Tier mixes and measure 58.05/57.68/56.45 average FPS with p95
24.89/21.99/29.56 ms, eight detailed Tier1 presenters, zero per-agent AI Updates and bounded memory. Existing
tree LODs, terrain distances, the 650-collider proxy cap and wildlife tiers are sufficient; no density was
arbitrarily reduced. Horizontal camera-plane LOD distance fixes the elevated-camera detail regression, and
diagnostic-only tier-policy suppression keeps benchmark populations exact without changing normal gameplay.
Affected EditMode passed 33/33, WM05/WM08 PlayMode passed 3/3, corrected U29 regressions passed 2/2 and the
WM14 profile passed 1/1. V2 remains inactive; Player/GPU, broad regression and visual acceptance are WM15.

WM13 World Map V2 version and saves (2026-10-23): DONE. The active compact-world branch remains on format
v29, save generation `p109-stone-age-buildings-v1` and its existing directory. The proposed V2 branch has an
explicit, inactive profile with isolated format v30, generation `p109-world-map-v2-proposed` and separate
directory. Existing production, building/work, connected-world and event payloads remain authoritative; v30
adds only canonical wildlife lifecycle and hunting-pressure state. Save and connected-world generations are
validated before any runtime state changes. Focused EditMode passed 3/3, PlayMode passed 1/1, affected
EditMode passed 97/97, and all 44 protected hashes matched. No activation, art or dependency was added. WM14
is next and has not started.

WM12 World Map V2 construction and clearance (2026-10-23): DONE. The immutable authored buildability policy
now drives one deterministic green/amber/red evaluator over WM02 terrain, WM03 water, eight buildability
masks and WM11 POI protection. WM10 first sites remain green; allowed natural source colliders are amber and
retain stable IDs; water, excessive slope, restricted ridge/marsh zones, POIs, unknown sources and impassable
geometry are red. Amber confirmation reuses existing exclusive worker production jobs, physical resource
output/hauling and construction-buffer/build work; the coordinator independently rejects hard sources and
reconstructs only allowed blockers on load. Focused EditMode passed 9/9, PlayMode passed 1/1 and affected
EditMode passed 49/49. No art/dependency/save schema was added, V2 remains inactive and compact-world behavior
is unchanged. WM13 is next and has not started.

WM11 World Map V2 POI and exploration (2026-10-23): DONE. Exactly nine authored points retain exact
coordinates, regions and protection radii, receive deterministic stable IDs, and have dry slope-bounded
last-mile approaches to the reachable WM09 road network. Connected-world revealed cells remain the sole
discovery state; nine explicit player events use existing active/history save state and do not replay after
load. Streamed POI presentation reuses audited existing prefabs, while exact circular protection is a hard
placement blocker with no automatic clearance. Focused EditMode passed 4/4, PlayMode save/load passed 1/1
and affected EditMode passed 33/33. No dependency was added, V2 remains inactive and legacy unchanged; WM12
followed this completed checkpoint.

WM10 World Map V2 start territory (2026-10-23): DONE. The typed canonical NW start produces one deterministic
population-owned plan at runtime `(-385,720)`: a 145 m prepared clearing, 135 m reveal radius, two starter
objects, four blocker-free first construction sites, ten resident spawns, the full five-type starting resource
guarantee and east/west/south expansion corridors. Prepared space excludes vegetation and resources; resource
placement includes a 2 m corridor safety margin. `NewGameBootstrap` optionally consumes this plan without
changing legacy startup. The existing WM09 road/NavMesh authority provides complete paths to the river and
across `crossing_start_ford`. Existing building, resource and nature assets cover the plan; no dependency was
added. Focused EditMode passed 3/3, affected EditMode passed 21/21 and the PlayMode first-start/NavMesh smoke
passed 1/1. V2 remains inactive and legacy unchanged; WM11 is next and has not started.

WM09 World Map V2 roads and navigation (2026-10-23): DONE. One pure deterministic plan projects exactly seven
authored routes, three crossings and the ridge/marsh/central chokepoints onto the shared WM02/WM03 surface.
All eight regions are present in the start-region reachability matrix; road grades stay within 22/30 degrees.
Seven physical route ribbons feed the existing per-tile NavMesh surfaces, water is Not Walkable, and only the
three authored crossing spans plus authored pass/causeway links can bridge excluded geometry. Existing road,
bridge, rope-bridge, stone, sign and fence assets are reused with no dependency. Focused EditMode passed 3/3,
affected EditMode passed 22/22 and the real north-west-ford Player route passed PlayMode 1/1 with a complete
NavMesh path. V2 remains inactive and legacy unchanged; WM10 is next and has not started.

WM08 World Map V2 species ecology (2026-10-23): DONE. Typed authored wildlife data drives one pure seeded
habitat/population plan and one canonical mutable population service for deer, boar, hare, fox, wolf, bear and
waterfowl. Food/rest/water/shelter nodes connect all eight regions; suitable-biome capacities are map-wide and
concentration zones only multiply density. Seed `20261023` starts 119 stable animals within authored ranges.
Hunting pressure drives adjacent migration, reproduction stays within carrying capacity, and the existing
clock advances the service. Tier1 GameObjects, Tier2 entities in the existing ECS World and object-free Tier3
preserve identity with 35/80/4 projections and no parallel manager or per-animal Update. Seven audited existing
prefabs provide presentation; no art or dependency was added. Affected EditMode passed 13/13 and short PlayMode
passed 1/1. V2 remains inactive and legacy unchanged; WM09 is next and has not started.

WM07 World Map V2 fish and water-body recovery (2026-10-23): DONE. The proposed plan contains exactly 24
stable populations: 16 lake sources at capacity 48 and eight river sources at capacity 42. Every production
point is dry/buildable with a local dry approach, while its disposable fish visual remains over WM03 water.
Partial and full depletion recover through the existing `ResourceEcologyService` with bounded daily logistic
growth; the existing production snapshot calendar baseline prevents same-tick replay after load. The audited
PNB fish prefab is reused with no new art or dependency. EditMode passed 16/16, PlayMode passed 1/1 and the
legacy affected slice passed 13/13. Legacy remains active and unchanged; WM08 is next and has not started.

WM06 World Map V2 natural resources (2026-10-23): DONE. Typed resource types/zones now drive a pure seeded
`TiledWorldPopulation` plan through canonical resource masks, terrain/biome affinity and authored exclusions.
Seed `20261023` yields 277 visible nodes and 19 hidden deposits, preserves the complete start guarantee and
stable IDs across tile order, and leaves fish to WM07. Visible nodes use the existing Production and
ResourceEcology authorities; non-renewable depletion and save/load persist canonically. Ten audited existing
prefabs supply V2 presentation with no imported dependency. Focused EditMode passed 6/6, Save/Load PlayMode
1/1, affected EditMode 26/26 and final research integration 18/18. Legacy remains active and unchanged; WM07
followed this completed checkpoint.

WM05 World Map V2 vegetation and LOD (2026-10-23): DONE. Typed JSON vegetation classes/zones/clearings and a
pure seeded planner produce 3,500 interactive trees, 9,000 decorative tree/shrub instances and 26,500 Terrain
details at hash `5EE931B502E2D754`. Global Poisson spacing, water/slope/start/route/POI/build exclusions, stable
IDs and density-priority overlap ownership are deterministic. Four approved-asset tree variants retain mesh
LODs but remove colliders/billboards; one centralized selector caps disposable nearby collider proxies at 650
with no per-instance `Update`. Focused EditMode passed 4/4; affected WM01–WM05 EditMode passed 23/23 and
PlayMode 5/5. Legacy remained active and unchanged; WM06 followed this completed checkpoint.

WM04 World Map V2 biomes and Terrain paint (2026-10-23): DONE. One immutable typed biome definition now owns
the eight authored regions and ecotone contract. A pure deterministic sampler combines those masks with WM02
height/slope and WM03 water distance into seven normalized Terrain roles; global edge-aligned sampling makes
all 24 tile borders bit-identical. V2 reuses seven existing official TerrainDemo layers through a separate
catalog field, while legacy keeps its five previous layers and remains active. Affected EditMode passed 30/30
and focused PlayMode passed 1/1. No new art/dependency, generation activation, save change or WM05 work was
introduced. WM05 is next and has not started.

WM03 World Map V2 hydrology (2026-10-23): DONE. The canonical water package now produces one immutable
hydrology graph consumed by `TiledWorldHeightSampler` and `TiledTerrainWorld`: per-point main-river widths,
interpolated tributary widths, spatially validated connections, monotonic level policies, four polygonal lake
basins with authored depths/8/8/17/36 m surfaces, bounded dry banks and connected drainage to `river_main`.
The proposed local/strategic water meshes use those same shapes and levels. Affected EditMode passed 25/25,
focused WM03 passed 5/5 and short PlayMode bank checks passed 1/1. The legacy generation remains unchanged;
WM04 is next and has not started.

WM02 World Map V2 terrain generator (2026-10-23): DONE. `TiledWorldHeightSampler` now has an isolated
data-driven branch for WM01 `MapData`: deterministic macro/meso/detail octaves, eight authored anchors,
ridge/erosion, marsh, pass and plateau masks, plus a pure slope-budget histogram. `TiledTerrainWorld` consumes
the authored 257 resolution and 96 m vertical range when explicitly initialized with the proposed definition.
All 24 shared borders across sixteen tiles are bit-identical. The 129×129 audit records 55.21% ≤12°,
76.47% ≤18°, 2.01% >32°, 5.54–82.68 m and 0% height clamps. Focused/affected EditMode passed 20/20.
`p109-compact-world-v1` remains active and its tests pass; WM03 is next and has not started.

WM01 world-map data contracts (2026-10-23): DONE. Immutable Domain contracts and a separate strict package
loader cover schema/generation identity, design-to-Unity transform, polygons, width-bearing splines, river,
tributaries, four lakes and landings, POIs, crossings, height/vegetation/resource/wildlife/buildability masks,
manifest coverage and cross-layer IDs/references. All eleven JSON files are packaged as Resources and load
without activating `p109-world-map-v2-proposed`; `p109-compact-world-v1` remains the live reference. Focused
EditMode passed 19/19 and the side-by-side PlayMode smoke passed 1/1. No broad suite or Windows build was run
by WM01 scope. WM02 is the next unblocked world-map stage and has not started.

P10.9 world-map design package (2026-10-23): DONE as documentation only; overall P10.9 stays PARTIAL.
'docs/world_map/' defines 'serenity-world-map-v1' with coordinated JSON, eight technical map layers,
supplemental concept art, buildability/generation/implementation specifications and 15 gated follow-up tasks.
The generator validation passes. No Unity scene/runtime/save was changed, and proposed generation
'p109-world-map-v2-proposed' is not active. F1 remains the next unblocked scope.

P10.9 modern HUD/site-clearance feedback (2026-10-23): DONE in implementation; overall P10.9 stays PARTIAL.
The local strategy HUD, project-owned icon atlas, resource/resident/navigation/build/context/notification
layout and green/amber/red placement states are connected. Amber sites clear natural blockers through normal
exclusive work orders before existing haul/build logistics start. Priority, cancellation and save/load rebind
preserve canonical authorities; cleared sites with a material shortage wait without dispatching a builder.
Focused EditMode and HUD PlayMode are green; Windows Player build and D3D11
bootstrap smoke pass. User visual acceptance and the remaining F1 multi-settlement/tier matrix remain open.
See 'P109_MODERN_HUD_CLEARANCE.md'.

P10.9 notification/23102026 asset feedback (2026-10-23): DONE in implementation; final user acceptance remains
F1. Clicking a notification focuses its target and removes the matching feed entry. Eleven local archives were
audited and a code-free 217-asset subset supplies URP woodland details, day/night ambience, hunting feedback and
heavy-rain lightning through existing clock/work/weather projections. EditMode 6/6 and PlayMode 6/6 passed;
Windows build succeeded with 0 errors / 27 warnings, GUID `c4cf71172408462d9635033be0b9da68`.

P10.9 shore/lifecycle feedback (2026-10-23): implementation DONE; user acceptance remains F1. Worker-driven
demolition with saved progress, no age damage/brown damage tint, eleven validated fishing landing markers and
snapping, cardinal palisade/path drag, single Garden choice, corrected outdoor model scales and deer locomotion.
Focused EditMode 22/22; affected EditMode 41/41; full-scene PlayMode 1/1; actual Player bootstrap/landing smoke
passed. Meeting Area's social/mood scenario remains unimplemented and is now explicitly labelled in the UI.
No third-party dependencies added. Final rebuild evidence is in P109_BUILDINGS_IMPLEMENTATION.md.

P10.9 Stone Age buildings plan (authorized 2026-10-21): B1-B3, C1-C4, D1-D2 and E1-E4 DONE; C5 DEFERRED by
explicit user direction on 2026-10-22; the gameplay-feedback stabilization slice is DONE in code
with final Player acceptance retained for F1. F1 is next and remains inside P10.9. Internal slices keep targeted
automated tests, documentation and local commits. By user direction on 2026-10-22, C1-C4 run
continuously with one sufficiently broad regression and separate Windows build after C4; broad
regressions are otherwise reserved for F1 or a concrete compatibility/data-loss risk.
The implementation order and gates are defined in `P109_STONE_AGE_BUILDINGS_PLAN.md`.

P10.9 post-E visual/gameplay feedback repair (2026-10-22): DONE in implementation, acceptance remains F1.
Mixed-category construction buffers fix Clan House's cured-hide delivery/progress deadlock. Existing authored
resource colliders suppress duplicate broad selection volumes. Campfire VFX has an explicit URP particle
material. Completed facilities show downloaded CityBuilderThree/Low Poly Farm/RTS Toon/Synty final models
instead of construction primitives; Primitive Shelter and Storage Basket are included. Primitive Garden uses
a bounded 2×2–8×8 held-LMB rectangle and persists the exact
zone cells. Focused EditMode passed 31/31 and new visual/model regression passed 7/7. Broader P109 recorded
65/68 EditMode and 6/9 PlayMode, with failures outside this repair documented in PROJECT_STATE. The separate
Windows Development build now succeeds with 0 errors / 53 warnings, GUID
`64ec73cb93504dbe8490f9fd129428fc`. The post-build P9.4 bootstrap regression caused by obsolete
renderer-count/mesh-prefix checks is repaired; 10/10 focused EditMode and the exact UI/two-resident PlayMode
bootstrap test pass 1/1. F1 retains the final visible native route and broader acceptance matrix. The
starter-prefab rerun passed 2/2.

P10.9 E1-E4 extended buildings and lifecycle (2026-10-22): DONE. Fourteen canonical definitions and
project-owned prefabs cover clan housing, healer/shrine/chief/meeting/training facilities, fishing,
gathering, garden, well, palisade, gate, watchtower and path. Home/work assignments enforce capacity and
exclusivity; healer/training and passive social effects use existing character authorities. Shoreline and
groundwater placement rules, fishing-camp gating and well water production are data-driven. Gates alter
the shared occupancy graph; destruction releases occupied cells. Wear, repair, hut upgrade and demolition
are exactly-once operations over canonical building/inventory state, preserving stable identity and cargo.
P10.9 EditMode passed 24/24 and affected EditMode 66/66. Windows Development build succeeded with
0 errors / 53 warnings, GUID `709397d549104081bccd3d70d355d5e7`. Affected PlayMode stalled during
known UMA LocalGameplay initialization and produced no NUnit result; final native acceptance remains F1.

P10.9 D1-D2 hunting and preservation (2026-10-22): DONE. An operational Hunting Camp gates hunting;
whole carcasses are conserved through a Butcher Station into meat, hides and bones. Food Storage,
Drying Rack and Hide Workshop complete the shared logistics/production chain with dried edible meat
and cured hides. The five D-stage prefabs have staged visuals and canonical definitions. Focused
EditMode passed 37/37; Windows Development build succeeded with 0 errors / 53 warnings, GUID
dc35880aece74843b8f7d799bb8576c2. E1-E4 are now complete; C5 remains deferred and final acceptance stays at F1.

P10.9 gameplay-feedback stabilization (2026-10-22): DONE in implementation. Independent gatherers
and source-capacity rejection, locomotion/sleep/indoor visibility, construction cancel/reasons/progress,
click-to-focus notifications, bounded animal roaming, hunting arrows/HP, human HP, Campfire work/fire,
material-color correction, upgraded hut art, bed capacity/ground sleep/mood/work refusal, persistent
favorite activities, skill effects/progression and persisted weapon durability are integrated through
the existing authorities. Affected EditMode passed 46/46. PlayMode runner attempts stalled during UMA
scene initialization without NUnit results and were cleaned by verified PID; final Player acceptance is
therefore not claimed here. The follow-on D1-D2 slice is now complete.

P10.9 screenshot-feedback slice (2026-10-21): PARTIAL pending
user-owned visual acceptance. Six visual/interaction defects and normal-RMB priority
addressed; final EditMode 76/76. Expanded PlayMode 15/16 followed by construction fixture
correction and focused 1/1 pass. Separate Windows build succeeded: 0 errors / 53 warnings,
GUID ac1af2b6526f4451ae7dd2d1b1822398. Audit A and proposed architecture/task graph
for the 25-building request are delivered in `P109_STONE_AGE_BUILDINGS_PLAN.md`.
Stages B–F remain proposed, not executed: user approval is the explicit gate in the
attached prompt. Cooking/campfire belong to that first approved functional slice.
Manual QA is now performed by the user; the historical firewall wait below is obsolete.
No new asset import, additional chats, remote push or P10.10 work in this slice.

P10.9 compact-world/content slice (2026-10-21): PARTIAL pending final acceptance.
User-approved 1 km square, original geographic scheme, multiple fish schools per lake,
shore output piles and reviewed local art integration are implemented. No vendor game
framework adopted; remaining library integration has a staged plan in
`P109_COMPACT_WORLD_HANDOFF.md`. Old saves remain isolated and unchanged.
Final EditMode 50/50; expanded PlayMode 13/14 followed by bank fix and focused 2/2.
Windows build succeeded (0 errors / 53 warnings, c09db63a70084c12927995abacd7965c).
Native acceptance awaits dismissal of the firewall dialog by the user; no benchmark started.

P10.9 Worksite V2 feedback slice (2026-10-21): PARTIAL. User requested immediate correction
of animation/ground-output/hauling/building/resource-selection/fishing defects. New assets are
analysis-only. Implemented; EditMode 54/54, focused PlayMode 3/3 and physical partial-haul 1/1.
Windows build GUID 017e3d80ef804d86af8678a2abc68c6b succeeded, 0 errors / 56 warnings.
Native move/idle, two tree outputs, mining, fishing, zoom and LMB/MMB dismissal exercised.
Native construction/close-up work-tool/fall review remain open; consolidated PlayMode **13/13 passed**.
Old saves preserved; new generation/path isolates changed mechanics. Overall P10.9 remains PARTIAL.

P10.9 Synty playable starter slice (2026-10-20): PARTIAL. New tree presentation, shoreline,
ripple water and harvest transition implemented; final EditMode 10/10 and PlayMode 8/8 passed.
Windows build succeeded (0 errors / 54 warnings), GUID 7d986af66c3b4dc9aca0af6341ebd670.
Native selection/move/chop/fall/zoom passed on the earlier candidate; final water inspected
in the final Player. User confirmed Home/Space and smooth held-MMB movement (recorded
2026-10-21). Full native chop-route repetition on the final binary is still pending.
Protected hashes match 44/44 including 8/8 old saves. See `docs/P109_SYNTY_PLAYABLE_HANDOFF.md`.
Overall P10.9 remains PARTIAL; P10.10 and deferred performance work are not started.

P10.9 import slice (2026-10-20): user explicitly approved direct Synty Meadow Forest import
with rollback. Pre-import playable changes checkpointed at `c732c96` (not visually accepted).
Scoped Meadow/Core URP assets are imported without replacing gameplay scenes or saves;
EditMode passed 9/9 and playable-patch PlayMode passed 5/5. The diagnostic Windows build
passed with 0 errors / 34 warnings; protected hashes match 44/44 (including 8/8 old saves).
The import slice is complete, but gameplay art binding and manual acceptance are not claimed.
P10.9 stays PARTIAL and P10.10 is not started.

P10 is user-authorized on 2026-10-16 and normally executes P10.1→P10.10. Explicit user decisions D-141 and D-143 permit P10.4 and then P10.5 implementation to proceed while P10.3 and P10.4 remain PARTIAL. These sequencing exceptions waive no acceptance gate: all deferred P10.3/P10.4 debt is mandatory for P10.9/P10.10. P10.9 is PARTIAL and remains the sole active task; P10.10 has not started. P8.6/U29/U30/U31 retain PARTIAL/PARTIAL/IN_PROGRESS/BLOCKED and are not silently resumed or closed by P10 evidence.

U30 remediation (user-authorized after run4): implementation and short verification are tracked in `U30_HANDOFF.md`. No replacement eight-hour run is authorized; U30 remains IN_PROGRESS, U29 PARTIAL, U31 BLOCKED. The requested product plan is `PRODUCT_ROADMAP_RU.md`: Russian UI, main menu, hero appearance and a finished Stone Age world need visible product acceptance in addition to these technical stages. P10 supersedes the product sequencing after P9 but does not automatically activate or close this legacy post-MVP backlog.

Partial handoff (2026-10-12, user-authorized): U29 infrastructure, gameplay-safe optimizations, tests and evidence are retained, but the task is explicitly PARTIAL rather than DONE. The latest clean sustained control remains FAIL at p95 38.216 ms for 1000 Characters versus the 33.33 ms gate. The failed four-worker experiment was not adopted. Residual work is documented in `docs/U29_HANDOFF.md`; U30 may be prepared in a new chat as an accepted sequencing exception, but U29 debt remains open and U31 cannot use PARTIAL as proof that its U29 dependency passed.

Latest sustained diagnostic (2026-10-12): `diagnostic-20261011-212803-791` completed with p95 38.029 ms and 4859/47294 over-budget diagnostic frames. Capture integrity passed; the detailed report was recovered after a wrapper/report-orchestration error. Slow intervals show increased callback, worker/weather and native navigation costs, not dominant native animation cost. This was U29 IN_PROGRESS evidence and is now superseded by the PARTIAL handoff above; U30 preparation subsequently moved to IN_PROGRESS.

Latest U29 review (2026-10-12): complete `offline-20261011-194641` remains FAIL; 8/12 repeats pass. Worst p95 at 1000 improved to 40.626 ms but exceeds 33.33 ms; 500 repeat 3 fails the existing GC/tail proxy despite acceptable p95. Full regression is 414/102 passed. U29 was later checkpointed as PARTIAL and U30 preparation was authorized as an explicit sequencing exception. Profile slow intervals, validate allocation diagnostics and separate actual GC/native animation/callback timings before another U29 matrix; short smoke PASS is insufficient.

Diagnostic follow-up (2026-10-12): `Tools/Run-U29-Diagnostic.cmd` now provides a separate local 1000-Character sustained capture (180+900 seconds plus fresh build), with calibrated allocation counters, frame-aligned callback sections, available native profiler markers and slow/healthy comparisons. Detailed observation is opt-in and cannot complete U29. The long diagnostic is left for the user to run independently; no gameplay semantics or gate thresholds are changed.

| Product ID | Status | Task | Depends on | Definition of Done |
|---|---|---|---|---|
| P1 | DONE | Русский интерфейс и понятные действия | U27,U28,U30A | Все обычные и developer игровые поверхности русские; стабильные ключи/fallback; понятные причины отказа; 1280×720 + 125%; тесты, build и Player smoke. |
| P2 | DONE | Главное меню и начало партии | P1 | Чистый запуск даёт Continue/New Game/Load/Settings/Exit на русском; повреждённые сохранения объяснены; cancel/back и первая группа настроек работают. |
| P3 | DONE | Эталонная сцена и арт-производство | P2 | Реальный Player содержит проверенный эталон лагеря/персонажей; направление обратимо, финальная визуальная приёмка пользователем остаётся открытой до P8. |
| P4 | DONE | Создание и внешность героя | P3 | Герой/династия/AppearanceState сохраняются и одинаково отображаются после переходов и Save/Load. |
| P5 | DONE | Обитаемый регион | P3,P4 | Связный читаемый Stone Age регион без обязательных кубов. |
| P6 | DONE | Понятный первый час | P2,P5 | Опциональный маршрут проходит реальные Work/Production/Research/Event/Military/Map команды, объясняет блокировки и сохраняет P4/P5 identity; ручная UX-приёмка пользователя остаётся открытой до P8. |
| P7 | DONE | Полный каменный век | P4,P5,P6 | Связный маршрут до войны/наследования; симметричное занятие пустого поселения, бой при защите, победа/поражение, продолжение и Save/Load проверены. |
| P8 | PARTIAL | Визуальное производство, масштаб и выпуск | P7 | P8.1–P8.5 завершены. P8.6 остановлен пользователем без полной U29/U30/U31 квалификации; выпускные gates не считаются пройденными. |
| P8.1 | DONE | Визуальный прототип и выбор базового набора | P7 | В малом Player-стенде согласованы стилизованно-естественные PBR берег/земля/лес/камень, спокойная палитра, коричневые стволы, свет/атмосфера и один настраиваемый персонаж; без преждевременной переделки всей карты, групп и UI. |
| P8.2 | DONE | Новая игровая карта | P8.1 | Направление P8.1 перенесено на связный авторский регион P5 с читаемыми маршрутами, биомами, ресурсами, стартовой и вражеской территориями; каноническая навигация/строительство/Save-Load сохранены. |
| P8.3 | DONE | Рабочие группы и простые задания | P8.2 | Поток «клик по объекту → действие → приоритет», назначаемые группы добытчиков, строителей, носильщиков, ремесленников, земледельцев/собирателей, учёных, разведчиков, воинов и лекарей/смотрителей, а также «Прямо сейчас» работают через canonical scheduler/reservation/Save-Load без per-NPC `Update`. |
| P8.4 | DONE | Полноценный редактор внешности | P8.3 | Игрок настраивает поддерживаемые параметры внешности героя через canonical `AppearanceState`; preview, New Game, Tier-переходы и Save/Load дают согласованный результат без vendor-owned state. |
| P8.5 | DONE | Красивый интерфейс | P8.4 | Игровой UI получает цельный производственный визуальный язык, ясную иерархию и читаемые состояния; открытые visual/first-hour/campaign acceptance P3/P5/P6/P7 проходят отдельно от автоматических тестов. |
| P8.6 | PARTIAL | Интеграция и квалификация | P8.5 | Сохранены integration/performance fixes, тесты и build. Пользователь остановил оставшиеся прогоны 2026-10-14; U29/U30/U31 не закрыты, см. P86_HANDOFF. |
| P9 | DONE | Реалистичный мир на официальных Unity-ассетах | P8.5 | P9.1–P9.6 завершены: официальный Terrain source, физический регион, биомы, поселение, атмосфера/global map и интегрированная визуальная приёмка. |
| P9.1 | DONE | Импорт и малый реалистичный benchmark | P8.5 | Официальный Unity URP Terrain Sample импортирован выборочно без downgrade пакетов; отдельная Terrain-зона в LocalGameplay проходит EditMode/PlayMode/build/видимый RTS+TPS Player smoke, а старая карта сохранена. |
| P9.2 | DONE | Перенос игрового региона на новый мир | P9.1 | Канонические территории, 24 ресурса, строительство, NavMesh, Save/Load, RTS/TPS и погода перенесены на проверенную 128 m Terrain-композицию; прежние видимые map roots удалены после regression/build/Player proof. |
| P9.3 | DONE | Биомы и наполненный мир | P9.2 | Весь доступный локальный регион заполнен различимыми лесными, береговыми, почвенными, каменистыми и ресурсными зонами правдоподобной плотности и масштаба; рассеивание детерминировано, ресурсы кликабельны, навигация работает, LOD/коллайдеры обоснованы, происхождение ассетов задокументировано. |
| P9.4 | DONE | Поселение, ресурсы и взаимодействие | P9.3 | Детализированные укрытие, склад, огород, лагерь, шесть рабочих мест, четыре запаса и world-индикаторы приоритетов образуют цельную Stone Age presentation; добыча, переноска, строительство, NavMesh, погода и Save/Load сохраняют существующую authority. Новые внешние ассеты не импортированы. |
| P9.5 | DONE | Атмосфера и глобальная карта | P9.4 | Один data-driven профиль проецирует канонические время/погоду/сезон в свет, мягкие тени, воздух/туман, воду, дождь/снег и день/ночь; additive global map показывает рельеф, реку, маршруты и шесть подписанных канонических локаций без новой authority. |
| P9.6 | DONE | Интеграция и визуальная приёмка | P9.5 | Обычный Player-маршрут, identity и субъективная визуальная приёмка проходят при 1280×720/1600×900. UMA использует brown-hide Human 3.0 recipes, mass draw — bounded humanoid LOD, post-Load UI полностью перерисовывается; P8.6/U29/U30/U31 не объявлены пройденными. |
| P10 | IN_PROGRESS | Завершённая связная игра по уточнённому контракту | P9.6 | P10.1–P10.10 обычно выполняются последовательно; D-141/D-143 разрешают продолжить с P10.4/P10.5 при PARTIAL предшественниках, но итоговая normal-Player приёмка всё равно обязана закрыть весь долг P10.3/P10.4 вместе с единым миром, персонажами, домом/семьёй, войной/захватом, cheats, Save/Load и 1920×1080 performance. |
| P10.1 | DONE | Требования и технический baseline | P9.6 | Сведены FRS, ответы пользователя и фактическое состояние; перечислены 7 текущих типов и 24 узла, P10 asset/save policy, UI flows, P10.2–P10.10 и acceptance matrix; normal Player baseline снят при 1920×1080 без реализации следующих этапов. |
| P10.2 | DONE | Единый связный мир и стратегический обзор | P10.1 | Один 512×512 м physical world authority и same-world orthographic view; 233 344 м² sampled accessible dry land, connected NavMesh/construction, шесть сохранённых + четыре append-only IDs, вода/горы/леса/пустоты, canonical fog/scouting, snapshot v20/P10 namespace и green tests/build/1920×1080 Player evidence. |
| P10.3 | PARTIAL | Выбор, контекстные действия и очереди | P10.2 | Код, v21 queue persistence, StableEntityId Tier/Save-Load continuity, focused/affected tests and provenance build are complete. Visible D3D11 Player passed LMB, RMB menu/reasons/edge clamp, ordinary Move/Guard and UI Save/Load selection continuity, exposing and fixing click-through and Load-clear defects. A human must still prove physical Shift-click/Shift-command, slow multi-frame drag, needs/protected-phase resume, Tier1→Tier2→Tier1 and the remaining target/action matrix before DONE. |
| P10.4 | PARTIAL | Природные ресурсы и промышленная добыча | P10.3 implementation; D-141 defers its visible debt | Code/snapshot v22 and focused/affected coverage remain. P10.9 closed the inherited U15 physical-walk regression (1/1), supplied a provenance-stamped build and four cleanly exiting D3D11 subsystem smokes. Still required: a saved physical normal-Player route for build/extract/haul, exact 1×/7×, exhaustion/15-day blocked respawn, restorative mining and Save/Load. Automation cannot substitute for that route, so status remains PARTIAL. |
| P10.5 | DONE | Персонажи, одежда, LOD и анимация | P10.4 implementation; D-143 defers its acceptance debt | User-authorized Quaternius CC0 role outfits/base heads and authored animation libraries provide worker/military visuals and carry/gather/chop/mine/build/fish/hunt/combat/sleep playback. Normal New Game deterministically covers three age groups. Detailed skinned, instanced mid, lower-detail far and cull stages preserve stable appearance. Accepted evidence: 45/45 affected EditMode, 15/15 affected PlayMode, 4/4 AI EditMode, 35/35 AI PlayMode, 4/4 exact-route PlayMode, provenance-stamped build and passing 1920×1080 D3D11 Player smoke with role/action/LOD/Save-Load proof. P10.3/P10.4 debt is unchanged. |
| P10.6 | DONE | Дом, сон, взрослые отношения и P10 Save/Load | P10.5 | Канонические дом/indoor/access/назначенная кровать/occupancy управляют сном и P10.3 safe interrupt/resume; отношения требуют двух независимых согласий и adult-only U18 pregnancy с неявным fade/skip. Snapshot v23 сохраняет domestic + Tier/position/needs поверх полного v22 payload, P10 slots отделены, P9 v1–19 видны только как read-only metadata. Focused EditMode 5/5, focused PlayMode 2/2, affected P10.3–P10.6 10/10, U29 save 1/1, build и 1920×1080 D3D11 Player 13/13 прошли; полный suite-диагностический долг записан в P106_HANDOFF. |
| P10.7 | DONE | Военные, обучение, цели и захват | P10.6 | Казарма повышает того же жителя по data-driven Recruit→Elite curve и выдаёт skill/военную одежду/roster; `Напасть` доступно только обученному единому отряду, запрещает прямые civilian/friendly/non-military цели и физически идёт к цели, а прибытие заявляет только близкие flags. Persisted v24 capture требует main+active-barracks flags (или только main без казарм), идёт ровно 5 game hours/5 real minutes at x1, масштабируется скоростью, freeze/reset/next-barracks и детерминированно передаёт owner/buildings/stores/residents/soldiers. Focused 6/6 EditMode, 2/2 PlayMode, exact affected regressions 2/2+1/1, build 0 errors and visible 1920×1080 D3D11 Player smoke pass; broad inherited failures are enumerated in P107_HANDOFF. |
| P10.8 | DONE | Коды и диагностические сценарии | P10.7 | Pause→Codes выполняет `serenity`, повторяемый atomic `res_serenity` (+10 000 каждого из 9 текущих ресурсов в canonical common storage) и повторяемый `army_serenity` (по одному уникальному canonical бойцу каждого из 4 data-driven ranks); отдельная family diagnostic создаёт взрослых, взаимные отношения/consent, indoor bed и P10.6 pregnancy через canonical services. Snapshot v25 необратимо сохраняет `cheatsUsed`/achievements off и diagnostic sequence; invalid/failure не помечают партию. Focused 9/9 EditMode + 3/3 PlayMode, affected 90/90 + 39/39, provenance build and visible 1920×1080 D3D11 Player pass. U31 remains independently BLOCKED at 1/3. |
| P10.9 | PARTIAL | Сквозная интеграция и стабилизация | P10.8 | Tiled-world Stages 1–2 and the prior video follow-up remain integrated. Active 2026-10-20 scope is one playable starter patch: stable projected scenery, physical starter roster, direct context commands, crown selection, readable whole-source harvesting/fall feedback and shoreline water, with old saves isolated. Current test/build/manual evidence is recorded at the top of PROJECT_STATE; automated passes alone do not complete the requested normal-Player review. Inherited P10.3/P10.4 manual product routes remain and performance profiling is deferred. P10.10 has not started. See D-151–D-154. |
| P10.10 | TODO | Финальная продуктовая приёмка | P10.9 | Clean checkout/build и normal UI/Input Player проходят полный продуктовый сценарий и визуальный review при 1920×1080 без developer shortcuts. Не закрывает P8.6/U29/U30/U31 без их собственных gates/решения пользователя. |

| ID | Status | Task | Depends on | Phase | Definition of Done |
|---|---|---|---|---|---|
| U00 | DONE | Среда, Unity-проект и Git | — | MVP | Проект открывается без ошибок, создаёт Windows Development build, первый commit создан. |
| U01 | DONE | Архитектура assemblies и слоёв | U00 | MVP | Все assemblies компилируются; dependency graph задокументирован. |
| U02 | DONE | Пакеты и базовая конфигурация | U00,U01 | MVP | Package restore стабилен; проект открывается на чистом checkout. |
| U03 | DONE | Игровое время и календарь | U01 | MVP | Unit tests времени проходят; save/load времени работает. |
| U04 | DONE | Stable IDs и Save/Load | U01,U03 | MVP | Save→load восстанавливает идентичное состояние ключевых сущностей. |
| U04A | DONE | PostgreSQL Persistence Layer | U04 | MVP | Persistence abstraction; versioned migrations; transactional save/rollback; stable-ID round trips; separate integration-test database. |
| U05 | DONE | Локальная сцена, RTS-камера и input | U02 | MVP | Камера/выбор/команды работают в test scene. |
| U05A | DONE | Asset Pipeline + Free Asset Acquisition | U02,U05 | MVP | Runtime/staging folders, asset registry, free/placeholder base set, import normalization and traceable licenses. |
| U06 | DONE | Domain-модель персонажа Tier 1 | U03,U04 | MVP | EditMode tests модели; data-only snapshot готов для будущей карточки NPC. |
| U07 | DONE | GameObject presentation Tier 1 | U05,U05A,U06 | MVP | Tier1 персонаж видим, двигается, взаимодействует; состояние не хранится только в View. |
| U08 | DONE | Строительная сетка и blueprint-система | U04,U05,U05A | MVP | Можно спроектировать и достроить дом; состояние сохраняется. |
| U09 | DONE | Ресурсы, инвентари и склады | U06,U08 | MVP | Нет дублирования ресурсов; стройка требует реальный ресурс. |
| U10 | DONE | Utility AI Tier 1 | U06,U07,U09 | MVP | 100 NPC выполняют потребности и работу без 100 Update-heavy scripts; dev selection читает live canonical resources/claims. |
| U11 | DONE | Рабочие группы и задания | U10 | MVP | Игрок управляет >100 NPC через группы. |
| U12 | DONE | DOTS bootstrap Tier 2 | U02,U06 | MVP | Тысячи тестовых entities обновляются без GameObject-per-entity. |
| U13 | DONE | Tier manager 1↔2↔3 | U04,U07,U12 | MVP | Именованный NPC сохраняет ID/семью/здоровье при смене tier. |
| U14 | DONE | Детерминированный off-camera simulation | U03,U04,U13 | MVP | Одинаковый seed даёт тот же итог; zoom не меняет результат. |
| U15 | DONE | Производство и фермерство | U09,U10,U14 | MVP | Поселение устойчиво производит еду/материалы несколько игровых лет. |
| U16 | DONE | Research framework и каменный век | U06,U15 | MVP | Законченная каменная ветка открывает здания/рецепты. |
| U16A | DONE | Debug gameplay controls / usability patch | U05,U08,U10,U11,U15,U16 | MVP | Игрок назначает research workers, выбирает research-gated здания и управляет временем без конфликта с work-group hotkeys. |
| U17 | DONE | Династия и наследование | U03,U04,U04A,U06 | MVP | Смерть монарха передаёт управление допустимому наследнику; extinction=GameOver. |
| U18 | DONE | Семейные события, болезни и травмы | U06,U17 | MVP | Событие может изменить состояние и способности наследника. |
| U19 | DONE | Погода и сезоны | U03,U05,U15 | MVP | Погода имеет измеримые gameplay-модификаторы. |
| U20 | DONE | Тактический RTS-бой | U05,U05A,U06,U07 | MVP | Два отряда завершают бой с детерминируемыми потерями. |
| U21 | DONE | Армия, капитаны и абстрактные отряды | U17,U20 | MVP | Отряд корректно сохраняется и переходит detailed↔aggregate. |
| U22 | DONE | Third-person control | U02,U07,U20,U21 | MVP | Переключение RTS↔TPS не теряет приказы и не ломает state. |
| U23 | DONE | Автобой и возврат в физическую битву | U14,U21 | MVP | Физический и abstract бой используют общие параметры и сходятся по диапазону результатов. |
| U24 | DONE | Разрушение, пожар и захват зданий | U08,U20,U23 | MVP | Здание разрушается/захватывается и правильно меняет производство. |
| U25 | DONE | Глобальная карта как отдельная сцена/режим | U04,U14 | MVP | Local↔Global переход сохраняет состояние. |
| U26 | DONE | AI одного государства | U08,U09,U14,U15,U16,U17,U18,U21,U23,U24,U25 | MVP | Независимый AI bootstrap владеет своими research/resources/production, строит, создаёт армию, атакует и согласованно захватывается. |
| U27 | DONE | UI Toolkit/uGUI gameplay UI | U04,U06,U08,U11,U16,U20,U22,U25,U26 | MVP | Все MVP действия, включая Save/Load и work groups/jobs, доступны без debug меню. |
| U28 | DONE | Событийная система | U04,U04A,U06,U17,U18,U27 | MVP | Одиночные события и цепочки с weights/cooldowns сохраняются и воспроизводимы при seed. |
| U29 | PARTIAL | Performance gates: 100→300→500→1000 | U10,U12,U13,U14,U20,U26,U27,U28 | MVP | Реальные 1000 Characters (250/500/250 tiers) держат Player p95 ≤33.33 ms без runaway allocations. Остаточный gate не пройден; см. U29_HANDOFF. |
| U30 | IN_PROGRESS | Automated tests и soak | U04A,U10,U13,U14,U23,U26,U28,U29 | MVP | Run4 is historical runner-PASS evidence, not accepted DONE. 2026-10-13 fixes AI arrival, seed, exact runtime hashes, telemetry, provenance and health/weather; 60/12 targeted tests, regressions and build pass. Short2: 180 productive/186 actual seconds, 92 exact checkpoints, zero errors. Manual eight-hour command supplied, not launched by the agent; native/history/sustained acceptance limits remain in U30_HANDOFF. Approved undefended occupation is planned in PRODUCT_ROADMAP_RU P7, not implemented. |
| U30A | DONE | Pre-U31 FRS compliance patch | U22,U25,U26,U27,U28,U30 | MVP | Canonical New Game создаёт ровно 10 детерминированных player Characters и независимое AI-государство из 6 Characters; discovery, AI construction, inventory-backed supply, TPS attack через U20, victory continuation и snapshot v15 закрыты и проверены. U30 dependency sequencing was explicitly authorized while its external run remains pending; this does not satisfy U29/U30 or unblock final U31 completion. |
| U31 | BLOCKED | MVP Integration Gate | U08,U10,U13,U16,U17,U19,U22,U23,U25,U26,U27,U28,U29,U30,U30A | MVP | Собственный normal UI/Input integration path, regressions, build и Player smoke проходят. Финальный DONE заблокирован только U29 PARTIAL и U30 IN_PROGRESS; см. U31_HANDOFF. |
| U32 | TODO | Wealth / Influence | U06,U17 | POST | Показатели влияют на статус/события/отношение. |
| U33 | TODO | Министры, губернаторы и ведомства | U11,U17,U32 | POST | Замена чиновника меняет эффективность ведомства. |
| U34 | TODO | Казна, налоги, коррупция | U32,U33 | POST | Ставки меняют казну/стабильность; коррупция измерима. |
| U35 | TODO | Законы и протесты | U33,U34 | POST | Законы влияют на систему; протест может эскалировать. |
| U36 | TODO | Дипломатия | U17,U25,U26 | POST | AI оценивает условия и хранит договоры/последствия. |
| U37 | TODO | Шпионаж | U26,U36 | POST | Миссия имеет время/стоимость/шанс/последствия. |
| U38 | TODO | Эпохи после каменного века | U16,U31 | POST | Следующая эпоха не ломает save/data schemas. |

Product sequencing note (2026-10-13): by explicit user decision, `PRODUCT_ROADMAP_RU.md` directions P1–P7 execute sequentially before final U29/U30/U31 qualification. This does not change the statuses or relax the gates above. P8 performs final profiling, history/native-memory closure, the complete U29 matrix, continuous-process endurance, a fresh segmented eight-hour U30 and U31 finalization against the feature-complete first-era build. Each P direction runs in a separate Codex chat from committed predecessor state; no concurrent shared-checkout edits.

P8 staging note (2026-10-14): P8 is split into P8.1–P8.6. P8.1 establishes the reversible visual benchmark and approved UMA provider; P8.2 applies the accepted map language to the finite authored region; P8.3 adds nine canonical role groups, direct object actions, deterministic assignment/reservation, safe «Прямо сейчас» interruption and snapshot v18 work persistence. P8.4 adds the canonical UMA appearance editor and snapshot v19. P8.5 adds the shared natural UI theme, responsive Main Menu/editor/gameplay/global/TPS surfaces and 1280×720/125% evidence; details are in `P85_HANDOFF.md`. P8.6 was the final qualification slice and is now checkpointed PARTIAL by the note below. U29 remains PARTIAL, U30 remains IN_PROGRESS and U31 remains BLOCKED.

P8.6 checkpoint note (2026-10-14): retained fixes establish normal seeded technical-scenario launch, exact U29 tier/save wiring, bounded Tier1 detailed presentation with centralized lightweight movement/rendering, and cadence/revision-gated hot paths. P8.6 EditMode 8/8 and U29 PlayMode 19/19 passed; the Windows Development build succeeded with errors=0/warnings=9. A full-length 1000-Character diagnostic smoke recorded p95 16.673 ms and exact state/save invariants, but cannot complete U29. The earlier full matrix failed the unchanged GC/tail proxy, and the replacement matrix was incomplete. By explicit user instruction no further performance, endurance, connected or release tests are to be run in this checkpoint. Therefore P8/P8.6 are PARTIAL, U29 remains PARTIAL, U30 remains IN_PROGRESS and U31 remains BLOCKED. There is no active task; resumption starts from `P86_HANDOFF.md`.

P9.1 completion note (2026-10-14): official Unity URP Terrain Sample 1.0.3 content is selectively imported and proven in a separate normal-`LocalGameplay` benchmark with 4 Terrain layers, 64 trees, 24 rocks, 240 foliage objects and water. P9.1 EditMode 2/2, PlayMode 1/1 and P8.2 preservation 3/3 passed; Windows Development build and visible RTS/TPS Player smoke passed. P9.2 is the next unblocked product task. This new visual track does not resume or change P8.6/U29/U30/U31 qualification statuses.

P9.2 completion note (2026-10-14): the canonical six-location region and 24 finite resources now run on one 128 m physical Unity Terrain using the approved official layers, vegetation, rocks and water. The disposable P9.2 projection contains 4 Terrain layers, 76 trees, 44 rocks and 288 foliage instances; it terrain-projects buildings and Tier1 Characters while canonical coordinates, stable IDs, resource amounts and save authority stay unchanged. The obsolete visible P3/P5/P8.1/P8.2/P9.1 map roots were removed only after validation. Focused P9.2 passed 3/3 EditMode and 3/3 PlayMode; affected suites passed 9/9 EditMode and 7/7 PlayMode; broad non-U31 PlayMode passed 137/137; Windows Development build and visible RTS/TPS Player smoke passed. Broad EditMode recorded 505 passed, 25 skipped PostgreSQL cases and one unrelated existing abstract-battle timing overrun. P9.3 is next. U29 remains PARTIAL, U30 remains IN_PROGRESS and U31 remains BLOCKED.

P9.3 completion note (2026-10-14): the accessible local Terrain is now a deterministic populated biome world with 7 official layers, 7 named zones/6 biome kinds, 170 added trees, 94 added rocks, 930 added ground-cover instances and 24 readable resource formations. Large props retain official LOD plus physical collision; ground cover and formation satellites are collider-free. All six canonical location IDs, 24 resource identities/amounts, construction, weather projection and Save/Load remain authoritative in their existing services. Focused suites passed 3/3 EditMode and 3/3 PlayMode; affected P5/P8.2/P9.2 suites passed 10/10 and 9/9; Windows Development build and visible RTS/TPS Player smoke passed. Broad non-U31 EditMode passed 498 with 25 unavailable PostgreSQL skips; the inherited P9.2 abstract-battle timing overrun did not reproduce (13.70 s). P9.4 is next. U29 remains PARTIAL, U30 remains IN_PROGRESS and U31 remains BLOCKED.

P9.4 completion note (2026-10-15): the P9.3 Terrain now carries project-owned detailed shelter/storage/crop prefabs (43/27/26 renderers), six distinct Stone Age worksite affordances, four detailed canonical camp-supply presentations and one centralized bounded work-order indicator pass. Existing production sites, piles, buildings, priorities, stable IDs and snapshot authority remain unchanged. Focused P9.4 suites passed 3/3 EditMode and 3/3 PlayMode; affected P8.3/P9.2/P9.3 suites passed 12/12 and 9/9. The Windows x64 Mono Development build reported `errors=0; warnings=39`; visible 1280×720 normal-Player smoke passed gathering, hauling, building, priority, navigation, winter and Save/Load checks and produced inspected RTS/TPS images. Only approved official Terrain Sample content and project-owned derivatives are used; incompatible static Character overlays were rejected during visual review. P9.5 is next. P8.6 remains PARTIAL, U29 remains PARTIAL, U30 remains IN_PROGRESS and U31 remains BLOCKED.

P9.5 completion note (2026-10-15): local atmosphere now derives from one project-owned definition profile plus existing `GameClock`/`WeatherService`: continuous solar light, soft shadows, ambient/fog/sky/water treatment, centralized rain/snow and seasonal Terrain projection remain disposable. The additive global scene now presents an irregular island relief, surrounding water, river, routes, natural affordances and permanent Russian labels for the unchanged six `WorldLocationId` values. Focused P9.5 EditMode passed 2/2 and final PlayMode passed 2/2, including GlobalMap unload and local camera/atmosphere restoration; affected P9.4/U19/U25 PlayMode passed 8/8. The final Windows x64 Mono Development build reported `errors=0; warnings=39`, build GUID `42202671cbfc44ecab7745163c076124`; visible 1600×900 D3D11 Player smoke passed all atmosphere/map/water/navigation/Save-Load/RTS/TPS flags and its final local/global images were visually inspected. P9.6 is next. P8.6 remains PARTIAL, U29 remains PARTIAL, U30 remains IN_PROGRESS and U31 remains BLOCKED.

P9.6 completion note (2026-10-15): the normal visible Player route proves Main Menu/New Game, edited UMA ruler, local RTS/TPS, the six-location global map and return, finite-resource gather/haul/build, rain/snow/seasons/day-night, and Save/Load preservation of stable IDs, appearance, orders and resources at exact 1280×720 and 1600×900. Project-owned brown-hide Human 3.0 recipes replace modern clothing, a clean UMA prefab removes the excluded sample walker, the aggregate capsule is replaced by one bounded humanoid LOD mesh, and scheduled post-Load repaint removes the 1600×900 clipping. Combined P9.6/P8.4/P9.3/P9.4/P9.5 suites passed 15/15 EditMode and 12/12 PlayMode; the Windows x64 Mono Development build reported `errors=0; warnings=44`. Both final Player results record technical and subjective acceptance true with no Character caveat. P9 and P9.6 are DONE. P8.6 remains PARTIAL, U29 remains PARTIAL, U30 remains IN_PROGRESS and U31 remains BLOCKED.

Completion note (2026-10-13): P6 adds a dismissible seven-stage first-hour projection over existing canonical selection, work, shelter/storage, food production, research, event/danger and scouting evidence. Settlement and Character panels explain the starting group, food horizon, needs and current actions; blocked work/research/military/travel actions show a fix. P6 passed 3/3 focused EditMode, 2/2 focused headless PlayMode, 2/2 D3D11 PlayMode, 20/20 affected EditMode and 33/33 affected PlayMode; the Windows Development build and visible Player route passed with P4/P5 Save/Load identity intact. Manual usability acceptance remains open. P7 is next; U29/U30/U31 statuses are unchanged.

Completion note (2026-10-06): U14 deterministic Tier 3 catch-up, keyed RNG, tier-path equivalence and 1,000/10,000-character scale coverage are complete. This historical note predates the U15 completion below.

Follow-up note (2026-10-06): U11 RTS right-click Move is complete. Selected characters or the active WorkGroup now issue tier-aware, formation-spaced U11 Move jobs through the U05 pointer pipeline and U10 movement bridge. This note predates the U15 completion recorded below.

Completion note (2026-10-06): U15 reusable recipes/sites/tasks, finite gathering, primitive crop plots, canonical output ownership, food consumption, target-stock automation, tier-independent deterministic execution and version-2 production persistence are complete. Five simulated years remain food-positive with no shortage; full regression, PostgreSQL, GPU, asset and Windows build gates passed. U16 is next but has not started.

Completion note (2026-10-06): U16 engine-independent research definitions/service, validated 14-technology Stone Age graph, typed building/recipe/capability unlocks, centralized Tier1/Tier2/Tier3 work, deterministic checkpoints, version-3 research persistence and debug progression panel are complete. End-to-end capstone, multi-year U15+U16, full regression, PostgreSQL, GPU and Windows build gates passed. U17 is next but has not started.

Completion note (2026-10-07): U16A removes hardcoded demo research assignments, distinguishes assigned from authoritative active research, adds the catalog-backed research-gated building selector and adds canonical pause/speed controls without consuming U11 digit shortcuts. Focused/full/regression/GPU/build gates passed. U17 remains not started pending review.

Completion note (2026-10-07): U17 adds canonical stable-ID dynasty state, read-only family queries over U06 links, deterministic agnatic primogeniture, event-driven exactly-once succession, player-ruler transfer, extinction/Game Over, succession history, tier-independent/off-camera behavior, version-4 character/dynasty persistence with v1-v3 compatibility and a click-blocked LocalGameplay debug overlay. Focused, full, persistence, tier-route, collection-order, GPU, PostgreSQL and Windows build gates passed. U18 is next but has not started.

Completion note (2026-10-07): U18 adds one canonical deterministic health/family authority, nine injury/disease/temporary/permanent definitions, capability effects integrated with AI/production/research, U17 death hooks, four exactly-once relationship events, pregnancy/birth with deterministic child IDs and dynasty membership, Tier1/2/3 off-camera equivalence, version-5 persistence with v1-v4 compatibility and a click-blocked debug panel. Focused, full, regression, persistence, determinism, long-run, GPU and Windows build gates passed. U19 is next but has not started.

Completion note (2026-10-08): U19 adds canonical four-season derivation, keyed deterministic daily temperate weather, fixed-point temperature/precipitation/wind, data-driven crop/outdoor-work/movement/health/wild-food modifiers, weather-aware U15 production and farming, U18 cold/heat exposure with exactly-once markers, Tier1/Tier2/Tier3 hooks, version-6 persistence with v1-v5 compatibility and a click-blocked debug/presentation layer. Focused, full, regression, persistence, catch-up, actual tier-route, long-run, graphics and Windows build gates passed. U20 is next but has not started.

Completion note (2026-10-08): U20 adds a reusable stable-ID two-side combat domain, deterministic eligibility/targeting/cooldown melee, U18 injury/incapacitation/death with U17 succession, bounded morale and retreat, terminal outcomes, player group Attack and centralized hostile tactics, active-combat Tier1 pinning, a physical 4v4 demo, version-7 combat persistence with v1-v6 compatibility and a click-blocked diagnostic panel. Focused, full, regression, persistence, collection-order, save/load, 100-combatant, physical NavMesh, GPU and Windows build gates passed. U21 is next but has not started.

Completion note (2026-10-08): U21 adds canonical stable-ID military units with owner/side, historical Character-ID rosters, eligible captains and deterministic fallback/Leaderless behavior, explicit Detailed/Aggregate transitions over the existing TierManager, U11 Move reuse, U20 encounter integration and active-combat pinning, derived casualty/injury/morale/cohesion summaries, logical aggregate orders/location hooks, version-8 persistence with v1-v7 compatibility and a click-blocked LocalGameplay unit panel. Focused, full, regression, persistence, collection-order, save/load, tier-route, combat-integration, 10-unit/100-member, GPU and Windows build gates passed. U22 is next but has not started.

Completion note (2026-10-08): U22 adds engine-independent RTS/ThirdPerson control state over an existing Character stable ID, explicit NavMesh movement authority, Utility/work suspension with U11 Move requeue, U20 movement suppression without replacing combat, U21 captain/unit continuity, Tier1 pinning, reusable Cinemachine/input-map handoff, safe death/incapacity fallback and snapshot v9 with v1-v8 compatibility. U22 passed 7/7 EditMode, 12/12 headless and GPU PlayMode, 320/320 full EditMode, 65/65 full PlayMode, required regressions, persistence and Windows build. U23 is next and has not started.

Completion note (2026-10-08): U23 extends the canonical U20 encounter into a stable-ID cross-mode battle with shared combat rules, deterministic one-minute abstract steps, compact history and U21 unit links. Rollback-safe Detailed/Physical↔Aggregate/Abstract transitions preserve Character/unit/battle identity, health, morale, targets, attack sequence/cooldowns and retreat state; U22 TPS blocks abstraction, while U18/U17 deaths and U21 captain fallback remain authoritative off-camera. Snapshot v10 reads v1-v9. U23 passed 12/12 EditMode, 3/3 headless and 3/3 GPU PlayMode, 332/332 full EditMode, 68/68 full PlayMode, PostgreSQL 18 integration 25/25, required regressions, persistence, convergence, 500-participant stress and Windows build. U24 is next but has not started.

Completion note (2026-10-08): U24 extends the canonical Building with integrity, structural/fire/checkpoint/owner state and compact history. One centralized service applies generic, combat and fixed-step weather-modified fire damage, leaves destroyed buildings as occupying ruins, atomically spills storage to WorldPiles, stops production/crops and validates in-place capture from a finished hostile victory. Snapshot v11 reads v1-v10. U24 passed 14/14 EditMode, 3/3 headless and GPU PlayMode, 346/346 full EditMode, 71/71 full PlayMode, required regressions, persistence, 100-fire/1,000-building determinism stress and Windows build. U25 is next but has not started.

Completion note (2026-10-09): U25 adds a validated stable-ID world graph, settlements and absolute-tick travel, rollback-safe Local↔Global representation changes over U13/U21/U22/U23, deterministic hostile-arrival handoff to the canonical abstract battle, a separate additive schematic map scene, continued off-camera simulation and snapshot v12 with v1-v11 compatibility. U25 passed 12/12 EditMode, 3/3 headless and GPU PlayMode, 358/358 full EditMode, 74/74 full PlayMode, required regressions, persistence, 100-unit/50-location stress and Windows build. U26 is next but has not started.

Completion note (2026-10-09): U26 adds a deterministic normal-runtime rival with canonical Characters/dynasty, owner-scoped U15/U16 economy/research, funded U09 construction, U21 army/captain, U25 travel, U23 abstract attack and atomic U24/U25 settlement capture. Multi-state scheduling, dynasty-aware health death routing, no-settlement defeat, loss/replacement and snapshot v13 are validated without parallel AI-only foundations. U26 passed 10/10 EditMode, 2/2 headless PlayMode, 160/160 dependency EditMode and 31/31 dependency PlayMode regressions, persistence 35/35, full 368/368 non-PostgreSQL EditMode, full 76/76 PlayMode and Windows build. U27 is next but has not started.

Completion note (2026-10-09): U27 adds one player-facing UI Toolkit projection with HUD, inspector, settlement/research/work/build/dynasty/military/combat/TPS/global panels, notification feed and three-slot local Save/Load. Legacy debug UI is default-off behind F10; UI blocking, throttled refresh, isolated subscriptions and post-load clock/global/Tier3 rebinding preserve existing authorities. U27 passed 3/3 EditMode, 6/6 headless and GPU PlayMode, 170/170 dependency EditMode and 33/33 dependency PlayMode regressions, persistence 35/35, full 371/371 non-PostgreSQL EditMode, full 82/82 PlayMode and Windows build. U28 is next but has not started.

Completion note (2026-10-09): U28 adds one centralized engine-independent event scheduler with validated immutable definitions, stable typed targets/IDs, keyed weighted selection, exact-tick cooldowns, player and AI choices, persisted delayed chains, exactly-once canonical U18 outcomes, bounded transition/history state, U27 event projections and snapshot v14 with v1-v13 compatibility. U28 passed 20/20 EditMode, 4/4 headless and GPU PlayMode, 72/72 dependency EditMode and 12/12 dependency PlayMode regressions, persistence 35/35, full 391/391 non-PostgreSQL EditMode, full 86/86 PlayMode and Windows build. PostgreSQL discovered 25 tests but skipped them because its test database was absent. U29 is next but has not started.

Completion note (2026-10-12): U30A closes the confirmed pre-MVP FRS gaps without replacing existing authorities. Deterministic New Game owns exactly ten player Characters and a separate six-Character AI state; normal UI exposes New Game and Save/Load; rival detail remains hidden until canonical contact; AI construction stays on U09/U15/U26; Stone Age berries supply is conserved by canonical inventories and modifies U25 travel/U20 combat; TPS attack delegates to U20; a completed player capture records victory exactly once and sandbox time continues. Snapshot v15 persists campaign and supply with safe v1-v14 defaults. Verification passed 8/8 focused EditMode, 5/5 focused headless PlayMode, 133/133 critical EditMode, 44/44 critical PlayMode, 35/35 persistence, 5/5 GPU PlayMode, an isolated Windows x64 Mono Development build, and a fresh 30-second soak with 39 actual seconds, zero incidents, verified resume/memory and final hash `C5FB112880CD4E99E34147356F5FE3107F03697AA986D41A1DD763DC6248E199`. PostgreSQL was skipped because `DYNASTYGAME_PG_TEST_DATABASE` was not configured. U29 remains PARTIAL, U30 remains IN_PROGRESS pending an audited full eight-hour result, and U31's final gate remains blocked.

Progress note (2026-10-11): U29 now has a local offline runner, raw evidence and detailed diagnostics. The latest complete matrix passed all verification/build gates but still measured 36–38 ms p95 at 500 and 54–56 ms at 1000, isolating automatic per-character animation as the remaining CPU presentation bottleneck. Tier1 now keeps canonical AI and movement independent from a hard 16-skeleton-per-frame shared animation budget and cheaper autonomous NavMesh avoidance. A 1000-Character control with 60 seconds warm-up and 120 seconds measurement then passed at p95 16.671 ms, p99 16.693 ms, max 23.133 ms and zero frames above 33.33 ms; exact tiers and save/load hash remained intact. Its pre-warm-up screenshot exposed several initial bind poses, so `Bind` now forces one Idle evaluation before manual scheduling; a corrected-build visual smoke was clean and retained p95 16.670 ms. Targeted/dependency/persistence tests and the Windows build pass. U29 remains IN_PROGRESS pending a fresh complete 100/300/500/1000 ×3 offline matrix.

Progress note (2026-10-11, second pass): `offline-20261011-175150` completed all 12 repeats and all 414/101 full regressions but still failed sustained performance at 500 repeat 2/3 and all 1000 repeats (worst p95 48.784 ms). Granular evidence showed scalable disposable Tier1 work plus long-run CPU/GPU slowdown, not state growth. Movement start/stop parameters are now event-driven, unchanged global modifiers reconcile at distributed 2 Hz, idle scheduler entries avoid native position reads, no-project research traversal exits immediately and the skeleton budget is 8/frame. A rebuilt 1000-Character 60+120-second control passed at p95 16.671 ms, p99 16.698 ms and max 21.193 ms with zero over-budget frames; movement presentation fell to 0.010 ms average and animation to 0.617 ms while exact tiers/save hash and clean poses remained intact. Focused/dependency/persistence tests and the build pass. U29 remains IN_PROGRESS pending another complete matrix.

Progress note (2026-10-11, locomotion correction): visual observation showed that the sparse manual skeleton budget made moving residents slide. Moving presenters now remain in the continuous Animator loop and are disabled only after completing the locomotion-to-idle transition; idle presenters stay frozen, so animation cost scales with actual movers. The new regression passed in U29 PlayMode 16/16, all required regressions/persistence/build passed, and a rebuilt 1000-Character 60+120-second control retained p95 16.671 ms, p99 16.711 ms, max 21.930 ms and zero over-budget frames. U29 remains IN_PROGRESS pending the complete matrix.

WM15 current-feedback correction note (2026-10-27): adaptive all-bank pier placement, explicit single-species
pasture founding, accumulation-style mountain snow, requested light multipliers and smooth schools plus solitary
silver fish are implemented. Focused evidence is effectively 11/11; Windows build-e succeeded with 0 errors;
Player-b passed 55 relevant/runtime checks with two unrelated historical hunt timing failures. WM15 stays
IN_PROGRESS for user visual acceptance, initial loading and deferred full 100/300/500 gates.

WM15 living-building runtime feedback note (2026-10-29): all fifteen newly reported behavior/UI defects are
implemented and locally verified. The affected EditMode set is green after the deterministic P83 rerun; two
full-world PlayMode scenarios passed 2/2; Windows Development build `Builds/WM15UserFeedback/Serenity.exe`
succeeded with 0 errors / 82 warnings. This feedback slice is DONE and awaits user retest. WM15 as a whole
remains IN_PROGRESS only for separately owned visual/runtime acceptance and deferred population gates.

WM15 direct user-retest repair note (2026-10-29): shore placement/action reachability, unfinished pasture
presentation, livestock shelter exclusion, six-place stone housing, clickable occupants, Russian military
ranks/training/barracks state and authored armour presentation are implemented. Focused EditMode passed 34/34;
the pre-horizon-adjustment Player route passed 25/25 with zero runtime errors. Final Development build
`Builds/WM15UserRetestFinal/Serenity.exe` succeeded with 0 errors / 85 warnings. At the user's instruction the
final decorative-sea edge adjustment was built without another automated launch; this slice awaits the user's
own visual retest and WM15 remains IN_PROGRESS. A persistent Unity test/build runner is the next separate task.
