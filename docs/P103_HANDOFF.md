# P10.3 handoff — selection, context actions and queues

Status: **PARTIAL** on 2026-10-16. Implementation, automated acceptance and Windows build are complete. Only the required visible real-mouse Player route remains blocked by a Windows firewall/security prompt that automation may not operate.

## Implemented contract

- LMB selection, Shift toggle and drag-box multi-selection are centralized in `SelectionProbe`; selected residents persist by `StableEntityId` when Tier1 presenters are rebuilt or absent.
- Characters, buildings, natural sources and world piles bind stable hit proxies with colliders and hover/selection rings. Decorative geometry is skipped by selectable/context raycasts; Terrain remains a ground target.
- RMB without residents selected does nothing. With residents selected it opens an edge-clamped, scrollable menu containing all eight actions: Inspect, Gather, Mine, Chop, Haul, Build, Guard and Move. Unsupported actions stay visible with a reason.
- Domain `ContextActionRules` covers target, role, age, status, tool, technology, distance, path, resource, capacity, cooldown, busy, critical-needs and protected-phase reasons.
- `ContextCommandService` creates canonical orders. Normal commands go to the front; Shift appends. Safe active work is requeued, while carrying/non-interruptible phases and critical hunger/rest delay execution.
- The local game runs AI in `NeedsOnly`: residents start idle and autonomously handle only hunger/rest. Legacy autonomous work remains explicit, disabled-by-default compatibility behavior.
- Snapshot v21 persists queue order and cursors. v18–v20 remain readable; selection/order continuity is covered through Save/Load and Tier1↔Tier2↔Tier1.

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

Affected EditMode (`P103;WorkGroupAndJobTests;Tier1UtilityAiTests;TierManagerTests;PersistenceTests;P102ConnectedWorldTests`): 92/92 passed.

Affected PlayMode (`P103;P102ConnectedWorldPlayModeTests;P94StoneAgeSettlementPlayModeTests;ThirdPersonControlPlayModeTests`): 19/19 passed.

Broad diagnostic EditMode: 536/538 on the first run. The P10.2 version assertion was stale and corrected to serialize explicit v20; the remaining P8.2 `NaturalMapKeepsFiniteBoundaryOnTheSupersedingTerrain` failure reproduces alone against the unchanged P10.2 scene and is outside P10.3.

Broad headless PlayMode: 141/159. Failures include tests whose old contract expects automatic production/haul/research or immediate RMB movement, the GPU-only U31 test correctly rejecting `-nographics`, and existing connected-world placement/NavMesh assumptions. The focused and affected P10.3 suites above are green; the broad XML is retained without claiming it passed.

## Build

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -executeMethod U00Build.WindowsDevelopment -serenityBuildPath 'C:\serenity_game\Builds\P103\Serenity.exe' -quit -logFile 'C:\serenity_game\Artifacts\P103\build.log'
```

Result: `Build Finished, Result: Success` and `U00_BUILD_RESULT: Succeeded; errors=0; warnings=44`. `Serenity.exe` was produced. The first dirty-tree provenance pass did not emit `build-provenance.json` because Unity observed transient source-manifest differences; rerun from the committed tree before final visible acceptance.

## Remaining visible Player route

1. Dismiss the Windows firewall/security prompt manually. Automation must not click Allow or Cancel.
2. Launch the committed Player at exact 1920×1080, windowed, D3D11, using isolated P10 save/settings paths.
3. Verify a resident is idle at start; LMB selects, Shift toggles a second resident, and a drag box selects a group without decoration stealing the click.
4. With no resident selected, LMB a building/source/pile and confirm the right inspector is readable and scrollable; RMB does not issue a resident command.
5. Select a resident, RMB a natural source, pile, building, another Character and ground. Confirm the menu always shows eight actions, disabled reasons are readable, and menus near all four screen edges remain fully reachable.
6. Issue Move normally, then Shift+Move; confirm the first is at queue front and the second at the tail. Exercise Gather/Mine/Chop, Haul, Build, Inspect and Guard against suitable targets.
7. Confirm critical hunger/rest delays a queued manual command and the command resumes afterward; a protected carrying phase is not broken.
8. Save/Load and transition the selected resident Tier1→Tier2→Tier1; confirm selected stable identity and queue order survive.
9. Capture screenshots and Player log under `Artifacts/P103`, copy final evidence to `docs/validation/P103`, then mark P10.3 DONE and advance `NEXT_TASK` to P10.4.

Do not start P10.4 until this route is complete. Preserve P10.2 p95 128.342 ms as P10.9 debt. P8.6/U29/U30/U31 remain unchanged.
