# NEXT_TASK.md

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
