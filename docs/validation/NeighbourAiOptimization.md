# Neighbour AI cumulative-stall optimization — 2026-10-31

## Scope

Remove the increasing per-month simulation cost observed during the two-year neighbour/Ollama run, reduce
local-model contention with Unity, preserve canonical work/save behavior, and produce a new Windows build.

## Changes

- `WorkManager` maintains a stable-ID-sorted open-job index. Hot assignment, production, construction,
  industrial and streaming queries no longer allocate and sort terminal history.
- Completed/cancelled/failed work retains only the newest 256 entries. A FIFO queue evicts older entries in
  O(1); `nextJobId` remains monotonic through capture/restore.
- Terminal `ProductionTask` objects are removed from the service registry immediately. Save state already
  persisted active tasks only, so the canonical payload is unchanged.
- `RulerActionAudit` scans for new agents/decisions at 2 Hz rather than every rendered frame and caches the
  resident-to-faction mapping. Action transitions remain event driven and JSONL remains buffered.
- Ollama receives `num_thread`. The automatic policy uses 8 threads on the tested 22-logical-processor CPU,
  leaving Unity/system headroom; `SERENITY_OLLAMA_THREADS=1..64` is the optional override.
- Each external call appends `provider_timing` with elapsed milliseconds to `AiAudit`.

## Validation

Commands ran from `C:/serenity_game` through Unity 6000.6.4f1.

```powershell
& .\Tools\UnityRunner.ps1 compile -OutputPath 'Artifacts\NeighbourAiOptimization\compile-c' -TimeoutSeconds 600
```

Succeeded: 0 errors, 7 inherited warnings. Earlier incremental compile-a passed with 0/0; compile-b passed
with 0 errors and 104 inherited import/API warnings.

```powershell
& .\Tools\UnityRunner.ps1 test -GroupNames '.*(WorkGroupAndJobTests|ProductionAndFarmingTests|StrategicAiTests).*' -OutputPath 'Artifacts\NeighbourAiOptimization\core-tests-b' -TimeoutSeconds 1200
```

Passed 60/60 in 44.93 s. Included results:

- 10,000 completed work orders in 45.56 ms; 256 terminal entries retained; no ID reuse after save/load;
- 1,000 workers / population 10,000 / 30 game days in 742.99 ms, zero measured current-thread allocation,
  deterministic hash `1995D6BD41AAC1EC`;
- completed/cancelled production tasks leave `TrackedTaskCount == 0`;
- Ollama JSON includes the selected thread limit and the automatic policy is covered at 2/8/16 CPUs.

```powershell
& .\Tools\UnityRunner.ps1 test -TestNames 'Game.Tests.Editor.NeighbourProgressionRepairTests.ThreeAcceleratedMonthsKeepWorkRegistriesAndStepCostBounded' -OutputPath 'Artifacts\NeighbourAiOptimization\three-months-test' -TimeoutSeconds 900
```

Passed 1/1 in 466.73 s including full world startup. The measured simulation portions were:

- month 1: 6,787.8 ms; 41 retained jobs, 3 open, 0 production tasks;
- month 2: 6,723.2 ms; 75 retained jobs, 3 open, 1 production task;
- month 3: 5,239.4 ms; 109 retained jobs, 4 open, 0 production tasks.

The per-month simulation cost did not rise. Retained history continues only to 256; the separate 10,000-order
gate proves eviction after the cap.

## Real local Ollama check

One direct POST to `http://localhost:11434/api/chat` used `qwen3.5:4b`, `think=false`, JSON mode, context 2048,
128 output tokens and `num_thread=8`. It completed in 21.54 s: model load 3,940.9 ms, prompt evaluation 766.1 ms,
generation 14,689.2 ms. This was local-only; no cloud endpoint or key was used. The shorter measurement prompt
is performance evidence, not plan-validation evidence; strict plan validation remains covered by the 60 tests
and the prior 730-day local-only endurance audit.

## Build

```powershell
& .\Tools\UnityRunner.ps1 build -BuildPath 'Builds\NeighbourAiOptimized\Serenity.exe' -OutputPath 'Artifacts\NeighbourAiOptimization\build-b' -TimeoutSeconds 1800
```

Passed in 38.66 s: Windows Development build, 0 errors / 0 warnings. The preceding build-a produced binaries
but correctly failed immutable provenance because UMA resaved its already-dirty `AssetIndexerProject.asset`
four seconds after source capture. The stable repeat passed without bypassing the gate.

Delivery: `Builds/NeighbourAiOptimized/Serenity.exe`.
