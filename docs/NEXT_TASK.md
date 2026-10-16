# NEXT_TASK.md

Active task: **P10.3 — «Выбор, контекстные действия и очереди» (PARTIAL; finish visible Player acceptance only)**.

Source contract: `docs/P101_REQUIREMENTS_AND_TECHNICAL_BASELINE.md`, especially the P10.3 interaction/queue requirements and acceptance matrix. Start from the committed P10.2 connected world. Work only on P10.3; do not begin ecology/industry, Character production, family, military capture, cheats or final performance work owned by P10.4–P10.9.

P10.3 must deliver reliable resident/group selection by click, Shift and drag box with practical hit proxies; right click must always open the contextual action menu and expose disabled reasons rather than silently doing nothing. Manual commands and Shift queues must be canonical and survive the required representation/Save-Load boundaries. Needs may suspend queued work, but ordinary autonomous jobs are disabled: only needs remain autonomous. Preserve the P10.2 world/fog/save authority, `StableEntityId`, the hybrid tier design and the single scheduled AI/runtime boundary.

Implementation, focused/affected EditMode/PlayMode tests and the final provenance-bearing Windows x64 build are complete. After the user dismissed the firewall prompt, visible D3D11 Player evidence passed LMB selection, RMB eight-action/reason presentation, edge clamping, ordinary Move/Guard commands and selection continuity through the UI Save/Load route. That run exposed and fixed context-menu click-through and explicit Load-time selection clearing.

The only remaining gate is the unobserved part of the normal visible mouse-driven route: physical Shift-click selection, Shift+menu-click tail queueing, a slow multi-frame drag box, critical hunger/rest and protected-carry interruption/resume, Tier1→Tier2→Tier1 continuity, and the remaining natural-source/pile/Character target-action combinations. The available Computer Use API cannot hold Shift across a mouse click, and its drag completed inside one render frame; keyboard-focused Shift+Return/Space did not activate the menu button and is not accepted as evidence. A human must run and record the exact remaining route from `docs/P103_HANDOFF.md`. Do not start P10.4 until this evidence is green.

No new Asset Store/package dependency is authorized. New P10 external content remains limited to the already approved Unity URP Terrain Sample 1.0.3 and UMA 3.1f3; ask before importing anything else.

P8.6/U29/U30/U31 remain PARTIAL/PARTIAL/IN_PROGRESS/BLOCKED. P10.2's measured 1920×1080 frame-time debt is retained for the feature-complete P10.9 gate and must not be relabelled as solved by P10.3.
