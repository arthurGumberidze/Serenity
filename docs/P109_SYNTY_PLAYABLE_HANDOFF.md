# P10.9 Synty playable starter slice — 2026-10-20

Status: PARTIAL. Implementation, tests and Windows build complete. Native tree/order/zoom
route passed on the first Synty candidate; close water review passed on the final candidate.
Keyboard Home/Space and held-middle-button camera acceptance remain open.

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
The Player is left open at the pond with isolated saves. The user was asked to check Home,
Space and held-MMB movement because keyboard injection had no visible result and the native
tool does not support held-MMB dragging. No human confirmation has yet been received.
P10.9 stays PARTIAL; do not infer complete camera/input acceptance from camera bookmarks.

Final SHA256: exe `437691663C5FB21231524F4886E60A10B1841F13E42FBBC93EE39FBD827930CD`;
Game.Infrastructure.dll `4A5241D1A657E4560D39AEED4D279FAF47C4E6A9E17F2DA7326057BD3B38CB52`;
Game.Presentation.dll `F24071D918BFDF8F32A11DC1DBAD07C9C9EE3A885DDBDC681C2A8489F5F1EFB9`.
