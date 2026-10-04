# NEXT_TASK.md

## Active task: U08 — Строительная сетка и blueprint-система

### Prerequisites
U04, U05 and U05A are DONE. U07 is also DONE and provides the current LocalGameplay composition and selection integration that U08 must preserve.

### Goal
Implement the first data-driven building placement and construction foundation so a house can be planned as a blueprint, completed from explicit state and restored without making a scene GameObject the canonical building record.

### Scope boundary
U08 has not started. Read `docs/U07_HANDOFF.md`, the U04 persistence boundary, the U05 input/selection architecture and the U05A wrapper/license constraints before implementation. Do not implement resources/inventories (U09), worker AI (U10/U11), production (U15), combat/destruction (U20/U24), global-map streaming (U25) or full gameplay UI (U27).

### Relevant handoffs
Keep persistent identity and construction state in Domain/Simulation data, use presentation objects as replaceable projections, and preserve the explicit `LocalSceneCompositionRoot` dependency style. Existing primitive shelter assets are placeholders only and may be replaced through presentation/configuration without changing canonical building state.
