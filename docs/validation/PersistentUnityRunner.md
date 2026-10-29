# Persistent Unity runner validation — 2026-10-29

Scope: infrastructure only. No gameplay, water presentation, content asset or `Builds/WM15UserRetestFinal`
file was changed. Unity version: 6000.6.4f1. Source commit at validation start: `f8cd8cb2`.

All successful operations below used the same live Editor PID `34784`.

## Incremental compile

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityRunner.ps1 compile `
  -OutputPath Artifacts\PersistentUnityRunner\validation-compile-final4 -TimeoutSeconds 600
```

Exit 0. `result.json`: 0 errors, 0 warnings, 0.263 s. Evidence:
`Artifacts/PersistentUnityRunner/validation-compile-final4/`.

## Focused EditMode

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityRunner.ps1 test `
  -TestNames 'Game.Tests.Editor.BuildingFoundationTests.DefinitionIdIsSeparateValidatedContentIdentity' `
  -OutputPath Artifacts\PersistentUnityRunner\validation-test-repeat -TimeoutSeconds 600
```

The same command and output path ran twice consecutively, proving callback cleanup and safe evidence overwrite.
Both exited 0. Final NUnit XML is well-formed and reports total 1, passed 1, failed 0; final duration: 5.539 s.
Evidence: `Artifacts/PersistentUnityRunner/validation-test-repeat/`.

The assembly-graph regression
`Game.Tests.ArchitectureTests.AllProjectAssemblyDefinitionsAreAcyclicAndLayersAreExplicit` also passed 1/1
through PID 34784 in 6.069 s; evidence: `Artifacts/PersistentUnityRunner/validation-architecture/`.

Fail-closed selection check:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityRunner.ps1 test `
  -TestNames 'Game.Tests.Editor.DoesNotExist' `
  -OutputPath Artifacts\PersistentUnityRunner\validation-empty-selection -TimeoutSeconds 600
```

Expected exit 1. XML is well-formed and reports total 0; JSON status is `failed`. This proves that an empty or
mistyped filter cannot become a false green run.

## Windows Development build

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityRunner.ps1 build `
  -BuildPath Artifacts\PersistentUnityRunner\ValidationBuildFinal2\Serenity.exe `
  -OutputPath Artifacts\PersistentUnityRunner\validation-build-final2 -TimeoutSeconds 3600
```

Exit 0. Build result: Succeeded, 0 errors, 0 warnings in 41.909 s. The executable and
`build-provenance.json` exist. Provenance records Unity 6000.6.4f1, source commit
`f8cd8cb2deb413dc7556650e0b9a25d8f614d4ef`, build GUID `b17d64ee115744ae860a747987b377ab` and source
fingerprint `5322D5E9C3BAE886191528EA6C76CEE6A1AF7921BA123F23C207C47BDD9BEB5B`.

The pre-existing retest executable remained at `Builds/WM15UserRetestFinal/Serenity.exe`; its SHA-256 after
validation was `437691663C5FB21231524F4886E60A10B1841F13E42FBBC93EE39FBD827930CD`.
