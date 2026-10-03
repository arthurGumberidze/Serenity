# NEXT_TASK.md

## Active task: U03 - Игровое время и календарь

### Prerequisite
U01 DONE (and U02 also DONE). Unity 6000.6.4f1 is the user-approved pinned Editor (D-008). Follow the assembly dependency contract in docs/ARCHITECTURE.md and the package baseline in docs/PACKAGES.md. See docs/U02_HANDOFF.md for the latest verification evidence.

### Goal
Implement deterministic game time and calendar rules in the pure Domain/Simulation layers, including persisted clock state needed by later save/load work.

### Required output
- Define testable game-clock and calendar value types/rules without UnityEngine dependencies.
- Cover pause, time scale, day/calendar rollover and deterministic advancement with EditMode unit tests.
- Define and test the serializable time state needed for save/load; do not implement the full U04 persistence system.
- Preserve the documented assembly directions, run relevant tests and a Windows Development build, then complete the end-of-chat protocol.

### Do not do
Do not begin U04 save/load, U05 scene/camera/input or later gameplay tasks. Do not change the pinned Editor or package versions without a scoped decision. This file selects the next chat; U03 has not started.
