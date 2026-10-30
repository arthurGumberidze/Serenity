# Neighbour progression and monthly Ollama endurance — 2026-10-31

## Scope

Repair the reported neighbour settlement stall, use local Ollama only, request one plan per game month, run
two game years at high acceleration, and verify through `AiAudit` that residents execute canonical work rather
than standing idle. Cloud/ChatGPT was not selected or called.

## Root causes and repairs

- `LocalSceneCompositionRoot` installed a player-only assignment predicate on the shared `WorkManager`,
  silently preventing neighbour residents from claiming their own logistics and construction jobs. The scope
  now resolves the resident's faction before filtering jobs.
- Neighbour production did not continuously reconcile deliveries for active workplaces and buildings. It now
  creates bounded canonical transfers and uses constant-time off-camera approaches while preserving ownership,
  reservations and completion rules.
- Critical hunger/thirst could repeatedly interrupt the same resident without successful recovery. The needs
  route now recognizes available meals and returns residents to ordinary work.
- Controlled fire now unlocks the campfire/cooking route at the point the neighbour can research it.
- Production inputs prefer stable building storage over ground piles, avoiding competing cleanup/delivery jobs.
- Multiple workers could reserve the same crop phase. Crop plots are now exclusive and stale phase tasks cancel
  safely before consuming inputs.
- Ollama requests use UTF-8, `think=false`, a compact bounded JSON contract, `num_ctx=2048`, `num_predict=256`
  and a 180-second local timeout. Numeric targets and workforce shares are normalized before acceptance.

## Two-game-year local-only run

The Unity endurance route advanced 730 game days with two 12-hour simulation slices per day. During an Ollama
request the game clock was paused, so provider latency could not consume the plan month. Settings selected only
`Ollama`; every journal entry observed during the run had provider `Ollama` and status `Accepted`.

Audit file:
`C:/Users/Lenovo/AppData/LocalLow/Serenity/Serenity/AiAudit/20261030-204752-996.jsonl`

Observed result:

- 42,288 total JSONL records;
- 25 accepted plans, 25 distinct sequences, no missing strategic-execution correlation;
- 2,922 neighbour strategic-execution records and one `ConstructBuilding` directive;
- 19,737 neighbour resident-action transitions and zero `Eat/Failed` transitions;
- six residents, with productive transitions respectively 1,655; 1,634; 1,771; 1,658; 1,709; 1,795.

The live route ran all 730 days in about 86.5 real minutes. Its then-current final assertion required the
operational-building count to increase under coarse 12-hour slices and reported 4 -> 4. All provider, cadence,
plan, cooking, correlation, per-resident productivity and starvation-loop assertions had already passed. The
test contract was corrected to require a canonical construction directive in this coarse endurance route;
physical building completion is tested by the exact hourly route below. The expensive provider run was not
repeated solely to re-evaluate that already captured JSONL file.

Audit replay command (PowerShell) parsed every line with `ConvertFrom-Json`, grouped accepted plans,
`strategic_execution` and neighbour `resident_action` records, then checked productive prefixes
`Produce/`, `Haul/`, `Research/`, `ManualHaul/`, `ManualTask/`. It returned the counts above and an empty list
of plan sequences missing strategic execution.

## Final Unity gates

Commands ran from `C:/serenity_game` through the persistent Unity 6000.6.4f1 runner.

Earlier focused gates in the same final repair series passed:

- `StrategicAiTests`: 30/30 (`Artifacts/NeighbourProgressionRepair/strategic-tests-b`), covering monthly
  plan validity, normalization and canonical strategic execution;
- `P109FoodIntegrationTests`: 3/3 (`Artifacts/NeighbourProgressionRepair/food-recovery-c`), covering recovery
  from critical hunger and prepared-food availability;
- focused building-production logistics: 1/1 (`Artifacts/NeighbourProgressionRepair/logistics-unit-a`).

```powershell
& .\Tools\UnityRunner.ps1 test -TestNames 'Game.Tests.Editor.ProductionAndFarmingTests' -OutputPath 'Artifacts\NeighbourProgressionRepair\production-final' -TimeoutSeconds 1200
```

Passed 13/13, 0 failed/skipped/inconclusive, including exclusive crop-phase reservation.

```powershell
& .\Tools\UnityRunner.ps1 test -TestNames 'Game.Tests.Editor.NeighbourProgressionRepairTests.NeighbourCooksResearchesAndBuildsDuringFourteenGameDays' -OutputPath 'Artifacts\NeighbourProgressionRepair\editor-final-c' -TimeoutSeconds 1200
```

Passed 1/1 in 438.31 seconds. Using exact one-hour simulation steps, the neighbour cooked food, completed
controlled-fire/shelter research, placed a new canonical building, delivered its materials and made it
operational within 14 game days. This also completed without the former crop-phase exception.

```powershell
& .\Tools\UnityRunner.ps1 build -BuildPath 'Builds\NeighbourProgressionRepair\Serenity.exe' -OutputPath 'Artifacts\NeighbourProgressionRepair\build-final-b' -TimeoutSeconds 1800
```

Passed in 47.45 seconds: Windows Development build, 0 errors / 0 warnings. The immediately preceding build
produced binaries but correctly failed immutable provenance because UMA resaved its already-dirty
`AssetIndexerProject.asset` after the source manifest was captured. The stable repeat passed without bypassing
the provenance gate.

Delivery: `Builds/NeighbourProgressionRepair/Serenity.exe`.
