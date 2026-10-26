# WM15 — evening feedback, 2026-10-26

Status: repair candidate verified on 2026-10-27. WM15 remains IN_PROGRESS for user visual acceptance,
loading/content refinements and deferred performance gates. Final executable:
`C:/serenity_game/Builds/WorldMapEveningFix/Serenity.exe` — start a **new game**.
Build-i exited 0 with 0 errors / 82 warnings. Player-g exited 0 with 31/31 checks and no gameplay runtime
errors. Eleven distinct focused Editor regressions passed. Earlier Player-c separately verified crop
sprouts/maturity, pasture, female hunting/skinning and chopping; these unaffected routes were not rerun.
Final source fingerprint: `773907AF0100D6F418E7DA17725FCE5CCF8D38AE9F16F9D1E164639B36489010`.

## User decisions
- General building context-menu redesign is deferred. Questions: [WM15_BUILDING_ACTION_QUESTIONS.md](WM15_BUILDING_ACTION_QUESTIONS.md).
- Fish stock is finite per water body. No natural replenishment. Only fishing-camp breeding mode restores live fish. Manual fishing is three times slower than camp fishing.
- No full 100/300/500 performance gates. Do not repeat the earlier shutdown request.

## Implemented changes
- Owned looping animal clips; boar rig motion checked beyond three walk cycles. Wild mountain cattle use the locally imported Toon Farm bull model and its actual Idle/Walk clips (vendor cow controllers referenced a missing source and were not usable directly).
- One saved natural fish site per water body, shared by manual/camp fishing. Manual cycle 1 fish / 12 game minutes, camp 1 / 4; camp buffer 30. Breeding restores 6 live fish / 24 game minutes, capped by original capacity, produces no inventory item. Switching modes cancels the previous mode's task. Mode persists through saved workplace flags.
- Uniform separate swimming cells across lakes; independent phases/drift, no fishing circles. Visible fish track stock density. Geometry instantiated near the camera; one scene scheduler, no per-fish Update.
- Shoreline geometry and static construction masks are prepared once on a worker thread while the world loads, for lakes and river banks. Preview snaps to nearby candidates instead of restarting an exhaustive search for each mouse pixel. Entire occupied footprint remains dry; protected areas/occupancy still checked. Camp foundation tolerance 4.5 m and forward water reach 4.5 m.
- Buildings copied into WM15Evening and enlarged 1.5×; listed undersized facilities 2.25×, with matching grid footprints. Drawn plots/paths and compatible wall/gate modules keep their requested dimensions.
- Separate animal-pasture definition, 8×8–24×24 drag area, imported RTS stables shelter, fences/trough and saved breeding pair. Capacity up to 16, limited by area; no duplicate free breeding pairs from rebuilding pens.
- Crop progress advances within a day. Crop visual copies keep a usable LOD/render state. Growing meshes scale from sprouts to mature plants using existing crop assets.
- Rock depletion no longer shrinks the model. Chopping has a two-hand preparation/contact/follow-through pose. Bow and projectile model orientation corrected. Dead penned animals stop blocking navigation; the production action approaches the corpse work stance before skinning.
- Owned snowy quarry cliff replaces the mismatched mine. Ridge stone views use the matching snowy asset; finite stone sources keep independent views so exhausting a small rock cannot remove the industrial quarry.
- Mountain surface snow palette strengthened. Injury/illness notifications select player conditions and can focus canonical character positions when no active presentation exists.

## Verification so far
Unity executable: `C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe`.
All commands use `-batchmode -nographics -projectPath C:/serenity_game`.

- Compile-a found a local variable name conflict; corrected.
- Author-a/b corrected additional local name conflicts and a material adapter method-group mismatch.
- Author-c exposed broken vendor cow-controller clip references. Author-d uses the imported bull FBX clips and completed successfully (`-executeMethod Game.Infrastructure.Editor.WM15EveningAuthoring.Generate -quit -logFile C:/serenity_game/Artifacts/WM15Evening/author-d.log`).
- Editor-a: boar multi-cycle rig motion and within-day crop growth/save restore passed; breeding initially failed because the old task stayed assigned.
- Editor-b: breeding/save passed; ratio assertion exposed that the base recipe still used the old duration. Base and playable recipe aligned.
- Editor-c: shared finite stock/breeding/save/ratio passed; previous buffer test needed its assertion narrowed after adding the new stock site to its fixture.
- Editor-d: 30-unit buffer, transfer/resume and save round trip passed.
- Editor-e: normal research unlocks include breeding and the new pasture; passed.

Exact test invocation pattern:
`-runTests -testPlatform EditMode -testFilter <fully-qualified-test> -testResults C:/serenity_game/Artifacts/WM15Evening/editor-<letter>.xml -logFile C:/serenity_game/Artifacts/WM15Evening/editor-<letter>.log`
Filters: `Game.Tests.Editor.WM15EveningFeedbackTests` (a); `.FishingUsesFiniteSharedStockAndOnlyBreedingRestoresIt` (b/c); `Game.Tests.Editor.WM15MorningFeedbackTests.FishingCampCapsAtThirtyResumesAfterTransferAndPersistsContinuousWork` (c/d); `Game.Tests.Editor.WM15EveningFeedbackTests.NewContentIsAvailableThroughResearchWithoutCheats` (e). In c, both filters are separated by a semicolon.

Build-a was deliberately stopped before completion to include research wiring. Build-b uses:
`-executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/WorldMapEveningFix/Serenity.exe -quit -logFile C:/serenity_game/Artifacts/WM15Evening/build-b.log`

## Remaining acceptance work
- User visual review of crops, building proportions, snowy ridge/quarry, animal motion and work animation. Automated checks do not replace visual acceptance.
- Use a new game for this candidate: previous saves contain the retired multiple fish sources and old building footprints. No old saves have been deleted.
- General building menu remains deferred; user questions are linked above.

## Follow-up validation
- Build-b completed (exit 0), but Player-a exposed an obsolete 20–50 source-capacity guard during world creation. Candidate not delivered. Both population and ecology registrations now permit whole-water-body stocks while retaining small-deposit constraints for other resources.
- Editor-f: 2/3 passed (shared stock count/identity, dry reachable landing); finite ecology registration exposed the second capacity guard, corrected.
- Fish groups changed from radial formation to staggered schools with smooth independent drift; no circle geometry. River fishing chooses a nearby dry bank too. New orders recompute their bank approach rather than retaining the previous order's location.
- Shared-stock capacity is configured in WORLD_MAP_WATER.json. Old per-anchor authoring data remains for shoreline placement compatibility; runtime sources consolidate those anchors.
- Crop elapsed-time growth is interleaved with abstract daily work, preserving planting/harvest order during long advances.
- Editor-g runs the corrected finite-stock registration, within-day crop/save check and existing long-calendar food-supply regression. This is functional coverage, not the deferred population performance gates.
- Build-c follows Editor-g only on success, with the same build command and output path as build-b.

- Editor-g passed 3/3; nine distinct focused Editor regressions now pass across the recorded runs.
- Build-c: 0 errors, 82 warnings; fingerprint C63D307F42B6FC34703BE058BFF7B005598564F32161C4C70834E5ED5145CB3A. Player-b reached terrain creation but found a startup building overlap: the enlarged barracks still used fixed old placement coordinates. Not accepted for delivery.
- Starter barracks and enemy initial buildings now find a valid dry free footprint near their preferred anchor. Build-d caught the settlement-ID parameter type; corrected. Build-e recompiles the fix.
- Diagnostic startup failures now terminate promptly and retain their exception, rather than waiting for the review deadline.

- Build-e: succeeded, 0 errors / 82 warnings, fingerprint 68884B7CDA4112A8BBABEB3ADAE31B987CE7EEFF0984D0AC3FC2E21A0C9F55EF. Player-c loaded a new world, showed four crop types at sprouts/maturity, built a dedicated pasture and completed female hunting/skinning and chopping with impact fragments. Shore preview availability failed; river-bank selection sampled outside the world. Not accepted yet.
- Shore foundation allowance was inconsistent (4.5 m in domain/catalog but 3 m in the actual surface checks). Both surface callers now use 4.5 m. Editor-h verified 39 / 37 / 20 / 32 valid dry candidate anchors at the four lakes. Physical obstacles and protection remain checked at runtime.
- River and lake approach candidates are bounds-checked before terrain sampling. Cow habitats are restricted to alpine_scrub (the previous rocky_highland alias also included forest ravines). Snow quarry keeps the existing Serenity/SnowProps material instead of losing its snow treatment during generic material conversion.
- Build-f includes these corrections. Player-d will repeat only shoreline/fishing and the changed mountain/cattle visuals; already passed crop/hunting/chopping checks are not repeated.

- Build-f completed with 0 errors / 82 warnings, fingerprint 6911F5FE4C53BFB28691756EB5AFD366D1E8BA7DF202CF651F1569926D8A1EBC. Player-d passed the complete manual fishing route (dry stance, visible rod, nearby ground catch, production) without runtime errors. All four shoreline-preview availability checks still failed, so this candidate was not accepted.
- Candidate generation now starts before terrain loading and retains an in-progress search while new candidates arrive. The former late preparation kept resetting searches after world entry. Runtime diagnostics now retain the actual candidate rejection separately from the raw unsnapped water cursor.
- The remaining olive cave model was a separate POI, not the quarry catalog entry. An owned SnowCave copy now keeps the imported cave geometry with the same snow material as mountain rocks. Author-e completed; build-g includes it and the shoreline scheduling fix.

- Build-g: 0 errors / 82 warnings; fingerprint DB993EE3FC910B70BF8D1A7AE9B37A26227DCDB9B1BCAC75204DB86BD044357E. Player-e kept the fishing route error-free and visually confirmed the snowy cave. Moving coarse candidate generation earlier alone was insufficient: detailed terrain validation still took too many preview frames. Candidate not delivered.
- Final shoreline approach: `ShorePlacementIndex` is a pure domain preparation using its own height sampler. A worker task evaluates immutable water/mask/slope/height data during loading. The main thread imports its cell cache before exposing gameplay; current colliders, occupancy and construction funding are still checked at placement/confirmation. The cache holds up to 65,536 cells. Known validated anchor/orientation pairs skip duplicate immutable shoreline calculations.
- Editor-i passed the new prepared-index regression: 213 candidates, 18,702 cached cells; nearby choices at the four lakes 5/5/4/5. Every inspected footprint was ready even with a near-zero preparation budget. Eleven distinct focused Editor tests now pass. Command uses the prior test pattern with filter `Game.Tests.Editor.WM15ShorePlacementTests.PreparedIndexOffersFourNearbyChoicesPerLakeWithoutTerrainRecalculation`.
- Build-h includes the prepared index. Its targeted Player route additionally confirms one actual camp construction per lake and records worst preview call duration (150 ms functional freeze guard, not the deferred population benchmark).

## Preserved limits
- Full 100/300/500 population performance gates were not run.
- Main-world initial loading is still several minutes; preparation stages remain visible. This repair does not claim to resolve total loading time.
- The pre-existing Unity shutdown warning about two Persistent allocations remains; Player gameplay exception/error checks are reported separately.
- User/vendor asset imports and four pre-existing P3 material modifications are excluded from the repair commit. New presentation copies live under owned Game art folders.
- This is a repair candidate for user visual acceptance, not completion of all WM15 art and performance work.

- Build-h: 0 errors / 82 warnings; fingerprint 131C363D6528BD96490E716850B1D27F875EF441DE14483AA0A3A03863D4ACB7. Player-f confirmed camp construction on all four lakes and 5/5/4/5 nearby previews; worst calls 50.3/40.7/26.7/31.2 ms. Gameplay logged no runtime errors. With four construction orders active, the fisher started work away from its bank (30/31 checks), so the candidate was not accepted yet.
- Navigation arrival now requires physical proximity to the current requested endpoint; the previous path's zero remaining distance cannot complete a new movement. Partial paths are failures. Fishing execution also refuses to work away from its assigned stance and reissues the approach. Build-i compiles these fixes for a repeat of the same focused scenario.

## Final verification and commands
- Player-g: 31/31, exit 0, empty gameplay errors file. Each lake accepted construction; shore choices 5/5/4/5. Worst preview calls 32.5 / 38.8 / 24.6 / 41.7 ms. The fisher reached its bank stance (reported distance 0.00 m), held the rod and completed production with nearby dry ground output, with camp construction orders active.
- Viewed Player-c crop/pasture captures, Player-e snowy cave, Player-f quarry/shore preview and Player-g physical fishing stance. Snow quarry is an exposed snowy rock face using the imported cliff model; cave retains its entrance geometry.
- Build command: `"C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe" -batchmode -nographics -projectPath C:/serenity_game -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/WorldMapEveningFix/Serenity.exe -quit -logFile C:/serenity_game/Artifacts/WM15Evening/build-i.log`.
- Player command: `"C:/serenity_game/Builds/WorldMapEveningFix/Serenity.exe" -screen-fullscreen 0 -screen-width 1440 -screen-height 900 -wm15OctoberSmoke C:/serenity_game/Artifacts/WM15Evening/player-g -wm15Evening -wm15EveningFollowup -logFile C:/serenity_game/Artifacts/WM15Evening/player-g.log`, with `SERENITY_SAVE_DIRECTORY=C:/serenity_game/Artifacts/WM15Evening/player-g-saves`.
- Editor commands and exact filters are recorded above and in the XML files. Final evidence: `docs/validation/WM15Evening/`; screenshots/logs: `Artifacts/WM15Evening/`.
- `git diff --check -- Assets/Game docs` passed (line-ending notices only). No full population performance gates were run.
