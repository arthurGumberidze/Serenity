# NEXT_TASK.md

Active task: **P10.9 Stage 2 — tiled-world population and visual cleanup (PARTIAL)**.

Stage 1 is complete and its contracts are the foundation: normal new games use 144 separate 1000 × 1000 m Terrains; deterministic world-coordinate heights make seams exact; river/lake water and buildability share the same authority; the visual ring stays bounded while physical actors and active command corridors retain collision/NavMesh; the strategic view/fog/global graph use the full 12 km bounds; and `p109-tiled-world-v2` saves are isolated from preserved P10 saves. See `docs/P109_STAGE1_HANDOFF.md` and D-151.

Work only on P10.9 Stage 2. Populate and visually distinguish the approved data-driven zones: yellow agriculture, blue hunting, purple berries, three grey settlements, red river/fishing lakes, green tree concentrations and pink ore regions. Relocate or replace legacy 512 m authored projections so local content, resources, routes and settlement presentation match the tiled geography without introducing a second gameplay authority. Finish readable strategic geography/fog/labels and normal local presentation across representative near and far tiles. Preserve stable IDs and canonical domain services.

Add focused EditMode/PlayMode coverage for population ownership, deterministic placement, unload/reload continuity and representative zone visibility. Then run affected suites, produce a provenance-stamped Windows Development build, and inspect a normal visible 1920 × 1080 D3D11 Player route with local, far-tile and strategic screenshots. Do not use smoke instrumentation as a substitute for visual review.

Performance profiling/optimization remains deferred by the user's instruction. Do not run the historical 60+300 s benchmark or claim its p95 gate. Do not start P10.10, and do not change P10.3/P10.4 or P8.6/U29/U30/U31 statuses. Existing P10 saves must remain preserved, read-only and unmigrated.
