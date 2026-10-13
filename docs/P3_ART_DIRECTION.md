# P3 art direction — Stone Age shore camp

Status: reversible reference baseline implemented on 2026-10-13. It is suitable for P4/P5 production work, but final visual acceptance belongs to the user and remains open.

## Visual target

Use a warm, grounded stylized-PBR/mid-poly language: readable silhouettes, restrained material response, natural greens/browns/stone greys, warm fire and daylight, and no mixture of realistic high-frequency assets with toy-like scenery in one focal cluster. One Unity unit is one metre. Shapes must read both from the default RTS height and from the monarch's close/TPS camera.

The reference composition places water and a curved sandy/rocky shore on the left, an open buildable clearing in the centre, and a tree/rock frame on the perimeter. The shelter/storage/fire cluster is the human focal point. Vegetation must leave navigation, characters and future construction cells visible; it is framing, not a wall. Long soft shadows give scale without hiding interactable silhouettes.

## Implemented reference inventory

- 18 approved WizardHat trees, 10 approved WizardHat rocks and one approved WizardHat campfire.
- 52 project-owned low-cost grass clusters, a curved shore mesh and a water plane.
- One canonical deterministic shelter and one canonical deterministic storage building, tinted with shared earth/wicker URP materials.
- Canonical starting Character presenters with approved stone axe and spear attachments; the wider ten-person starter roster remains canonical and may materialize through the existing tier policy.
- Clear daylight, overcast/rain fog and light changes, downward stretched rain, soft masked fire/smoke, fire flicker, and looping procedural campfire/shore/rain ambience.
- Project-owned reusable URP materials/meshes under `Assets/Game/Art/Materials/P3` and `Assets/Game/Art/Meshes/P3`.

## Production constraints for P4 and P5

- Preserve the open centre, shore/buildable separation and readable routes when expanding the region.
- New character parts must be compatible with the selected body/rig/attachment contract; appearance IDs must survive Tier transitions and Save/Load.
- Use shared materials and wrapper prefabs; do not edit vendor sources. Add simple colliders only where gameplay needs them, and add LOD/batching rules before multiplying costly content.
- Keep weather legible without obscuring UI, selection or close combat. Audio remains spatial and low enough not to mask feedback.
- Any new external art requires a confirmed source/license, registry entry, scale/pivot/material/rig/collider/LOD review and explicit purchase/import approval when applicable.

## Known provisional gaps

- Hodaart people are grade-C cartoon/low-poly placeholders and do not meet the eventual semi-realistic character target.
- Shelter/storage geometry is a project-owned primitive placeholder; P3 supplies a cohesive colour/material treatment, not final Stone Age construction art.
- Water is a simple transparent URP surface without waves, foam or shoreline interaction. Terrain is a compact authored mesh/clearing rather than the final P5 region.
- The close clear camera shows the procedural skybox ground band at the high horizon; camera/sky refinement belongs with P5 world composition.
- No new vendor source was selected because all visually tempting unapproved packages remain `LICENSE_REVIEW_REQUIRED`.

## Acceptance evidence

The final normal Windows Player route used seed `310033`, entered through Main Menu → New Game, and captured RTS clear, monarch TPS clear and monarch TPS rain at 1280×720. The smoke validates canonical Character/building links, stable identity, two equipped tools, two spatial ambience sources, active campfire FX, clear/rain projections and both camera modes. Evidence is listed in `docs/P3_HANDOFF.md`.
