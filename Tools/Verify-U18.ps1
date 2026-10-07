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
}

function Assert-TestResult([string]$Name, [int]$Minimum) {
    [xml]$result = Get-Content -LiteralPath (Join-Path $logPath "$Name.xml") -Raw
    if ($result.'test-run'.result -ne 'Passed' -or [int]$result.'test-run'.passed -lt $Minimum -or
        [int]$result.'test-run'.failed -ne 0 -or [int]$result.'test-run'.skipped -ne 0) {
        throw "$Name did not pass every required test."
    }
    Write-Output "$Name : $($result.'test-run'.passed) passed, $($result.'test-run'.failed) failed, $($result.'test-run'.skipped) skipped."
}

Invoke-Unity 'U18-compile' @('-quit')
Invoke-Unity 'U18-editmode' @('-runTests','-testPlatform','EditMode','-testCategory','U18',
    '-testResults',(Join-Path $logPath 'U18-editmode.xml'))
Assert-TestResult 'U18-editmode' 13
Invoke-Unity 'U18-playmode' @('-runTests','-testPlatform','PlayMode','-testCategory','U18',
    '-testResults',(Join-Path $logPath 'U18-playmode.xml'))
Assert-TestResult 'U18-playmode' 2
Invoke-Unity 'U18-required-edit-regressions' @('-runTests','-testPlatform','EditMode',
    '-testCategory','U06;U10;U14;U15;U16;U16A;U17',
    '-testResults',(Join-Path $logPath 'U18-required-edit-regressions.xml'))
Assert-TestResult 'U18-required-edit-regressions' 99
Invoke-Unity 'U18-persistence' @('-runTests','-testPlatform','EditMode',
    '-testFilter','Game.Tests.Editor.PersistenceTests','-testResults',(Join-Path $logPath 'U18-persistence.xml'))
Assert-TestResult 'U18-persistence' 35
Invoke-Unity 'U18-required-play-regressions' @('-runTests','-testPlatform','PlayMode',
    '-testCategory','U10;U14;U15;U16;U16A;U17',
    '-testResults',(Join-Path $logPath 'U18-required-play-regressions.xml'))
Assert-TestResult 'U18-required-play-regressions' 17

if ($FullRegression) {
    Invoke-Unity 'U18-full-edit' @('-runTests','-testPlatform','EditMode','-testCategory','!PostgresIntegration',
        '-testResults',(Join-Path $logPath 'U18-full-edit.xml'))
    Assert-TestResult 'U18-full-edit' 263
    Invoke-Unity 'U18-full-play' @('-runTests','-testPlatform','PlayMode',
        '-testResults',(Join-Path $logPath 'U18-full-play.xml'))
    Assert-TestResult 'U18-full-play' 46
}

if ($PostgresIntegration) {
    Invoke-Unity 'U18-postgres' @('-runTests','-testPlatform','EditMode','-testCategory','PostgresIntegration',
        '-testResults',(Join-Path $logPath 'U18-postgres.xml'))
    Assert-TestResult 'U18-postgres' 25
}

if ($GpuValidation) {
    Invoke-Unity 'U18-playmode-gpu' @('-runTests','-testPlatform','PlayMode','-testCategory','U18',
        '-testResults',(Join-Path $logPath 'U18-playmode-gpu.xml')) $false
    Assert-TestResult 'U18-playmode-gpu' 2
}

if ($BuildPlayer) {
    Invoke-Unity 'U18-build' @('-quit','-buildTarget','Win64','-executeMethod','U00Build.WindowsDevelopment')
    if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath 'Builds/Windows/Serenity.exe'))) {
        throw 'Windows player missing.'
    }
}

Write-Output 'U18 validation passed.'
