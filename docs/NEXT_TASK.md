# NEXT_TASK.md

## Active task: U02 - Пакеты и базовая конфигурация

### Prerequisite
U00 and U01 DONE. Unity 6000.6.4f1 is the user-approved pinned Editor (D-008). Follow the assembly dependency contract in docs/ARCHITECTURE.md and see docs/U01_HANDOFF.md for verification evidence.

### Goal
Establish the Unity package set and baseline project configuration required by the FRS technical profile, with a stable restore on a clean checkout.

### Required output
- Add and pin the required Unity packages for the planned hybrid stack (including Entities/DOTS, Burst/Jobs/Collections/Mathematics, Cinemachine and Addressables where not already present).
- Add only package references needed for assemblies to compile; preserve the dependency directions documented in docs/ARCHITECTURE.md.
- Record package versions and relevant baseline project settings.
- Verify package restore and project compilation from a clean checkout, run relevant tests and a Windows Development build, then complete the end-of-chat protocol.

### Do not do
Do not implement gameplay systems from U03 onward or the Tier2 ECS bootstrap from U12. Do not change the pinned Editor version or add Asset Store dependencies without explicit approval. This file selects the next chat; U02 has not started.
