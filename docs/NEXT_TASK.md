# NEXT_TASK.md

Active task: **P10.9 acceptance follow-up (PARTIAL; wait for user direction)**.

The user-directed video follow-up is implemented: old authored trees are anchored to tiled ground and hidden with their terrain tile; tactical character visibility covers maximum RTS zoom; repeated LOD refresh does not restart NavMesh paths; context-menu group Move uses separated formation endpoints; `map_serenity` reveals the canonical full-world fog grid. Focused tests and a Windows BuildReport passed. The user's local visual retest in the new Player is still needed; do not mark the full P10.9 acceptance gate DONE from automated evidence alone. If a resident is reported delayed, inspect critical needs/protected work phase rather than silently bypassing that canonical rule.

Stage 1 is complete and its contracts are the foundation: normal new games use 144 separate 1000 × 1000 m Terrains; deterministic world-coordinate heights make seams exact; river/lake water and buildability share the same authority; the visual ring stays bounded while physical actors and active command corridors retain collision/NavMesh; the strategic view/fog/global graph use the full 12 km bounds; and `p109-tiled-world-v2` saves are isolated from preserved P10 saves. See `docs/P109_STAGE1_HANDOFF.md` and D-151.

Stage 2 is complete. Every tile has deterministic canonical resources; approved forest/berry/hunting/ore/fishing regions add stable concentrated nodes and industrial zones; local Terrain painting/water/field/settlement presentation and strategic geography use the tiled authority; the broad legacy route strips are disabled; and streamed GameObjects are disposable views over canonical state. Exact evidence is in `docs/P109_STAGE2_HANDOFF.md` and D-152.

Do not start P10.10. Await explicit user direction on the remaining P10.9 acceptance work: the inherited P10.3/P10.4 manual product-route debt and whether/when to resume the deferred performance gate. Do not repeat the completed tiled-world Stage 1 or Stage 2 work.

Performance profiling/optimization remains deferred by the user's instruction. Do not run the historical 60+300 s benchmark or claim its p95 gate. Do not start P10.10, and do not change P10.3/P10.4 or P8.6/U29/U30/U31 statuses. Existing P10 saves must remain preserved, read-only and unmigrated.
