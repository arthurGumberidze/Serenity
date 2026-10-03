# NEXT_TASK.md

## Active task: U04 - Stable IDs + Save/Load Core

### Prerequisite
U03 DONE. Unity 6000.6.4f1 is the user-approved pinned Editor (D-008). Preserve the pure Domain time state and Simulation clock contract introduced by U03. See docs/U03_HANDOFF.md for verification evidence.

### Goal
Implement StableEntityId and the versioned Save/Load core, including round-trip restoration of the U03 clock state.

### Required output
- Define stable persistent entity identifiers without Unity instance IDs or ECS Entity handles.
- Define versioned save DTOs and persistence boundaries following docs/ARCHITECTURE.md.
- Round-trip key state, including calendar ticks, biological ticks, selected speed and pause state.
- Add automated tests, run a Windows Development build and complete the end-of-chat protocol.

### Do not do
Do not begin PostgreSQL U04A, U05 scene/camera/input or later gameplay tasks. Do not add SQL to Domain/Simulation and do not change the pinned Editor or package versions without a scoped decision. This file selects the next chat; U04 has not started.
