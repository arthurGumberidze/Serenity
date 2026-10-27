# WM15 — current feedback candidate (2026-10-27)

Status: implementation and automated verification complete; WM15 remains IN_PROGRESS for user visual
acceptance, slow initial loading and deferred population/performance gates. Candidate:
`C:/serenity_game/Builds/WorldMapCurrentFix/Serenity.exe`. Start a new game; existing saves were not deleted.

## Implemented feedback

- Manual fishing requires a completed player-built fishing pier. The pier is now a compact 4×4 adaptive
  footprint and exposes every dry water-facing bank candidate, including steep banks up to a 20 m local
  foundation delta; water, protected areas, occupancy and access remain hard blockers. The large fishing
  camp retains its six prepared non-overlapping pads per lake.
- Visible free fish form separated randomized schools of 6–15 fish. Rotation is rate-limited instead of
  snapping to a new heading, and additional solitary fish use a silver metallic presentation. Canonical
  finite fish stock remains authoritative.
- The boar rig faces its travel direction. Wild cattle placement is restricted to the lake/mountain-foot
  habitat instead of forest ravines.
- Winter terrain uses owned snow albedo, normal and height data plus additional snow rocks. The mountain
  snow mask now combines altitude, slope and two-frequency breakup: summit shelves accumulate bright snow,
  while steep faces remain dark exposed rock instead of receiving one broad grey ellipse.
- Movement endpoint selection and retry handling were tightened so residents do not remain stuck on an old
  or unreachable NavMesh point.
- Campfire illumination is a further three times stronger (intensity 45); ground torches are twice as bright
  (intensity 12). Their falloff radii remain unchanged.
- A completed pasture starts empty and is never presented as a berry plot. Its inspector lets the player
  choose sheep, cattle, pigs, goats, ducks, chickens or horses and explicitly settle one male/female pair.
  Each pen keeps its one chosen species and cannot grant another pair after restore. Saved birth tick and sex
  continue to drive gradual three-day growth and reproduction from a living adult pair.
- Crop interaction separates Grow and Harvest actions. Crop hover text reports lifecycle growth, including
  ready `100%`, instead of the natural-source `0/0` representation.

## Verification

Unity: `C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe`, version 6000.6.4f1.
Unity and Player were launched as hidden Windows processes outside the filesystem sandbox so the normal
Unity Personal license/profile was available. No Computer Use or visible Editor window was used for the
accepted test/build/Player runs.

Focused EditMode command:

```text
-batchmode -nographics -projectPath C:/serenity_game -runTests -testPlatform EditMode -testFilter Game.Tests.Editor.WM15CurrentFeedbackTests;Game.Tests.Editor.WM15CurrentPresentationTests;Game.Tests.Editor.WM15ShorePlacementTests -testResults C:/serenity_game/Artifacts/WM15Current/editor-batch-b.xml -logFile C:/serenity_game/Artifacts/WM15Current/editor-batch-b.log
```

Result: exit 0, **9/9 passed**, 0 failed, 0 skipped. The log resolves Unity Personal. An earlier
in-sandbox `author-a` attempt exited 198 before project execution because the sandbox-created licensing
client had no entitlement; this was an execution-environment failure, not a source/test result.

Final Windows Development build:

```text
-batchmode -nographics -projectPath C:/serenity_game -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/WorldMapCurrentFix/Serenity.exe -quit -logFile C:/serenity_game/Artifacts/WM15Current/build-batch-c.log
```

Result: exit 0; `Succeeded; errors=0; warnings=30`. Build GUID
`3deaf406f85b4bf6b489f0ab9f11fa6b`; immutable source fingerprint
`FC12B7EA6B56F91CA06A66CB815409C55E302A48C65281D5F817556DDD333607`.
The preceding build-batch-b was manually interrupted after compilation and is not acceptance evidence.

Disposable Player route, with `SERENITY_SAVE_DIRECTORY` set to
`C:/serenity_game/Artifacts/WM15Current/player-a-saves`:

```text
C:/serenity_game/Builds/WorldMapCurrentFix/Serenity.exe -screen-fullscreen 0 -screen-width 1440 -screen-height 900 -wm15OctoberSmoke C:/serenity_game/Artifacts/WM15Current/player-a -wm15Evening -wm15EveningFollowup -logFile C:/serenity_game/Artifacts/WM15Current/player-a.log
```

Result: exit 0, **32/32 passed**, no gameplay runtime errors. It covered world loading, finite fish stocks,
free schools, lake construction/shore previews, boar/cow population and the physical fishing route. This is
functional evidence, not subjective visual acceptance of every new animal, winter or lighting asset.

Durable summaries are under `docs/validation/WM15Current/`; complete local logs and captures remain under
`Artifacts/WM15Current/`. Full 100/300/500 resident gates were not run.

## Latest correction validation (2026-10-27)

- `editor-f.xml`: 10/11; only the newly added adaptive-pier count threshold failed (21 actual versus an
  arbitrary 24 expected at `lake_mountain`). The corrected exact case passed 1/1 in `editor-g.xml`; no
  production code changed between the runs. Effective requested regression result: 11/11.
- `build-e.log`: Windows Development build succeeded with 0 errors / 82 warnings. GUID
  `7b04788a7032443fa98021ac3e9d1f95`; fingerprint
  `6E6A82877C9DE5BADFB01832B01F514B76E468C3573AEF1FB622DDAD6A524FA4`.
- `player-b.log`: 55 passed checks and `No runtime errors`, including pasture, lake/fish/shore and manual
  fishing routes. Two old hunt-route timing assertions failed outside this correction (bow-draw sample and
  hunt completion); damage, wood and fish completion still passed. This does not replace user visual review.
