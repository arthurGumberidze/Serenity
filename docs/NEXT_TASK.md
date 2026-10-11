# NEXT_TASK.md

U28 is DONE. U29 was checkpointed as PARTIAL by explicit user decision on 2026-10-12; its unresolved performance gate is documented and remains open. U30 is TODO and has not started.

## Next authorized task for a new chat: U30 — Automated tests and 8+ hour soak

### Status

TODO. Do not begin U30 in this chat. Use `serenity_automation/serenity_night_automation_v3/prompts/U30_PROMPT.txt` in a new chat. U29 remains PARTIAL rather than DONE: the latest clean sustained 1000-Character control measured p95 38.216 ms against 33.33 ms.

### Dependencies

U04A, U10, U13, U14, U23, U26 and U28 are DONE. U29 is PARTIAL; proceeding is a user-authorized sequencing exception, not proof that its acceptance gate passed.

### Goal

In a new chat, implement and verify an offline U30 reliability harness, then run the official 8+ wall-clock-hour soak outside Codex so idle time consumes no model allowance.

### Next action

Start a fresh Codex chat with the repository U30 prompt. The runner must be fail-soft for evidence collection: recoverable invariant/exception/restart incidents are appended and make the final result fail, but the external observer continues to the requested deadline whenever safe, retains performance samples across segments, checkpoints, restarts from the last verified checkpoint when possible, and writes one aggregate result at the end. Only evidence-integrity/disk/safety failures may stop immediately. U30 remains IN_PROGRESS until a separate finalizer verifies an actual >=28800-second result.

### Scope boundary

Read `AGENTS.md`, `docs/U29_HANDOFF.md`, `docs/PERFORMANCE_GATES.md`, this file, the task graph, decisions, architecture, and the U30 prompt. Preserve all canonical authorities. Do not start U30A or U31, and do not mark U29 DONE from U30 evidence alone.
