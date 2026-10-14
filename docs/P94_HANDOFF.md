# P9.4 handoff — Stone Age settlement, resources and interaction

P9.4 is DONE as of 2026-10-15. Commit only this task; do not push. P9.5 is the next active task.

## Delivered

- Replaced the primitive visible shelter and storage while preserving their prefab GUIDs; added a distinct crop-plot prefab instead of reusing storage art.
- Added seven reusable project-owned P9.4 meshes, ten URP materials and two small authored textures. Only the registered official Terrain Sample pine bark texture/normal is reused; no new external asset or dependency was imported.
- Added six readable static work sites: knapping, hide working, woodworking, drying, hauling and construction.
- Rebuilt the four existing camp pile roots as detailed wood/fibre/stone/hide supplies without changing their canonical inventory owners, IDs, colliders or presenters.
- Added `P94StoneAgeSettlement` deterministic metadata/validation and one `P94WorkOrderIndicatorSystem`: a scene-level 4 Hz pass capped at 24 open jobs. It projects existing target/priority/status and owns no gameplay state or per-NPC loop.
- Added a reproducible P9.4 builder, focused EditMode/PlayMode tests, Windows Player smoke and `Tools/Verify-P94.ps1`.
- Static P9.4 clothing overlays were removed after Player review: wrapper-root geometry did not follow the UMA/fallback skeleton and visually degraded TPS. Character polish remains optional/deferred rather than shipping that artifact.

Canonical buildings, construction costs, finite resources, production, work groups/orders, `StableEntityId`, NavMesh, weather and snapshot state are unchanged. Save/Load reconstitutes presenters and priority indicators from the existing authorities.

## Final verification

Successful commands:

```powershell
$args = @('-batchmode','-nographics','-quit','-projectPath','C:\serenity_game','-logFile','C:\serenity_game\Logs\P94-generate-final.log','-executeMethod','Game.Infrastructure.Editor.P94StoneAgeSettlementBuilder.Build'); $p = Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -ArgumentList $args -WindowStyle Hidden -PassThru; $p.WaitForExit()

$args = @('-batchmode','-nographics','-projectPath','C:\serenity_game','-logFile','C:\serenity_game\Logs\P94-focused-editmode-final.log','-runTests','-testPlatform','EditMode','-testCategory','P94','-testResults','C:\serenity_game\Logs\P94-focused-editmode-final.xml'); $p = Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -ArgumentList $args -WindowStyle Hidden -PassThru; $p.WaitForExit()

$args = @('-batchmode','-nographics','-projectPath','C:\serenity_game','-logFile','C:\serenity_game\Logs\P94-focused-playmode-final.log','-runTests','-testPlatform','PlayMode','-testCategory','P94','-testResults','C:\serenity_game\Logs\P94-focused-playmode-final.xml'); $p = Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -ArgumentList $args -WindowStyle Hidden -PassThru; $p.WaitForExit()

$args = @('-batchmode','-nographics','-projectPath','C:\serenity_game','-logFile','C:\serenity_game\Logs\P94-affected-editmode.log','-runTests','-testPlatform','EditMode','-testCategory','P83|P92|P93','-testResults','C:\serenity_game\Logs\P94-affected-editmode.xml'); $p = Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -ArgumentList $args -WindowStyle Hidden -PassThru; $p.WaitForExit()

$args = @('-batchmode','-nographics','-projectPath','C:\serenity_game','-logFile','C:\serenity_game\Logs\P94-affected-playmode.log','-runTests','-testPlatform','PlayMode','-testCategory','P83|P92|P93','-testResults','C:\serenity_game\Logs\P94-affected-playmode.xml'); $p = Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -ArgumentList $args -WindowStyle Hidden -PassThru; $p.WaitForExit()

$args = @('-batchmode','-nographics','-projectPath','C:\serenity_game','-logFile','C:\serenity_game\Logs\P94-build-final3.log','-quit','-executeMethod','U00Build.WindowsDevelopment','-serenityBuildPath','Builds/P94/Serenity.exe'); $p = Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -ArgumentList $args -WindowStyle Hidden -PassThru; $p.WaitForExit()

$out='C:\serenity_game\Logs\P94-player-smoke-20261015-013430'; New-Item -ItemType Directory -Force -Path $out | Out-Null; $oldSave=$env:SERENITY_SAVE_DIRECTORY; $oldSettings=$env:SERENITY_SETTINGS_PATH; $env:SERENITY_SAVE_DIRECTORY=Join-Path $out 'saves'; $env:SERENITY_SETTINGS_PATH=Join-Path $out 'settings\user-settings.json'; try { $args=@('-logFile',(Join-Path $out 'Player.log'),'-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-force-d3d11','-p94Smoke','-p94SmokeOutput',$out); $p=Start-Process -FilePath 'C:\serenity_game\Builds\P94\Serenity.exe' -ArgumentList $args -WindowStyle Normal -PassThru; $p.WaitForExit() } finally { $env:SERENITY_SAVE_DIRECTORY=$oldSave; $env:SERENITY_SETTINGS_PATH=$oldSettings }
```

Results:

- P9.4 focused EditMode: 3/3 passed.
- P9.4 focused PlayMode: 3/3 passed.
- Affected P8.3/P9.2/P9.3 EditMode: 12/12 passed.
- Affected P8.3/P9.2/P9.3 PlayMode: 9/9 passed.
- Windows x64 Mono Development build: `U00_BUILD_RESULT: Succeeded; errors=0; warnings=39` in `Logs/P94-build-final3.log`.
- Visible 1280×720 D3D11 normal-Player smoke: all flags passed; 6 work sites, 4 supply presentations, 43/27/26 shelter/storage/crop renderers, layout hash `681E040B60C66ED2`.
- Final inspected evidence: `Logs/P94-player-smoke-20261015-013430/p94-player-smoke.json`, `p94-rts-work.png`, `p94-tps-settlement.png` and `Player.log`.

## Diagnostic history retained

- The first sandboxed generator launch crashed inside Unity `BuildReportRestService`/Mono `HttpListener` before project code; the approved outside-sandbox command succeeded.
- Initial focused tests exposed incorrect test assumptions (camp piles versus natural production sites, and an initially locked demo crop presenter). Tests were corrected to the actual canonical boundaries; final focused and affected suites are green.
- The first Player smoke exposed an invalid synthetic group reassignment before Save/Load. The final route keeps group membership valid and uses the two existing Tier1 residents; canonical code was not weakened.
- Visual inspection rejected static wrapper-root clothing and then identified the existing aggregate lightweight crowd draw in the paused evidence frame. The clothing was removed. The smoke suppresses that draw only after the functional RTS frame so the clean TPS image contains authored settlement art rather than diagnostic capsules.
- Unity Package Manager repeatedly emitted two `Curl error 28` timeouts of about 300 seconds before successful normal builds. A single `-noUpm` diagnostic launch omitted package assemblies and failed compilation; it was abandoned. The final normal build succeeded.
- On this host, `Start-Process` sometimes retained a stale process handle after Unity had exited; final success is taken only from the completed build log marker and resulting Player smoke, not from the stalled wrapper.

## Next task boundary

P9.5 may tune atmosphere, lighting, fog, water, weather/season/day-night projection and the six-location global map. It must not move canonical world, resource, building or campaign authority. P8.6 remains PARTIAL, U29 remains PARTIAL, U30 remains IN_PROGRESS and U31 remains BLOCKED.
