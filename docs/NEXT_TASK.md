# NEXT_TASK.md

Active task: **P10.9 — «Сквозная интеграция и стабилизация» (PARTIAL)**.

## User correction, 2026-10-19 — authoritative current scope

Continue P10.9 with the video-reported camera, tree LOD, strategic-map and LMB regressions, then migrate the world to **12 × 12 Terrain tiles, each 1000 × 1000 m (12 × 12 km, 144 km²)**. Do not scale a single giant Terrain. The latest explicit map legend and save policy override the earlier, incorrect interpretation of coloured rectangles. See `docs/P109_WORLD_REVISION.md`.

Current implementation: camera/tree/LMB repairs pass affected PlayMode 20/20; grid/configuration foundation plus affected EditMode passes 21/21. The player explicitly chose the lower near-central grey settlement. **Next unblocked implementation slice is shared-coordinate terrain generation and bounded tile-view loading**, replacing single-Terrain queries before switching new-game bootstrap. The current playable scene is still the old 512 m world; do not claim the 12 km migration is done. Keep old saves separate when activating the new generation. Verify normal Player visuals/physical input after runtime integration.

Current implementation: camera/tree/LMB repairs pass affected PlayMode 20/20; grid/configuration foundation plus affected EditMode passes 21/21. The player explicitly chose the lower near-central grey settlement. **Next unblocked implementation slice is shared-coordinate terrain generation and bounded tile-view loading**, replacing single-Terrain queries before switching new-game bootstrap. The current playable scene is still the old 512 m world; do not claim the 12 km migration is done. Keep old saves separate when activating the new generation. Verify normal Player visuals/physical input after runtime integration.

The new geography is for **new games only**. Preserve existing save files; do not migrate, delete, overwrite or silently reinterpret old-world coordinates. This authorizes new geography, not a Unity Editor/package migration. The user temporarily paused execution and then explicitly resumed it. Existing edits and partial test output must be inspected before continuing; interrupted runs are not passes.

Performance profiling/optimization remains deferred by the user's 2026-10-18 instruction; the historical failed gate below is evidence, not an instruction to resume benchmark runs. Do not claim P10.9/P10.10 DONE from this repair slice.

P10.8 is DONE. Do not reopen its canonical diagnostic commands, atomic storage semantics, stable-ID army/family creation or snapshot-v25 irreversible cheat evidence unless P10.9 exposes a regression. Exact implementation and evidence are recorded in `docs/P108_HANDOFF.md`, D-148 and `Artifacts/P108`.

Work only on P10.9. The retained checkpoint narrows/terrain-projects the 13 route strips, restores four nearby stable starter resources, closes the inherited U15 physical-walk regression, adds an exact-Full-HD performance profile and produces a provenance-stamped final build. Focused/affected tests and four final-build Player subsystem smokes pass. Exact evidence is in `docs/P109_HANDOFF.md` and `Artifacts/P109`.

Remaining gates are explicit. Two valid focused Full-HD 60+300 s repeats fail at p95 60.618/75.126 ms (third diagnostic 48.428 ms also fails and contains background samples), so optimize the render/GPU path to p95 ≤33.33 ms with three fully focused repeats. The native computer-control surface did not expose the Unity Player, so physical Shift-click/Shift-command, slow multi-frame drag, needs/protected-carry resume, Tier1→Tier2→Tier1, the remaining target/action matrix and the complete normal-Player resource/industry route remain unproven. The two U31 failures remain separately BLOCKED. P8.6/U29/U30/U31 remain PARTIAL/PARTIAL/IN_PROGRESS/BLOCKED.

Do not implement P10.10 final product acceptance in P10.9.
