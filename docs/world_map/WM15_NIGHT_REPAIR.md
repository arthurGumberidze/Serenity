# WM15 night repair — 2026-10-24

Status: IN PROGRESS. This repair does not grant overall WM15 DONE or visual acceptance.

## Scope and immutable baseline

Source run: `Artifacts/WM15/night-20261024-002730-4c6d7c`, source commit
`2bc3b8980e082d38cfa2d2d5c29b8d5897770e80`. The 182-step run finished with 139 PASS,
38 FAIL, 4 ERROR and 1 PARTIAL. Its NUnit XML contains 62 failed cases and 881 passed
cases. Original reports, XML, logs, builds and hashes must remain unchanged.

Repair output: `Artifacts/WM15/repair-20261024`. `failed-cases.json` and
`failed-cases.md` retain each exact NUnit fullname, failure, stack and original XML
SHA-256. Only those failed cases, the two HealthAndFamily cases whose wrapper
cleanup failed, runner protection/cleanup regressions and failed performance gates
are eligible for repeat. No full-night rerun, broad fixture rerun, new chat,
automation, worktree or parallel agent is authorized by this repair.

The seven inherited dirty materials and `New Unity Project/` remain excluded from
repair commits. Their baseline hashes are in `inherited-files.json`. V2 remains
inactive. No old-save migration or PostgreSQL action is part of this work;
PostgreSQL remains PARTIAL without configured test infrastructure.

## Runner verification

`Tools/Validation/Test-WM15Runner.ps1 -Repeats 2` passed 72 assertions. Evidence:
`Artifacts/WM15/runner-tests/serenity WM15 runner 571330d5568f4adfb37443e6e991a1a9`.
This uses a fake executable and temporary project, never the real Unity project.
Checks include a denied live-process cleanup, an exited-process access race,
generated root TestResults.xml, changed/new protected saves, and exact failed-case
selection excluding a passed neighbour.

Cleanup diagnostics are independent of NUnit verdicts. An unresolved owned
process blocks subsequent runtime steps. A completed but slow performance matrix
is FAIL, while missing/incomplete evidence is ERROR. Only the exact root-level
generated TestResults.xml is excluded from protected-save inventory; nested XML
and real saves remain protected.

`Run-WM15.ps1 -RepairFrom <original-run> -RepairStage Tests|Performance|Protection`
builds an explicit restricted plan. Tests extracts exact cases from original XML;
Performance builds fresh source and repeats only failed population gates. Always
provide a new output directory outside the original evidence directory.

## Measurements and validation so far

- `edit-a.xml`: six exact originally failed EditMode cases passed, 6/6.
- `EditMode-b.arguments.json`: the next compile stopped before test execution because
  a test lacked the Resources transfer namespace. No NUnit verdict is claimed.
  After that import was fixed, `EditMode-c.xml` passed 3/3 in 31.949 seconds,
  exit 0 with complete owned cleanup. All nine original EditMode failures now pass.
- `play-a.arguments.json`: exact invocation for the first 22-case PlayMode repair
  batch. `play-a.xml`: 18 PASS / 4 FAIL, 723.342 seconds, exit 2, owned cleanup
  complete with no diagnostics. The four remaining failures concern context-command
  eligibility, research integration ticks, haul baselines and temporary transport
  inventory semantics. They remain eligible for another exact-case repeat.
- `PlayMode-c.arguments.json`: 35 exact unresolved original cases, 25 PASS / 10 FAIL,
  1110.808 seconds, exit 2, owned cleanup complete without diagnostics. Together with
  earlier results this closes 52/62 original failures. `PlayMode-d.arguments.json`
  selects those ten remaining cases, the two HealthAndFamily cleanup cases, and two
  affected originally failed visibility/ecology regressions after caching changes.
- `PlayMode-d.xml`: 12/14 PASS, 476.124 seconds, exit 2, complete clean process
  cleanup. Both HealthAndFamily cases passed. The original-failure matrix reaches
  60/62 PASS; the remaining cases require explicit available storage capacity and
  explicit conserved military resupply before testing the travel deduction.
  `PlayMode-e.arguments.json` selects only those two remaining cases.
- `perf-before-short`: diagnostic use of the original night binary, 100 residents,
  10-second warmup and 30-second measurement; p95 45.944 ms, GPU mean 43.784 ms,
  194182 allocated bytes/frame. All 678 samples focused, memory growth bounded.
  This short diagnostic is not a qualifying gate or evidence for repaired source.
- `perf-before-trace` and `trace-export`: original-binary native trace. Allocation
  call counts identify per-animal task snapshots and label strings as avoidable
  work. GPU time remains the primary frame bottleneck. A Mono thread-allocation
  probe returned zero despite measured allocations; zero section values must not
  be interpreted as allocation-free execution.

`PlayMode-e.xml` passed the connected journey; the haul test reached its debug-total
assertion and exposed omission of CarryLoad quantities (61/62 original cases now pass).
The two-case run took 137.393 seconds, exit 2, with clean owned cleanup.

`build-e.arguments.json` records the fresh Windows Development invocation. Build-e passed
in 123.038 seconds, exit 0, with clean owned cleanup. Build GUID:
`d7e67f14db6444c28bb67e608e92c799`; source fingerprint:
`8C694A71B488410C8ECDA90516A5A80F99E60EA7FCC7C0D021E62B5B0381F3C8`.
This is the diagnostic build before the final CarryLoad total repair and any measured GPU fix.

At that checkpoint the final matrix/build were pending; the final section below supersedes it. The
33.33 ms frame threshold, population mix, density and 3 x (60s warmup + 300s
measurement) gate duration remain unchanged.

## Final repair checkpoint and offline handoff

PlayMode-f: 9/9 PASS in 352.359 seconds, exit 0, clean owned cleanup. This closes
all 62 original NUnit failures. Both extra HealthAndFamily cleanup cases passed.
Build-f: exit 0 in 115.789 seconds, clean cleanup. GUID 50fae654d8e64da397c715e0297111a4;
fingerprint 559F2CCBAEA41A2C5A90F5352C850649CB983248C96D0E33F528AEBEDE27F432.
Both copied source manifests match that fingerprint. The later P6-only test-isolation
edit is not part of build-f; the offline launcher rebuilds all current source.

Exact invocations remain in build-f.arguments.json, PlayMode-f.arguments.json and
render-f-baseline.arguments.json under Artifacts/WM15/repair-20261024. Their sibling
process.json files record exit, elapsed time and cleanup. The normal rendered build-f
smoke uses 100 residents, one repeat, 10s warmup/30s measurement, DetailedDiagnostics,
RecordOnly and ContinueOnFailure, with no RenderDiagnostic isolation flag. Exit 0,
73.276 seconds, clean cleanup. All 383 samples focused, p95 90.422 ms, GPU mean
77.527 ms, 178077 bytes allocated/frame. Terrain channels now match used channels;
the screenshot was inspected with terrain, trees, characters and HUD enabled.
This is diagnostic evidence only: the 33.33 ms performance requirement is NOT passed.

Run Tools/Validation/Run-WM15-Performance-Repair.cmd by double-clicking, with Unity
Editor closed, AC power connected, desktop unlocked and no project edits during the run.
Allow roughly one hour plus build time. It calls the sibling PS1, which calls:

    Run-WM15.ps1 -RepairFrom Artifacts/WM15/night-20261024-002730-4c6d7c -RepairStage Performance -RunDirectory <new performance-repair timestamp directory> -BusyWaitMinutes 0 -MaxHours 3

The safe verification command was:

    Tools/Validation/Run-WM15-Performance-Repair.ps1 -DryRun

Its plan is in Artifacts/WM15/performance-repair-20261024-101704-b69096: preflight,
protected-inputs-before, windows-build, performance-100, performance-300,
performance-500, protected-inputs-after, git-diff-check. No NUnit/UI suite is selected.
Each gate uses 3 repeats, 60s warmup and 300s measurement. The real long run was NOT
started. Read REPORT.md and STATUS.json in the newly created performance-repair-* folder.

## Save-protection incident — FAIL, recovery unresolved

The final audit found original StoneAgeBuildingsV1/Saves/00000000000000000000000000002702.xml
changed at 06:34:08 UTC, during PlayMode-c's P96 SaveLoadAndStrategicRoundTrip test.
Slot 2701 was also created at 06:28:19 UTC. P6FirstHourPlayModeTests had cleared the
inherited save environment variable after its own isolated test; later fixtures then
used the normal profile. P6 now restores the previous directory, like the other repaired
fixtures. This does not undo the earlier overwrite. No exact old slot-2702 copy was found
among project save candidates by SHA256. The current file is preserved in repair evidence
as save-2702-current-incident-copy.xml.data; the original hash remains in protected-before.json.
Do not substitute a different save or claim restoration. Ten other original XML/JSON
payloads still match, as do all seven inherited materials and original failing-test XMLs.
The earlier after-play-a audit was accurate at that time, but is superseded by this final
FAIL. A later offline run checks against its own starting state and cannot clear this incident.

The checkpoint remains IN_PROGRESS: performance acceptance pending, protection incident
unresolved, user visual acceptance pending and PostgreSQL PARTIAL. No overall DONE claim.

PlayMode-g verifies the P6 teardown repair followed by the originally failed P96 save/roundtrip
case: 2/2 PASS, exit 0, 113.780 seconds, clean owned cleanup. Exact arguments are in
PlayMode-g.arguments.json. The initial unfiltered post-run comparison failed only because
Unity rewrote the exact root TestResults.xml; after the documented generated-report exclusion,
all 12 current save/settings payloads are unchanged (post-g-save-audit.json). This verifies
no further overwrite during this pair; it does not restore the earlier lost slot contents.
The final selection audit records 93 executions, 64 unique cases: the 62 original failures
and two HealthAndFamily cleanup cases. No case from the other 881 passed tests was run.
