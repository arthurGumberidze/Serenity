# WM15 follow-up — source checkpoint, 2026-10-24

Later same-day steering and changes are recorded in `WM15_CONTENT_SOURCE_REPAIR.md`: Polytope residents,
UMA rulers, approved White Rabbit (not locally available), removed fox/wolf, crop choice, tools and water.
That checkpoint supersedes the all-UMA-residents and three-species-replacement scope below.

Status: IN_PROGRESS, UNCOMPILED / UNTESTED. The user explicitly requested fixes while collecting more bugs,
without tests or rebuilding. No Unity Editor, batchmode, Player or build command was run for this checkpoint.
`Builds/WorldMapV2Feedback/Serenity.exe` remains the preceding candidate, not these changes.

## Implemented in source

- Quarry: `IndustrialWorkCoordinator` dispatches saved `Mine` and `Haul` orders through the existing WorkManager.
  Workers must be controllable, able to work, present in Tier1 and available for interruption. The authored
  worker capacity is respected, with a resident left for other settlement work. A canonical built facility and
  its operational player warehouse are required. Existing technology/cost prerequisites remain in force.
  The Tier1 scheduler calls IndustrialExtractionService only after the worker reaches the quarry. Work is
  processed in bounded shifts; extraction remainder, stock and jobs use existing saves. Real output inventory
  is carried in batches up to eight, bounded by the worker inventory and warehouse capacity. Missing/full storage
  stops new mining assignments. Instant one-day payout buttons were removed. Dispatch runs once per game minute.
  Movement timeout now measures lack of movement for direct tasks and haul, allowing distant map routes.
- Trees: all rendered tree variants (including the former decorative tree variants, excluding shrubs/ground cover)
  can receive bounded nearby collision/selection proxies. Their canopy volume comes from source mesh bounds.
  Proxies bind stable production sources and the existing selection/context/harvest pipeline. Finite timber stock
  is saved in Production; depletion removes the corresponding batched tree. No additional persistent resource
  authority or camera-dependent ecology registration was introduced. The existing 650-proxy budget is retained.
- Fish: V2 now uses the existing shared 3D fish mesh/tail scheduler, rather than the particle-mask prefab.
  Each school binds its canonical fishing source; displayed fish count drops with remaining stock. Canonical
  bank work points and population recovery remain unchanged. Both lake and river fish use this path.
- Characters/editor: generated UMA atlas channels are rebound to URP BaseMap where necessary, and legacy
  color-only hair/skin/eye shader parameters are projected to BaseColor. Generation waits up to 30 seconds.
  The editor reuses its UMA instance and displays preparation/failure status instead of silently presenting
  the toon fallback as the edited result. Failed generation is explicit and does not enable that fallback.
  UMA now takes precedence over role-body replacement. The avatar allocation cap matches the 32-resident
  detailed simulation tier, while the existing animation/draw budget remains separate. This increases avatar
  memory/generation work relative to the old cap of eight and requires later measurement. Canonical role clothing
  IDs remain preserved; the admitted UMA tunic is still the supported garment set.
- Ownership: player authority resolves canonical settlement ownership, falling back to registered allegiance
  only for residents without settlement membership. Move, context, direct-role assignment, combat, military attack
  and TPS entry receive this permission predicate. Foreign inspection is distinct from command selection, so
  single-person training/research controls cannot accidentally act on the inspected foreign resident. Group/box
  selection excludes foreign residents. Foreign inspectors are read-only; player rosters no longer fall back
  to all characters when empty. Warehouse selection for player haul/quarry funding is restricted to player stock.

## Pending

- Hare, fox and wolf models are **not replaced**. Searches of local prefab/FBX filenames and inventories of
  `Low Poly Asset Bundle v1.20`, `Animals FREE` and the local `Low Poly Nature Pack` found no suitable species.
  Existing cat/dog placeholders remain in the wildlife catalog. The user has been asked for another local asset
  location or authorization to select new Asset Store models. No downloads or purchases occurred.
- Compilation and runtime verification of every change above are deliberately deferred. The old 30/30 tests,
  build-l and route-h are historical evidence for the prior commit only, not evidence for this checkpoint.
- Later acceptance needs real quarry arrival/work/haul and save/load, all tree variants and depletion after reload,
  visible fish at each lake, UMA preview/body/material consistency, and negative foreign-command cases. Do not
  execute these until the user allows validation. The changed UMA allocation contract also needs its old cap-of-eight
  assertions reconciled with the new requirement when validation work resumes.

## Commands / execution boundary

Only source/package reads, edits and static diff review were performed. `git diff --check` was used for whitespace
review; it is not a compiler or gameplay test. Unity/batchmode/test/build commands: **none**, by explicit user instruction.
The seven inherited material changes and `New Unity Project/` are excluded from this checkpoint.
