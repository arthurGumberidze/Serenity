# NEXT_TASK.md

Active task: **P10.5 — «Персонажи, одежда, LOD и анимация»**.

The user explicitly authorized this sequencing exception on 2026-10-18. P10.4 remains **PARTIAL**, not DONE: P10.5 may use its committed implementation but must not claim or repair its acceptance debt unless separately directed. The mandatory deferred P10.4 gates are the affected U15 Tier1 physical-walk regression (currently 14/15 PlayMode), missing build provenance, incomplete saved normal visible D3D11 resource/industry route and telemetry, and the native UnityPlayer/UIAutomation crash observed on Alt+F4 shutdown. See D-143 and `docs/P104_HANDOFF.md`.

Work only on P10.5. Deliver a unified original soft-stylized look for the hero and residents, credible visible diversity, stable Character and environment LOD behavior, correct RTS/TPS locomotion, work and combat animation, and a military wardrobe hook for P10.7. Preserve canonical Character identity/state, the connected-world authority, P10.3 queues, P10.4 resource/industry services and P10 save isolation. Keep mass-NPC presentation bounded; do not add an Update loop or full Animator cost per Character.

Use only project-owned content and the already approved Unity URP Terrain Sample 1.0.3 and UMA 3.1f3. If the required look, rig, clothing or animation coverage cannot be delivered from those sources, record the exact missing categories and request approval before importing any new Asset Store/package dependency. Do not silently accept placeholders as final art.

Completion requires focused and affected EditMode/PlayMode tests, Windows build provenance, and a normal visible 1920×1080 D3D11 Player route proving hero/resident appearance diversity, LOD transitions without identity or pose defects, RTS/TPS locomotion, representative work/combat animation, wardrobe behavior and Save/Load/Tier continuity. Record all warnings, performance observations and visual limitations honestly.

Deferred debt remains mandatory for P10.9/P10.10: all P10.4 blockers above; P10.3 physical Shift-click/Shift-command, slow multi-frame drag, needs/protected-carry resume, Tier1→Tier2→Tier1 and remaining target/action matrix; and P10.2's measured 1920×1080 p95 128.342 ms performance debt. P8.6/U29/U30/U31 remain PARTIAL/PARTIAL/IN_PROGRESS/BLOCKED.
