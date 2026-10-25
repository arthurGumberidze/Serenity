# WM15 map, hunting and pasture preview — 2026-10-25

The user authorized compilation and a new Windows build; full performance tests remain deferred.

## Delivered build

`C:\serenity_game\Builds\WorldMapArtHunting\Serenity.exe`

Unity 6000.6.4f1, Windows x64 Development Player. Build-e completed with process exit 0,
`U00_BUILD_RESULT: Succeeded; errors=0; warnings=30`. No `error CS` or `Shader error` entries.
Build GUID: `e26a85af0d9444cbbe32f984cc107731`.
Provenance: `Builds/WorldMapArtHunting/build-provenance.json`, created 2026-10-25T09:46:46Z.
Source base ec51d928 plus the four compile fixes below; the provenance fingerprint includes dirty source.
The executable launcher timestamp can remain older; the Player data and provenance identify this build.

## Repairs and evidence

- Build-a: sandbox launch could not initialize the licensing client; no compile pass.
- Build-b: two CS0234 errors. Explicitly qualify UnityEngine.Resources.Load in CropPlotPresentation
  and WorldMapArtDirection to avoid the Game.Presentation.Resources namespace collision.
- Build-c: CS0246 in LivestockPresentation. Import Game.Domain.AI for WorldPosition.
- Build-d: C# compiled, but Direct3D11/12 reported 12 errors in water/vegetation shaders. HLSL reserves
  `point`; rename the ellipse helper parameter to `samplePosition`. Unity reported Succeeded despite
  errors=12, so this intermediate artifact was NOT accepted or handed over as a valid preview.
- Build-e: successful with zero reported errors, 30 warnings. The immutable-source check passed.
- Local command access stalled after build-d. Explicit absolute shell and working directory restored access.
- Whitespace/diff inspection passed for the four fixes. No automated test suites or Player gameplay run;
  neither visual correctness, hunting behavior, pasture behavior nor performance is certified by this build.

Each attempt used the following command, changing only build-a/b/c/d/e.log. Actual process launch used
PowerShell Start-Process -WindowStyle Hidden -PassThru, WaitForExit and reported ExitCode. Builds b-e ran
outside the restricted sandbox to access the installed Unity licensing service.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/WorldMapArtHunting/Serenity.exe -logFile C:\serenity_game\Artifacts\WM15Preview\build-e.log
```

The 100/300/500 performance suites were NOT launched. Unity's build callback generated four untracked
PerformanceTestRunInfo/Settings JSON/meta files with an empty Results array; those are build metadata,
not evidence of a performance-test run. They are excluded from the repair commit. Inherited imports remain.

## Manual preview

Start this EXE and choose a new game to inspect the new map without old world state. For hunting select
an owned resident, then an animal and its bow-hunting action. HP should decrease only on valid impacts;
at zero HP the worker approaches and skins the corpse. An owned completed empty, unreserved field offers
“Сделать овечий загон”; the first pair of sheep is available once per campaign. Inspect before accepting.
Existing content limits remain: missing White Rabbit package/proxy hare, initial sheep husbandry only,
and unresolved full visual/gameplay/performance acceptance. WM15 stays IN_PROGRESS.
