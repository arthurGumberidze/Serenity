# U30 HANDOFF

## Task

ID: U30

Title: Automated tests and 8+ hour soak

Status: IN_PROGRESS — preparation complete; external official run active and not yet finalized

## Implemented

- Command-line-only `U30SoakController` on the normal composition root; normal gameplay startup is unchanged without `-u30Soak`.
- Reused U29's deterministic 100-Character population with exact 25/50/25 tiers and accelerated centralized simulation covering production, research, dynasty/health/birth, weather, military/combat, fire/capture, global travel, strategic AI and events.
- Read-only committed-barrier invariant monitoring for StableEntityId, tier representation, resource ownership/quantity, birth/pregnancy, dynasty/succession, military/combat, building/fire, event exactly-once and AI targets/orders.
- Atomic snapshot-v14 plus exact tier-runtime checkpoints, independent reread/SHA-256 verification, append-only index, exact resume hash, birth-aware restore and transactional TierManager representation restore.
- Fail-soft PowerShell orchestration with real/productive duration, segment restart manifest, heartbeat/performance journals, first-failure reproduction bundle, bounded backoff, verified-checkpoint fallback, watchdog and aggregate fail-closed result.
- Process-scoped Windows sleep guard; display power-off remains allowed and persistent power settings are not changed.
- Separate exact checkpoint hash and cross-process logical outcome hash, documented in D-090.

## Short-run evidence

- Same seed/config repeat A: `C:\Users\Lenovo\AppData\Local\Temp\serenity-u30-det-h-20261012025410`; PASS, 17 actual seconds, 12 productive seconds, two segments, resume verified, zero critical failures.
- Repeat B: `C:\Users\Lenovo\AppData\Local\Temp\serenity-u30-det-i-20261012025434`; same PASS metrics.
- Both ended with exact logical hash `38ACBA28253F7E052DAD8696F7F4E0F0CA5BCDFC680339C31413E88BC79FFDE0`.
- Invariant injection: `C:\Users\Lenovo\AppData\Local\Temp\serenity-u30-invariant-20261012025503`; expected exit 1/FAIL after 18 actual and 12 productive seconds, one invariant/critical incident, two completed segments, one restart, verified resume, one incident line and two manifest lines.
- Watchdog injection: `C:\Users\Lenovo\AppData\Local\Temp\serenity-u30-watchdog-20261012025529`; expected exit 1/FAIL after 28 actual and 12 productive seconds, one watchdog/critical incident, two completed segments after the stalled attempt, two restarts, verified resume, 55,633 retained performance samples, one incident line and three manifest lines.

## Verification

- `& .\Tools\Verify-U30.ps1 -SkipU29Regression`: PowerShell AST validation, Unity compile, U30 EditMode 5/5, U30 PlayMode 2/2 and Windows x64 Mono Development build passed. The final build log reports `Build Finished, Result: Success`, 0 errors and 2 warnings.
- `& .\Tools\Verify-U30.ps1`: U30 5/5 EditMode and 2/2 PlayMode; U29 focused 28/28 EditMode and 16/16 PlayMode; required dependency regressions 158/158 EditMode and 38/38 PlayMode; persistence 35/35; PostgreSQL 25 discovered/25 skipped without configured DB; Windows x64 Mono Development build succeeded with 0 errors/2 warnings. Logs are under `C:\serenity_game\Logs` with timestamps 2026-10-12 02:56–03:01 local.
- Short real Player runs above prove integrated coverage, checkpoint/restart, deterministic logical outcome and fail-soft evidence behavior.

## Active official run

The user launched:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\serenity_game\Tools\Run-Soak-U30.ps1" -Seed 3030 -WallClockHours 8 -CheckpointIntervalMinutes 5 -OutputFolder "C:\serenity_u30_soak_20261012_run3"
```

First heartbeat: `2026-10-12T00:08:26.7481820Z`. During preparation handoff the run had reached heartbeat sequence 424, 422.477 segment seconds, game date Y1 D12 17:30, 105 Characters, two published checkpoints and zero published aggregate result because the run was still active. This observation is progress only, not acceptance evidence.

## Required next action

After the process exits, start a fresh finalizer and follow `docs/NEXT_TASK.md` and `docs/SOAK_TESTING.md`. Verify `C:\serenity_u30_soak_20261012_run3\soak-result.json` plus every supporting artifact. U30 stays IN_PROGRESS unless requested and actual wall time are both at least 28,800 seconds and every structured gate passes. Do not start U30A/U31. U29 remains PARTIAL.
