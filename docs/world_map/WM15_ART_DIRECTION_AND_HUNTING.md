# WM15 — approved art direction and animal health, 2026-10-25

Status: source implementation, **UNCOMPILED / UNTESTED / NOT VISUALLY ACCEPTED**.
The user's prohibition on tests, Unity/Player execution, compilation and rebuilding remains active.
`Builds/WorldMapV2Feedback/Serenity.exe` is unchanged. Earlier route-h/build-l evidence does not cover this work.

## Accepted direction

The user answered the 13 questions: **1 A, 2 B, 3 A, 4 C, 5 A, 6 B (eight regions approved),
7 B, 8 B, 9 A for lakes/rivers plus separate murky swamp water, 10 A at the start and B locally with
weather/night preserved, 11 A+B**. For 12, vivid imagery comes first, followed by rows, varied crops,
small fences, animals in pens and detail density. Rejected: empty fields, monotony, abrupt terrain,
insufficient scenery and small living-world details. References: Stranded Alien Dawn, Anno 1800, Tropico 5.
These answers supersede the earlier analysis-only restriction on map appearance.

## Implemented in source

- Eight configurable regional foliage palettes with continuous world-space blending. Grass has more weight
  than dirt in the starting valley and southern meadows. Terrain-layer tint copies leave vendor assets intact.
- Conifers in the northern forest, willow/swamp trees in wetlands, clustered wildflowers in meadow/open/grove
  areas. Existing canonical vegetation identities, resource stock and selection remain attached to the same trees.
  No new per-tree Update and no camera-triggered generation of the vegetation plan.
  Native pine/willow materials had empty URP `_BaseMap` slots despite retaining `_Main_Texture` references.
  Project-owned prefab/material copies reconnect those atlases, avoiding new white foliage from that conversion.
- A large permanent pink flowering grove plus two smaller spring-flowering pockets. This is a stylized
  flowering treatment of existing broadleaf meshes, **not an imported botanical sakura model**. Ordinary
  deciduous crowns change seasonally; weather and night lighting remain active.
- Clearer blue-green water with real alpha transparency increasing with depth; wetland water is dark and
  nearly opaque. Expanded dry-bank blends reduce the steep walls around water. Outer scenic ridges, south/east
  sea and a continuation of the river mouth conceal the finite terrain boundary. This scenery is outside the
  playable/navigation area, not newly claimable land.
- Local haze is lighter around the start and stronger in wetland/forest areas. Night no longer inherits the
  old high minimum sun intensity. All lighting changes still use the weather/day-night profile.

## Health and hunting

- People retain their canonical six-part BodyHealth and injury/capability rules. Their inspector reports
  health out of 100; a dead character reports 0 even when non-vital body parts retain health.
- `AnimalHealthRegistry` persists animal identity, species, maximum/current HP, domestic status, shot progress,
  shot sequence and harvested status. Domestic animals additionally persist home, sex and birth tick.
  Wildlife tier/camera changes do not recreate HP. Dead wildlife stops roaming, migrating and reproducing.
- Canonical wildlife individuals now have matching hunting production sites. Legacy deer sites receive the
  same HP authority. Resource ecology may not resurrect a harvested animal under its previous identity;
  wildlife births create fresh identities instead.
- The hunter approaches to bow range. Drawing and arrow flight are separate timed phases. At flight completion,
  deterministic accuracy and the physical range/obstruction adapter determine damage. The arrow uses an admitted
  model; misses aim wide. Successful impacts emit a short blood effect; visual effects cannot create damage.
- At 0 HP, the hunter moves to the corpse, performs a separate skinning task, and only then produces meat/hide
  into the existing canonical output/haul system. Another worker cannot simultaneously harvest the same animal.
  Cancelling a shot preserves damage already inflicted but clears an unfinished shot.
- Current tuning: deer 120, boar 150, bear 300, hare/waterfowl 30, sheep 90, cow 180, pig 120 HP;
  arrow 30 damage, 85% base accuracy, 18m range, 60 game seconds draw + 24 game seconds flight.
  At normal clock speed these shot phases are approximately 1s + 0.4s real time. All are in AnimalHunting.json.
- Physical hunting is restricted to presented Tier1 hunters; aggregate workers choose other production.
  There is no invented offscreen raycast result. The strategic factions' separate aggregate production retains
  its domain policy. A complete aggregate hunting model is outside this checkpoint.

## First domestic pen

An owned, completed, empty, unreserved field offers **«Сделать овечий загон»**. It stops growing crops and keeps
the fence/entrance. One founding male/female pair is granted once per saved campaign, not once per rebuilt pen.
Toon Farm sheep have stable IDs, selectable HP, a saved home, physical avoidance and corpse/harvest state.
Living mature male and female sheep can produce offspring after five game days, up to four unharvested animals;
maturity takes three days. These timings/capacity are configurable. Automatic hunters do not slaughter domestic
stock. Explicit interaction with a foreign pen's animal is rejected by its inspector.

This is a first pasture/breeding implementation, not a complete husbandry economy: no wool/milk production,
livestock market, feeding ration, transport between pens or cow/pig herds is claimed. Cow/pig HP definitions are
available, but this checkpoint instantiates sheep only. Fish remain fishing-stock-backed schools, not individual
combat targets. White Rabbit is still absent locally; the pre-existing hare proxy has not been replaced.

## Persistence, assets and execution boundary

Production XML has an optional `animals` extension; older snapshots without it remain readable. New snapshots
preserve HP/corpses/shot progress and domestic homes/sex/age. Older binaries are not promised forward compatibility
with the extended snapshot. No old saves were deleted. Recipe timing and generated shoreline changes require
future migration/restore and visual checks before release.

New project-owned bow/arrow and sheep dependencies, plus references to already tracked nature assets, are listed
in `WM15_ART_HUNT_ASSETS.json`. No paid asset purchase or vendor mutation. Existing user imports remain untouched.

Commands used for file authoring (not game/test execution), from C:\serenity_game:

```powershell
& 'C:/Users/Lenovo/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' Artifacts/WM15ArtHunt/source_edits.py
& 'C:/Users/Lenovo/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' Artifacts/WM15ArtHunt/hunting_art.py
& 'C:/Users/Lenovo/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' Artifacts/WM15ArtHunt/map_source_edits.py
& 'C:/Users/Lenovo/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' Artifacts/WM15ArtHunt/livestock_art.py
& 'C:/Users/Lenovo/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' Artifacts/WM15ArtHunt/final_source_edits.py
& 'C:/Users/Lenovo/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' Artifacts/WM15ArtHunt/nature_materials.py
git -c core.safecrlf=false diff --check -- Assets/Game
```

The asset admission helpers completed (8 hunting files, 4 sheep files, 9 foliage prefab/material copies); map edits and UTF-8 name preservation
completed. Text/diff review found no whitespace errors. These results establish **no compile/runtime/visual or
performance pass**. Exact Unity/batchmode/test/build commands executed: **none**.

After the user permits execution, the required checks are save/reload of wounded/dead/domestic animals,
blocked and missed arrows, automatic approach/skinning/haul, pen ownership and breeding, visual bow grip and
corpse pose, transparent water/fish, shoreline navigation, start/grove/swamp day/night views, missing materials,
and the deferred 100/300/500-resident performance gates. WM15 remains IN_PROGRESS.
