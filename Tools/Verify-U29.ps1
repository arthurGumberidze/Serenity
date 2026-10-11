param(
    [string]$ProjectPath = (Split-Path $PSScriptRoot -Parent),
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [int]$StepTimeoutSeconds = 1200,
    [switch]$FullRegression,
    [switch]$PostgresIntegration,
    [switch]$GpuValidation,
    [switch]$BuildPlayer,
    [switch]$RunPerformanceGates,
    [string]$PerformanceOutputDirectory,
    [int]$PerformanceProcessTimeoutSeconds = 900
)

$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$logPath = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Force -Path $logPath | Out-Null

function Assert-CleanLog([string]$Name, [string]$Path) {
    $patterns = @(
        'error CS\d+', 'Scripts have compiler errors', 'Unhandled Exception', 'NullReferenceException',
        'AssertionException', 'There are 2 audio listeners', 'JobTempAlloc has allocations',
        'Native Collection.*leak', 'device.*lost', 'Player crash',
        'invalid/missing result', 'ThemeStyleSheet.*(error|failed)', 'UI.*resource.*(error|failed)'
    )
    $fatal = Select-String -LiteralPath $Path -Pattern $patterns -ErrorAction SilentlyContinue
    if ($fatal) { throw "$Name log contains a fatal compiler/runtime diagnostic: $($fatal[0].Line)" }
}

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
    Assert-CleanLog $Name $stepLog
}

function Assert-TestResult([string]$Name, [int]$Minimum, [bool]$AllowSkipped = $false) {
    $resultPath = Join-Path $logPath "$Name.xml"
    if (-not (Test-Path -LiteralPath $resultPath)) { throw "$Name did not produce NUnit XML." }
    [xml]$result = Get-Content -LiteralPath $resultPath -Raw
    if ($result.'test-run'.result -ne 'Passed' -or [int]$result.'test-run'.passed -lt $Minimum -or
        [int]$result.'test-run'.failed -ne 0 -or (-not $AllowSkipped -and [int]$result.'test-run'.skipped -ne 0)) {
        throw "$Name did not pass every required test."
    }
    Write-Output "$Name : $($result.'test-run'.passed) passed, $($result.'test-run'.failed) failed, $($result.'test-run'.skipped) skipped."
}

function Assert-PostgresDiscovery([string]$Name) {
    $resultPath = Join-Path $logPath "$Name.xml"
    if (-not (Test-Path -LiteralPath $resultPath)) { throw "$Name did not produce NUnit XML." }
    [xml]$result = Get-Content -LiteralPath $resultPath -Raw
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

Invoke-Unity 'U29-compile' @('-quit')
Invoke-Unity 'U29-editmode' @('-runTests','-testPlatform','EditMode','-testCategory','U29',
    '-testResults',(Join-Path $logPath 'U29-editmode.xml'))
Assert-TestResult 'U29-editmode' 28
Invoke-Unity 'U29-playmode-headless' @('-runTests','-testPlatform','PlayMode','-testCategory','U29',
    '-testResults',(Join-Path $logPath 'U29-playmode-headless.xml'))
Assert-TestResult 'U29-playmode-headless' 16
Invoke-Unity 'U29-required-edit-regressions' @('-runTests','-testPlatform','EditMode',
    '-testCategory','U10;U12;U13;U14;U15;U16;U18;U19;U25;U26;U27;U28',
    '-testResults',(Join-Path $logPath 'U29-required-edit-regressions.xml'))
Assert-TestResult 'U29-required-edit-regressions' 158
Invoke-Unity 'U29-required-play-regressions' @('-runTests','-testPlatform','PlayMode',
    '-testCategory','U10;U12;U13;U14;U15;U16;U18;U19;U25;U26;U27;U28',
    '-testResults',(Join-Path $logPath 'U29-required-play-regressions.xml'))
Assert-TestResult 'U29-required-play-regressions' 38
Invoke-Unity 'U29-persistence' @('-runTests','-testPlatform','EditMode',
    '-testFilter','Game.Tests.Editor.PersistenceTests',
    '-testResults',(Join-Path $logPath 'U29-persistence.xml'))
Assert-TestResult 'U29-persistence' 35
Invoke-Unity 'U29-postgres' @('-runTests','-testPlatform','EditMode','-testCategory','PostgresIntegration',
    '-testResults',(Join-Path $logPath 'U29-postgres.xml'))
Assert-PostgresDiscovery 'U29-postgres'

if ($FullRegression) {
    Invoke-Unity 'U29-full-editmode' @('-runTests','-testPlatform','EditMode','-testCategory','!PostgresIntegration',
        '-testResults',(Join-Path $logPath 'U29-full-editmode.xml'))
    Assert-TestResult 'U29-full-editmode' 419
    Invoke-Unity 'U29-full-playmode' @('-runTests','-testPlatform','PlayMode',
        '-testResults',(Join-Path $logPath 'U29-full-playmode.xml'))
    Assert-TestResult 'U29-full-playmode' 102
}

if ($GpuValidation) {
    Invoke-Unity 'U29-playmode-gpu' @('-runTests','-testPlatform','PlayMode','-testCategory','U29',
        '-testResults',(Join-Path $logPath 'U29-playmode-gpu.xml')) $false
    Assert-TestResult 'U29-playmode-gpu' 16
}

$playerPath = Join-Path $ProjectPath 'Builds/Windows/Serenity.exe'
if ($BuildPlayer) {
    Invoke-Unity 'U29-build' @('-quit','-buildTarget','Win64','-executeMethod','U00Build.WindowsDevelopment')
    if (-not (Test-Path -LiteralPath $playerPath)) { throw 'Windows player missing.' }
    $buildResult = Select-String -LiteralPath (Join-Path $logPath 'U29-build.log') `
        -Pattern 'U00_BUILD_RESULT: Succeeded; errors=0' | Select-Object -Last 1
    if (-not $buildResult) { throw 'Windows Development build did not report success.' }
}

if ($RunPerformanceGates) {
    if (-not (Test-Path -LiteralPath $playerPath)) { throw 'RunPerformanceGates requires a built Windows player.' }
    if ([string]::IsNullOrWhiteSpace($PerformanceOutputDirectory)) {
        $stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
        $PerformanceOutputDirectory = Join-Path $ProjectPath "Logs/U29/final-$stamp"
    }
    $runner = Join-Path $PSScriptRoot 'Run-U29-Performance.ps1'
    $performanceLog = Join-Path $logPath 'U29-performance-runner.log'
    $performanceFailure = $null
    try {
        & $runner -PlayerPath $playerPath -OutputDirectory $PerformanceOutputDirectory `
            -ProcessTimeoutSeconds $PerformanceProcessTimeoutSeconds -ContinueOnFailure |
            Tee-Object -FilePath $performanceLog
    } catch {
        $performanceFailure = $_
    }
    $reporter = Join-Path $PSScriptRoot 'Write-U29-PerformanceReport.ps1'
    try {
        & $reporter -ResultsDirectory $PerformanceOutputDirectory -VerificationLogDirectory $logPath
    } catch {
        if ($null -eq $performanceFailure) { $performanceFailure = $_ }
    }
    if ($null -ne $performanceFailure) { throw $performanceFailure }
    $finalSummary = Join-Path $PerformanceOutputDirectory 'U29-final-summary.json'
    if (-not (Test-Path -LiteralPath $finalSummary)) { throw 'U29 performance matrix did not produce its final summary.' }
    $summary = Get-Content -LiteralPath $finalSummary -Raw | ConvertFrom-Json
    if (-not $summary.passed -or $summary.populations.Count -ne 4) { throw 'U29 final performance summary failed validation.' }
    Get-ChildItem -LiteralPath $PerformanceOutputDirectory -Recurse -Filter 'player.log' | ForEach-Object {
        Assert-CleanLog $_.FullName $_.FullName
    }
    Write-Output "U29 performance matrix passed: $finalSummary"
}

Write-Output 'U29 validation passed.'
