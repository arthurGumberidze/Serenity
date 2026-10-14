# P8.6 integration/qualification handoff

Date: 2026-10-14

Base commit: `286926ecedc149871807df2c6504f8b2c8d6cccb`

Outcome: **PARTIAL checkpoint by explicit user instruction; release qualification not completed**

## Retained implementation

- Technical `-u29Benchmark`, `-u30Soak` and `-u31Smoke` launches enter the same normal seeded game startup path before attaching instrumentation; conflicting or incomplete technical arguments are rejected.
- U29 creates exact deterministic 25/50/25 tier populations and includes campaign progress, appearance and work state in its Save/Load observation.
- `TierManager.Revision` invalidates cached Tier3 worker rosters only when representations change.
- Mass Tier1 presentation keeps eight detailed NavMesh/skeleton Characters. Other Tier1 Characters retain canonical IDs, root presenters and collision, but use centrally advanced lightweight NavMesh movement and one instanced capsule render batch. Selection/direct control restores the detailed/native-agent path.
- Lightweight NavMesh paths reuse a 64-corner buffer via `GetCornersNonAlloc`.
- Tier policy, weather/exposure, camera transform and campfire-light refreshes are change- or cadence-gated.
- No canonical rules moved into presentation, no per-NPC `Update` was added, no dependency/editor version/gate changed.

## Completed verification

- P8.6 technical launch EditMode: **8/8 passed** — `Logs/P86-launch-editmode.xml`.
- Affected TierManager EditMode: **10/10 passed**.
- Affected camera/P3 EditMode: **8/8 passed**.
- Final U29 PlayMode after the non-alloc path change: **19/19 passed** — `Logs/P86-u29-nonalloc-playmode-fresh.xml`.
- Windows x64 Mono Development build: **Succeeded; errors=0; warnings=9** — `Logs/P86-u29-nonalloc-build-retry.log`. Build GUID: `538309928bac46ff8b66c5d181ec45c6`.
- Full-length diagnostic Player control, 1000 Characters, 60 s warm-up + 300 s measurement: **SMOKE_PASS**, not acceptance — `Logs/P86/U29-nonalloc-control-1000-smoke`.
  - exact tiers 250/500/250 and 1000 distinct stable IDs;
  - p50 16.667 ms, p95 16.673 ms, p99 16.815 ms, max 27.703 ms;
  - 13,705 GameObjects start/end, 4 UI subscriptions start/end;
  - exact identity/tier and canonical save hash round-trip;
  - bounded managed heap/working set; 68.6 KiB allocated/frame;
  - Mono aliased collection counter 38.2/min, but no recurring-major-GC-storm verdict because p99 stayed below 33.33 ms.

## Incomplete or failed qualification

- `Logs/P86/U29-final-matrix-20261014` is a complete pre-buffer matrix and remains **FAIL**. All p95 values passed, including a worst 1000-Character p95 of 27.095 ms, but several repeats failed the unchanged recurring-GC/tail proxy.
- `Logs/P86/U29-final-matrix-nonalloc-20261014` is an incomplete replacement matrix. Population 100 repeat 1 produced valid evidence (p95 16.671 ms, p99 16.697 ms, max 23.538 ms); later processes exited without summaries. The runner was interrupted when the user asked to stop and must not be treated as acceptance evidence.
- No fresh continuous-process eight-hour U30 run was performed.
- No final U31 connected/PostgreSQL route, full final regressions or release Player route was performed.
- P3/P5 visual taste, P6 first-hour usability and P7 long-route campaign acceptance remain open human items.

On 2026-10-14 the user explicitly requested that no further performance tests "and so on" be run and asked to finish. These omissions are recorded rather than converted into passes.

## Exact final commands and results

Final U29 PlayMode test:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath 'C:\serenity_game' `
  -logFile 'C:\serenity_game\Logs\P86-u29-nonalloc-playmode-fresh.log' `
  -runTests -testPlatform PlayMode -testCategory U29 `
  -testResults 'C:\serenity_game\Logs\P86-u29-nonalloc-playmode-fresh.xml'
```

Result: exit 0; 19 passed, 0 failed, 0 skipped.

Final completed build:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath 'C:\serenity_game' `
  -logFile 'C:\serenity_game\Logs\P86-u29-nonalloc-build-retry.log' `
  -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment `
  -serenityBuildPath 'Builds/P86/Serenity.exe'
```

Result: `U00_BUILD_RESULT: Succeeded; errors=0; warnings=9`. Unity later logged two 300-second `Curl error 28` shutdown timeouts; the build report itself was complete and successful.

Full-length diagnostic control:

```powershell
$populations = @(1000)
& 'C:\serenity_game\Tools\Run-U29-Performance.ps1' `
  -PlayerPath 'C:\serenity_game\Builds\P86\Serenity.exe' `
  -OutputDirectory 'C:\serenity_game\Logs\P86\U29-nonalloc-control-1000-smoke' `
  -Populations $populations -Repeats 1 -WarmupSeconds 60 `
  -MeasurementSeconds 300 -ProcessTimeoutSeconds 720 -Smoke -ContinueOnFailure
```

Result: `SMOKE_PASS (1/1 repeats)` with the metrics above.

Interrupted replacement matrix:

```powershell
$populations = @(100, 300, 500, 1000)
& 'C:\serenity_game\Tools\Run-U29-Performance.ps1' `
  -PlayerPath 'C:\serenity_game\Builds\P86\Serenity.exe' `
  -OutputDirectory 'C:\serenity_game\Logs\P86\U29-final-matrix-nonalloc-20261014' `
  -Populations $populations -Repeats 3 -WarmupSeconds 60 `
  -MeasurementSeconds 300 -ProcessTimeoutSeconds 720 -ContinueOnFailure
```

Result: incomplete; one valid repeat, subsequent missing summaries, interrupted on user request. No active Player/runner process remained afterward.

No tests were run after the user's final instruction to omit remaining qualification.
