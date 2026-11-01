# Research tree — approved UI08-R4 implementation

Status: **APPROVED**, implemented for validation on 2026-11-02. The user approved the Anno-inspired R4 HTML
for Unity and separately accepted the group contribution cap. This supersedes the earlier horizontal/OR rules.
The reviewed HTML remains in `docs/prototypes/research-review-v4/Serenity-Research-Review.html` as design history.
Runtime definitions and positions live in `Assets/Game/Domain/Research/StoneAgeResearchPlan.cs`; the partial
GameplayUiController.Research.cs renders those definitions without owning progression state.

Five large circular direction hubs lead into smaller round icon nodes. Progress goes top to bottom. Local
branches fork without long inter-direction connectors. Every research has exactly one actual predecessor;
all configured edges are visible. We simplified the proposed rules rather than concealing requirements.
Cooking needs fire alone. Fishing follows shelters, farming follows reliable housing, and military organization
follows formation training. Players can still pursue multiple branches. All 51 technologies/33 legacy IDs remain.

The common root fans into five hubs. Leaf names appear on hover/focus and for the selected technology, with
full descriptions and prerequisites in the right panel. Search, category emphasis, fit, zoom/pan and centering
are retained. The HTML uses local SVG line art; Unity reuses the existing project-owned HUD atlas.
No external art or Anno assets were copied. The runtime viewport fits all 51 nodes at 1920x1080; detail text
wraps and has its own vertical scroll. UI pointer capture blocks the world camera below it.

The epoch is a separate bottom strip without incoming graph edges. Its rules remain:
- 14 non-starting, non-future technologies.
- At least 3 technologies in each of any 3 directions.
- A key achievement in each of any 3 directions.
- Fire, stone tools and shelter construction.
- One built shelter, 40 food in stocks and 10 living residents.

Military and complete-tree completion are not mandatory. Four demo presets satisfy the epoch. Later eras
remain U38; remaining Stone Age technologies stay accessible. Future-only research cannot gate playable nodes.
Costs, unlock bindings, practical/material gates and bounded science modifiers are now runtime rules.
The immutable ResearchBalanceSettings config exposes contribution/epoch/practical thresholds.

Care is labelled «Лечиться». Resident priority/research/scout/military task controls are removed, including the
old research shortcut in the selection ribbon. Object/task priorities remain. The scientist professional group
and its current shift supply researchers through the existing central simulation callback; no NPC Update loop.
Validation: `docs/validation/UI08ResearchR4.md` (runtime), `docs/validation/UI08ResearchProposalR4.md` (proposal).
