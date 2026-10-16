# P10.3 handoff — selection, context actions and queues

Status: **PARTIAL** on 2026-10-16. Implementation, focused automated acceptance, provenance build and a substantial visible Player route are complete. The firewall prompt was dismissed by the user. Physical Shift/multi-frame-drag and state-forcing portions of the visible route remain unobserved, so this is not marked DONE.

## Implemented contract

- LMB selection, Shift toggle and drag-box multi-selection are centralized in `SelectionProbe`; selected residents persist by `StableEntityId` when Tier1 presenters are rebuilt or absent.
- Characters, buildings, natural sources and world piles bind stable hit proxies with colliders and hover/selection rings. Decorative geometry is skipped by selectable/context raycasts; Terrain remains a ground target.
- RMB without residents selected does nothing. With residents selected it opens an edge-clamped, scrollable menu containing all eight actions: Inspect, Gather, Mine, Chop, Haul, Build, Guard and Move. Unsupported actions stay visible with a reason.
- Domain `ContextActionRules` covers target, role, age, status, tool, technology, distance, path, resource, capacity, cooldown, busy, critical-needs and protected-phase reasons.
- `ContextCommandService` creates canonical orders. Normal commands go to the front; Shift appends. Safe active work is requeued, while carrying/non-interruptible phases and critical hunger/rest delay execution.
- The local game runs AI in `NeedsOnly`: residents start idle and autonomously handle only hunger/rest. Legacy autonomous work remains explicit, disabled-by-default compatibility behavior.
- Snapshot v21 persists queue order and cursors. v18–v20 remain readable; selection/order continuity is covered through Save/Load and Tier1↔Tier2↔Tier1.
- Context-menu commands capture the stable selected roster before UI dispatch and restore it after the UI event, so the same click cannot fall through and clear/replace command recipients.
- The production UI Load path no longer clears selection after the snapshot service has restored stable IDs.

## Automated validation

Focused EditMode:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -runTests -testPlatform EditMode -testFilter 'P103' -testResults 'C:\serenity_game\Artifacts\P103\p103-editmode-final.xml' -logFile 'C:\serenity_game\Artifacts\P103\p103-editmode-final.log'
```

Result: 17/17 passed.

Focused PlayMode:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -runTests -testPlatform PlayMode -testFilter 'P103' -testResults 'C:\serenity_game\Artifacts\P103\p103-playmode-final.xml' -logFile 'C:\serenity_game\Artifacts\P103\p103-playmode-final.log'
```

Result: 2/2 passed.

Post-visible-route regressions:

- `Artifacts/P103/p103-editmode-ui-shield.xml`: 17/17 passed after the menu click-through fix.
- `Artifacts/P103/p103-playmode-ui-load.xml`: 2/2 passed after the UI Load continuity fix. The test executes the real `GameplayUiController.ConfirmLoadSlot` path and verifies the selected stable ID survives.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -runTests -testPlatform EditMode -testFilter 'P103' -testResults 'C:\serenity_game\Artifacts\P103\p103-editmode-ui-shield.xml' -logFile 'C:\serenity_game\Artifacts\P103\p103-editmode-ui-shield.log'
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -runTests -testPlatform PlayMode -testFilter 'P103' -testResults 'C:\serenity_game\Artifacts\P103\p103-playmode-ui-load.xml' -logFile 'C:\serenity_game\Artifacts\P103\p103-playmode-ui-load.log'
```

Affected EditMode (`P103;WorkGroupAndJobTests;Tier1UtilityAiTests;TierManagerTests;PersistenceTests;P102ConnectedWorldTests`): 92/92 passed.

Affected PlayMode (`P103;P102ConnectedWorldPlayModeTests;P94StoneAgeSettlementPlayModeTests;ThirdPersonControlPlayModeTests`): 19/19 passed.

Broad diagnostic EditMode: 536/538 on the first run. The P10.2 version assertion was stale and corrected to serialize explicit v20; the remaining P8.2 `NaturalMapKeepsFiniteBoundaryOnTheSupersedingTerrain` failure reproduces alone against the unchanged P10.2 scene and is outside P10.3.

Broad headless PlayMode: 141/159. Failures include tests whose old contract expects automatic production/haul/research or immediate RMB movement, the GPU-only U31 test correctly rejecting `-nographics`, and existing connected-world placement/NavMesh assumptions. The focused and affected P10.3 suites above are green; the broad XML is retained without claiming it passed.

## Build

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -executeMethod U00Build.WindowsDevelopment -serenityBuildPath 'C:\serenity_game\Builds\P103\Serenity.exe' -quit -logFile 'C:\serenity_game\Artifacts\P103\build-final-ea55dd7.log'
```

Result: `Build Finished, Result: Success` and `U00_BUILD_RESULT: Succeeded; errors=0; warnings=26`.

- source commit: `ea55dd712d9f84dc39699abe9f067eb8a289dbdd`
- source fingerprint: `C28A07678081694B4DC7305A238BE3A05B89207D9E2CAE1C9D4AC008F2A38343`
- before/after source manifests: identical SHA-256 above
- build GUID: `6b60fff2e13a490d96195ffbf0ba616e`
- Unity: `6000.6.4f1`
- provenance: `Builds/P103/build-provenance.json`

The build itself completed before the long post-build source hash. The elevated Editor remained responsive and consuming CPU; it was allowed to exit normally, after which Unity wrote the matching after-manifest and provenance. No manually fabricated provenance is used.

## Visible Player evidence

Launch contract: normal windowed Player with `-screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -force-d3d11`, plus `SERENITY_SAVE_DIRECTORY` and `SERENITY_SETTINGS_PATH` pointing to isolated `Artifacts/P103/player-final*` paths. `Player.log` confirms `Forcing GfxDevice: Direct3D 11` and Direct3D 11.0 feature level 11.1. The precise exception/assertion/crash scan returned no match. The log is not otherwise warning-free: it records the existing ICU fallback and UMA missing-atlas-area warnings, and shutdown reports two persistent allocations. Those warnings are preserved in `player-final3.log` and are not classified as P10.3 acceptance passes.

Observed with real mouse input:

1. Main Menu → New Game → ruler editor → Start.
2. LMB selected Tarin; the selection ring and right inspector agreed and the resident began idle.
3. RMB on ground and a building opened the same always-visible eight-action menu. Unsupported actions retained readable reasons, and the menu remained reachable at the right screen edge.
4. Ordinary Move and Guard commands each reported one order. Selection remained on Tarin after command dispatch.
5. Saves → Save slot 1 → Load confirmation → Load preserved Tarin's selection ring and inspector through the production UI path.

The first menu click initially passed through the UI and cleared selection (`Идти: приказов 0`). Commit `32b0260` fixed dispatch shielding and stable-roster restoration. The first UI Load then exposed an explicit `ClearSelection`; commit `ea55dd7` removed it and added the regression above. Screenshots and the final Player log are copied to `docs/validation/P103`.

The JPEGs are Windows application-window captures at 1282×751 logical pixels because display DPI scaling is active. They are evidence of the visible route, not a claim that the files themselves are 1920×1080 screenshots; the Player was requested at exact 1920×1080 by its launch arguments.

## Remaining visible Player route

1. Launch `Builds/P103/Serenity.exe` with the exact windowed 1920×1080/D3D11 and isolated save/settings contract above.
2. Physically Shift-click a second resident, then Shift-click again to remove that resident while preserving the first.
3. Hold a drag box across multiple rendered frames and prove group selection without decorative geometry stealing the gesture. The available automation drag completed inside one frame and was interpreted as a click, so it is not a pass.
4. With a resident selected, physically hold Shift while clicking Move in the contextual menu. Prove the prior ordinary command remains at the front and the Shift command is appended at the tail. The automation API cannot hold Shift across a mouse click; keyboard-focused Shift+Return/Space did not activate the button and is not a pass.
5. With no resident selected, LMB a building, natural source and pile; confirm the inspector is readable/scrollable and RMB issues no resident command.
6. With residents selected, complete the natural-source, pile and another-Character target matrix. Exercise suitable Gather/Mine/Chop, Haul, Build and Inspect actions in addition to the already observed Move/Guard, and check all four screen edges rather than only the observed right edge.
7. Force a critical hunger/rest condition through an accepted gameplay/test control, show that it delays and then resumes a queued manual command, and show that a protected carrying phase is not broken.
8. Force Tier1→Tier2→Tier1 through an accepted gameplay/test control and prove selected stable identity plus queue order survive. Save/Load selection continuity through the normal UI is already visibly green; include queued-order continuity in the final route.
9. Add the remaining screenshots/log/telemetry to `docs/validation/P103`, then mark P10.3 DONE and advance `NEXT_TASK` to P10.4.

Do not start P10.4 until this route is complete. Preserve P10.2 p95 128.342 ms as P10.9 debt. P8.6/U29/U30/U31 remain unchanged.
