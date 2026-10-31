# UI01 design handoff

Date: 2026-10-31<br>
Status: design ready, awaiting user approval

## Deliverables

- `docs/UI01_INTERFACE_REDESIGN.md` — screen map, navigation, HUD hierarchy, entity cards, workflows,
  service boundaries, accessibility requirements and staged implementation plan.
- `docs/ui01/prototype/index.html` — dependency-free interactive prototype.
- `docs/ui01/prototype/screenshots/` — nine representative states at 1920x1080 and 1280x720.
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

Result after revision 2: passed. Headless Microsoft Edge rendered 18 PNGs, navigated through the bottom menu,
tested selection through the persistent resident tracker, enforced the compact construction-drawer height,
tested the local mini-map, keys 1-9 and map-to-diplomacy navigation, observed zero console/page errors and found
no horizontal overflow at 1920x1080 or 1280x720.

```powershell
git diff --check -- docs/PROJECT_STATE.md docs/NEXT_TASK.md docs/TASK_GRAPH.md docs/DECISIONS.md docs/UI01_INTERFACE_REDESIGN.md docs/UI01_HANDOFF.md docs/ui01
```

Result: passed; only line-ending notices were reported for pre-existing tracked Markdown files.

No Unity runtime, scene, asset or canonical state changed in UI01. Unity tests and a Player build were therefore
not run and are not claimed. UI02 must run focused UI EditMode/PlayMode checks, resolution captures and a Windows
Development build after the user approves implementation.

## Approval gate

Before UI02, confirm or revise these decisions:

1. World-first HUD with a compact, scrollable resident tracker that selects and locates any resident.
2. Bottom navigation with direct worker-group and military entries plus combined Realm/diplomacy.
3. Local mini-map, world-visible compact construction drawer, right context sheet and contextual command ribbon.
4. Ash and Ember visual direction and current information density.
