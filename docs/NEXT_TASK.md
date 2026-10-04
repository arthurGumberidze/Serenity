# NEXT_TASK.md

## Active task: U05A — Asset Pipeline + Free Asset Acquisition

### Prerequisites
U02 and U05 are DONE. Unity stays pinned to 6000.6.4f1. Existing vendor packs and staging content remain uncommitted user data until U05A deliberately inventories and processes them.

### Goal
Define the runtime/staging asset layout, registry and license traceability; inventory the approved free/vendor content; normalize only the selected imports; and prepare project-owned prefabs/content for later presentation tasks according to `docs/TASK_GRAPH.md`, the FRS and asset update documents.

### Scope boundary
U05A has not started. Read the asset update documents, registry template and relevant FRS/package constraints before changes. Do not rework the completed U05 input/camera foundation, implement Character Domain, gameplay systems, PostgreSQL expansion, buildings or combat.

### U05 handoff
Read `docs/U05_HANDOFF.md`, D-014 and the U05 architecture section. Preserve `LocalGameplay` as the functional project-owned test scene while replacing placeholders only through explicit, traceable U05A asset decisions. Do not introduce vendor dependencies into camera/input code.
