# P8.5 handoff — cohesive polished interface

## Delivered

P8.5 gives the shipped Russian interface one production-facing visual language derived from the accepted P8 natural direction: dark moss and stone surfaces, warm firelight accents, restrained borders, readable hierarchy and consistent interactive states. It covers:

- Main Menu and its empty/disabled states;
- the two-column ruler identity, full-body preview and appearance editor;
- local RTS HUD, navigation, main panels, onboarding, notifications, inspector, status bar and confirmations;
- global-map panels plus the legacy IMGUI resource/developer controls;
- the TPS HUD and compact long-text treatment.

`SerenityUiDesign` supplies shared presentation constants and component helpers; `SerenityRuntimeTheme.tss` supplies the reusable USS vocabulary. Normal, hover, active, keyboard focus, selected, disabled, success, warning and danger states are distinguishable. Long Russian text wraps, dense regions scroll, safe-area offsets are applied and the accepted floor is 1280×720 at 125% UI scale.

The 1280×720 pass fixed real layout defects found in visible Player captures: menu horizontal overflow, ruler-editor wrapping, onboarding/inspector overlap, bright default scrollbars and an over-wide TPS hint. The final TPS hint wraps inside the center viewport without covering the right inspector.

## Authority and compatibility boundary

This is a Presentation-only change. Existing named controls and public test hooks remain stable. Buttons still delegate to the same New Game, Save/Load, work, research, event, map, combat, tier and settings services. UI folding, selection and modal state are disposable and are not serialized.

World-pointer blocking now converts GUI screen coordinates to panel coordinates and tests actual visible element bounds. It does not replace world selection authority. The global-map IMGUI restyle retains the existing resource/work delegates. No new Asset Store dependency was added, no canonical schema changed and no per-NPC `Update` loop was introduced.

## Automated verification

All commands used Unity 6000.6.4f1 from `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe` and project `C:\serenity_game`.

```powershell
# Focused EditMode
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testCategory P85 -testResults C:\serenity_game\Logs\P85-focused-editmode-final2.xml -logFile C:\serenity_game\Logs\P85-focused-editmode-final2.log
# Passed 3/3.

# Focused headless PlayMode
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testCategory P85 -testResults C:\serenity_game\Logs\P85-focused-playmode-final5.xml -logFile C:\serenity_game\Logs\P85-focused-playmode-final5.log
# Passed 2/2.

# Focused D3D11 PlayMode
Unity.exe -batchmode -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testCategory P85 -force-d3d11 -testResults C:\serenity_game\Logs\P85-gpu-playmode-final5.xml -logFile C:\serenity_game\Logs\P85-gpu-playmode-final5.log
# Passed 2/2.

# Affected EditMode: GameplayUiTests, P1 localization, P2 startup and P8.5 UI.
# Logs/P85-affected-editmode-final.xml: passed 10/10.

# Affected PlayMode: P1 localization, P2 menu, Gameplay UI, global map, TPS and P8.5 UI.
# Logs/P85-affected-playmode-final2.xml: passed 26/26.

# Windows x64 Mono Development build
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/P85/Serenity.exe -quit -logFile C:\serenity_game\Logs\P85-build-final13.log
# Build Finished, Result: Success.
# U00_BUILD_RESULT: Succeeded; errors=0; warnings=9.
```

`Tools/Verify-P85.ps1` reproduces the focused, affected, build and visible-player route, including a hard 1280×720 check for every PNG. On this host Unity sometimes remained alive after writing a completed test/build marker; only the exact batch PID was stopped after its result was captured.

## Visible Player evidence

The final visible D3D11 Development Player used windowed 1280×720 and a forced 125% UI scale. `Logs/P85-player-smoke-final5/p85-player-smoke.json` reports `passed: true` for resolution, scale, Main Menu, ruler editor, local RTS, global map, TPS and stable hooks. Each image is exactly 1280×720 and was visually inspected:

- `p85-main-menu.png`
- `p85-ruler-editor.png`
- `p85-local-rts.png`
- `p85-global-map.png`
- `p85-tps.png`

The Player log retains inherited UMA/sample warnings and reports two persistent allocations during shutdown. P8.5 does not hide or close that diagnostic; P8.6 owns native-memory investigation and U31 qualification.

## Acceptance and next task

Automation demonstrates presentation consistency, layout bounds, contrast helpers, stable hooks and the visible route. It does not decide subjective taste or prolonged usability. P3/P5 visual acceptance, P6 first-hour acceptance and P7 campaign pacing remain open human checks.

No complete U29 matrix, eight-hour U30 route or U31 final gate was run. U29 remains PARTIAL, U30 remains IN_PROGRESS and U31 remains BLOCKED. The only next unblocked product task is P8.6 integration and qualification.
