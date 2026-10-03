# U02 — Пакеты и базовая конфигурация

## Result
U02 is complete. The FRS 42.4 hybrid stack is installed and pinned in `Packages/manifest.json`, and the complete resolved dependency graph is committed in `Packages/packages-lock.json`. `docs/PACKAGES.md` records the selected versions and baseline settings. The architecture dependency graph is unchanged; U02 adds no gameplay or ECS bootstrap.

The direct package set is Entities 6.6.0, Burst 2.0.0, Collections 6.6.0, Mathematics 1.4.0, Cinemachine 6.6.0, Addressables 2.11.2, Input System 1.20.0, AI Navigation 2.0.12, URP 17.6.0 and Unity Test Framework 1.8.0. The installed Editor supplies Jobs and its Unity 6.6 engine modules. Unity generated the empty Entities client build-filter setting committed under `ProjectSettings`.

## Verification commands
Executed from `C:\serenity_game` in PowerShell:

```powershell
./Tools/Verify-U02.ps1
```

Then a detached worktree containing only committed files was created and verified with no `Library` or `Temp` directory:

```powershell
git worktree add --detach C:\serenity_game-u02-clean HEAD
& 'C:\serenity_game-u02-clean\Tools\Verify-U02.ps1' -ProjectPath 'C:\serenity_game-u02-clean'
```

The verifier invoked these Unity commands in each project path and checked their exit codes:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath <project> -logFile <project>\Logs\U02-tests.log -runTests -testPlatform EditMode -testResults <project>\Logs\U02-tests.xml
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath <project> -logFile <project>\Logs\U02-build.log -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment
```

## Results
- Clean detached worktree: package restore and project open succeeded; 20/20 EditMode tests passed in 1.092 s; Windows x64 / Mono / Development build exit 0.
- Clean build result: `Succeeded; errors=0; warnings=2`. The warnings are the existing stripped debug shader warnings documented since U00.
- Clean player: `Builds/Windows/Serenity.exe`, 667136 bytes.
- The verifier hashed `Packages/manifest.json` and `Packages/packages-lock.json` before tests/build and confirmed both remained unchanged after restore and build.
- Test evidence: `docs/validation/U02-clean-tests.xml`.
- `git diff --check` passes for the final U02 change set.

The first U02 import exposed an ambiguous `PackageInfo` type in the new test; the alias was made explicit before the successful runs. An earlier sandboxed Editor attempt crashed while validating the external licensing client and left `Temp/UnityLockfile`; the stale lock from that terminated process was removed and all recorded verification used successful licensed batch processes.

## Scope limits
No U03 clock/calendar code, U05 camera/input implementation, U12 ECS bootstrap, Addressables content group or gameplay system was added. U03 is the next selected task because U01 satisfies its dependency; it was not started in this chat.
