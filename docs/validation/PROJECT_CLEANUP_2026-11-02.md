# DOC01 — Project chats and development plan

Date: 2026-11-02. Baseline game commit: cfa01926, main synchronized with origin/main before edits.
Scope: reversible Codex chat archival and project documentation only. No gameplay/asset/save edits.

## Inventory and carry-forward

Listed local Codex active and archived history (50 per page, both nextCursor null); read final turns of
the 15 old project chats. All returned completed turns; no active project stage was found among them.
The current chat 01a179c0-04ef-7203-b3c8-55a35ec70a54 remains active.

| Archived work | Count | Retained result / remaining obligation |
|---|---:|---|
| UI01–UI08, including the original and recovery UI02 chats | 9 | Completed commits and validation; historical cross-fixture readiness failures carried into DEV01/DEV04 |
| Asset/WM16 follow-up | 1 | Completed delivery; observed frame spikes and native allocation warnings require current verification in DEV04 |
| Project-size cleanup | 1 | Historical cleanup retained; obsolete unpushed-commit warning superseded by synchronized current main |
| Persistent runner | 1 | Existing runner instructions retained; no new Editor or runner implementation |
| Living buildings / WM15 | 1 | Completed fixes retained; last water patch's unverified visual result carried into DEV03 |
| Map warm-up | 1 | Completed warm-up improvement; does not qualify 1000-NPC performance |
| World map planning / night-test handoff | 1 | U29/U30/U31 obligations retained; no replacement night run started |

15 set_thread_archived calls succeeded. Re-listing active/archived history confirmed exactly one active
project chat (the current one) and all 15 target IDs in archive, with no further pages. Other projects
were untouched. Exact source titles, final reports and tool responses: [chat audit](../archive/2026-11-02/chat-audit.json).

Referenced follow-up 01a16c76-1417-7d32-ba98-cc950237fc27 was not found by read_thread on local.
It was not modified and its unknown contents are not treated as evidence. The accessible U29/U30/U31
handoffs carry the outstanding requirements. The old serenity-ui01-ui08 automation view was requested;
the tool returned only a rendered card, not machine-readable status. No scheduler change is claimed.

## Documents

- Original three working documents preserved byte-for-byte under docs/archive/2026-11-02.
- PROJECT_STATE now describes the latest verified baseline and explicit residual debts.
- NEXT_TASK has exactly one next stage, DEV01, READY; no implementation was launched.
- TASK_GRAPH maps DEV stages to original U/P requirements without closing partial gates.
- DEVELOPMENT_ROADMAP_RU defines scope, sequence and outcomes; old PRODUCT_ROADMAP_RU points to it.
- D-246 records the organizational change and authorization boundaries.

## Verification

Checks run from C:\serenity_game:

```powershell
git status --short --branch
git diff --check
git diff --stat
git hash-object docs/archive/2026-11-02/PROJECT_STATE.md
git rev-parse HEAD:docs/PROJECT_STATE.md
git hash-object docs/archive/2026-11-02/NEXT_TASK.md
git rev-parse HEAD:docs/NEXT_TASK.md
git hash-object docs/archive/2026-11-02/TASK_GRAPH.md
git rev-parse HEAD:docs/TASK_GRAPH.md
```

The paired blob hashes are compared before DOC01 commit. Local Markdown links in the rewritten current
documents, roadmap, report and archive README are checked with Test-Path relative to each document.
Historical frozen documents retain their original relative-path context as explained in archive README.
The JSON audit is parsed and checked for exactly 15 distinct IDs and successful archive responses.

Results: PASS for all three original/archive Git blob pairs, 38 local links, 15 distinct successful
archive operations, and git diff --check (exit 0). Blob IDs before DOC01:

- PROJECT_STATE: c812e906a42951ed12ce162c0e400b07a249883e
- NEXT_TASK: 917859730ddee1534da3295791a56e63b36c16d2
- TASK_GRAPH: 269ee3ca4a4f0b7260d13cf07eb088c9bd5403d7

No Unity/test/build commands were run for this docs-only cleanup. Prior UI08-R5a evidence is referenced,
not reissued as a fresh result. Future gameplay work still requires relevant tests and a Windows build.
