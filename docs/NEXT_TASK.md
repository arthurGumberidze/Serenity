# NEXT_TASK.md

Active task: **P10.2 — «Единый связный мир и стратегический обзор»**.

Source contract: `docs/P101_REQUIREMENTS_AND_TECHNICAL_BASELINE.md`, especially sections 2.1 and 6. Work only on P10.2; do not begin selection/orders, ecology/industry, Character production, family, military capture or cheats owned by P10.3–P10.8.

P10.2 must deliver one canonical connected world with a same-world strategic view: Terrain footprint at least 512×512 m; at least 163,840 m² of accessible dry land measured on a 2 m grid; connected travel/buildable space; the six existing `WorldLocationId` values plus planned districts/settlements; lakes/rivers/mountains/forests/empty distances; fog/scouting; and building on accessible land except for explicit blockers. Preserve canonical `StableEntityId`, Domain/Simulation authority, hybrid tiers and data-driven definitions.

Definition of Done requires focused and affected EditMode/PlayMode tests, Windows x64 build, and a normal visible D3D11 Player route at exact 1920×1080 showing local RTS and the strategic camera over the same world. Record exact commands/results and commit with prefix `P10.2:`.

No new Asset Store/package dependency is authorized. New P10 external content is limited to the already approved Unity URP Terrain Sample 1.0.3 and UMA 3.1f3; ask before importing anything else.

P8.6/U29/U30/U31 remain PARTIAL/PARTIAL/IN_PROGRESS/BLOCKED and must not be resumed or relabelled by P10.2.
