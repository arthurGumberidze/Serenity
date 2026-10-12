# NEXT_TASK.md

U31's existing integration tests pass but its final gate is BLOCKED. The authorized U30 remediation across AI, health/weather and the evidence harness is implemented and short-verified; the parent U30 acceptance task remains open. The user explicitly prohibits a new eight-hour run. See `U30_HANDOFF.md` for the 60/12 targeted tests, expanded regressions, build, failed first short control and independently audited passing second control. Next is review/approval of the remaining acceptance work or an explicit switch to the proposed Russian UI delivery below; do not start either a long run or unrelated features silently.

## Exact remaining gates

1. Finalize U29 separately from fresh qualifying performance evidence. The complete 100/300/500/1000 matrix must pass every integrity gate and the worst sustained 1000-Character Player p95 must be at or below 33.33 ms. The current retained sustained control is 38.216 ms, so U29 remains PARTIAL.
2. Preserve completed `C:\serenity_u30_soak_20261012_run4` as immutable historical runner-PASS evidence. The 2026-10-13 remediation fixes AI arrival/retained capture intent, canonical seed, exact tier checkpoint state, actual workload reporting, telemetry and provenance; see the latest `U30_HANDOFF.md` section for verification and limits. U30 remains IN_PROGRESS. **Do not launch another eight-hour run without separate user approval.** Native shutdown diagnostics, continuous-process memory safety, retained history growth and sustained coverage/performance need further evidence; a short TestMode PASS is not acceptance. Schema-1 soak checkpoints are historical evidence, not valid schema-2 resume inputs; normal gameplay save v15 is unchanged.
3. Only after U29 and U30 are DONE, rerun `Tools\Verify-U31.ps1`, audit `docs\U31_HANDOFF.md`, and change U31 from BLOCKED to DONE in a dedicated finalization commit.

Do not reinterpret short controls, preflights or the U31 Player smoke as U29/U30 acceptance evidence. Do not modify existing U29/U30 evidence while finalizing U31.

## Proposed player-facing continuation

After this repair commit, ask the user to approve switching the active task to the first bounded delivery in `PRODUCT_ROADMAP_RU.md`: Russian UI and intelligible action/denial feedback, then a real main menu. The approved visual reference camp, hero appearance editor and authored region follow in that order. Do not silently activate the legacy U32 row or expand this remediation into all seven eras. Technical U31 integration acceptance does not replace visual/UX acceptance.
