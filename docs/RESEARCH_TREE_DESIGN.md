# Research tree — UI08-R4 design review

Status: **AWAITING USER APPROVAL**, 2026-11-01. The user rejected the horizontal OR-link tangle and requested
the attached Anno visual format. This latest instruction supersedes the earlier horizontal/OR requirements.
Open `docs/prototypes/research-review-v4/Serenity-Research-Review.html`; no game data is read or written.

Five large circular direction hubs lead into smaller round icon nodes. Progress goes top to bottom. Local
branches fork without long inter-direction connectors. Every research has exactly one actual predecessor;
all configured edges are visible. We simplified the proposed rules rather than concealing requirements.
Cooking needs fire alone. Fishing follows shelters, farming follows reliable housing, and military organization
follows formation training. Players can still pursue multiple branches. All 51 technologies/33 legacy IDs remain.

The common root fans into five hubs. Leaf names appear on hover/focus and for the selected technology, with
full descriptions and prerequisites in the right panel. Search, category emphasis, fit, zoom/pan and centering
are retained. Icons are project-local SVG line art; no external art or Anno assets were copied.
Layout is intended for a full 1920x1080 overview; browser rendering has not been verified.

The epoch is a separate bottom strip without incoming graph edges. Its rules remain:
- 14 non-starting, non-future technologies.
- At least 3 technologies in each of any 3 directions.
- A key achievement in each of any 3 directions.
- Fire, stone tools and shelter construction.
- One built shelter, 40 food in stocks and 10 living residents.

Military and complete-tree completion are not mandatory. Four demo presets satisfy the epoch. Later eras
remain U38; remaining Stone Age technologies stay accessible. Future-only research cannot gate playable nodes.
All costs and new unlock bindings/modifiers are still proposals, not existing game rules.

No Unity implementation before approval. Pending character requests remain: Care → «Лечиться», remove
per-resident priorities/task controls, retain task-level priorities and automatic professional-group assignment.
Validation: `docs/validation/UI08ResearchProposalR4.md`.
