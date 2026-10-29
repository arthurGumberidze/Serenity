# WM15 direct user-retest repair validation — 2026-10-29

## Delivered build

- Player: `Builds/WM15UserRetestFinal/Serenity.exe`
- Unity: 6000.6.4f1, Windows x64 Mono Development
- Build GUID: `cafd475637524d259ee95ac650dbf2b2`
- Source fingerprint: `FD6B1EF52EA18A672C4CA124FF4E060ACB313897CD456F99C926EF09F7E09810`

## EditMode

Command shape (hidden process outside the sandbox):

```powershell
Unity.exe -batchmode -projectPath C:\serenity_game -runTests -testPlatform EditMode `
  -testFilter <WM15 focused filters> `
  -testResults C:\serenity_game\Artifacts\WM15UserRetest\editor-h.xml `
  -logFile C:\serenity_game\Artifacts\WM15UserRetest\editor-h.log
```

Result: 34 total, 34 passed, 0 failed, 205.441 s.

Two later compile attempts used `-runTests ... -quit`. Unity 6000 completed compilation and exited 0 before
the Test Runner wrote XML. They are deliberately not counted as test passes. This cold-start/tooling defect is
assigned to the requested separate persistent-runner task.

## Actual Player route

Command shape:

```powershell
Builds\WM15UserRetestFinal\Serenity.exe -screen-width 1280 -screen-height 720 `
  -window-mode borderless -wm15UserRetest C:\serenity_game\Artifacts\WM15UserRetest\player-final-b `
  -logFile C:\serenity_game\Artifacts\WM15UserRetest\player-final-b.log
```

Result before the final decorative-sea-edge adjustment: exit 0, 25/25 passed, 0 runtime errors. It validated
real shoreline positions and reachable Build actions for fishing camp and pier, unfinished pasture visuals,
six-place stone housing, unique ruler Generalissimo, nine diagnostic ranks, military work exclusion and armour
palette, Private training/barracks status, later service gating and the authored-depth ordinary-lake profile.

The final adjustment moves the straight scenic southern-sea mesh edge underneath terrain. The user explicitly
requested: build it, do not launch or verify it, because they will run it and send a screenshot. Accordingly no
post-adjustment Player or visual pass is claimed.

## Final build

Command:

```powershell
Unity.exe -batchmode -projectPath C:\serenity_game -executeMethod U00Build.WindowsDevelopment `
  -serenityBuildPath C:\serenity_game\Builds\WM15UserRetestFinal\Serenity.exe -quit `
  -logFile C:\serenity_game\Artifacts\WM15UserRetest\build-final-d.log
```

Result: Unity exit 0; `U00_BUILD_RESULT: Succeeded; errors=0; warnings=85`.

An earlier full build failed only because C: had 0.73 GiB free. The failed partial build and the obsolete
three-GiB candidate from this same task were removed; no neighbouring build, source asset or imported package
was deleted. The user subsequently freed additional disk space.
