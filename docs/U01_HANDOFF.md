# U01 — Архитектура assemblies и слоёв

## Result
U01 is complete. Added compiled boundaries for `Game.Domain`, `Game.Simulation`, `Game.ECS`, `Game.Presentation`, `Game.Infrastructure` and Editor-only `Game.Tests`. The approved graph is acyclic and enforced by four architecture tests. `docs/ARCHITECTURE.md` now records layer and service ownership, command/data flow, scene/session lifetime, save-state ownership, ECS synchronization and tier bridges, and the testing strategy.

The runtime assemblies intentionally contain only assembly metadata in U01. Gameplay, package installation and DOTS implementation remain assigned to later tasks. This keeps U01 limited to architecture while still forcing every boundary through Player compilation.

## Verification command
Executed from `C:\serenity_game` in PowerShell:

```powershell
./Tools/Verify-U01.ps1
```

The script ran these Unity commands via `Start-Process -Wait` and checked their exit codes:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U01-tests.log -runTests -testPlatform EditMode -testResults C:\serenity_game\Logs\U01-tests.xml
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U01-build.log -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment
```

## Results
- Final EditMode run: exit 0; 7 passed, 0 failed. This includes 3 existing environment tests and 4 architecture tests. Result copied to `docs/validation/U01-main-tests.xml`.
- Windows x64 / Mono / Development build: exit 0; `U00_BUILD_RESULT: Succeeded; errors=0; warnings=2`.
- The two warnings are the existing URP template debug shader stripping warnings already documented by U00; U01 introduced no compiler or build errors.
- Player output: `Builds/Windows/Serenity.exe`, 667136 bytes.
- `git diff --check` passes after protocol updates.

The first attempt could not open the project because an existing Unity process held `Temp/UnityLockfile`. After the user released Unity, two intermediate test runs exposed overly broad assertions about Unity's implicit precompiled assembly references (4/7, then 6/7). The checks were narrowed to the architectural guarantees controlled by project asmdefs. The final complete test and build run above passed.

## Scope limits
No package set was changed and no gameplay, scene bootstrap, persistence implementation or ECS system was added. U02 is the next selected task because U00 and U01 satisfy its dependencies.
