# Persistent Unity test/build runner

`Tools/UnityRunner.ps1` keeps one Unity 6000.6.4f1 Editor process alive and sends compile, EditMode-test and
Windows Development-build commands to it. The queue lives under ignored `Library/SerenityPersistentRunner`,
so commands survive an ordinary assembly/domain reload without adding mutable project content.

## Start and inspect

Run from the repository root in PowerShell:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityRunner.ps1 start
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityRunner.ps1 status
```

The first `start` may pay the normal project-open/import cost. Later commands reuse the PID reported by
`status`; they do not start another Editor. If this project is already open, `start` uses the PID in
`Library/EditorInstance.json` and waits for that Editor instead of opening a competing project process.
The launched Editor is an ordinary hidden Unity Personal Editor: the runner never supplies `-runTests`,
`-batchmode`, `-nographics`, or `-quit`.

## Compile changed scripts

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityRunner.ps1 compile `
  -OutputPath Artifacts\PersistentUnityRunner\compile
```

Compile asks the live Editor to synchronously refresh the Asset Database. Unity compiles only changed scripts;
an already-current project returns quickly. `result.json` and `command.log` record the Editor PID, Unity
version, timestamps, duration, warnings/errors and exit code.

## Run EditMode tests

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityRunner.ps1 test `
  -TestNames 'Game.Tests.Editor.BuildingFoundationTests.DefinitionIdIsSeparateValidatedContentIdentity' `
  -OutputPath Artifacts\PersistentUnityRunner\editmode
```

`-TestNames`, `-GroupNames`, `-CategoryNames` and `-AssemblyNames` accept PowerShell arrays and map directly to
Unity Test Framework filters. The asynchronous API is intentional: multi-frame EditMode tests are not removed.
Every completed test command writes authoritative `test-results.xml`, `result.json` and `command.log`. A
selection with zero test cases fails closed with exit code 1 and still writes XML with `total="0"`.

## Build a Development Player

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityRunner.ps1 build `
  -BuildPath Builds\LocalDevelopment\Serenity.exe `
  -OutputPath Artifacts\PersistentUnityRunner\build `
  -TimeoutSeconds 3600
```

Build uses the existing `U00Build` preflight, Windows x64 Development settings and immutable-source check. It
writes the Player, `build-provenance.json`, `result.json` and `command.log`. Unity build-worker processes are
ignored by the runner; only the primary PID from `Library/EditorInstance.json` may own the queue.

## Stop

```powershell
# Disable the runner but leave an already-open Editor running.
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityRunner.ps1 stop

# Also close the Editor, only if this runner launched that exact PID.
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityRunner.ps1 stop -QuitOwnedEditor
```

The ownership check refuses to close an Editor opened by the user or another tool.

## Exit codes and evidence

- `0`: the Editor reported success.
- `1`: compilation/test/build completed and failed, including a zero-test selection.
- `2`: protocol, interrupted-process or timeout failure; no success is inferred.

Each invocation defaults to a timestamped folder below `Artifacts/PersistentUnityRunner`. The long-lived Editor
log is `Logs/PersistentUnityRunner/Editor.log`. A timeout creates a failed local `result.json`; it never invents
test XML or a successful build result. Commands are serialized, so callers may queue work without racing the
Editor main thread.
