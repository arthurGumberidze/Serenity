param(
    [string]$ProjectPath = (Split-Path $PSScriptRoot -Parent),
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [switch]$ManagedTestCluster
)

$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path

& "$PSScriptRoot/Verify-U04A.ps1" -ProjectPath $ProjectPath -EditorPath $EditorPath -ManagedTestCluster:$ManagedTestCluster
if ($LASTEXITCODE -ne 0) { throw 'U04A regression gate failed.' }

$logPath = Join-Path $ProjectPath 'Logs'
$arguments = @(
    '-batchmode', '-nographics', '-projectPath', ('"' + $ProjectPath + '"'),
    '-logFile', ('"' + (Join-Path $logPath 'U05-playmode-tests.log') + '"'),
    '-runTests', '-testPlatform', 'PlayMode',
    '-testResults', ('"' + (Join-Path $logPath 'U05-playmode-tests.xml') + '"')
)
$process = Start-Process $EditorPath -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "U05 PlayMode tests failed (exit $($process.ExitCode)); see Logs/U05-playmode-tests.log." }

[xml]$result = Get-Content (Join-Path $logPath 'U05-playmode-tests.xml') -Raw
if ($result.'test-run'.result -ne 'Passed' -or [int]$result.'test-run'.passed -lt 2 -or [int]$result.'test-run'.skipped -ne 0) {
    throw 'U05 PlayMode suite did not pass all required tests.'
}

Write-Output "U05-playmode-tests : $($result.'test-run'.passed) passed, $($result.'test-run'.failed) failed, $($result.'test-run'.skipped) skipped."
Write-Output 'U05 validation passed: all inherited EditMode/PostgreSQL tests, LocalGameplay PlayMode tests, and Windows Mono Development build.'
