param(
    [string]$ProjectPath = (Split-Path $PSScriptRoot -Parent),
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [switch]$ManagedTestCluster
)

$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$logPath = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Force -Path $logPath | Out-Null

function Invoke-Unity([string]$Name, [string[]]$Arguments) {
    $base = @('-batchmode', '-nographics', '-projectPath', ('"' + $ProjectPath + '"'),
        '-logFile', ('"' + (Join-Path $logPath "$Name.log") + '"'))
    $process = Start-Process $EditorPath -ArgumentList ($base + $Arguments) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "$Name failed (exit $($process.ExitCode)); see Logs/$Name.log." }
}

Invoke-Unity 'U08-generate' @('-quit', '-executeMethod', 'Game.Infrastructure.Editor.U08BuildingFoundation.Build')

& "$PSScriptRoot/Verify-U07.ps1" -ProjectPath $ProjectPath -EditorPath $EditorPath -ManagedTestCluster:$ManagedTestCluster
if ($LASTEXITCODE -ne 0) { throw 'U07 regression gate failed.' }

Invoke-Unity 'U08-editmode-tests' @('-runTests', '-testPlatform', 'EditMode', '-testCategory', 'U08',
    '-testResults', ('"' + (Join-Path $logPath 'U08-editmode-tests.xml') + '"'))
Invoke-Unity 'U08-playmode-tests' @('-runTests', '-testPlatform', 'PlayMode', '-testCategory', 'U08',
    '-testResults', ('"' + (Join-Path $logPath 'U08-playmode-tests.xml') + '"'))

[xml]$edit = Get-Content (Join-Path $logPath 'U08-editmode-tests.xml') -Raw
[xml]$play = Get-Content (Join-Path $logPath 'U08-playmode-tests.xml') -Raw
if ($edit.'test-run'.result -ne 'Passed' -or [int]$edit.'test-run'.passed -lt 8 -or
    [int]$edit.'test-run'.failed -ne 0 -or [int]$edit.'test-run'.skipped -ne 0) {
    throw 'U08 focused EditMode suite did not pass every required test.'
}
if ($play.'test-run'.result -ne 'Passed' -or [int]$play.'test-run'.passed -lt 3 -or
    [int]$play.'test-run'.failed -ne 0 -or [int]$play.'test-run'.skipped -ne 0) {
    throw 'U08 focused PlayMode suite did not pass every required test.'
}

Write-Output "U08-editmode-tests : $($edit.'test-run'.passed) passed, $($edit.'test-run'.failed) failed, $($edit.'test-run'.skipped) skipped."
Write-Output "U08-playmode-tests : $($play.'test-run'.passed) passed, $($play.'test-run'.failed) failed, $($play.'test-run'.skipped) skipped."
Write-Output 'U08 validation passed: building placement tests and all inherited U07 gates.'
