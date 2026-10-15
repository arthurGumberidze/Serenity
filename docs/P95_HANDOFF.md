# P9.5 handoff — Atmosphere and global map

P9.5 is DONE as of 2026-10-15. Commit only this task; do not push. P9.6 is the next active task.

## Delivered

- Added the project-owned `P95NaturalAtmosphereProfile` definition asset. It validates exactly six canonical location IDs and has deterministic layout hash `84BE8C806F7B2B61`.
- Upgraded the existing scene-level weather presentation to evaluate the canonical `GameClock` tick and `WeatherService` state continuously: solar day/night direction and colour, soft shadows, ambient/sky/fog, water response, centralized rain/snow and the established seasonal Terrain projection.
- Rebuilt the additive GlobalMap presentation as runtime island relief with surrounding water, a river, raised routes, location-specific natural affordances and permanent Russian labels for all six locations. Existing resource/unit/travel/battle markers and commands remain bound to the same services.
- Added a reproducible P9.5 builder, focused EditMode/PlayMode tests and a normal-Player smoke covering local RTS/TPS, global map, six locations, weather/seasons/day-night, water/collision, NavMesh and Save/Load.
- Added no package, external asset, canonical mutable state or snapshot field. Presentation objects remain replaceable.

## Authority and known boundaries

`GameClock`, `WeatherService`, `GlobalMapService`, `WorldLocationId`, finite production sources, travel/campaign state, building/work services and Save/Load remain authoritative. The profile contains definitions only. Global relief and labels are recreated scene-lifetime art. No per-NPC `Update` was added.

The P9.4 Character caveat remains: static wrapper-root clothing was rejected because it did not follow the skeleton. The existing centralized lightweight aggregate crowd draw may still be visible in RTS. P9.5 does not claim either issue solved; P9.6 must keep this explicit during integrated visual acceptance.

## Final verification

Unity executable: `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`.

Successful commands (the early focused EditMode and affected regression launches used direct PowerShell invocation and were accepted only after their NUnit XML appeared; later generator/PlayMode/build/Player processes used `Start-Process -PassThru` and `WaitForExit()`):

```powershell
$args=@('-batchmode','-nographics','-projectPath','C:\serenity_game','-logFile','C:\serenity_game\Logs\P95-build-content-final2.log','-quit','-executeMethod','Game.Infrastructure.Editor.P95AtmosphereAndGlobalMapBuilder.Build')
$p=Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -ArgumentList $args -WindowStyle Hidden -PassThru; $p.WaitForExit()

& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\P95-editmode.log' -runTests -testPlatform EditMode -testCategory P95 -testResults 'C:\serenity_game\Logs\P95-editmode.xml'

$args=@('-batchmode','-nographics','-projectPath','C:\serenity_game','-logFile','C:\serenity_game\Logs\P95-playmode-final4.log','-runTests','-testPlatform','PlayMode','-testCategory','P95','-testResults','C:\serenity_game\Logs\P95-playmode-final4.xml')
$p=Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -ArgumentList $args -WindowStyle Hidden -PassThru; $p.WaitForExit()

& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\P95-affected-playmode.log' -runTests -testPlatform PlayMode -testCategory 'P94|U19|U25' -testResults 'C:\serenity_game\Logs\P95-affected-playmode.xml'

$args=@('-batchmode','-nographics','-projectPath','C:\serenity_game','-logFile','C:\serenity_game\Logs\P95-windows-build-final4.log','-quit','-buildTarget','Win64','-executeMethod','U00Build.WindowsDevelopment')
$p=Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -ArgumentList $args -WindowStyle Hidden -PassThru; $p.WaitForExit()

$out='C:\serenity_game\Logs\P95\player-smoke-final4'; New-Item -ItemType Directory -Force -Path $out | Out-Null
$args=@('-screen-width','1600','-screen-height','900','-screen-fullscreen','0','-force-d3d11','-p95Smoke','-p95SmokeOutput',$out,'-logFile',(Join-Path $out 'Player.log'))
$p=Start-Process -FilePath 'C:\serenity_game\Builds\Windows\Serenity.exe' -ArgumentList $args -WindowStyle Normal -PassThru; $p.WaitForExit()
```

Results:

- Focused P9.5 EditMode: 2/2 passed.
- Final focused P9.5 PlayMode: 2/2 passed, including GlobalMap unload and restoration of the local camera and atmosphere.
- Affected P9.4/U19 Weather/U25 GlobalMap PlayMode: 8/8 passed.
- Final Windows x64 Mono Development build: `U00_BUILD_RESULT: Succeeded; errors=0; warnings=39`; `UNITY_EXIT=0`; build GUID `42202671cbfc44ecab7745163c076124`; source fingerprint `C8C59AB28BE88713475AE268B92FD9C71B511CB9349F0DB14C19813328679B8C`.
- Visible 1600×900 D3D11 normal-Player smoke: `PLAYER_EXIT=0`, every JSON flag true, layout hash `84BE8C806F7B2B61`.
- Final inspected evidence: `Logs/P95/player-smoke-final4/p95-player-smoke.json`, `p95-local-rts-day.png`, `p95-local-tps-day.png`, `p95-global-map.png` and `Player.log`.

## Diagnostic history retained

- The first generated ScriptableObject asset had `m_Script: {fileID: 0}` because the source filename did not match `P95NaturalAtmosphereProfile`. The source was renamed, the generated asset was recreated, and subsequent scene loading/tests passed.
- The first inspected global image was over-zoomed and too dark. The camera was reframed and a map-only night contrast floor added. The second image exposed oversized/buried location markers; their scale and relief-relative height were corrected and permanent labels added. The third/final image visibly contains all six labelled locations.
- After adding the return-to-local lifecycle assertion, one intermediate PlayMode run checked the local camera in the same frame that scene unloading completed and failed 1/2. The assertion now waits one presentation frame; the final run passed 2/2 and proves camera plus atmosphere restoration.
- An early asynchronous build launch left one Unity process hashing the full source manifest while later attempts hit its project lock. The exact process was identified and stopped only after it failed to update the Player and continued holding the lock. All final Unity commands were awaited synchronously.
- Normal build startup twice emitted inherited `Curl error 28` timeouts of about 300 seconds, then continued. `U00Build` also intentionally hashes the full source tree before and after the build; final before/after manifests match and the build exited 0.
- Headless PlayMode weather previews mutate the existing shared P9.2 seasonal Terrain material in the Editor. The tracked material value was restored after test runs; no P9.2 material change is part of P9.5.

## Next task boundary

P9.6 performs the ordinary end-to-end player route and subjective visual acceptance at 1280×720 plus higher resolution. It may fix integration/readability defects, but must not move canonical authorities or silently claim the P9.4 Character/crowd caveats solved. P8.6 remains PARTIAL, U29 remains PARTIAL, U30 remains IN_PROGRESS and U31 remains BLOCKED; do not run those gates in P9.6 unless separately authorized.
