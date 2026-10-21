# P10.9 Synty playable starter slice — 2026-10-20

Status: PARTIAL. Implementation, tests and Windows build complete. Native tree/order/zoom
route passed on the first Synty candidate; close water review passed on the final candidate.
Keyboard Home/Space and held-middle-button camera were confirmed working by the user;
this confirmation was recorded on 2026-10-21. Full native chop-route repetition on the final
shoreline binary remains separate from that limited input confirmation.

The approved imported Meadow Forest supplies four mesh tree variants, five terrain layers and
the lake ripple texture. Project-owned prefabs/materials under `Assets/Game/Art/StarterNature`
retain vendor texture/vertex-colour shading but disable wind, jitter and LOD dithering.
Only LOD0 mesh renderers are copied; there are no billboard, animation, collider or vendor
script components in these prefabs. Runtime replacement retains each authored resource root,
canonical StableEntityId and finite stock. Legacy visuals/colliders are disabled. Replacement
height is 9–13 world metres regardless of historical root scale, with a narrow physical trunk
and the existing separate selection trigger. Streamed copies retain that normalized scale and
do not expand retired tree formations. Resource rules and save generation are unchanged.

Water still uses the canonical clipped shoreline mesh, with depth-colour, Synty ripple texture,
moving highlights and a restrained shore edge. No rectangular vendor water plane is placed
over land. Tree fall is mesh-only, 2.8 seconds plus a 4-second fallen hold; selection refresh
must not reset the depletion transition. Production remains whole-source harvesting, with
work progress followed by exact finite log output, not a second independent visual inventory.

## Commands and evidence

Unity executable: `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`.
All runs set `SERENITY_SAVE_DIRECTORY=C:\serenity_game\Artifacts\P109\synty-playable\test-saves`.
Prefix for each command: `-batchmode -force-d3d11 -projectPath C:\serenity_game`.

- Authoring: `-executeMethod P109StarterNatureAuthoring.Generate -quit -logFile C:\serenity_game\Artifacts\P109\synty-playable\authoring-shore.log`: succeeded.
- EditMode: `-runTests -testPlatform EditMode -testFilter Game.Tests.Editor.P109SyntyImportTests;Game.Tests.U05CameraAndInputTests -testResults C:\serenity_game\Artifacts\P109\synty-playable\editmode-shore.xml -logFile C:\serenity_game\Artifacts\P109\synty-playable\editmode-shore.log`: 10/10 passed.
- PlayMode: `-runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.P109PlayablePatchTests;Game.Tests.PlayMode.P109TiledWorldStage1PlayModeTests -testResults C:\serenity_game\Artifacts\P109\synty-playable\playmode-shore.xml -logFile C:\serenity_game\Artifacts\P109\synty-playable\playmode-shore.log`: 8/8 passed.
- Build: `-executeMethod P109StarterNatureAuthoring.BuildPlayable -quit -buildTarget Win64 -logFile C:\serenity_game\Artifacts\P109\synty-playable\build-shore.log`: Succeeded, 0 errors / 54 warnings, GUID `7d986af66c3b4dc9aca0af6341ebd670`. Normal enabled gameplay scenes only, no vendor demo. This scoped BuildReport run does not produce U00's full source-fingerprint provenance.

Initial authoring rejected two incorrect terrain-layer names before saving the populated
catalog; initial PlayMode was 5/6 because the new catalog guard correctly failed. Names were
corrected and the catalog regenerated before the final checks. These rejected runs are not
acceptance evidence.

Final verification matched 44/44 protected-file hashes against the import baseline, including
8/8 old saves. No vendor, package or project-setting changes were made in this integration.
Manual Player must use `Artifacts/P109/synty-playable/manual-saves`, not the old save folders.
Do not claim full 12 km world redesign, first-person acceptance or the deferred performance gate.

## First native route (normal Player, no smoke mode)

Build GUID `07df32b936184418ab9e982284fc3cc3`: Succeeded, 0 errors / 55 warnings.
Final pre-build PlayMode `playmode-release.xml`: 6/6 passed, including streamed tree scale.
Launched with `-screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -force-d3d11
-logFile C:\serenity_game\Artifacts\P109\synty-playable\manual-player.log` and manual-save override.
Native Computer Use: Main Menu -> New Game (generated seed 384360963) -> LocalGameplay.
Observed all ten starting residents, Synty grove and camp in rain. LMB selected Aren, not a
tree; RMB empty clearing produced one Move order; he reached the target and became idle.
Wheel -480 zoomed closer, +2400 twice reached the far tactical view. Tree crowns remained
anchored and camp residents remained visible (small at maximum zoom), not culled away.
No MMB drag was performed: the available native tool does not expose a held middle-button drag.
Keyboard Space/Home/W calls did not produce a visible response; this is unresolved, not a pass.

Paused with the actual HUD button. LMB birch trunk selected a finite source 23/23. Selected
Aren, RMB that birch issued one Chop. Resumed with HUD. Observed walk to trunk, work arm pose,
29% then 48% progress. At completion HUD logs went 30 -> 53, Aren's inventory gained 23 logs,
and successive native captures showed the birch tilt and lie across the ground before removal.
Exited via Main Menu -> without saving -> Exit; no old file was opened. Shutdown repeats the
pre-existing two Persistent-allocation leak warning. ICU fallback and stripped inactive
post-processing warnings remain; no gameplay exception was observed in this route.

Water was only seen from the far overview and partly behind HUD, so close visual acceptance
was not claimed. Added opt-in `-p109NatureReview` development camera bookmarks (Camp/Lake/
Lake close), which change only camera focus/zoom after a normal new game. They do not automate
orders, change weather/resources, load saves or serve as proof of keyboard/MMB controls.
Normal launches have no review overlay. Subsequent review builds are recorded below.

Close water review on GUID `87164a2199bf4ff39a456dc2148afefc` (0 errors / 54 warnings;
`playmode-review.xml` 6/6) rejected the surface: angular shore, excessive pale glare and bare
surroundings. That candidate is not final visual acceptance. A normal new game with seed
1113762471 was entered by native input, then the explicit Lake camera bookmark was clicked.
No synthetic production orders or save writes were used; exited normally without saving.

Correction: uniformly 257 height samples per kilometre tile (same deterministic height authority,
extents and seam contract), 128 terrain paint/basemap, fifth Meadow mud/gravel shore layer,
reduced reflection/glare and larger irregular ripple scales. Twelve additional starter-pond
trees join the same canonical clickable/harvestable registration; 72 small Synty ground props
are decorative bank details, without resource colliders or hidden stock. South-west access
from camp remains open. They are anchored once and share tile visibility, not camera billboards.
Regeneration, EditMode, PlayMode (including affected Stage1 streaming checks), and build
completed successfully; exact commands/results are above.

## Final native water review and remaining gate

Current executable: `Builds/P109SyntyPlayable/Serenity.exe`, build GUID
`7d986af66c3b4dc9aca0af6341ebd670`. Launched with
`-screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -force-d3d11 -p109NatureReview -logFile C:\serenity_game\Artifacts\P109\synty-playable\manual-shore.log`,
and `SERENITY_SAVE_DIRECTORY=C:\serenity_game\Artifacts\P109\synty-playable\manual-saves`.
Native Main Menu -> New Game -> Start generated seed 1723002474, cloudy without rain.
Observed ten residents and Synty trees, then clicked the explicit Lake camera bookmark.
Close inspection showed a continuous shore with mud/gravel, grounded stones/grass and trees,
and reduced white glare. Water remains a simple opaque stylized surface with visible repeating
ripples, not planar reflections/refraction. No gameplay exception was observed.

The full native chop route above was on the earlier Synty candidate, not repeated on the final
shoreline candidate; final automated harvest/selection tests passed. Do not conflate these runs.
The Player was left open at the pond with isolated saves. The user was asked to check Home,
Space and held-MMB movement because keyboard injection had no visible result and the native
tool does not support held-MMB dragging. The user answered "да" to the explicit question
whether Home returns to camp, Space pauses, and held-MMB camera movement is smooth.
Recorded 2026-10-21: these three checks pass by user report, not agent input automation.
P10.9 stays PARTIAL; this confirmation does not close unrelated acceptance debt.
This follow-up changes documentation only; no new build/test run or save write was needed.

Final SHA256: exe `437691663C5FB21231524F4886E60A10B1841F13E42FBBC93EE39FBD827930CD`;
Game.Infrastructure.dll `4A5241D1A657E4560D39AEED4D279FAF47C4E6A9E17F2DA7326057BD3B38CB52`;
Game.Presentation.dll `F24071D918BFDF8F32A11DC1DBAD07C9C9EE3A885DDBDC681C2A8489F5F1EFB9`.

## Worksite V2 follow-up — 2026-10-21

This section supersedes the older binary for the new feedback, not the historical evidence above.
The user's two videos were inspected as contact sheets in
`Artifacts/P109/feedback-20261021/video1.jpg` and `video2.jpg`.
Current changes: active-skeleton tool, looped chopping/building clips, visible idle transitions,
ground outputs with capacity-reserved physical hauling, planned construction with persistent labor,
resource collider/source bindings, dry-shore fishing and cosmetic fish, LMB/MMB menu dismissal.
New normal save path: `Serenity/P109/WorksiteV2/Saves`; generation `p109-worksite-v2`.
Old saves are neither loaded into this world nor migrated/deleted. The original five dirty material
files and unrelated `New Unity Project/` are not part of the commit.

### Exact checks

All Unity commands use `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`
with `-batchmode -projectPath C:\serenity_game -force-d3d11`. Each log/result below has absolute
prefix `C:\serenity_game\Artifacts\P109\feedback-20261021\`.
Tests inherit `SERENITY_SAVE_DIRECTORY=C:\serenity_game\Artifacts\P109\feedback-20261021\test-saves`.

- `-executeMethod Game.Infrastructure.Editor.P105QuaterniusFoundation.RepairWorkAnimations -quit
  -logFile ...\repair-import-3.log`: exit 0, `P109_WORK_ANIMATIONS_REPAIRED`. Preserved the original
  valid humanoid mapping; only loop flags are intentionally changed in the two FBX importers.
- `-runTests -testPlatform EditMode -testFilter
  Game.Tests.Editor.P109HarvestTests;Game.Tests.Editor.ResourceInventoryTests;Game.Tests.Editor.P109TiledWorldDefinitionTests;Game.Tests.Tier1UtilityAiTests;Game.Tests.Editor.BuildingDestructionFireCaptureTests;Game.Tests.Editor.P109SyntyImportTests
  -testResults ...\editmode-final.xml -logFile ...\editmode-final.log`: **54/54 passed**.
- `-runTests -testPlatform PlayMode -testFilter
  Game.Tests.PlayMode.P109PlayablePatchTests;Game.Tests.PlayMode.BuildingPlacementPlayModeTests
  -testResults ...\playmode-final.xml -logFile ...\playmode-final.log`: draft **10/12 passed**;
  the fish test omitted Gatherable and the manually evaluated Animator test was still culled.
  These two failed drafts are not described as a clean final run.
- `-runTests -testPlatform PlayMode -testFilter
  Game.Tests.PlayMode.P109PlayablePatchTests.WorkCyclesHaveToolsAndAllStarterOutcropsAndPondAreSelectable;Game.Tests.PlayMode.P109PlayablePatchTests.FishingUsesReachableShoreAndLeavesFishOnGround;Game.Tests.PlayMode.P109PlayablePatchTests.ChopCommandFellsWholeTreeCommitsExactLogsAndSurvivesIsolatedSave
  -testResults ...\playmode-workcycles.xml -logFile ...\playmode-workcycles.log`: **3/3 passed**
  after correcting those test setups and preserving the original animation skeleton mapping.
- `-runTests -testPlatform PlayMode -testFilter
  Game.Tests.PlayMode.P109PlayablePatchTests.IdleHaulFillsOnlyFiveFreeSlotsAndLeavesNineteenLogs
  -testResults ...\playmode-haul.xml -logFile ...\playmode-haul.log`: **1/1 passed**; actual
  approach/pickup/dropoff fills five free slots, leaves nineteen logs and no residual carry load.
- `-executeMethod P109StarterNatureAuthoring.BuildWorksite -quit -logFile ...\build.log`:
  exit 0, Succeeded, 0 errors / 56 warnings, GUID `017e3d80ef804d86af8678a2abc68c6b`.

- Final consolidated rerun: `-runTests -testPlatform PlayMode -testFilter
  Game.Tests.PlayMode.P109PlayablePatchTests;Game.Tests.PlayMode.BuildingPlacementPlayModeTests
  -testResults ...\playmode-complete.xml -logFile ...\playmode-complete.log`: **13/13 passed**,
  392.10 seconds, exit 0. This clean full run supersedes the draft failures above.

The final consolidated XML covers all 13 PlayMode cases; focused results are retained for diagnosis.
No deferred performance benchmark was run. Protected baseline read-only audit matched 44/44 files,
including eight old saves. The normal manual Player must use a separate Artifacts-only save folder.

### Native Worksite Player review

Launched `Builds/P109Worksite/Serenity.exe` with `-force-d3d11 -screen-fullscreen 0
-screen-width 1920 -screen-height 1080 -p109NatureReview -logFile
C:\serenity_game\Artifacts\P109\feedback-20261021\player.log` and
`SERENITY_SAVE_DIRECTORY=C:\serenity_game\Artifacts\P109\feedback-20261021\manual-saves`.
Used computer-use native mouse input, not a scripted command-injection acceptance harness.
Started an ordinary new game, seed 166496655. Selected Rowan, issued RMB Move and saw arrival
followed by natural standing idle. Observed initial physical log carrying. RMB mining exhausted
a 32-unit gold source and increased the canonical total by 32. Two RMB chop orders produced
26 and 35 logs (total 30 -> 56 -> 91), with separate visible ground log piles and work progress
19% -> 70% -> completion. Screenshots did not capture the entire fall animation or resolve the
hand tool closely enough to claim full close-up animation acceptance; loop/tool checks passed
in PlayMode. Both MMB and LMB outside the context menu dismissed it in this binary.

Used the explicitly labelled Lake/Lake-close camera bookmarks to inspect water, not to claim
ordinary camera-input acceptance. Fish visibly changed positions around the lake. RMB on water
issued Gather, the resident walked to the dry west/southwest bank, progressed through Fishing,
and produced four fish (0 -> 4). Zoom in/out and returning to camp retained residents/scenery;
no spontaneous tree displacement was seen in this sampled route. Held-MMB dragging is not
supported by this native tool and was not repeated; the earlier user confirmation remains distinct.
Water is still opaque stylized ripples; fish are simplified procedural silhouettes. White legacy
camp pieces and rough weather/ground presentation remain visible: this is not art-final acceptance.
Construction in a fresh game was research-locked, so native placement/build completion was not
claimed. Its actual-worker approach/progress/completion passed PlayMode. P10.9 stays PARTIAL.
Player log contains pre-existing stripped post-processing shader warnings, no matched gameplay
exception. Rechecked protected hashes after this route: 44/44, including 8/8 old saves.

Binary SHA256: exe `437691663C5FB21231524F4886E60A10B1841F13E42FBBC93EE39FBD827930CD`;
Game.Infrastructure.dll `041B1795F9288F7BECBEAF02432075BBE59D597DC9A4B2AECCA91668D7AF5E7E`;
Game.Presentation.dll `29C74FE311AB546B21D3438DBBA769D8098B6AE6F15530460AC27DE2F9828D7F`.

### Asset analysis scope

No proposed pack was downloaded/imported. Of the eight candidates, Toon Farm is the most immediately
useful for crop/animal/rural dressing; it does not supply human work animations. RTS Pack is a later
settlement/ship option, and Synty Tropical Jungle is for a distinct tropical biome, not a replacement
for the temperate starter. Tower Defense is thematically narrower; Cartoon Army and Low Poly Insurgent
are modern military art; the large JustCreate bundle includes much irrelevant modern content.
The officially free Wild Boar sample is suitable for an isolated animation/style trial, not a promise
that realistic furry wildlife will match the Synty forest. Compare actual URP imports, licenses,
silhouettes and LOD behavior before approving any future package integration.

Official product references (the supplied mirror pages could not be verified; do not assume their
archives match these versions/licenses):

| Candidate | Recommendation for this project |
| --- | --- |
| [Toon Farm Pack / SICS Games](https://marketplace.unity.com/packages/3d/environments/industrial/toon-farm-pack-235041) | First useful candidate: crops, animated farm animals, fences and rural props. Select era-appropriate pieces, omit modern machinery. Human models do not include human work animations. Test actual Unity 6000.6.4f1 URP import before adoption. |
| [RTS Pack / weesals](https://assetstore.unity.com/packages/3d/environments/fantasy/rts-pack-stylized-buildings-props-ships-set-237270) | Later medieval settlements, ships and river trade; not a Stone Age starter replacement and not a construction simulation. |
| [Cartoon Army Universe / PolySquid](https://assetstore.unity.com/packages/3d/environments/cartoon-army-universe-240055) | Modern military styling; skip for the current era. |
| [Tower Defense Pack / Tarbo](https://marketplace.unity.com/packages/3d/environments/fantasy/tower-defense-pack-low-poly-3d-art-143627) | Optional later fortifications. Building/destroyed variants are art states, not a worker/building system. |
| [POLYGON Tropical Jungle / Synty](https://syntystore.com/products/polygon-tropical-jungle-nature-biome) | Good family match only for a genuinely tropical region. Ruins, docks, canoes and vegetation can create points of interest. Cosmetic fish/animal FX are not catchable wildlife. Audit shared PNB_Core updates against the existing Meadow import. |
| [Low Poly Asset Bundle / JustCreate](https://justcreate3d.itch.io/low-poly-asset-bundle) | Broad pack with much irrelevant modern content; do not bulk-import for model count. Curated subsets only if an actual gap remains. |
| [Low Poly Insurgent / ArtStore3D](https://marketplace.unity.com/packages/3d/characters/humanoids/humans/low-poly-insurgent-228607) | Modern armed character; skip for this era and visual direction. |
| [Wild Boar Free Sample / Rip Vertices Studio](https://assetstore.unity.com/packages/3d/characters/animals/wild-boar-free-sample-ultrarealistic-forest-animals-415578) | Official free sample is an optional isolated wildlife trial. Realistic styling may clash with Synty; do not assume the full collection's animation count or rendering cost applies to this sample. |

Proposed later stages, not authorized implementation in this pass: (1) close starter-loop native
acceptance; (2) curate a farm test sector from Toon Farm; (3) connect animals, hunting, fishing and
crop yields to actual canonical gameplay; (4) add meaningful settlements/ruins/bridges/trade routes;
(5) propagate validated biome compositions across the existing 144 terrain chunks, preserving
identities and streaming only presentation. A generator swap or mass random prop fill does not
solve command execution, hauling or an interesting world economy.
