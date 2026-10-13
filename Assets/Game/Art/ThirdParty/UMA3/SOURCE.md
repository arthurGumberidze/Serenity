# UMA 3 runtime content

- Source: <https://github.com/umasteeringgroup/UMA>
- Revision: tag `v3.1f3`, commit `c92bf29f6f1c17c8bf05d6465288e76b2dfedd08`
- License: MIT; engine package license is available at
  `Packages/com.umasteeringgroup.uma/LICENSE.md` after package resolution.
- Imported for P8.1 on 2026-10-13/14.

This directory is a selective copy of official UMA3 runtime content. It includes races,
wearables, textures, animation, DNA and the getting-started avatar prefab from the tagged
source checkout. The `SRP` subset comes from the official 1.03 GiB `UMA31_f3.unitypackage`
GitHub release because the source checkout does not contain the material definitions required
by the UMA3 overlays. Demo scenes, tests, vehicles, sample GUI, HDRP content and legacy UMA2
content are intentionally excluded. The selected runtime footprint is about 692 MiB; this was
explicitly accepted by the user for the visual prototype.

Canonical Serenity appearance remains `Game.Domain.Characters.AppearanceState`; UMA is a
replaceable presentation provider and the P8.1 scene retains a project-owned fallback.
