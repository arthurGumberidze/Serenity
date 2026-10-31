# NEXT_TASK.md

## UI01 — review and approve the new interface direction (2026-10-31)

Prepare and present a reviewable interface specification and prototype before changing the production Unity
UI. The deliverable must redefine navigation and player flows rather than reskin the existing panels. It must
cover the world-first HUD, resident/building/settlement inspectors, construction, research, dynasty, events,
army, map and a future diplomacy surface. Every action must map to the existing canonical services; UI state
remains disposable and may not become another gameplay or save authority.

The design plan is ready at `docs/UI01_INTERFACE_REDESIGN.md`; open
`docs/ui01/prototype/index.html` to review the interactive states. Confirm or revise: world-first HUD without
the permanent resident strip, the revised bottom navigation, the local mini-map, a right context sheet plus
contextual command ribbon, and the Ash and Ember visual density. The bottom navigation now has direct worker
group and military entries, while Realm combines the strategic map and diplomacy. Reference PNGs at 1920x1080
and 1280x720 are under the prototype's `screenshots` folder.

Do not begin the full Unity implementation until the user explicitly approves the direction. Critical bugs
discovered during inspection may be fixed with focused checks, but general optimization and final polish stay
deferred. Preserve the current dirty WM15/terrain/vendor work and keep it out of UI01 commits.

The optimized monthly Ollama build was successfully retested and accepted by the user on 2026-10-31. It no
longer blocks development.

## Historical — optimized monthly Ollama retest (accepted 2026-10-31)

Launch `Builds/NeighbourAiOptimized/Serenity.exe`, select local Ollama and the monthly interval. The game now
limits local inference threads automatically (8 on the tested 22-thread CPU), bounds completed work history and
removes terminal production tasks. Compare frame responsiveness during each request and inspect
`provider_timing` entries in the newest `AiAudit` JSONL file. Override only if needed with the user environment
variable `SERENITY_OLLAMA_THREADS` (for example 6 for more game headroom). Await user feedback before unrelated
work. The user requested one PC shutdown after this delivery; do not repeat it on a later retest unless asked.

## Next action — user retest of neighbour monthly Ollama progression (2026-10-31)

Launch `Builds/NeighbourProgressionRepair/Serenity.exe`, select `Ollama локально` and the one-month interval,
then start a new game. The first external request is after two game days; each accepted plan remains valid for
30 game days and the deterministic AI executes it through canonical settlement jobs. The automated 730-day
run observed 25 accepted local plans and productive work from every neighbour resident; the exact 14-day route
also completed cooking, research and a new operational building. Inspect the newest JSONL file under
`LocalLow/Serenity/Serenity/AiAudit`. Await user feedback before unrelated work. The current one-time shutdown
request is fulfilled after this delivery; do not repeat it on a later retest unless requested again.

## Next action — user retest of WM16 follow-up (2026-10-30)

Launch Builds/WM16Followup/Serenity.exe and start a new game. Menu access, full snow preparation,
loading progress, concurrent upgrades, AI cadence/audit and livestock renderer repair are DONE.
71 tests and the real five-upgrade/snow/menu route passed through the persistent runner; Windows build
and native Player smoke passed. First external AI request is after two game days; select month / half-year /
year in main-menu settings. Inspect LocalLow/Serenity/Serenity/AiAudit for plan/action JSONL.
Await user retest before unrelated work. Do not shut down the PC. Older deliveries below are historical.

## Next action — user retest of optional ruler strategy AI (2026-10-30)

Launch `Builds/RulerStrategyAI/Serenity.exe`. In main-menu settings choose `Ollama локально`, start a new game,
press F10 and use `Запросить внешний план` in the strategic-AI debug card. The request is asynchronous and may
take about 38 seconds on the tested CPU; the game AI keeps running. The card then shows provider, preferred goal
and validity tick. Test `Облачный API → Ollama` only after starting a new game process that can read
`LLM_GATE_API_KEY`; do not paste the key into the game or repository. Setup and fallback details:
`docs/AI_RULER_STRATEGY.md`. Automated evidence: U26 29/29, P2 5/5, local Ollama JSON smoke and a successful
Windows Development build. Await user feedback before extending diplomacy/conversation UI.

## Next action — user retest of WM15 screenshot/video repair (2026-10-30)

Launch `Builds/WM15RetestRepair/Serenity.exe` and start a new game. All ten requested repairs are implemented;
63 domain/role cases and the complete real gameplay route passed through the persistent runner. The Windows
build passed with immutable provenance; actual Player exited 0 and its hut/snow images were reviewed.
Close-snow Full HD measurements: about 58 FPS paused, 31 FPS with simulation; no 60-FPS simulation claim.
General population-performance gates remain a separate deferred task. See `docs/validation/WM15RetestRepair.md`.
Await user feedback before further unrelated work. The prior overnight shutdown was already fulfilled;
do not shut down the PC for this daytime follow-up.

## WM15 user video/screenshot repair — DONE (2026-10-30)

Fix the ten reported retest issues: brown hut/roof holes; research-gated central storage renamed Склад;
solid building/tree/rock navigation and physical collision; world preloading during character creation;
actual builder upgrades with work-site scaffolding; automatic housing and urgent sleep; reachable fishing
work on the player's own pier; construction delivery and distant automatic assignment; bounded snow cost
and readable real walking tracks; naturally distributed individual fish. Review both supplied videos.
Use the persistent Unity runner, actual gameplay routes, visual evidence and a new Windows build.
This new request reopens the relevant startup and gameplay restrictions below. The prior night's one-time
PC shutdown request was fulfilled and does not apply to this new daytime repair task.

## Next action — user retest of WM15 asset expansion (2026-10-30)

The authorized asset-expansion work is complete. Start a new game with
`Builds/WM15AssetExpansion/Serenity.exe`; review building upgrades, hut4 → stone house6, the separate
communal house, military rank variants, alpine snow trails and the mountain lake at close range.
Costs, work duration and research conditions are editable as described in
`docs/world_map/WM15_ASSET_EXPANSION.md`. Validation: `docs/validation/WM15AssetExpansion.md`.
Do not resume deferred population gates, unrelated mountain reauthoring or Ollama work without new direction.
The user authorized deleting obsolete saves if needed and requested PC shutdown after completion.

## WM15 existing-asset expansion — DONE (2026-10-30)

User explicitly reopened building upgrades and mountain-foot snow/lake fixes. Priority: improve and add
buildings, author a distinct level II for every base building, and implement resource-funded builder upgrades
with editable costs/conditions. Add rank-dependent military uniforms (one shared enlisted suit below lieutenant),
deformable snow trails, fix close mountain-lake rendering. Validate through Tools/UnityRunner.ps1 and deliver a
new Windows Player. Preserve unrelated imported/mountain work and previous builds. This authorization supersedes
the older pause/retest-only restrictions below. Do not resume unrelated population gates or startup optimization.


## Persistent Unity test/build runner — DONE (2026-10-29)

Use `Tools/UnityRunner.ps1` and `docs/UNITY_PERSISTENT_RUNNER.md` for subsequent compile, EditMode and Development
build work. The runner is verified on one live Unity 6000.6.4f1 PID and never uses the `-runTests`/`-quit`
command-line path. The next product action remains the user's own launch/screenshots from
`Builds/WM15UserRetestFinal/Serenity.exe`; do not change gameplay or water presentation until that feedback.

## WM15 living-building feedback — user retest (2026-10-29)

Deliver `Builds/WM15UserFeedback/Serenity.exe` and ask the user to create a new world, use the military/resource
diagnostics, assign builders and hunter/day/night group members, place a pier and fishing building, and inspect
a hut, pen animal, campfire pathing, paused camera movement, UI wheel/TPS input and close lake edge. Automated
evidence is 2/2 full-world PlayMode scenarios plus all affected EditMode assertions green; the Development
build succeeded with 0 errors / 82 warnings. Do not resume startup optimization. Preserve the neighbouring
mountain/import work and do not reopen deferred building upgrades, Ollama settlements or mountain graphics.

## WM15 resident map cache — user retest (2026-10-29)

Deliver `Builds/WM15MapWarmup/Serenity.exe` and ask the user to create a new world. On the first incomplete
cache preparation, the bottom-centre `Подготовка карты: N%` indicator appears once and hides after 100%; later
launches of the same generation/seed/version use the completed cache without showing it. Ask the user to pan
across the full map after 100%. Automated Player evidence is cache hit 16/16 and worst warm refresh 5.4 ms,
down from 1205.7 ms. Do not change the accepted mountain, mountain lake or water appearance. Preserve unrelated
dirty/vendor changes and keep the deferred 100/300/500 population gates separate.

## WM15 corrected Player — user retest (2026-10-29)

Deliver the rebuilt `Builds/WM15WaterFinal/Serenity.exe` and ask the user to create a new world. The stripped
Procedural Water shader and the steep rock-spire POI approach that blocked real Player world creation are both
fixed. Focused POI validation passed 4/4; the actual Player reached `WM15_BOOT gameplay ready` without either
exception; the final Development rebuild succeeded with 0 errors / 85 warnings. Do not change the accepted
mountain, mountain lake, ordinary lake or marsh presentation. Preserve unrelated dirty/vendor changes.

## Resume parallel WM15 visual/water work (2026-10-29)

The living-buildings scope is complete and verified; do not reopen it unless a new defect is reported.
Continue the already active WM15 visual/water task in the neighbouring chat. The user explicitly stopped
additional startup optimization for this delivery. Preserve all imported assets and the shared dirty-tree
work. The building-upgrade table/mechanics, Ollama-controlled neighbouring settlements and further mountain
graphics stay paused in `docs/PAUSED_TASKS.md` until the user starts one of those tasks.

## WM15 ordinary lakes and marsh — deliver verified build (2026-10-29)

Deliver `Builds/WM15WaterFinal/Serenity.exe` and ask the user to review it in a new game. The mountain,
mountain lake, denser ordinary lake and continuous blue-to-dark-olive marsh transition are visually accepted.
Focused EditMode validation passed 16/16 and the Windows Development build succeeded with 0 errors / 85
warnings. The old reported `draw_water` mismatch is absent from the current catalog; only its localization and
an explicit absence regression remain. Preserve old saves, previous builds and unrelated dirty/vendor changes.

## WM15 Terrain Sample ridge and water transition review — next unblocked work (2026-10-28)

Await the user's review of `Artifacts/WM15EnvironmentReview3/`. The current source selectively uses three
approved Terrain Asset Pack heightmaps for the alpine ridge, makes lake water less transparent, and replaces
the stepped marsh material boundary with continuous per-vertex shader blending. Do not run tests or produce
a Player build until the user approves this visual direction. On approval, resolve the unrelated current
`draw_water` catalog mismatch, run focused validation and build the next WM15 candidate. Preserve old saves,
the original package archives, selectively admitted art and unrelated dirty/vendor changes.

## WM15 after full asset-library audit (2026-10-28)

The requested audit is complete; see docs/world_map/WM15_ASSET_LIBRARY_REVIEW.md. Recommended next
content pass: 6–8 existing buildings and their work-site props, then terrain/shore consistency.
This is a recommendation, not a new import authorization. Keep WM15 active and preserve the existing
visual-approval gate before tests/new Player build. No asset deletion or bulk import is scheduled.

## WM15 river/lake/mountain/marsh review — superseded by Terrain Sample review (2026-10-28)

The older four PNGs remain in `Artifacts/WM15EnvironmentReview/`. That candidate removed
lake ice, uses the approved Procedural Water Shader URP asset with river/lake/mountain-lake/marsh profiles, and
uses Snow Mountain plus Winter Mountains & Stamps for the alpine landmark. Its review gate is now represented
by the newer `WM15EnvironmentReview3` candidate above.

## WM15 winter mountain — next unblocked work (2026-10-28)

Await the user's yes/no review of `Artifacts/WM15MountainReview/winter-environment-final.png`. The source
now uses StampIT terrain plus Winter Environment URP scenery/weather for the alpine region and surrounding
world. Do not run tests or produce a new Player build until the user approves this visual direction. On
approval, perform focused compilation/visual checks and build the next WM15 review candidate. Preserve the
full imported Winter Environment, the user's StampIT import, old saves and unrelated dirty/vendor changes.


## WM15 current feedback — next unblocked work (2026-10-27)

Candidate: `Builds/WorldMapCurrentFix/Serenity.exe`. Latest build-e succeeded with 0 errors / 82 warnings
and focused verification is effectively 11/11
(editor-f 10/11 contained only an over-strict candidate-count assertion; corrected case editor-g passed 1/1).
Player-b passed 55 checks including the requested pasture/fish/shore routes and reported no runtime errors;
two unrelated historical hunt-animation/completion timing assertions failed. See
`docs/world_map/WM15_CURRENT_FEEDBACK.md`. Ask the user to review a **new game**, especially adaptive pier
placement, chosen single-species pasture, mountain snow, brighter campfire/torch and smooth/silver fish.
Preserve old saves and inherited vendor/P3 changes. WM15 remains IN_PROGRESS for
visual acceptance, slow loading and deferred full 100/300/500 gates. Run Unity batchmode only as a hidden
process outside the sandbox so the local Unity Personal profile is available; GPU-backed hidden batchmode is
required because UMA's startup window hangs under `-nographics`. Do not use GUI automation.


## WM15 evening feedback — next unblocked work (2026-10-27)

Candidate: `Builds/WorldMapEveningFix/Serenity.exe`, build-i 0 errors / 82 warnings, Player-g 31/31,
exit 0 with no gameplay runtime errors; eleven focused Editor regressions passed. See
`docs/world_map/WM15_EVENING_FEEDBACK.md`. Ask the user to review a new game; preserve old saves.
Continue WM15 visual/content refinement and initial loading work from user feedback. General building
context menus are deferred until answers to `docs/world_map/WM15_BUILDING_ACTION_QUESTIONS.md`.
Finite fish stocks replenish only through camp breeding. Do not rerun passed checks without new changes
or concerns, and do not run full 100/300/500 gates without authorization. WM15 remains IN_PROGRESS.
Preserve inherited imports/P3 materials. No repeated shutdown.


## WM15 afternoon feedback — current work (2026-10-26)

Candidate: `Builds/WorldMapAfternoonFix/Serenity.exe` (build-f 0 errors / 82 warnings; Player-d 47/47, exit 0,
no runtime errors; 19 focused Editor checks). See `docs/world_map/WM15_AFTERNOON_FEEDBACK.md`.
Next unblocked work stays WM15: user visual review, initial loading and remaining content. Ore, hide,
rod and rigged fish are now available; do not ask for duplicate general packs. Keep WM15 IN_PROGRESS.
No full performance gates and no repeated shutdown. Preserve inherited imports and unrelated materials.

## WM15 after local prop replacement (2026-10-26)

Current candidate: `Builds/WorldMapAssetReplacement/Serenity.exe` (build-b, 0 errors / 81 warnings;
Player-b 29/29, no runtime errors; 17 distinct focused Editor tests passed). See
`docs/world_map/WM15_ASSET_REPLACEMENTS.md` for actual replacements and the remaining targeted asset list.
Next unblocked work stays within WM15: user visual review, art/content refinement and loading duration.
Do not reacquire existing general prop/nature packs. Distinct raw materials, compatible work animation,
animated fish and dedicated impact effects are the remaining asset gaps. Keep WM15 IN_PROGRESS.
No full 100/300/500 performance gates without authorization; no repeated shutdown. Preserve unrelated imports.

## WM15 asset selection after complete local audit (2026-10-26)

Audit delivered: `docs/world_map/WM15_IMPORTED_ASSET_COVERAGE.md`. Next WM15 content work should use
the already imported props first, then selectively admit the rod/static fish/bone models and All Nature
VFX URP content already present in local archives. Do not ask the user to download duplicate general
nature/resource/pier packs. Distinguish missing compatible animations and specialized raw resources
from model availability. This turn only analyzed and documented; no import/replacement/build/testing.
Keep WM15 IN_PROGRESS and preserve vendor imports/unrelated material edits. User visual acceptance,
loading/content tasks and deferred performance gates remain open. No repeated shutdown.

## WM15 morning feedback — next unblocked work (2026-10-26)

Repair candidate: `Builds/WorldMapMorningFix/Serenity.exe`, build-e exit 0, 0 errors / 77 warnings.
Player-d 31/31 and saved-game Player-e 6/6 passed without runtime exceptions; 41 distinct focused tests
passed. See `docs/world_map/WM15_MORNING_FEEDBACK.md`. Next: user review of this build and selection of
professional replacement assets from `docs/world_map/WM15_HANDMADE_ASSET_INVENTORY.md`; then address
remaining WM15 art/loading/content items. Keep WM15 IN_PROGRESS, do not repeat passed checks without
new changes or concerns, and do not run full performance gates without authorization. Prior batch
shutdown already fulfilled. Preserve user/vendor imports and unrelated materials. Historical candidates
and shutdown statements below are superseded by this entry.


## WM15 late feedback — next unblocked work (2026-10-26)

Current candidate: `Builds/WorldMapLateFix/Serenity.exe`, build-c 0 errors / 71 warnings.
Player-c passed 51/51 checks, no runtime errors; four distinct focused regressions passed.
See `docs/world_map/WM15_LATE_FEEDBACK.md`. Next remain user review of this candidate and WM15
visual refinement/loading duration; preserve the regional palette and inherited imports. AQUAS is
partially reused through its wave texture; a full URP port is separate outstanding work, not claimed done.
Do not repeat passed checks without new changes/concerns. Full 100/300/500 gates still await authorization.
Keep WM15 IN_PROGRESS pending visual acceptance and outstanding White Rabbit/husbandry content.
The user requested shutdown after saving this completed repair batch; do not start unrelated work first.

## WM15 October 25 feedback — active

Build-f is ready at Builds/WorldMapOctoberFix/Serenity.exe: 0 errors / 69 warnings. Player-f passed
38/38 functional checks with no runtime errors; eight distinct focused regressions passed. See
`docs/world_map/WM15_OCTOBER_FEEDBACK.md` for exact commands, captures and validation boundaries.
Next unblocked work remains WM15: incorporate feedback on this candidate, refine shoreline/presentation
details and loading duration. Do not repeat already passed checks without a new change or concern.
Do not run full performance gates without authorization.
Keep WM15 IN_PROGRESS pending user visual acceptance, White Rabbit admission and wider husbandry.

## WM15 preview delivered — next work (2026-10-25)

Compilation and the requested Windows build are complete: Builds/WorldMapArtHunting/Serenity.exe,
0 errors / 30 warnings. See docs/world_map/WM15_PREVIEW_BUILD.md. Continue user feedback and focused
map/hunting/pasture inspection within WM15; do not mark DONE. Full 100/300/500 performance gates remain
prohibited for now. White Rabbit and complete husbandry remain open. Preserve inherited imports.
Historical no-execution restrictions below are superseded by the user's compile/build authorization.

## WM15 preview build authorized (2026-10-25)

The user now requests compilation and a new Windows Player to inspect the map, hunting and sheep pasture.
This supersedes the execution prohibition in historical entries below. Full 100/300/500 performance suites
remain deferred. Compile, fix build blockers and produce Builds/WorldMapArtHunting/Serenity.exe; retain WM15
IN_PROGRESS pending gameplay/visual acceptance and outstanding content. Preserve inherited imports.

## WM15 approved art direction + animal health — current (2026-10-25)

The user's answers authorize the regional art implementation; the analysis-only restriction in older entries
below is historical. Current source changes are documented in `docs/world_map/WM15_ART_DIRECTION_AND_HUNTING.md`.
Work remains on WM15, IN_PROGRESS. Do not launch tests, Unity, Player, compilation or rebuilding until the user
revokes the execution prohibition. Do not present source changes as present in the existing executable.
Continue incoming bugs and source review. White Rabbit package admission is still blocked on obtaining the
approved local package. Full livestock feeding/trade/production and aggregate hunting are not implemented.
Once execution is authorized: compile, then targeted hunting/HP/persistence/ownership and start-grove-marsh
visual/navigation checks before claiming acceptance; the 100/300/500 performance gates remain deferred.
Preserve inherited imports and materials; no additional asset purchase without separate approval.

## WM15 content feedback — current, source only (2026-10-24)

Keep the user's prohibition on tests, Unity/Player execution, compilation and rebuilding. WM15 is IN_PROGRESS.
The latest source checkpoint adds Polytope residents / unchanged UMA rulers, task-specific work tools,
selectable crops and crop rows, lake-outlet/shore mesh repairs, removal of the photographed monolith and
retirement of fox/wolf. See `docs/world_map/WM15_CONTENT_SOURCE_REPAIR.md` for exact scope and limitations.
White Rabbit is approved but its package is not available locally; the old hare proxy remains until admission.
Animal pens are not implemented; Toon Farm provides suitable art but no canonical livestock gameplay.
The broad map color/biome request is ANALYSIS ONLY until the user answers
`docs/world_map/WM15_MAP_ART_DIRECTION_QUESTIONS.md`. Do not implement that proposed palette yet.
Continue remaining content/new user bugs; ask for missing rabbit files when the user returns. Paid purchases
need separate approval. All build/test/visual evidence below predates these source changes.
Preserve the expanded inherited vendor/import changes recorded in `Artifacts/WM15Content/baseline-status.txt`.

## WM15 follow-up source repair — active, no execution (2026-10-24)

User explicitly requests continuing fixes while collecting further bugs, **without running tests or rebuilding**.
This instruction overrides the validation timing below; do not launch Unity, Player, batchmode, suites or a build.
Current scope: quarry worker extraction/haul, selectable forest trees, lake fish presentation, consistent UMA
characters/editor, command isolation from other estates, and replacement hare/fox/wolf art.
Source changes are recorded in `docs/world_map/WM15_FOLLOWUP_SOURCE_REPAIR.md`; they are uncompiled and untested.
The existing WorldMapV2Feedback Player is unchanged and does not contain this follow-up.
The subsequent content checkpoint above supersedes the three-species art request: fox/wolf are retired,
White Rabbit is approved but missing locally. No paid dependency was purchased.
Continue remaining content/new user bugs; runtime checks and a fresh build wait for the user's instruction.
WM15 remains IN_PROGRESS, not DONE. Protect the seven inherited materials and `New Unity Project/`.


## WM15 user feedback repair — active (2026-10-24)

Latest user steering: settlement-first NPC strategy. No automatic opening raid: develop agriculture and barracks,
train canonical residents while retaining civilians, assess relations/relative strength, persist a one-day warning
before departure, and re-evaluate readiness. This is part of the active WM15 repair, not a new unrelated task.

The user's video and screenshots reject visual acceptance of the activation build. Active work is the
camera/terrain hitch, non-selectable overlapping animals, POI localization, invalid mesh colliders,
unbound building selection, and crossing/POI placement. Startup smoke is insufficient evidence.
Prepare the full terrain/navigation at loading and verify a route across the map in the native Player.
The long 100/300/500 gates remain separate; do not run the whole overnight suite.

The candidate for this feedback is `Builds/WorldMapV2Feedback/Serenity.exe`, with exact evidence in
`docs/world_map/WM15_FEEDBACK_REPAIR.md`. Build-l and native route-h now pass the functional checks (35 points,
zero recorded errors, no visible white materials, actual enemy growth/training and neutral peace). The new
strategic regression is 30/30; seven-species pointer selection also passes. Next unblocked WM15 work:
shorten full-map initial loading, reduce the remaining GPU cost (pan median/P95 32.271/37.861 ms), replace the hare/fox/wolf temporary models,
and consume the user's rendered 100/300/500 gate results. Keep visual acceptance open. The quarry uses existing
shift commands, not a completed automated industrial worker/logistics flow. The earlier activation notes below
are historical; do not launch `Builds/WorldMapV2/Serenity.exe` to review this repair.


## Active override — WM15 V2 activation and remaining performance acceptance (2026-10-24)

The user explicitly approved activating World Map V2 and deleting legacy saves. Normal New Game now loads
`p109-world-map-v2-proposed`, uses the v30 `WorldMapV2` save directory and has no legacy save catalog in the
menu. The old local save directories were deleted. A fresh Player with visible streamed vegetation is at
`Builds/WorldMapV2/Serenity.exe`; visual evidence is
`Artifacts/WM15Activation/world-map-v2-release-candidate.png`.

Activation tests and the Windows build pass. The next authorized work is the user's offline rendered
100/300/500 performance run with `Tools/Validation/Run-WM15-Performance-Repair.cmd`, followed by investigation
of any failed gates. Do not run the full 182-step night suite. WM15 remains IN_PROGRESS until those long gates
and user visual acceptance complete. PostgreSQL remains PARTIAL without a configured test database. Keep the
seven inherited dirty materials and `New Unity Project/` outside WM15 commits.


## User-requested offline validation tooling — 2026-10-24

The requested runner is ready at `Tools/Validation/Run-WM15-Night.cmd`; command-line entry is
`Tools/Validation/Run-WM15.ps1`. Usage, limitations and verification are recorded in
`docs/world_map/WM15_OFFLINE_RUNNER.md`. It has not started the real overnight gate. Finish the active WM14
worker before using it, and do not run a second WM15/Editor concurrently. The WM15 worker should consume
matching-source offline evidence and investigate failed/incomplete checks, rather than duplicate a live run.
This support deliverable does not advance the active WM task or grant visual acceptance.

## Current scope — WM15 final integration and visual acceptance, after WM14 completion on 2026-10-24

WM14 is DONE. Its short D3D11 Editor profile passes the 100/300/500 population gates, preserves exact hybrid
tier counts and existing content density, and records bounded memory plus zero per-agent AI Updates. The active
compact-world save/profile is unchanged and `p109-world-map-v2-proposed` remains inactive.

The next genuinely unblocked world-map task is **WM15 — final integration and visual acceptance**. Use the
existing offline-runner support as tooling, but independently verify its matching-source evidence. WM15 owns
broad EditMode/PlayMode regression, a fresh Windows build, Player/GPU performance evidence, the visible route,
coordinate/image audit and user visual acceptance. Do not treat the runner's implementation or WM14's short
Editor profile as completion of those gates. This WM14 chat stops after its commit and does not execute WM15.

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

## Latest steering — performance-only offline handoff (2026-10-24)

The user will run the long gates offline. Use Tools/Validation/Run-WM15-Performance-Repair.cmd,
not Run-WM15-Night.cmd. The eight-step plan builds fresh, measures only failed 100/300/500
populations at 3 x (60s + 300s), then checks protection and diffs. Reports go to a new
Artifacts/WM15/performance-repair-* directory. No long gates were started in this repair.
Next: consume that report, fix remaining performance failures, and resolve/document the
original save-slot overwrite recorded in WM15_NIGHT_REPAIR.md. WM15 remains IN_PROGRESS;
do not advance to another task or assert visual acceptance.
