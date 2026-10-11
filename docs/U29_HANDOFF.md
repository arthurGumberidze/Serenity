# U29 HANDOFF

## Task

ID: U29

Title: Performance gates 100→300→500→1000

Status: PARTIAL (explicit user-approved checkpoint; not DONE)

## Implemented

- Visible Windows Player benchmark for 100/300/500/1000 Characters, three repeats, exact 25/50/25 tiers, 60-second warm-up and 300-second measurement.
- Offline PowerShell verification/matrix runners, immutable raw frames/screenshots/logs, strict validation and Markdown/JSON diagnostic reports. They make no Codex/API calls.
- Opt-in sustained completed-frame diagnostics and an eight-second slow-cluster native trace with offline Editor export. Diagnostic evidence cannot qualify as acceptance evidence.
- Gameplay-safe optimizations to registry enumeration, Tier2 work consumption, AI/presentation scheduling, UI refresh, canonical service traversal, weather and production preparation.
- Smooth locomotion is preserved: moving residents remain continuously animated; idle Animators retire after the movement-to-idle transition.

## Acceptance result

NOT DONE. The latest complete matrix `Logs/U29/offline-20261011-194641` produced all 12 repeats and passed its verification/build suites, but only 8/12 performance repeats passed. Worst repeat by population:

| Population | Tier1/2/3 | p50 ms | p95 ms | p99 ms | max ms | Verdict |
|---:|---:|---:|---:|---:|---:|---|
| 100 | 25/50/25 | 16.667 | 16.671 | 16.697 | 19.131 | p95 PASS |
| 300 | 75/150/75 | 16.667 | 16.670 | 16.701 | 22.764 | p95 PASS |
| 500 | 125/250/125 | 16.667 | 27.199 | 35.820 | 79.472 | p95 PASS; one tail/GC-proxy repeat FAIL |
| 1000 | 250/500/250 | 16.668 | 40.626 | 51.520 | 152.310 | p95 FAIL |

Hardware/software: Windows 11 10.0.26200, Intel Core Ultra 7 155H (22 logical processors), 64,840 MB RAM, Intel Arc Graphics/D3D11, Unity 6000.6.4f1, Windows x64 Mono Development, 1600×900, quality PC, vSync 0, target 60 FPS.

The later clean sustained control `Logs/U29/diagnostic-20261011-220330-402` measured p50/p95/p99/max 16.668/38.216/46.131/136.472 ms. It reduced allocation from about 69,946 to 45,433 bytes/frame and collection frequency from about 62.2 to 37.1/min compared with the earlier sustained diagnostic, but did not close p95. Its detailed capture had 42,273 aligned frames, 7,019 over budget, no focus loss/gaps/coverage errors, exact 250/500/250 tiers and exact canonical save round-trip.

The native trace `Logs/U29/diagnostic-20261011-222946-104` triggered at 100.128 measurement seconds and captured eight seconds. With profiler overhead, 172 complete frames showed main-thread exclusive averages of 6.115 ms in `WaitForJobGroupID`, 5.846 ms in simulation callbacks and 3.393 ms in the scheduler; render-thread `DrawBuffersBatchMode` was 11.513 ms. Most main-thread job waiting was under NavMesh, rendering/culling and skinning, not the custom Tier2 completion. A four-worker diagnostic was worse and lost focus; it was rejected and no worker override remains in runtime/default scripts.

## Validation evidence

- `& .\Tools\Verify-U29.ps1 -ProjectPath C:\serenity_game -FullRegression -BuildPlayer`: 27 focused EditMode, 16 focused PlayMode, 158 required EditMode, 38 required PlayMode, 35 persistence, 418 full EditMode, 102 full PlayMode; PostgreSQL 25 discovered/25 skipped without a configured DB; build succeeded with 0 errors and 4 warnings.
- Final retained-state verification `& .\Tools\Verify-U29.ps1 -ProjectPath C:\serenity_game -BuildPlayer`: compile passed; focused EditMode 28/28, focused headless PlayMode 16/16, required EditMode 158/158, required PlayMode 38/38 and persistence 35/35 passed; PostgreSQL 25/25 were discovered and skipped without a configured DB; Windows build succeeded with 0 errors/2 warnings.
- Exact save hashes match their pre-restore canonical hashes in every complete matrix repeat. Latest matrix save payloads were 165,708 / 447,513 / about 729,000 / 1,433,547 bytes for 100/300/500/1000.

## Known debt

- U29 remains below its acceptance target only up to 500 Characters. Sustained 1000-Character p95 is 38–41 ms versus <=33.33 ms.
- Native evidence points to simultaneous scheduler/callback, NavMesh/job-wait and rendering pressure. It does not justify blaming animation, GC or temperature alone.
- U30 may collect additional long-duration performance telemetry, but it must not weaken or automatically close U29.

## Next task

U30 is TODO and not implemented in this chat. Use `serenity_automation/serenity_night_automation_v3/prompts/U30_PROMPT.txt` in a new chat. The user explicitly accepts this sequencing exception while U29 remains PARTIAL. U31 must still treat U29's failed acceptance gate as unresolved.
