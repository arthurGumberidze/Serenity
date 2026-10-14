# P8.3 handoff — рабочие группы и простые задания

## Result

P8.3 is DONE at its implementation and evidence boundary. The player can select a local resource, pile, building or Character, choose a supported action and choose Normal, High or «Прямо сейчас». Global resource markers expose the same gather command. Nine built-in empty role groups are canonical `WorkGroup` objects; Characters can be assigned or reassigned, and the work panel reports membership, availability, suitability, reasons and live jobs.

Orders cover gather, build, haul, craft, farm, research, scout, military and care. Selection uses target distance, suitability and stable ID. Exclusive object targets reject competing live jobs. Cancellation releases reservations. Reprioritization is canonical. «Прямо сейчас» can interrupt only safe scheduler phases; direct control, critical rest, carrying, production work and delivery remain protected. Explicit gather/farm/craft commands use the selected `ProductionSite` and the existing production/inventory/claim authorities. No per-NPC `Update` was added.

Snapshot v18 persists groups, roles, members, commander, jobs, target positions/IDs, priorities, assignees, statuses and claims. Restore rejects duplicate identities, assignees and exclusive targets before rebuilding reservations. Older v1-v17 saves remain supported. Work uses `StableEntityId` and remains outside GameObject/Entity representations, preserving queued and assigned state across tier changes and Save/Load.

## Verification

- P8.3 EditMode: 6/6 passed.
- P8.3 headless PlayMode: 3/3 passed.
- P8.3 D3D11 PlayMode: 3/3 passed.
- affected persistence/work/UI EditMode: 12/12 passed.
- affected P8.2/UI/P8.3 PlayMode: 12/12 passed.
- Windows x64 Mono Development build: succeeded, errors=0, warnings=18.
- visible 1280×720 D3D11 Player: passed all eight flags for nine role groups, local click/action/priority, deterministic assignment, conflict safety, guarded immediate preemption, exact Save/Load and global resource action.
- `p83-local-work.png` and `p83-global-work.png` were visually inspected; both show the nine groups and the selected resource's three priority choices plus gather action.

The intended scripted route is:

```powershell
& .\Tools\Verify-P83.ps1 -ProjectPath 'C:\serenity_game' -PlayerSmokeOutput 'C:\serenity_game\docs\validation\P83\player-smoke'
```

Exact Unity/Player operations used for final evidence:

```text
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testCategory P83 -testResults C:\serenity_game\Logs\P83-focused-editmode.xml
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testCategory P83 -testResults C:\serenity_game\Logs\P83-focused-playmode.xml
Unity.exe -batchmode -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testCategory P83 -force-d3d11 -testResults C:\serenity_game\Logs\P83-gpu-playmode.xml
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter Game.Tests.WorkGroupAndJobTests;Game.Tests.PersistenceTests;Game.Tests.PlayerControlStateTests;Game.Tests.GameplayUiTests -testResults C:\serenity_game\Logs\P83-affected-editmode.xml
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.P82NaturalRegionPlayModeTests;Game.Tests.PlayMode.GameplayUiPlayModeTests;Game.Tests.PlayMode.P83WorkGroupPlayModeTests -testResults C:\serenity_game\Logs\P83-affected-playmode.xml
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -quit -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/P83/Serenity.exe
Builds\P83\Serenity.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -force-d3d11 -p83Smoke -p83SmokeOutput C:\serenity_game\docs\validation\P83\player-smoke
```

The successful builds emitted `U00_BUILD_RESULT: Succeeded; errors=0; warnings=18`, then the batch Editor lingered in post-success external curl shutdown timeouts. Only the verified build processes were stopped after the success marker; the final Player assemblies and visible smoke are from the rebuilt output.

A supplementary run of the complete legacy `WorkGroupPlayModeTests` passed 8/10. The two retained headless failures are an environment-sensitive world-ray selection assertion and a 20-second NavMesh haul completion timeout; the focused P8.3 and selected affected suites above pass, and no test was weakened or silently relabelled.

## Scope boundary and next task

P8.3 does not implement the P8.4 full appearance editor, P8.5 interface restyle, P8.6 integration/qualification, a complete U29 matrix, a new U30 endurance run or final U31 closure. U29 remains PARTIAL, U30 remains IN_PROGRESS and U31 remains BLOCKED. The next unblocked task is P8.4.
