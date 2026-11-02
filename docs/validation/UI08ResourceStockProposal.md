# UI08-R5 — HTML resource/stock proposal validation

Date: 2026-11-02. Scope: proposal only, pending the user's HTML approval.

Deliverable: `docs/prototypes/stock-review-v1/Serenity-Stock-Review.html`.
Per-resource plan: `docs/RESOURCE_STOCK_PLAN.md`, generated from the prototype catalog by the build command.

## Evidence and boundaries

- Read AGENTS/project/task/decision/architecture records; inspected relevant FRS resource/logistics,
  production and work-group paragraphs directly from the DOCX XML.
- Audited actual ResourceCatalog.asset: 42 IDs, 41 represented and water intentionally excluded.
  Cooking/equipment/feed/medicine/butchering yields come from ProductionModels.cs. Current edible IDs and
  feed ratios were checked in ProductionService.cs. The actual food norm is 2/day; user-requested 3/day
  exists only in this proposal. No claim of an existing sword or hunter-bow integration.
- HTML contains six resource groups regardless of stock amounts, separate bell/settings cells, responsive
  layouts, bounded fixed popovers, keyboard focus/Escape, editable manual targets, automatic preview/apply,
  per-resource priority/reserve, large/empty stock scenarios, an editable daily menu and recipe availability.
- Monthly example: 48 × 3 × 30 = 4,320 portions. One dish per day; default cycle starts fish, meat, berries,
  dried meat. Unavailable dishes are removed rather than counted as available production.
- Operational stock horizon is separate and adjustable (default 3 days, up to 30). The preview exposes
  insufficient manual input targets and capacity overflows; it does not silently resize manual targets.
- The previous conversation's browser policy rejected local-file navigation and prohibited workarounds.
  No proxy, screenshot bypass or alternative browser transport was used. Rendered browser QA, pixel geometry
  and actual hover visuals remain unverified. The app file-open tool queued the local artifact for viewing;
  that queue acknowledgment is not a rendered-preview result.
- No Assets or gameplay save files changed. Unity was not launched; no Unity tests/build are claimed.
  There is no new instruction to repeat the previously completed one-time Windows shutdown.

## Commands and results

```powershell
node --check docs/prototypes/stock-review-v1/catalog.js
node --check docs/prototypes/stock-review-v1/app.js
node docs/prototypes/stock-review-v1/verify.cjs
node docs/prototypes/stock-review-v1/build.cjs
git diff --check
```

Syntax checks PASS. Verification PASS: exact catalog coverage, no duplicate IDs, daily food quantities,
single/open/no-recipe menus, zero population, role-specific equipment, assigned-item subtraction,
manual protection and conflicts, input netting, intermediate-reserve propagation, safe large integer counts.
The actual app code also runs in an event-capable DOM stub: preview/apply, automatic population updates,
manual edits surviving recalculation, all six HUD groups under 10,000-each stock, bell opening and Escape closing.
These are behavioral checks, not a browser renderer or accessibility audit.

The first verification attempt used one too many parent-directory segments to locate the source catalog;
the fixture path was corrected and the complete verification then passed. The HTML build inlines CSS/JS,
has no network dependencies and is intended to open directly as one local file.

## Implementation gate

Await user review. After approval, bind to canonical inventory reservations, issued equipment, active roles,
recipe unlocks, pending deliveries and actual storage/shelf-life rules; use the persistent Unity runner for
the implementation's tests/build. Do not treat demo treatment/equipment numbers as authoritative game rules.
