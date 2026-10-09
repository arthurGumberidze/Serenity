param(
    [string]$ProjectPath = (Split-Path $PSScriptRoot -Parent),
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [int]$StepTimeoutSeconds = 900,
    [switch]$FullRegression,
    [switch]$PostgresIntegration,
    [switch]$GpuValidation,
    [switch]$BuildPlayer
)

$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$logPath = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Force -Path $logPath | Out-Null

function Invoke-Unity([string]$Name, [string[]]$Arguments, [bool]$NoGraphics = $true) {
    $stepLog = Join-Path $logPath "$Name.log"
    $base = @('-batchmode')
    if ($NoGraphics) { $base += '-nographics' }
    $base += @('-projectPath', $ProjectPath, '-logFile', $stepLog)
    $process = Start-Process -FilePath $EditorPath -ArgumentList ($base + $Arguments) -WindowStyle Hidden -PassThru
    Write-Output "$Name : PID $($process.Id)"
    if (-not $process.WaitForExit($StepTimeoutSeconds * 1000)) {
        $process.Refresh()
        if (Test-Path -LiteralPath $stepLog) { Get-Content -LiteralPath $stepLog -Tail 120 }
        Stop-Process -Id $process.Id -Force
        throw "$Name timed out; only its launched Unity PID was terminated."
    }
    if ($process.ExitCode -ne 0) {
        if (Test-Path -LiteralPath $stepLog) { Get-Content -LiteralPath $stepLog -Tail 180 }
        throw "$Name failed (exit $($process.ExitCode)); see Logs/$Name.log."
    }
    $fatal = Select-String -LiteralPath $stepLog -Pattern 'error CS\d+|Scripts have compiler errors|NullReferenceException|Unhandled Exception|AssertionException|There are 2 audio listeners' -SimpleMatch:$false
    if ($fatal) { throw "$Name log contains a fatal compiler/runtime diagnostic." }
}

function Assert-TestResult([string]$Name, [int]$Minimum) {
    $resultPath = Join-Path $logPath "$Name.xml"
    if (-not (Test-Path -LiteralPath $resultPath)) { throw "$Name did not produce NUnit XML." }
    [xml]$result = Get-Content -LiteralPath $resultPath -Raw
    if ($result.'test-run'.result -ne 'Passed' -or [int]$result.'test-run'.passed -lt $Minimum -or
        [int]$result.'test-run'.failed -ne 0 -or [int]$result.'test-run'.skipped -ne 0) {
        throw "$Name did not pass every required test."
    }
    Write-Output "$Name : $($result.'test-run'.passed) passed, $($result.'test-run'.failed) failed, $($result.'test-run'.skipped) skipped."
}

function Assert-PostgresDiscovery([string]$Name) {
    [xml]$result = Get-Content -LiteralPath (Join-Path $logPath "$Name.xml") -Raw
    if ([int]$result.'test-run'.total -lt 25 -or [int]$result.'test-run'.failed -ne 0) {
        throw "$Name did not discover the PostgreSQL integration suite cleanly."
    }
    if ($PostgresIntegration -and [string]::IsNullOrWhiteSpace($env:DYNASTYGAME_PG_TEST_DATABASE)) {
        throw '-PostgresIntegration requires DYNASTYGAME_PG_TEST_DATABASE.'
    }
    if ($PostgresIntegration -and [int]$result.'test-run'.skipped -ne 0) {
        throw "$Name was requested but contained skipped tests."
    }
    Write-Output "$Name : $($result.'test-run'.passed) passed, $($result.'test-run'.failed) failed, $($result.'test-run'.skipped) skipped."
}

Invoke-Unity 'U28-compile' @('-quit')
Invoke-Unity 'U28-editmode' @('-runTests','-testPlatform','EditMode','-testCategory','U28',
    '-testResults',(Join-Path $logPath 'U28-editmode.xml'))
Assert-TestResult 'U28-editmode' 20
Invoke-Unity 'U28-playmode-headless' @('-runTests','-testPlatform','PlayMode','-testCategory','U28',
    '-testResults',(Join-Path $logPath 'U28-playmode-headless.xml'))
Assert-TestResult 'U28-playmode-headless' 4
Invoke-Unity 'U28-required-edit-regressions' @('-runTests','-testPlatform','EditMode',
    '-testCategory','U03;U06;U14;U17;U18;U27',
    '-testResults',(Join-Path $logPath 'U28-required-edit-regressions.xml'))
Assert-TestResult 'U28-required-edit-regressions' 1
Invoke-Unity 'U28-required-play-regressions' @('-runTests','-testPlatform','PlayMode',
    '-testCategory','U14;U17;U18;U27',
    '-testResults',(Join-Path $logPath 'U28-required-play-regressions.xml'))
Assert-TestResult 'U28-required-play-regressions' 1
Invoke-Unity 'U28-persistence' @('-runTests','-testPlatform','EditMode',
    '-testFilter','Game.Tests.Editor.PersistenceTests',
    '-testResults',(Join-Path $logPath 'U28-persistence.xml'))
Assert-TestResult 'U28-persistence' 35
Invoke-Unity 'U28-postgres' @('-runTests','-testPlatform','EditMode','-testCategory','PostgresIntegration',
    '-testResults',(Join-Path $logPath 'U28-postgres.xml'))
Assert-PostgresDiscovery 'U28-postgres'

if ($FullRegression) {
    Invoke-Unity 'U28-full-editmode' @('-runTests','-testPlatform','EditMode','-testCategory','!PostgresIntegration',
        '-testResults',(Join-Path $logPath 'U28-full-editmode.xml'))
    Assert-TestResult 'U28-full-editmode' 391
    Invoke-Unity 'U28-full-playmode' @('-runTests','-testPlatform','PlayMode',
        '-testResults',(Join-Path $logPath 'U28-full-playmode.xml'))
    Assert-TestResult 'U28-full-playmode' 86
}

if ($GpuValidation) {
    Invoke-Unity 'U28-playmode-gpu' @('-runTests','-testPlatform','PlayMode','-testCategory','U28',
        '-testResults',(Join-Path $logPath 'U28-playmode-gpu.xml')) $false
    Assert-TestResult 'U28-playmode-gpu' 4
}

if ($BuildPlayer) {
    Invoke-Unity 'U28-build' @('-quit','-buildTarget','Win64','-executeMethod','U00Build.WindowsDevelopment')
    if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath 'Builds/Windows/Serenity.exe'))) {
        throw 'Windows player missing.'
    }
}

Write-Output 'U28 validation passed.'
