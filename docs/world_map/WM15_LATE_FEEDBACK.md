# WM15 — October 25 late feedback

Scope: the two 22:13/22:15 recordings and six screenshots, plus the eleven local packages in
`Assets/25102026`. Preserve the existing map identities, simulation state and unrelated imports.
No full population/performance gates. No purchased or remotely downloaded dependency.

## Repairs

- An exhausted whole-source recipe returns no output, rather than constructing an illegal zero
  `ResourceQuantity` when the inspector asks for its output. The original Player stack identified this path.
- Health capability queries tolerate an externally recorded death just ahead of the health scheduler's
  last processed tick. Canonical death time and strict historical age validation remain intact.
- Wildlife death binds the actual model, not the temporary scenery-preparation child destroyed after
  instantiation. The model rolls onto its side, stops animation and rests its rendered bounds on ground.
- Work poses restore only their own still-applied rotation. A fresh Animator pose is no longer modified
  by subtracting the preceding frame's wrist offset. Axe/pick roll is fixed relative to the worker.
  Shared, camera-limited impact presentation emits wood chips or stone fragments at contact.
- Bow hands and string follow the canonical draw/release phase; the arrow is visible on the string
  before flight. Damage, miss chance and skinning still belong to the animal domain service.
- Fishing has a wooden rod and line. Fish output uses a dry bank point near the actual pier rather
  than the potentially distant resource-source marker. A completed player fishing camp offers
  `Ловить рыбу с пирса`, resolving a nearby available fish source through the normal command service.
- Water meshes no longer act as physical floors. Merged navigation exclusion volumes cover submerged
  terrain deeper than 1.25m, stopping below elevated pier/bridge decks. Drowning remains a separate rule.

## Local package review

The inventory was read without importing vendor scripts or executing package content. Exact original
paths/sizes are in `Artifacts/WM15LateFeedback/package-inventory.json`; admitted paths and GUID remaps
are in `admitted-assets.json`. Selected dependencies live under `Assets/Game/Art/WM15Late/Imported`.
Prepared prefabs/materials and `Resources/World/LateLandscape.asset` reference this owned copy.

| Package | Findings and disposition |
| --- | --- |
| `66d2c3cc80_uassets-dl-gtrsb9` / Arctic Tundra | 41 prefabs, 39 FBX. Selected snow tree, rocks/cliffs and ice shapes. The shared palette referenced by the supplied package is missing locally; explicit URP snow/bark/rock materials replace that missing dependency. This is not the original demo's atlas appearance. |
| `7b01a393b0_uassets-dl-skh3th` / AQUAS | Legacy water shaders and bundled PostProcessing, 98 C# files across the package. Selected medium-wave texture and normal maps. The color wave texture drives the existing URP water material. The AQUAS renderer, reflections, underwater effects and legacy postprocessing have **not** been ported or claimed compatible. Full use needs a separate compatibility adaptation; AQUAS is not categorically unusable. |
| `80cef589b5_uassets-dl-ysplrj` / TerrainComposer2 | Editor terrain-generation tool, 83 C# files. Not imported: the project already has deterministic authored hydrology/terrain and needs compatible presentation assets, not replacement of that authority. |
| Ambience — Banaue Rice Terraces | Ten WAV recordings, not rice-terrace terrain or vegetation. Retained as a possible audio source; no unrelated audio system added in this repair. |
| `ecc658483f_uassets-dl-qceo2b` / StylizedNatureBundle | 267 prefabs, 248 FBX, old shaders and an embedded URP 7 package. Selected four flower variants, two marsh plants and falling-leaf/firefly effects; converted materials to current URP. Old scripts, demo scenes and embedded pipeline package excluded. |
| FREE 3D VEGETATION PACK | Twelve FBX/prefabs, 24 PNG and five terrain layers. Reserve for later vegetation review; the selected stylized flowers/plants supply the current additions without replacing all trees. |
| Grass And Flowers Pack 1 | Primarily billboard/cutout images (73 PNG, six TIF), not a collection of authored 3D flower prefabs. Reserve; current close-view flowers use StylizedNatureBundle geometry. |
| Nature Materials vol.1 | Ten texture images / twenty legacy materials. Reserve; overlapping grass options, no need to admit every similar terrain material. |
| Nature Materials vol.2 | Eight texture images / eight materials. Reserve for later palette refinement. |
| Outdoor Ground Textures | Fourteen material sets, seventy TGA maps. Reserve; more realistic detail requires a deliberate regional style choice. |
| Terrain Textures Pack Free | Five color/normal pairs. Selected grass, dry forest leaves and shore pebbles; imported at a maximum 2048 texture size with compression/mipmaps. Existing terrain weights, snow and blossom palette remain authoritative. |

Snow trees reuse existing vegetation IDs/positions and the shared instancing renderer. New rock/ice
scenery is bounded and prepared once while loading. Flowers/plants replace selected existing groundcover
instances. Falling leaves and fireflies are bounded patches, distance gated; fireflies are also night gated.
Weather and the day/night cycle remain active. This is a first integration, not final visual acceptance.

AQUAS compatibility evidence: `Artifacts/WM15LateFeedback/aquas-compatibility.txt` records `GrabPass`
and `UnityCG.cginc` in the supplied desktop/river shaders and `OnWillRenderObject` in its reflection
script. Those legacy paths are why simply assigning the supplied materials is not an established
URP integration. The selected wave texture alone does not reproduce the complete AQUAS system.

## Validation commands and evidence

Unity executable: `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`.
Each Editor invocation runs singly with a hidden process window. Original user log preserved as
`Artifacts/WM15LateFeedback/user-player.log`. Video contact sheets: `axe.jpg`, `bow.jpg`.

```text
-batchmode -nographics -quit -projectPath C:\serenity_game -executeMethod WM15LateAssetAuthoring.Author -logFile C:\serenity_game\Artifacts\WM15LateFeedback\author-b.log
```

Author-a stopped with one C# error: long world seed passed to the int Random constructor. Corrected
the deterministic unchecked cast. Author-b exited 0 and authored the catalog, materials and prefabs.

```text
-batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter Game.Tests.Editor.HealthAndFamilyTests.ExternalDeathBeforeHealthClockAdvanceImmediatelyDisablesWork;Game.Tests.Editor.WM15OctoberFeedbackTests.ExhaustedWholeSourceHasNoZeroQuantityOutput;Game.Tests.Editor.WM15OctoberFeedbackTests.CorpseRollsActualModelAndRestsOnGround -testResults C:\serenity_game\Artifacts\WM15LateFeedback\focused-a.xml -logFile C:\serenity_game\Artifacts\WM15LateFeedback\focused-a.log
```

Focused-a: exit 0, **3/3 passed**, no broader suite or performance workload.

```text
-batchmode -quit -projectPath C:\serenity_game -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/WorldMapLateFix/Serenity.exe -logFile C:\serenity_game\Artifacts\WM15LateFeedback\build-a.log
```

Build-a: exit 0, Succeeded, 0 errors / 71 warnings. GUID `8091a35b8e084144be023e1380d1fb34`,
source fingerprint `BEC7F0CFF70C9897C2606E0782068DC3BDD9604D71C8F489A23A8384183BE12D`.
The recorded base commit is `d1fd3cf8819db381933731ff861026a8d1c1d188`; provenance includes the
dirty source manifest. No source changes occurred during this build.

```text
C:\serenity_game\Builds\WorldMapLateFix\Serenity.exe -screen-fullscreen 0 -screen-width 1440 -screen-height 900 -wm15OctoberSmoke C:\serenity_game\Artifacts\WM15LateFeedback\player-a -logFile C:\serenity_game\Artifacts\WM15LateFeedback\player-a.log
```

Player-a: exit 0, **50/50 checks passed**, empty `errors.txt`. This includes all four lake-bed
navigation exclusions, dock navigation, camp work dispatch, actual wood/stone/hunt/fish completion,
impact fragments, bow-string draw variation, rod activation, nearby dry catch location and model death.
The readable work route still used the ruler in this first pass. Ordinary resident work is covered next.

Rendered review found that the close fishing camera looked below the pier: its ground sampler followed
the submerged bed. The camera now uses the greater of ground and water surface. Ice floes changed from
small evenly spaced dots to a larger irregular cluster. The follow-up diagnostic uses an ordinary female
resident for woodcutting/fishing, controls the Cinemachine mount for side views, and uses a clearly scoped
noon presentation preview for close captures, restoring actual weather/time immediately afterwards.
Normal gameplay day/night and weather are unchanged; the first route already ran through evening/night.

Build-b repeats the build command with `build-b.log`; Player-b repeats the executable arguments with
`player-b` and `player-b.log`. Build-b exited 0 with 0 errors / 71 warnings. Player-b exited 1:
47/48 checks passed, with no runtime exceptions. The woodcutting fixture selected a resident without
a current presentation and therefore skipped that route; the selector now requires an already visible
ordinary resident and excludes diagnostic military additions. This was not a passing final route.

Rendered review also found that the generic labor animation fought the bow/fishing arm poses. These
two intents now use the neutral base animation with their own hand poses. Arrow normalization now
includes nested FBX transforms. Camera focus retains a selected pier occupant's height above the
water until the user pans; this prevents high pier occupants being cropped out at close zoom.

```text
-batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter Game.Tests.U05CameraAndInputTests.ElevatedPierFocusSurvivesIdleTicksUntilCameraPans -testResults C:\serenity_game\Artifacts\WM15LateFeedback\focused-camera.xml -logFile C:\serenity_game\Artifacts\WM15LateFeedback\focused-camera.log
```

Focused-camera: exit 0, 1/1 passed. This is a new regression, not a repeat of focused-a.
Build-c and Player-c use the same commands above with suffix `c`.
Build-c: exit 0, Succeeded, **0 errors / 71 warnings**. GUID `3d66c16a53494b76a84a3f762829fc35`,
source fingerprint `327E085F612DD7D53B3318B03AB45F9549F9D25073B5A4DD7CADDEDB38B68C30`,
created `2026-10-25T21:24:05.5456601Z`. Before/after source manifests matched. Editor exited normally.

Player-c: exit 0, **51/51 PASS**, empty errors.txt. Ordinary resident Sela completed chopping and
fishing; ruler completed mining and bow hunting/skinning. The route verifies rod, pier stance,
nearby dry catch, camp dispatch, four lake navigation exclusions, work fragments, varying bow draw,
HP loss before skinning, actual corpse roll, new snow scenery and the earlier 38 functional areas.
Close captures were visually reviewed: chips at the trunk, worker outside the rock, neutral bow body
with changing string/arm pose, visible rod/line on the pier and the boar lying on its side. Regional
snow ground, prepared snow rock, ice cluster and richer green ground were also reviewed. This is
functional/visual evidence for the changed paths, not a claim of final animation or landscape art quality.

Durable evidence is under `docs/validation/WM15LateFeedback/`: both focused XML reports, final build
provenance and source manifest, final check/error lists, selected captures and admission inventory.
Full local logs/capture sequence remain in `Artifacts/WM15LateFeedback/`. No full performance suites ran.
Loading still takes minutes; that limitation is not resolved by this repair batch. The original AQUAS
renderer/reflections/underwater pipeline is still not ported. No paid dependency was purchased.
WM15 remains IN_PROGRESS pending user visual acceptance and previously recorded outstanding content.
