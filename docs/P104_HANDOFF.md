# P10.4 handoff — natural resources and industrial extraction

Status: **PARTIAL** on 2026-10-17. `NEXT_TASK.md` must remain P10.4; do not start P10.5.

## Implemented

- Exactly nine catalog resources: `wood_log`, `stone`, `plant_fiber`, `hide`, `berries`, `iron_ore`, `gold_ore`, `meat`, `fish`.
- Animal hunting yields hide+meat; lake/water fishing yields fish.
- All natural source instances are stable-ID `ProductionSite` values with deterministic 20–50-unit capacity.
- `ResourceEcologyService` records depletion and retries a deterministic biome candidate after exactly 15 game days; occupied points defer without overlap.
- Multiple workers may reserve the same source. Completion is atomic at the final unit; controlled tests prove seven equivalent workers yield exactly 7× one worker when travel/haul are excluded.
- Six large zones cover forest workshop, quarry, iron mine, gold mine, fishing station and hunting lodge. `IndustrialExtractionService` validates technology, placement, one real funding inventory, real storage, worker capacity, output cycles, depletion and mine-only restoration.
- Industrial output is a canonical `WorldPile` inventory, so existing hauling/logistics remains authoritative.
- Normal UI selection/hover shows quantities and every output; industrial inspector exposes construction, 1/7-worker shifts, restoration, hauling and explicit failure reasons.
- The same-world strategic view retains connected-world fog and adds nine natural/industrial filters with revealed quantities.
- Research adds industrial extraction and restorative mining; Stone Age mastery depends on restoration.
- Snapshot v22 persists ecology/industry inside production; v21 remains readable and intentionally omits the new fields.

## Verification executed

All commands used Unity `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe` and project `C:\serenity_game`.

```powershell
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter P104 -testResults C:\serenity_game\Artifacts\P104\p104-editmode-final.xml -logFile C:\serenity_game\Artifacts\P104\p104-editmode-final.log
```

Result: **5/5 passed**.

```powershell
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter P104 -testResults C:\serenity_game\Artifacts\P104\p104-playmode-final-2.xml -logFile C:\serenity_game\Artifacts\P104\p104-playmode-final-2.log
```

Result: **2/2 passed**.

```powershell
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter 'P104;ProductionAndFarmingTests;ResearchFrameworkTests;PersistenceTests;P102ConnectedWorldTests;P103ContextCommandTests;PlayerControlStateTests' -testResults C:\serenity_game\Artifacts\P104\affected-editmode-final.xml -logFile C:\serenity_game\Artifacts\P104\affected-editmode-final.log
```

Result: **93/93 passed**.

```powershell
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter 'P104;P103;P102ConnectedWorldPlayModeTests;P93PopulatedBiomeWorldPlayModeTests;P94StoneAgeSettlementPlayModeTests;ProductionPlayModeTests' -testResults C:\serenity_game\Artifacts\P104\affected-playmode-final.xml -logFile C:\serenity_game\Artifacts\P104\affected-playmode-final.log
```

Result: **14/15 passed**. Only `ProductionPlayModeTests.Tier1WalksWorksAndCommitsOneCanonicalProductionOutput` failed: it observed a producer but did not observe `Working` within the test's 25-second real-time path window. The isolated retry `Artifacts/P104/production-tier1-final.xml` reproduced the timing failure. Do not relabel this as green.

```powershell
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -executeMethod U00Build.WindowsDevelopment -serenityBuildPath C:\serenity_game\Builds\P104\Serenity.exe -quit -logFile C:\serenity_game\Artifacts\P104\build-final.log
```

Result: build binary produced; log reports **Succeeded; errors=0; warnings=44**. Unity/package/post-build orchestration stalled and the wrapper was terminated after the success marker; `Builds/P104/build-provenance.json` was not written. This is not a complete provenance gate.

Visible launch contract:

```powershell
Serenity.exe -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -force-d3d11 -logFile C:\serenity_game\Artifacts\P104\player-retry\Player.log
```

Observed through the normal UI: Main Menu → New Game → LocalGameplay → world-map panel → same-world strategic view. The HUD visibly contained all nine resources. The strategic view visibly contained all nine resource filters and a revealed, fog-authorized `Брёвна 46/46` marker. `Player.log` confirms forced Direct3D 11.0 feature level 11.1. Existing UMA atlas warnings remain, and the final log audit found `Crash!!!` after Alt+F4 with a native UnityPlayer/UIAutomation shutdown stack; the route is therefore not a clean Player pass. The screenshots were inspected live through Computer Use but were not persisted as repository evidence; no P10.4 telemetry JSON was generated.

## Remaining blockers before DONE

1. Complete a cached Windows build through the post-build source-manifest step and retain `Builds/P104/build-provenance.json`.
2. Make the affected U15 Tier1 physical walk regression green without weakening its semantic assertion, then rerun the affected PlayMode filter.
3. Capture saved normal visible D3D11 screenshots/log/telemetry proving selection/inspection and ordinary commands for natural sources and industrial zones; facility research/build/cost/storage; real output hauling; controlled 1× and 7×; exhaustion/disappearance; blocked-point 15-day respawn; mine-only restoration; and Save/Load continuity.
4. Complete that Player route with a clean exit and investigate/retest the observed Alt+F4 native UnityPlayer/UIAutomation shutdown crash.
5. Re-scan the final Player log and record all warnings/errors honestly.

P10.3 remains PARTIAL under D-141. Its physical Shift-click/Shift-command, slow multi-frame drag, needs/protected-carry, Tier transition and target/action-matrix debt is unchanged and must not be claimed by P10.4 evidence.
