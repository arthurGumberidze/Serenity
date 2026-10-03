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
- Build and clean checkout verification pending at preparation time; final results recorded below when complete.

## Decisions
D-008 documents the user-approved Editor override. The original FRS reference remains unchanged as historical input. The only added assembly is an Editor-only U00 environment test assembly; U01 architecture is still unimplemented.

## Limitations
Batchmode verification does not constitute a visual gameplay review. This is the stock empty sample scene with environment/build infrastructure. Clean checkout verification uses the installed licensed Editor and may use the machine's package download cache, but never copies Library.
