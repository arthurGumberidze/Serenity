# U00 — Среда, Unity-проект и Git

## Scope and implemented work
- Unpacked the supplied context archive into the repository root, retaining both reference documents.
- Initialized Git on main, installed local LFS hooks and asset rules (including DOCX references).
- Created the project from the installed Editor's bundled URP blank template 17.2.1. Preserved template content/packages; no gameplay or U01 layer architecture implemented.
- Pinned Unity 6000.6.4f1 (12bfff696524), by explicit user instruction. URP 17.6.0; Test Framework 1.8.0; the complete dependency closure is in Packages/packages-lock.json.
- Verified installed Windows Mono and IL2CPP player variations. U00 builds use Windows x64 Mono with BuildOptions.Development.
- Added U00Build.WindowsDevelopment, three environment EditMode tests, and Tools/Verify-U00.ps1.
- Enabled Force Text serialization and Visible Meta Files. Excluded local Unity Hub, archive duplicate, build outputs, IDE output and validation checkout.

## Verification commands
Executed from C:\serenity_game in PowerShell:

```powershell
git --version
git lfs version
unity editors --json
unity releases --lts --limit 10 --json
git init -b main
git lfs install --local
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U00-import.log
./Tools/Verify-U00.ps1
```

The verification script expands to these Unity operations (Start-Process waits and checks exit codes):

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U00-tests.log -runTests -testPlatform EditMode -testFilter EnvironmentTests -testResults C:\serenity_game\Logs\U00-tests.xml
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U00-build.log -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment
```

## Results so far
- Git 2.51.0.windows.2 / LFS 3.7.0; local LFS initialization successful.
- Initial 6000.3.25f1 installation was denied; the user then explicitly approved keeping installed 6000.6.4f1. No replacement Editor installed.
- Initial import completed with exit code 0 and no C# compiler errors.
- First test run: 2 passed, 1 failed because the template had no default render pipeline. Fixed GraphicsSettings to reference PC_RPAsset. Original failure retained locally as Logs/U00-tests-initial.xml.
- Repeat tests: 3 passed, 0 failed, exit code 0.
- Main and clean-checkout build verification completed successfully; final evidence appears below.

## Decisions
D-008 documents the user-approved Editor override. The original FRS reference remains unchanged as historical input. The only added assembly is an Editor-only U00 environment test assembly; U01 architecture is still unimplemented.

## Limitations
Batchmode verification does not constitute a visual gameplay review. This is the stock empty sample scene with environment/build infrastructure. Clean checkout verification uses the installed licensed Editor and may use the machine's package download cache, but never copies Library.

## Main checkout build and player startup
- Windows Development / Mono x64 build: exit code 0; `U00_BUILD_RESULT: Succeeded; errors=0; warnings=2`.
- Warnings: `Hidden/Core/DebugOccluder` and `Hidden/Core/DebugOcclusionTest` have all SubShaders stripped because their pipeline tags do not match the configured pipeline. These are template/package debug shaders; no package sources were patched.
- Output: Builds/Windows/Serenity.exe (667136 bytes), accompanying data and runtime files retained locally, excluded from Git.
- Additional startup command:

```powershell
$u00Player = Start-Process -FilePath 'C:\serenity_game\Builds\Windows\Serenity.exe' -ArgumentList @('-batchmode','-nographics','-quit','-logFile','C:\serenity_game\Logs\U00-player.log') -WindowStyle Hidden -PassThru -Wait
```

The player initialized Mono, PhysX and loaded the scene with no logged exceptions. It did not terminate automatically with `-quit`, so only this exact smoke-check process was stopped. The wrapper consequently returned exit code -1 and reported failure. This is startup evidence only, NOT a passed automatic player-exit test or a visual rendering check. Mandatory U00 Editor tests/build checks are separate.

## Final clean-checkout evidence and end-of-chat protocol
First implementation commit: aa377bc (`U00: bootstrap URP project and Windows environment checks`).

```powershell
git clone . Validation/U00-clean
git -C Validation/U00-clean lfs fsck
./Tools/Verify-U00.ps1 -ProjectPath ./Validation/U00-clean
git -C Validation/U00-clean diff --stat
git lfs fsck
```

- Before verification, Validation/U00-clean had no Library directory. No generated files were copied from the main workspace.
- LFS restored all three objects (template PNG and two DOCX references); fsck OK in both repositories.
- Clean import and compilation succeeded. EditMode tests: 3 passed, 0 failed, exit 0; XML copied to docs/validation/U00-clean-tests.xml.
- Clean Windows build: exit 0; `U00_BUILD_RESULT: Succeeded; errors=0; warnings=2`. Same two debug shader stripping warnings as the main build.
- Logs: Validation/U00-clean/Logs/U00-tests.log, U00-tests.xml and U00-build.log. Main test evidence: docs/validation/U00-main-tests.xml.
- During build, Unity temporarily generated performance-test resources and changed preloadedAssets; these were restored/removed at completion. Final git diff --stat is empty. Git status can report seven rewritten settings due to line-ending conversion; there are no semantic/content diffs.
- All required U00 criteria pass under the user-approved Editor override. U00 marked DONE. PROJECT_STATE and DECISIONS updated; NEXT_TASK selects U01 for the next chat. No U01 work performed.
- Final protocol/evidence changes are committed separately with a U00 prefix. Only documentation/evidence changed after the clean-checkout build.

## Prompt for next chat
Read AGENTS.md, docs/PROJECT_STATE.md, docs/NEXT_TASK.md, docs/TASK_GRAPH.md and docs/DECISIONS.md. Execute only active U01. Use Unity 6000.6.4f1 per D-008. Do not redo U00. Build/test and complete the end-of-chat protocol.
