# UI08-R4 circular HTML research proposal — 2026-11-01

User requested the Anno reference format after rejecting the horizontal OR-link tangle. This is still an HTML
design-review slice. No Assets, Unity code, game saves or dependencies changed.

Deliverable: `docs/prototypes/research-review-v4/Serenity-Research-Review.html` (self-contained offline HTML).
R3 remains available as history. R4 changes actual proposed prerequisites: one parent per technology,
no OR and no cross-direction requirements. Direction hubs, small thematic circular nodes, short top-down
branches, hover/focus names, right-side details and a separate epoch strip replace rectangular lanes.

Commands/results:
- `node --check docs/prototypes/research-review-v4/catalog.js` — PASS.
- `node --check docs/prototypes/research-review-v4/app.js` — PASS.
- `node docs/prototypes/research-review-v4/verify.cjs` — PASS: 51 IDs, all 33 legacy IDs, valid DAG, no OR,
  exactly one parent per research, no cross-direction or mandatory future gate. Configured circles do not
  overlap; dependency segments neither cross unrelated segments nor pass through unrelated circles.
  Actual controller code with a minimal DOM stub verifies route gates, search, start/finish, blocked starts,
  fire alone opening cooking, one-time material payment, practical hide and epoch food requirements.
- `node docs/prototypes/research-review-v4/build.cjs` — PASS: portable HTML bundle regenerated.
- `git diff --check` — PASS.

Limits: geometric checks are mathematical, not a screenshot/render test. The previous browser tool rejection
prohibited local-file preview and workarounds; no alternate route was attempted. Mouse pan/zoom, CSS rendering
and full-screen appearance remain subject to user review. No Unity compile/test/build was run for this HTML-only
revision; game implementation and its persistent-runner validation await approval.
