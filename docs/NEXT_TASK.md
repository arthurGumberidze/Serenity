# NEXT_TASK.md

## Active task: U04A - PostgreSQL Persistence Layer

### Prerequisite
U04 DONE. Read docs/U04_HANDOFF.md and D-012. Unity remains pinned to 6000.6.4f1.

### Goal
Implement PostgreSQL persistence behind the U04 ISaveStore boundary with runtime state remaining in RAM. Follow FRS_Unity_v0.3.docx Appendix A and docs/updates/2026-10-03_postgres_assets.md.

### Required output
- Provider separated from gameplay, versioned schema migrations and transactional writes/rollback.
- Secrets outside Git; integration tests against a separate test database.
- Save-session creation and stable-ID round trips required by the U04A specification.
- Preserve U04 local provider, time round trips and failure safety; complete mandatory tests/build.

### Scope boundary
U04A has not started. Do not begin U05, asset integration or Character gameplay as part of U04A without resolving its explicit scope. The U04 chat stops after its commit and report.
