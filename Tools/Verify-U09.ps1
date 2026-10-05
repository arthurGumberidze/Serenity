param(
    [string]$ProjectPath = (Split-Path $PSScriptRoot -Parent),
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [int]$StepTimeoutSeconds = 900,
    [switch]$FullRegression
)

$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$logPath = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Force -Path $logPath | Out-Null

function Invoke-Unity([string]$Name, [string[]]$Arguments) {
    $stepLog = Join-Path $logPath "$Name.log"
    $base = @('-batchmode', '-nographics', '-projectPath', $ProjectPath, '-logFile', $stepLog)
    $process = Start-Process -FilePath $EditorPath -ArgumentList ($base + $Arguments) -WindowStyle Hidden -PassThru
    Write-Output "$Name : PID $($process.Id)"
    Write-Output "$Name : Unity.exe $(($base + $Arguments) -join ' ')"
    if (-not $process.WaitForExit($StepTimeoutSeconds * 1000)) {
        $process.Refresh()
        $activity = if (Test-Path -LiteralPath $stepLog) { (Get-Item -LiteralPath $stepLog).LastWriteTime } else { 'no log' }
        Write-Warning "$Name timed out; PID=$($process.Id); responding=$($process.Responding); last log activity=$activity"
        if (Test-Path -LiteralPath $stepLog) { Get-Content -LiteralPath $stepLog -Tail 100 }
        Stop-Process -Id $process.Id -Force
        throw "$Name timed out; only its launched Unity PID was terminated."
    }
    if ($process.ExitCode -ne 0) {
        if (Test-Path -LiteralPath $stepLog) { Get-Content -LiteralPath $stepLog -Tail 100 }
        throw "$Name failed (exit $($process.ExitCode)); see Logs/$Name.log."
    }
}

Invoke-Unity 'U09-generate' @('-quit', '-executeMethod', 'Game.Infrastructure.Editor.U09ResourceFoundation.Build')

if ($FullRegression) {
    Invoke-Unity 'U09-core-tests' @('-runTests', '-testPlatform', 'EditMode', '-testCategory', '!Postgres',
        '-testResults', (Join-Path $logPath 'U09-core-tests.xml'))
    Invoke-Unity 'U09-all-playmode-tests' @('-runTests', '-testPlatform', 'PlayMode',
        '-testResults', (Join-Path $logPath 'U09-all-playmode-tests.xml'))
}

Invoke-Unity 'U09-editmode-tests' @('-runTests', '-testPlatform', 'EditMode', '-testCategory', 'U09',
    '-testResults', (Join-Path $logPath 'U09-editmode-tests.xml'))
Invoke-Unity 'U09-playmode-tests' @('-runTests', '-testPlatform', 'PlayMode', '-testCategory', 'U09',
    '-testResults', (Join-Path $logPath 'U09-playmode-tests.xml'))
Invoke-Unity 'U09-build' @('-quit', '-executeMethod', 'U00Build.WindowsDevelopment')

[xml]$edit = Get-Content -LiteralPath (Join-Path $logPath 'U09-editmode-tests.xml') -Raw
[xml]$play = Get-Content -LiteralPath (Join-Path $logPath 'U09-playmode-tests.xml') -Raw
if ($edit.'test-run'.result -ne 'Passed' -or [int]$edit.'test-run'.passed -lt 10 -or
    [int]$edit.'test-run'.failed -ne 0 -or [int]$edit.'test-run'.skipped -ne 0) {
    throw 'U09 focused EditMode suite did not pass every required test.'
}
if ($play.'test-run'.result -ne 'Passed' -or [int]$play.'test-run'.passed -lt 4 -or
    [int]$play.'test-run'.failed -ne 0 -or [int]$play.'test-run'.skipped -ne 0) {
    throw 'U09 focused PlayMode suite did not pass every required test.'
}

Write-Output "U09-editmode-tests : $($edit.'test-run'.passed) passed, $($edit.'test-run'.failed) failed, $($edit.'test-run'.skipped) skipped."
Write-Output "U09-playmode-tests : $($play.'test-run'.passed) passed, $($play.'test-run'.failed) failed, $($play.'test-run'.skipped) skipped."
Write-Output 'U09 focused validation and Windows build passed. PostgreSQL remains an explicit separately managed integration gate.'
