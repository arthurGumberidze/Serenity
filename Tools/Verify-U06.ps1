param(
    [string]$ProjectPath = (Split-Path $PSScriptRoot -Parent),
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [switch]$ManagedTestCluster
)

$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$logPath = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Force -Path $logPath | Out-Null

& "$PSScriptRoot/Verify-U05A.ps1" -ProjectPath $ProjectPath -EditorPath $EditorPath -ManagedTestCluster:$ManagedTestCluster
if ($LASTEXITCODE -ne 0) { throw 'U05A regression gate failed.' }

$testArgs = @(
    '-batchmode', '-nographics', '-projectPath', ('"' + $ProjectPath + '"'),
    '-logFile', ('"' + (Join-Path $logPath 'U06-tests.log') + '"'),
    '-runTests', '-testPlatform', 'EditMode', '-testCategory', 'U06',
    '-testResults', ('"' + (Join-Path $logPath 'U06-tests.xml') + '"')
)
$tests = Start-Process $EditorPath -ArgumentList $testArgs -WindowStyle Hidden -Wait -PassThru
if ($tests.ExitCode -ne 0) { throw "U06 tests failed (exit $($tests.ExitCode)); see Logs/U06-tests.log." }

[xml]$result = Get-Content (Join-Path $logPath 'U06-tests.xml') -Raw
if ($result.'test-run'.result -ne 'Passed' -or [int]$result.'test-run'.passed -lt 29 -or
    [int]$result.'test-run'.failed -ne 0 -or [int]$result.'test-run'.skipped -ne 0) {
    throw 'U06 focused EditMode suite did not pass every required test.'
}

Write-Output "U06-tests : $($result.'test-run'.passed) passed, $($result.'test-run'.failed) failed, $($result.'test-run'.skipped) skipped."
Write-Output 'U06 validation passed: character domain tests and all inherited U05A gates.'
