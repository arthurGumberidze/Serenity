param(
    [string]$ProjectPath = (Split-Path $PSScriptRoot -Parent),
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [switch]$ManagedTestCluster
)

$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$logPath = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Force -Path $logPath | Out-Null

$generateArgs = @(
    '-batchmode', '-nographics', '-projectPath', ('"' + $ProjectPath + '"'),
    '-logFile', ('"' + (Join-Path $logPath 'U07-generate.log') + '"'),
    '-quit', '-executeMethod', 'Game.Infrastructure.Editor.U07PresentationFoundation.Build'
)
$generate = Start-Process $EditorPath -ArgumentList $generateArgs -WindowStyle Hidden -Wait -PassThru
if ($generate.ExitCode -ne 0) { throw "U07 generation failed (exit $($generate.ExitCode)); see Logs/U07-generate.log." }

& "$PSScriptRoot/Verify-U06.ps1" -ProjectPath $ProjectPath -EditorPath $EditorPath -ManagedTestCluster:$ManagedTestCluster
if ($LASTEXITCODE -ne 0) { throw 'U06 regression gate failed.' }

$editArgs = @(
    '-batchmode', '-nographics', '-projectPath', ('"' + $ProjectPath + '"'),
    '-logFile', ('"' + (Join-Path $logPath 'U07-editmode-tests.log') + '"'),
    '-runTests', '-testPlatform', 'EditMode', '-testCategory', 'U07',
    '-testResults', ('"' + (Join-Path $logPath 'U07-editmode-tests.xml') + '"')
)
$edit = Start-Process $EditorPath -ArgumentList $editArgs -WindowStyle Hidden -Wait -PassThru
if ($edit.ExitCode -ne 0) { throw "U07 EditMode tests failed (exit $($edit.ExitCode)); see Logs/U07-editmode-tests.log." }

$playArgs = @(
    '-batchmode', '-nographics', '-projectPath', ('"' + $ProjectPath + '"'),
    '-logFile', ('"' + (Join-Path $logPath 'U07-playmode-tests.log') + '"'),
    '-runTests', '-testPlatform', 'PlayMode', '-testCategory', 'U07',
    '-testResults', ('"' + (Join-Path $logPath 'U07-playmode-tests.xml') + '"')
)
$play = Start-Process $EditorPath -ArgumentList $playArgs -WindowStyle Hidden -Wait -PassThru
if ($play.ExitCode -ne 0) { throw "U07 PlayMode tests failed (exit $($play.ExitCode)); see Logs/U07-playmode-tests.log." }

[xml]$editResult = Get-Content (Join-Path $logPath 'U07-editmode-tests.xml') -Raw
[xml]$playResult = Get-Content (Join-Path $logPath 'U07-playmode-tests.xml') -Raw
if ($editResult.'test-run'.result -ne 'Passed' -or [int]$editResult.'test-run'.passed -lt 4 -or
    [int]$editResult.'test-run'.failed -ne 0 -or [int]$editResult.'test-run'.skipped -ne 0) {
    throw 'U07 focused EditMode suite did not pass every required test.'
}
if ($playResult.'test-run'.result -ne 'Passed' -or [int]$playResult.'test-run'.passed -lt 4 -or
    [int]$playResult.'test-run'.failed -ne 0 -or [int]$playResult.'test-run'.skipped -ne 0) {
    throw 'U07 focused PlayMode suite did not pass every required test.'
}

Write-Output "U07-editmode-tests : $($editResult.'test-run'.passed) passed, $($editResult.'test-run'.failed) failed, $($editResult.'test-run'.skipped) skipped."
Write-Output "U07-playmode-tests : $($playResult.'test-run'.passed) passed, $($playResult.'test-run'.failed) failed, $($playResult.'test-run'.skipped) skipped."
Write-Output 'U07 validation passed: Tier 1 presentation tests and all inherited U06 gates.'
