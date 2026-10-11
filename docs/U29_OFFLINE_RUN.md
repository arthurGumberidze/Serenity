# U29 offline validation

This workflow runs the long U29 verification entirely on the local machine. It does not call Codex, the OpenAI API, or any other model, so the 90–120 minute run does not consume model-token limits.

U29 remains IN_PROGRESS until the mandatory report says `PASS` and the results are reviewed. A short smoke run, an incomplete directory, or a performance-only iteration cannot complete the task.

## Focused diagnosis after the failed matrix

Use `Tools\Run-U29-Diagnostic.cmd` for the next investigation. It builds the current source, then runs only 1000 Characters: 3 minutes warm-up and 15 minutes measurement, approximately 18 minutes plus build. It is fully local; Codex can be closed. Keep the visible Player foreground and do not use the PC during measurement. The ordinary complete runner remains available for final validation after an optimization is confirmed.

Outputs are in `Logs/U29/diagnostic-*/performance/population-1000/repeat-1/`:

- `U29-FRAME-DIAGNOSTICS.md` and `.json`: slow/healthy timing comparison, 30-second windows, availability caveats and next investigation targets.
- `diagnostic-frames.csv`: previous completed Unity frame ID, frame duration, callback section timings, collection-counter changes, process CPU usage and available native profiler values.
- `diagnostic-marker-inventory.csv`: actual Unity marker categories/units and which were recorded. Missing markers are not zero-cost work.
- `diagnostic-metadata.txt`: 64 KiB allocation-counter self-test, sampling interpretation and limitations. `-1` means unavailable/no sample. A failed self-test invalidates the old all-zero per-thread allocation table.

Callback timing has an explicit simulation frame ID and is exported only when aligned with the completed frame. Native recorders remain continuous: resetting them inside Update would truncate outer Unity scopes. Their non-wrapping histories are reserved before measurement, and sample-count advancement distinguishes fresh values from stale ones. Missing core-marker coverage, truncated outer scopes, frame gaps/misalignment or lost focus fail diagnostic validation. The first transition frame remains in the ordinary summary but is excluded from detailed attribution. Nested/multithread profiler values cannot be summed. Collection counters do not measure pause duration; GPU timing can lag; process CPU is sampled once per second (1000 permille = one logical core). CPU frequency and temperature are not measured. Detailed collection allocates its buffers during warm-up and writes CSV after measurement; it is opt-in and adds observer/memory overhead, so it does not replace normal gate evidence.

`STATUS.txt` becomes `DIAGNOSTIC_COMPLETE (not U29 PASS)` or `FAILED`. Performance FAIL does not abort detailed report generation: successful capture and performance acceptance are separate. The console prints the unchanged performance verdict. Integrity/runtime/report errors still fail the process, with any recoverable detailed evidence retained. The underlying generic report may say `SMOKE_PASS`; that indicates harness completion only. A run without slow intervals has not reproduced the issue and does not prove a fix. Default fresh build avoids silently measuring stale source; `-SkipBuild` is only for an already verified current Player. Uncalibrated/failed per-thread allocation breakdowns are omitted, not reported as zero.

```powershell
& .\Tools\Run-U29-Diagnostic.ps1 -DryRun
# Short end-to-end harness validation only:
& .\Tools\Run-U29-Diagnostic.ps1 -SkipBuild -WarmupSeconds 10 -MeasurementSeconds 30
```

## Recommended launch

1. Save work and close Unity Editor and every running `Serenity.exe`.
2. Connect the computer to AC power.
3. Double-click `Tools\Run-U29-Offline.cmd`.
4. Do not use the computer until the console finishes. The visible Player must remain foreground; the runner prevents sleep and re-asserts the Player window every two seconds.
5. Open the newest `Logs\U29\offline-*\STATUS.txt` and `performance\U29-DIAGNOSTIC-REPORT.md`.

The `.cmd` file starts an independent Windows PowerShell process. Codex can be closed while it runs.

Equivalent terminal command:

```powershell
& .\Tools\Run-U29-Offline.ps1 -ProjectPath C:\serenity_game
```

## What the full run does

The default command performs, without interactive prompts:

1. compile/import validation;
2. U29 EditMode and headless PlayMode tests;
3. dependency, persistence, PostgreSQL-discovery, full EditMode and full PlayMode regressions;
4. GPU PlayMode smoke;
5. Windows x64 Mono Development build;
6. visible 1600×900 GPU Player matrix for 100/300/500/1000 Characters;
7. exact 25/50/25 Tier1/Tier2/Tier3 mix, 60-second warm-up, 300-second measurement and three repeats;
8. raw CSV, summary JSON, screenshot and Player log validation;
9. Markdown and machine-readable JSON diagnostics.

Every Player is started and closed serially. A timeout terminates only the PID created by the runner. Result directories must be new and empty, so stale or mixed evidence cannot be accepted.

## Files to inspect

- `STATUS.txt`: `RUNNING`, `PASS`, or `FAIL`, with paths to the live log and final report.
- `offline-run.log`: complete orchestration transcript.
- `performance\U29-final-summary.json`: aggregate machine result.
- `performance\U29-DIAGNOSTIC-REPORT.md`: primary human-readable analysis.
- `performance\U29-DIAGNOSTIC-REPORT.json`: structured report suitable for a later Codex optimization pass.
- `performance\population-N\repeat-R\summary.json`: exact repeat metrics and invariants.
- `performance\population-N\repeat-R\raw-frames.csv`: frame-level evidence.
- `performance\population-N\repeat-R\player.log`: Unity runtime diagnostics.
- `performance\population-N\repeat-R\visual-evidence.png`: rendered scene/UI evidence.

## Report verdicts

- `PASS`: complete mandatory matrix and all hard gates passed.
- `FAIL`: complete evidence exists but one or more hard gates failed.
- `INCOMPLETE`: at least one required repeat/artifact is missing or invalid.
- `SMOKE_PASS`: only the short harness smoke passed; this is never a final U29 result.

The hard performance limit is worst-repeat p95 `<= 33.33 ms`. Exact population/tier counts, unique StableEntityId values, visible GPU/UI/camera, foreground samples, bounded memory, exact save/load identity and canonical hashes are also mandatory.

The report additionally marks diagnostic warnings such as p50 above 20 ms, p99 above 33.33 ms, allocations above 128 KiB/frame, or total save observation above 500 ms. These are investigation guides, not substitutes for the hard gates. It reports raw spike frequency/streaks, CPU/render/GPU averages, profiler markers, allocation-source breakdown, heap/working-set deltas, GC behavior, save phases, test counts, probable causes and concrete next actions.

## Optimization iteration only

For native CPU investigation, `Tools/Run-U29-Diagnostic.ps1 -CaptureTrace -WarmupSeconds 60 -MeasurementSeconds 600` uses the same 1000-character scene, waits for at least 12 over-budget frames in the last 60, records eight seconds of binary CPU profiling and closes the Player early. The measurement argument is a deadline, not a required wait. If no complete trace is captured it fails explicitly. The script then exports native marker inclusive/exclusive times through the pinned headless Editor; the PC is available once Player closes. This trace has profiler overhead and can never qualify as acceptance evidence. No network/model calls are made. `-SkipBuild` is valid only when the current instrumented Player is already built.

After a failed report, a faster performance-only rerun may be used while tuning code:

```powershell
& .\Tools\Run-U29-Offline.ps1 -ProjectPath C:\serenity_game -PerformanceOnly
```

This reuses the existing Player build and does not rerun NUnit/build gates. It is useful for before/after comparison but is not final evidence. After optimization, always run the default full command again.

To validate paths and script wiring without starting Unity:

```powershell
& .\Tools\Run-U29-Offline.ps1 -ProjectPath C:\serenity_game -DryRun
```

Do not reuse an interrupted output directory. Start a new run so missing/stale artifacts remain fail-closed and results from different builds cannot be mixed.
