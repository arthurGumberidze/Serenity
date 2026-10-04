# NEXT_TASK.md

## Active task: U05 — Local Scene / RTS Camera / Input

### Prerequisites
U02 DONE. U04A is also DONE and must remain isolated behind the persistence boundary. Unity stays pinned to 6000.6.4f1.

### Goal
Implement the local test scene foundation, RTS camera, selection and input/command flow according to `docs/TASK_GRAPH.md`, the FRS and existing assembly architecture.

### Scope boundary
U05 has not started. Read the relevant FRS sections and create a dedicated U05 plan before changes. Do not fold U05A asset acquisition, Character Domain, NPC simulation, PostgreSQL expansion, buildings, combat or other later tasks into U05.

### U04A handoff
Read `docs/U04A_HANDOFF.md`, D-013 and the U04A architecture section. Runtime remains authoritative in RAM. Do not add SQL to gameplay, Presentation or tick loops. Future composition must rebind GameClock consumers after successful load.
