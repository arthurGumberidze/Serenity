# UI01 design handoff

Date: 2026-10-31<br>
Status: DONE, user approved

## Deliverables

- `docs/UI01_INTERFACE_REDESIGN.md` — screen map, navigation, HUD hierarchy, entity cards, workflows,
  service boundaries, accessibility requirements and staged implementation plan.
- `docs/ui01/prototype/index.html` — dependency-free interactive prototype.
- `docs/ui01/prototype/screenshots/` — ten representative states at 1920x1080 and 1280x720.
- `docs/ui01/prototype/verification-report.json` — reproducible browser-verification result.

## Verification

The following commands were run from `C:\serenity_game`:

```powershell
& 'C:\Users\Lenovo\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe' --check docs/ui01/prototype/prototype.js
```

Result: passed with exit code 0.

```powershell
$env:NODE_PATH='C:\Users\Lenovo\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\node_modules'
& 'C:\Users\Lenovo\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe' docs/ui01/prototype/verify.cjs
```

Result after revision 7: passed. Headless Microsoft Edge rendered 20 quiet black-and-gold PNGs with the
two-role Cyrillic typography system, navigated through the bottom menu,
tested group-to-member resident selection, the categorized research dependency tree and its filter, enforced
the compact construction-drawer height, checked actionable group controls and automatic-assignment presentation,
tested the local mini-map, keys 1-9 and map-to-diplomacy navigation,
observed zero console/page errors and found no horizontal overflow at 1920x1080 or 1280x720.
The group-screen check also rejects internal scheduling phrases from player-facing copy and requires actionable
work, commander, shift and priority controls instead.

Static palette checks measured 15.53:1 primary-text, 7.91:1 secondary-text and 8.20:1 gold-accent contrast
against the darkest panel; dark text on the primary gold button measured 6.35:1. All four exceed the prototype's
minimum readability target for the text sizes used.

```powershell
git diff --check -- docs/PROJECT_STATE.md docs/NEXT_TASK.md docs/TASK_GRAPH.md docs/DECISIONS.md docs/UI01_INTERFACE_REDESIGN.md docs/UI01_HANDOFF.md docs/ui01
```

Result: passed; only line-ending notices were reported for pre-existing tracked Markdown files.

No Unity runtime, scene, asset or canonical state changed in UI01. Unity tests and a Player build were therefore
not run and are not claimed. The user approved revision 7; UI02 must run focused UI EditMode/PlayMode checks,
resolution captures and a Windows Development build for its own implementation slice.

## Approved baseline

The following decisions are approved for UI02 implementation:

1. World-first HUD with a compact group tracker and a virtualizable member picker for 1000–2000 residents.
2. Bottom navigation with direct worker-group and military entries plus combined Realm/diplomacy.
3. Local mini-map, world-visible compact construction drawer, right context sheet and contextual command ribbon.
4. Categorized dependency-tree research map.
5. Profession-aligned exclusive work groups, urgent requeue semantics and automatic assignment of new residents.
6. Quiet Black and Gold visual direction, one-accent color hierarchy, two-role Cyrillic typography and current
   information density.

Internal scheduling rules, design rationale, prototype watermarks and verification annotations belong only in
Domain code, tests and documentation. They must not appear as literal gameplay labels.

## Exact UI02 handoff

Start from the current committed runtime UI, not from experimental UI02 edits. Implement only the foundation:
design tokens/theme resources, the bottom navigation shell and route model, Attention entry point, compatible
stable element names, and the approved typography asset strategy. Preserve all existing canonical commands and
keep UI state disposable. Do not implement the complete resident, group, research, construction, military or
realm workspaces in UI02; those remain UI03–UI06.

UI02 is complete only after focused EditMode and PlayMode tests pass, both required resolutions are visually
captured and reviewed, a Windows Development build succeeds, documentation is updated and a separate `UI02:`
commit is created without unrelated WM15/terrain/vendor changes.

## Future user participation

- Review the first real Unity captures at 1920×1080 and 1280×720@125% because browser fidelity cannot prove
  final UI Toolkit spacing and font rendering.
- Approve any exact redistributable Cyrillic font pair before new font files are admitted to the repository.
- Review diplomacy behavior only when a canonical diplomacy service is designed; UI01 approves only its honest
  unavailable/discovery presentation.
- Perform the final end-to-end usability and visual acceptance during UI07 after all functional workspaces exist.
