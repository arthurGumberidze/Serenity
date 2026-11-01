# Research tree — UI08-R3 design review

Status: **AWAITING USER APPROVAL**, 2026-11-01. The user's latest instruction requires an HTML
proposal before Unity implementation. Open `docs/prototypes/research-review-v3/Serenity-Research-Review.html`.
It is an offline, self-contained page; its demo never reads/writes game state or saves.

The latest attached brief supersedes the earlier vertical layout: five horizontal lanes progress left to
right. Data describes 51 technologies, preserving all 33 current IDs. Two subrows permit forks; the root is
separate, and genuinely independent proposals have no incoming edge. All mandatory edges are drawn.
AND and OR use distinct entry positions; OR has an explicit junction label and a complete explanation in
the right detail panel. The epoch card receives no graph edges.

The overview is designed for 1920x1080, fitting the entire graph with full descriptions in a separately
scrolling detail panel. Smaller screens retain fit/zoom/pan. Search, category emphasis, selected-node
dependency highlighting, centering, live demo states, practical conditions and route presets are included.
Stars identify key achievements. Future-only mechanics remain visible but cannot be researched.

Proposed epoch conditions, all mandatory:
- 14 non-starting, non-future technologies.
- At least 3 learned technologies in each of any 3 directions.
- At least one key achievement in each of any 3 directions.
- Controlled fire, stone tools and shelter construction.
- One completed shelter, 40 food in shared stocks and 10 living residents.

Military is optional. Peaceful, knowledge, military and mixed demo presets all satisfy these conditions.
Completing the milestone preserves access to the remaining Stone Age tree; later eras are still U38.
The numerical thresholds and material payments are proposals for approval, not current game rules.

Audit found many capability-only technologies without Simulation consumers. Rather than invent bonuses,
the proposal redistributes existing building/recipe unlocks and marks unsupported mechanics as future.
Only a small capped research-efficiency/worker-contribution modifier is proposed as new research-local logic.
Core NPC, military, production, save and world systems are unchanged.

Approval is still required before changing Unity. The earlier requests to rename Care to «Лечиться» and
remove per-resident priority/task controls remain explicitly recorded for the implementation follow-up.
Testing and browser-preview limitations: `docs/validation/UI08ResearchProposal.md`.
