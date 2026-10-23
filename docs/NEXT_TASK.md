# NEXT_TASK.md

## Current scope — WM13 world version and saves, after WM12 completion on 2026-10-23

WM12 is DONE. The proposed V2 buildability package now produces deterministic green/amber/red placement over
the existing building surface, occupancy and clearance chain. Clearable natural sources use ordinary worker
production, physical output/hauling and construction work; water, slope, restricted masks, protected POIs and
impassable geometry fail closed. The active `p109-compact-world-v1` reference remains unchanged and
`p109-world-map-v2-proposed` is still not activated.

The next unblocked world-map task is **WM13 — world version and saves**. Follow its scope and gates from
`docs/world_map/WORLD_MAP_CODEX_TASKS.md`: isolate the proposed generation/save path, persist only new
canonical V2 states through the existing SaveCoordinator/SaveXmlCodec boundary, reject wrong-generation
loads before rebind, and prove old compact-world saves remain untouched. Do not activate the V2 generation
outside that explicit versioned path and do not start WM14/WM15. This WM12 chat stops after its commit and
does not implement WM13.

## Design-only side deliverable — world map, 2026-10-23

'docs/world_map/' now contains the validated 'serenity-world-map-v1' design package. It proposes
'p109-world-map-v2-proposed' but does not activate it, alter the current compact world or authorize WM01.
The active task is unchanged: complete P10.9 F1 below. Do not start map implementation from the generated
WORLD_MAP_CODEX_TASKS.md without a later explicit task assignment.

## Current scope — P10.9 F1 after modern HUD/site-clearance implementation, 2026-10-23

The screenshot 1–4 request is implemented and built at 'Builds/P109ModernHud/Serenity.exe'. The next unblocked
work remains the rest of F1: user visual acceptance followed by the multi-settlement/tier isolation and final
broader regression matrix. Do not start P10.10, the deferred performance benchmark, a dependency download or
an old-save migration. See 'P109_MODERN_HUD_CLEARANCE.md' for the exact Player route and evidence.

The five inherited dirty materials and 'New Unity Project/' remain user-owned and excluded from the task
commit. The generated Serenity icon atlas is project content; no Asset Store dependency was added.

## Current scope — P10.9 Stone Age buildings F1, 2026-10-23

The user-requested `Assets/23102026` slice is complete before F1. Notifications dismiss their exact feed entry
after click. All eleven local archives were audited; only 217 reviewed nature/audio/lightning assets were
imported and connected through existing presentation adapters. No vendor C#, demo scene, Package Manager or
competing terrain/gameplay system was admitted. Focused EditMode and PlayMode both pass 6/6; the Windows build
at `Builds/P109Assets23102026/Serenity.exe` succeeds with 0 errors / 27 warnings, GUID
`c4cf71172408462d9635033be0b9da68`. See `P109_ASSETS_23102026_AUDIT.md`. F1 remains the next unblocked scope.

Completed user-feedback repair before F1: worker-driven demolition, no age wear/brown damage tint, marked and
snapped fishing landing sites, held-LMB palisade/path segments, one Garden menu choice, human-scale outdoor
facility visuals and walking deer. Focused EditMode 22/22, affected EditMode 41/41 and full-scene PlayMode 1/1
passed; actual Player bootstrap validates all eleven lake landings. Do not mark F1 complete. The Meeting Area
social/mood behavior remains an explicit gap rather than a functioning passive effect. Final Player rebuild
and exact commands are recorded in P109_BUILDINGS_IMPLEMENTATION.md; user visual acceptance remains open.
Current Player: `C:\serenity_game\Builds\P109ShoreLifecycle\Serenity.exe` (0 errors / 27 warnings,
GUID `cc026ce5123e4869aec37f24e2d1e20f`). Final native bootstrap/landing rerun passed; old builds remain intact.
Asset links supplied by the user are an assessment request, not authorization to install new dependencies.

The post-E visual/gameplay feedback repair is implemented before F1. Clan House construction buffers now
accept every resource category present in their authored contract, so cured hides no longer leave progress
at zero. Resource presenters preserve existing narrow colliders instead of adding broad invisible rock hit
volumes. Campfire particles receive an explicit URP material. Completed facilities hide construction-only
stages and use the already imported CityBuilderThree, Low Poly Farm, RTS Toon and Synty models; this now
includes the original Primitive Shelter and Storage Basket. Primitive Garden placement
is now a held-LMB rectangular drag from 2×2 through 8×8 cells, with the authored cells stored in canonical
placement state and repeated crop visuals centred on the selected rectangle. Focused EditMode passed 31/31;
the added visual/model regressions passed 7/7. The broader P109 run passed 65/68, retaining three unrelated
known failures (one fishing fixture and two expanded-import audit assumptions). P109 PlayMode passed 6/9;
its three failures are existing environment/fixture assertions outside this repair. The final starter-prefab
regressions passed 2/2. The updated Windows Development Player now succeeds at
`Builds/P109VisualFeedback/Serenity.exe` with 0 errors / 53 warnings and GUID
`64ec73cb93504dbe8490f9fd129428fc`. Its post-build P9.4 bootstrap failure is repaired: the legacy
renderer-count/mesh-prefix audit now accepts authored final-stage vendor models, the layout hash is stable and
refreshed by P10.9 authoring, focused EditMode passes 10/10 and a full-scene PlayMode bootstrap explicitly
confirms `GameplayUi` plus both starter residents 1/1. F1 remains the next unblocked scope for the broader
multi-settlement/tier regression and final visible native gameplay route.

The user approved the audited architecture and staged B-F implementation in
`P109_STONE_AGE_BUILDINGS_PLAN.md`. B1-B3, C1-C4, D1-D2 and E1-E4 are complete. C5 remains explicitly
deferred by the user because it is an acceptance/test stage; its end-to-end concerns are folded into
the next unblocked slice, **F1: multi-settlement/tier regression and final Player acceptance**.

E1-E4 add the clan/social/medical facilities, fishing/gathering/garden/well economy, palisade/gate/
watchtower/path set, and canonical wear/repair/upgrade/demolition lifecycle. Assignments enforce capacity
and prevent duplicate home/work ownership. Fishing and water use real production/resource outputs and
placement rules. Gate state changes shared occupancy, while destruction releases it. Repair/upgrade costs
and work are exactly-once, upgrades preserve stable identity, and demolition spills stored cargo plus the
configured construction return. Final P10.9 EditMode passed 24/24; directly affected EditMode passed
66/66. Windows build `Builds/P109BuildingsE/Serenity.exe` succeeded with 0 errors / 53 warnings, GUID
`709397d549104081bccd3d70d355d5e7`. The affected PlayMode runner stalled during known UMA scene
initialization and produced no NUnit XML, so no PlayMode pass is claimed for E.

F1 must exercise several settlements and simulation tiers, prove that workers/resources do not teleport
between settlements, cover save/load and camera/materialization invariance, and perform the final native
Player route. No P10.10 work, deferred performance benchmark, push, dependency download or old-save
migration is authorized.

The implementation must extend the existing `BuildingDefinition`, `Building`, placement,
resource, production, work and save authorities. It must not introduce parallel managers,
inventories or NPC systems. The five inherited dirty materials and `New Unity Project/`
remain user-owned and excluded from commits. Automated tests use an Artifacts-only save
directory; the protected 44-file baseline must still match. Manual Player acceptance belongs to the
user and receives one exact build path plus a short route at F1. Use targeted tests for the rule
and directly affected connections; do not repeat successful suites without relevant code
changes. Run the sufficiently broad regression and separate Windows build at F1,
or earlier only for a concrete data-loss/compatibility risk.

### Completed prerequisites

The screenshot-feedback fixes and architecture audit are committed at `3abb9217`.
Their earlier test/build evidence applies only to that commit and is not evidence for B1.
B1 adds `ConstructionSpec`, `PlacementSpec`, `HousingSpec`, `WorkplaceSpec`, `StorageSpec`,
`ProductionSpec`, `MaintenanceSpec`, `UpgradeSpec` and `BuildingDefinitionCatalog` while
preserving legacy constructor callers. Focused/affected EditMode passed 26/26; Windows build
`Builds/P109BuildingsB1/Serenity.exe` succeeded with 0 errors / 53 warnings, GUID
`38c83a9598994209ad352522fdaeee64`; protected files match 44/44. See
`P109_BUILDINGS_IMPLEMENTATION.md`. The B1 commit is the immediate parent of B2 work.

B2 adds settlement/stage/material progress to the existing `Building`, construction buffers as
owners in the shared inventory registry, deterministic stage consumption, snapshot v26 and the
isolated `p109-stone-age-buildings-v1` generation. Expanded save/building regressions passed
149/149 and the final focused rerun passed 30/30. `Builds/P109BuildingsB2/Serenity.exe`
succeeded with 0 errors / 27 warnings, GUID `525f7a3748aa4356914f184b450b84f1`;
protected files match 44/44. B3 must extend the existing
placement authority rather than adding a second grid or territory system.

B3 adds deterministic structure/zone/segment cell resolution, settlement territory and approach
perimeters, snapshot v27 placement-cell persistence and exact occupancy restore. Focused EditMode
passed 25/25, affected EditMode 154/154 and placement PlayMode 4/4. The separate B3 build succeeded
at `Builds/P109BuildingsB3/Serenity.exe` with 0 errors / 53 warnings, GUID
`563c001610724baf8e0a7b343e1a9dac`; protected files match 44/44.

C1 connects the normal `WorkManager`/Tier1 haul path to `ConstructionBuffer` destinations. Placement
does not deduct stock; high-priority delivery orders move batches of at most eight units from world
piles or operational storage, and source reservations prevent a second construction plan from
spending queued stock. Cancellation terminates construction work, spills buffered and installed
materials to physical piles and removes the plan/storage/occupancy. Snapshot v27 already carries
the buffer and work orders, so no format change was needed. Focused EditMode passed 7/7 and the
directly affected construction/resource/work/Tier1 set passed 34/34. No broad regression or Player
build was run by design; those gates remain after C4.

C2 adds five playable definitions and readable staged visuals: Primitive Shelter, Stone Age Hut,
Campfire, Storage Basket and Stone Tool Workshop. Housing/storage and technology prerequisites are
authored through the existing definition/catalog path. Confirmed construction preempts only
unpicked autonomous source claims needed for its high-priority deliveries, preventing a cleanup
reservation from deadlocking the site without teleporting carried stock. Focused EditMode passed
36/36 and the physical worker PlayMode route passed 1/1. No broad regression or Player build was
run by design; those gates remain after C4.

C3 extends the existing U15 production authority with building workplaces. Campfires own one shared
worker slot, recipe queues and bounded input/output inventories; raw food and wood fuel arrive through
ordinary high-priority haul orders, completed food leaves through the existing autonomous logistics
path, and full output or missing input pauses without consuming or duplicating a batch. Snapshot v28
persists queues, pause state, owner building, active progress and both buffers. The selected-building
panel exposes recipes, quantities, queue controls, pause, worker/progress and waiting reason. Focused
EditMode passed 25/25 and campfire placement PlayMode passed 1/1. No broad regression or Player build
was run by design; both gates now belong to C4.

C4 makes cooked berries, fish and meat the only edible resources. Tier1 `Eat` and aggregate Tier2/3
consumption delegate to the same service and spend one canonical unit per meal; materialized Tier1
residents are excluded from the aggregate population to prevent a second charge. Starter cooked
rations prevent a bootstrap hunger deadlock. Focused tests passed 32/32 EditMode and 6/6 PlayMode;
the affected PlayMode matrix is green after exact reruns. Broad EditMode passed 631/634 with only the
known P8.2 geography failure and two user-owned expanded-Synty failures. Windows build
`Builds/P109BuildingsC4/Serenity.exe` succeeded with 0 errors / 53 warnings, GUID
`94165ea0731843f4b0be90bfee0cfc87`; protected files match 44/44. C5 has not started.

## Superseded screenshot-feedback handoff

The latest user correction selects ONLY `C:/Users/Lenovo/Desktop/Задачи и промт.txt`
and the six distinct screenshots; ignore duplicate pasted prompts. Fix the screenshot
defects and make normal RMB replace previous work. The document's 25-building system
explicitly requires audit/architecture/task-plan approval before large implementation.
That audit is in `P109_STONE_AGE_BUILDINGS_PLAN.md`; do not implement all 25 or kitchen
production without the requested agreement. No new asset import is needed for this slice.

The user now performs manual Player checks themselves. Do not resume computer-use QA
or ask them to dismiss the old firewall prompt. Provide the separate executable and
the checklist in `P109_SCREENSHOT_FEEDBACK_HANDOFF.md` instead. Automated tests/builds
remain mandatory. Old saves and the five pre-existing dirty material files are protected.
P10.9 remains the only active task; no P10.10, benchmark, new chats or push.

Implemented: legacy primitive suppression, full-footprint obstacle/terrain placement,
Humanoid work-root normalization, hand-attached cargo, lightweight ground height,
foot picking, separated 3D fish, and replacing RMB with safe cargo drop/queue cancellation.
Current evidence: final EditMode 76/76; expanded PlayMode 15/16, then corrected placement
fixture and construction rerun 1/1. Build `Builds/P109ScreenshotFeedback/Serenity.exe`
succeeded (0 errors / 53 warnings; GUID ac1af2b6526f4451ae7dd2d1b1822398).
Next unblocked handoff: user's visual acceptance and agreement on building stages B/C.
Historical records below are superseded where they conflict with this section.

Active user override (2026-10-21, compact world): import and integrate the locally supplied
asset packages after conflict/code review; first implement multiple fish schools in EVERY lake,
shore-side fish output piles using existing priority/capacity-aware hauling, and change the
new-game world to exactly 1000 x 1000 metres while retaining the approved geographic layout.
Preserve old saves without migration. Audit source archives and shared Synty GUIDs before import.
Prepare a follow-up integration plan distinguishing imported art from working game mechanics.
This supersedes the analysis-only/new-import restriction and 12 km footprint below.
P10.9 remains the sole active task; no P10.10 or deferred performance benchmark.
Implementation/import and separate Windows build are complete. The user dismissed the
Windows firewall request and subsequently chose to perform all manual acceptance themselves.
The newer screenshot-feedback build/checklist above supersedes this older acceptance route.
Final checks: EditMode 50/50; expanded PlayMode 13/14, then final bank correction and
focused all-banks/physical-fishing PlayMode 2/2. Do not repeat imports or overwrite old saves.
Read `P109_COMPACT_WORLD_HANDOFF.md` for the remaining native checks and staged art roadmap.

Active user override (2026-10-21): fix the two newly supplied gameplay videos now; the eight
proposed asset packs are analysis-only, with no download/import. Worksite V2 is implemented:
visible idle/work cycles, hand-held tool, ground harvest piles and capacity-reserved hauling,
worker-built construction, RMB menu dismissal, clickable outcrops and shore fishing.
Do not treat the older Synty executable or its test results below as acceptance of this slice.
New games use `p109-worksite-v2` / `Serenity/P109/WorksiteV2/Saves`; old saves are not migrated.
Tests and native QA must use `Artifacts/P109/feedback-20261021` save directories.
`Builds/P109Worksite/Serenity.exe` succeeded (GUID 017e3d80ef804d86af8678a2abc68c6b).
Final checks: EditMode 54/54 and consolidated PlayMode 13/13 passed.
Native move/idle, two harvest outputs, gold mining, four-fish catch, zoom and LMB/MMB dismissal
were exercised in this binary. Next unblocked P10.9 acceptance: close-up work-tool/fall review
and native worker construction after unlocking the required research in an isolated test game.
Do not repeat imports or claim the whole 12 x 12 km world accepted. Read the Worksite V2
section of the handoff for the exact automated and native evidence and visual limitations.
P10.9 stays PARTIAL; no performance benchmark or P10.10 work is authorized.

Active immediate scope (updated 2026-10-21, supersedes historical status below): finish
final-binary native route acceptance for the integrated Synty starter area. Build
`Builds/P109SyntyPlayable/Serenity.exe` is complete (GUID 7d986af66c3b4dc9aca0af6341ebd670,
0 errors / 54 warnings); final tests passed 10/10 EditMode and 8/8 PlayMode. Native selection,
move, chop/fall and zoom were exercised on the earlier candidate; final water/shore inspected
in the final Player. User confirmed Home/Space and smooth held-MMB movement (recorded
2026-10-21); do not ask for the same confirmation again. Repeat the ordinary native selection,
move, chop/fall and zoom route on the final binary when closing full acceptance, using
Artifacts-only saves. Do not redo the completed import.
See `docs/P109_SYNTY_PLAYABLE_HANDOFF.md` for exact evidence and limitations.
Do not modify old saves, resume performance profiling or start P10.10.

Immediate user-approved P10.9 slice (2026-10-20): import the downloaded Synty Meadow Forest
directly into this project, retaining rollback checkpoint `c732c96` and the five unstaged
material backups in `Artifacts/P109/synty-import/pre-import-dirty-files.zip`.
The scoped Meadow/Core URP import is complete; EditMode passed 9/9 and playable-patch
PlayMode passed 5/5. Diagnostic Windows build passed (0 errors / 34 warnings), GUID
`e364cf885e63461e9bbe498e380e0b74`. Final protected-file hashes match 44/44, including
8/8 old saves; all four original scenes are unchanged. This import slice is complete.
Do not replace gameplay startup with the vendor demo or claim resource
integration from an asset import. Old saves remain untouched. The previous repeat native
Player review was stopped with Escape and is still pending. P10.9 remains PARTIAL.

Next unblocked P10.9 work: use the admitted art on one small playable area through existing
stable-ID resource presenters, then resume actual Player camera/selection/order/harvest/water
verification. Imported decorative trees are not yet production sources. Inspect mesh LODs,
material texture mappings and wind before binding them; do not assume an asset import fixes
the earlier camera/scenery defects. Do not expand to whole-world art replacement automatically.

Active task: **P10.9 playable starter patch (IN PROGRESS; resumed by user 2026-10-20)**.

Current direction supersedes the wait below: finish one usable starter area, build a new Windows Player,
and personally exercise camera/selection/orders/trees/water with real UI input. In particular investigate
trees crossing the camera, not only the automated LOD tests. User also requested a comparison of five
environment/terrain assets and understandable chopping/mining progress with tree-fall feedback.
Synty Meadow Forest is now imported (D-155), but not bound to gameplay. New-world saves are isolated under
`Serenity/P109/PlayablePatch/Saves`; old P10, TiledV2 and legacy files must remain unchanged.
Current changes are uncommitted and not yet accepted visually. Do not call this patch DONE from tests alone.

Latest completed run: `playmode-accepted.xml` 7/7 and `editmode.xml` 49/49. The first Windows
BuildReport for `Builds/P109PlayablePatch/Serenity.exe` reported Succeeded, 0 errors / 50 warnings,
but that binary was rejected during actual manual UI review. Do not hand it off as accepted.
Its post-build finalization was stopped after the review found a correction requiring a rebuild;
no provenance is claimed for the draft. The corrected test/build chain is now running with
`playmode-input.xml`, `editmode-input.xml` and `build-final.log`.
The new Player was launched with `SERENITY_SAVE_DIRECTORY` set to
`Artifacts/P109/playable-patch/manual-saves` and D3D11 1920 x 1080 arguments.
The user manually dismissed the Windows Firewall prompt. Main Menu -> New Game -> LocalGameplay
was exercised through native input. Broad foliage selection boxes stole clicks on visible residents;
wheel zoom was almost imperceptible. The draft was exited through its own menu without saving.
Corrections narrow the selection column, prioritize character hits, remove legacy 0.01 wheel scaling
(the installed Input System already normalizes notches), and prevent gameplay buttons from consuming
Space as a second submit/pause action. Repeat the normal selection / move / chop / zoom / water route
on the corrected binary; these pending checks must not be presented as already passed.
Corrected `playmode-input.xml` now passes **5/5**, including the source-identity guard and
screen-ray regression; `editmode-input.xml` passes **49/49**. The U05 filter in that latter command
had an extra `.Editor` namespace and matched no U05 tests. This debt is now resolved: the
correctly named `Game.Tests.U05CameraAndInputTests` passed all six cases in the import's
9/9 EditMode run. The prior playable build also finalized; its provenance is in PROJECT_STATE.

The user-directed video follow-up is implemented: old authored trees are anchored to tiled ground and hidden with their terrain tile; tactical character visibility covers maximum RTS zoom; repeated LOD refresh does not restart NavMesh paths; context-menu group Move uses separated formation endpoints; `map_serenity` reveals the canonical full-world fog grid. Focused tests and a Windows BuildReport passed. The user's local visual retest in the new Player is still needed; do not mark the full P10.9 acceptance gate DONE from automated evidence alone. If a resident is reported delayed, inspect critical needs/protected work phase rather than silently bypassing that canonical rule.

Stage 1 is complete and its contracts are the foundation: normal new games use 144 separate 1000 × 1000 m Terrains; deterministic world-coordinate heights make seams exact; river/lake water and buildability share the same authority; the visual ring stays bounded while physical actors and active command corridors retain collision/NavMesh; the strategic view/fog/global graph use the full 12 km bounds; and `p109-tiled-world-v2` saves are isolated from preserved P10 saves. See `docs/P109_STAGE1_HANDOFF.md` and D-151.

Stage 2 is complete. Every tile has deterministic canonical resources; approved forest/berry/hunting/ore/fishing regions add stable concentrated nodes and industrial zones; local Terrain painting/water/field/settlement presentation and strategic geography use the tiled authority; the broad legacy route strips are disabled; and streamed GameObjects are disposable views over canonical state. Exact evidence is in `docs/P109_STAGE2_HANDOFF.md` and D-152.

Do not start P10.10. Await explicit user direction on the remaining P10.9 acceptance work: the inherited P10.3/P10.4 manual product-route debt and whether/when to resume the deferred performance gate. Do not repeat the completed tiled-world Stage 1 or Stage 2 work.

Performance profiling/optimization remains deferred by the user's instruction. Do not run the historical 60+300 s benchmark or claim its p95 gate. Do not start P10.10, and do not change P10.3/P10.4 or P8.6/U29/U30/U31 statuses. Existing P10 saves must remain preserved, read-only and unmigrated.
