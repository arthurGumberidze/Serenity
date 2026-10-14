[CmdletBinding()]
param(
    [string]$ProjectPath,
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [int]$StepTimeoutSeconds = 1800,
    [int]$PlayerSmokeTimeoutSeconds = 300,
    [string]$PlayerSmokeOutput
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path -Path $PSScriptRoot -Parent }
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$logPath = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Force -Path $logPath | Out-Null
if ([string]::IsNullOrWhiteSpace($PlayerSmokeOutput)) {
    $PlayerSmokeOutput = Join-Path $logPath ('P82-player-smoke-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
}
$PlayerSmokeOutput = [IO.Path]::GetFullPath($PlayerSmokeOutput)

function Wait-P82Process([Diagnostics.Process]$Process, [int]$TimeoutSeconds, [string]$Name) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while (Get-Process -Id $Process.Id -ErrorAction SilentlyContinue) {
        if ([DateTime]::UtcNow -ge $deadline) {
            Stop-Process -Id $Process.Id -Force
            throw "$Name timed out; only PID $($Process.Id) was terminated."
        }
        Start-Sleep -Milliseconds 500
    }
    try { $Process.Refresh(); return $Process.ExitCode } catch { return $null }
}

function Assert-P82Log([string]$Name, [string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "$Name log is missing: $Path" }
    $fatal = Select-String -LiteralPath $Path -Pattern @('error CS\d+', 'Scripts have compiler errors',
        'Unhandled Exception', 'executeMethod method .* threw exception', 'NullReferenceException',
        'MissingReferenceException', 'AssertionException', 'invariant failure',
        'Native Collection.*leak', 'Player crash') -ErrorAction SilentlyContinue
    if ($fatal) { throw "$Name log contains a fatal diagnostic: $($fatal[0].Line)" }
}

function Invoke-P82Unity([string]$Name, [string[]]$Arguments, [bool]$NoGraphics = $true) {
    $stepLog = Join-Path $logPath "$Name.log"
    Set-Content -LiteralPath $stepLog -Value ''
    $base = @('-batchmode')
    if ($NoGraphics) { $base += '-nographics' }
    $base += @('-projectPath', $ProjectPath, '-logFile', $stepLog)
    $process = Start-Process -FilePath $EditorPath -ArgumentList ($base + $Arguments) -WindowStyle Hidden -PassThru
    Write-Output "$Name : PID $($process.Id)"
    $exitCode = Wait-P82Process $process $StepTimeoutSeconds $Name
    if ($null -ne $exitCode -and $exitCode -ne 0) {
        Get-Content -LiteralPath $stepLog -Tail 180
        throw "$Name failed with exit code $exitCode."
    }
    Assert-P82Log $Name $stepLog
}

function Assert-P82Tests([string]$Name, [int]$Minimum) {
    $path = Join-Path $logPath "$Name.xml"
    if (-not (Test-Path -LiteralPath $path)) { throw "$Name did not produce NUnit XML." }
    [xml]$result = Get-Content -LiteralPath $path -Raw
    $run = $result.'test-run'
    if ($run.result -ne 'Passed' -or [int]$run.passed -lt $Minimum -or
        [int]$run.failed -ne 0 -or [int]$run.skipped -ne 0) {
        throw "$Name failed (passed=$($run.passed) failed=$($run.failed) skipped=$($run.skipped))."
    }
    Write-Output "$Name : $($run.passed) passed."
}

$parseErrors = $null
[Management.Automation.Language.Parser]::ParseFile($PSCommandPath, [ref]$null, [ref]$parseErrors) | Out-Null
if ($parseErrors.Count -ne 0) { throw "Verify-P82.ps1 parser errors: $($parseErrors[0].Message)" }

Invoke-P82Unity 'P82-generate' @('-quit', '-executeMethod', 'Game.Infrastructure.Editor.P82NaturalRegionBuilder.Build')
Invoke-P82Unity 'P82-focused-editmode' @('-runTests', '-testPlatform', 'EditMode', '-testCategory', 'P82',
    '-testResults', (Join-Path $logPath 'P82-focused-editmode.xml'))
Assert-P82Tests 'P82-focused-editmode' 3
Invoke-P82Unity 'P82-focused-playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testCategory', 'P82',
    '-testResults', (Join-Path $logPath 'P82-focused-playmode.xml'))
Assert-P82Tests 'P82-focused-playmode' 3
Invoke-P82Unity 'P82-gpu-playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testCategory', 'P82',
    '-force-d3d11', '-testResults', (Join-Path $logPath 'P82-gpu-playmode.xml')) $false
Assert-P82Tests 'P82-gpu-playmode' 3

$editFilter = 'Game.Tests.Editor.P3ReferenceCampTests;Game.Tests.Editor.P5InhabitableRegionTests;' +
    'Game.Tests.Editor.ProductionAndFarmingTests'
Invoke-P82Unity 'P82-affected-editmode' @('-runTests', '-testPlatform', 'EditMode', '-testFilter', $editFilter,
    '-testResults', (Join-Path $logPath 'P82-affected-editmode.xml'))
Assert-P82Tests 'P82-affected-editmode' 20
$playFilter = 'Game.Tests.PlayMode.P3ReferenceCampPlayModeTests;' +
    'Game.Tests.PlayMode.P5InhabitableRegionPlayModeTests;Game.Tests.PlayMode.P7StoneAgePlayModeTests;' +
    'Game.Tests.PlayMode.ProductionPlayModeTests'
Invoke-P82Unity 'P82-affected-playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testFilter', $playFilter,
    '-testResults', (Join-Path $logPath 'P82-affected-playmode.xml'))
Assert-P82Tests 'P82-affected-playmode' 11

Invoke-P82Unity 'P82-build' @('-quit', '-executeMethod',
    'U00Build.WindowsDevelopment', '-serenityBuildPath', 'Builds/P82/Serenity.exe')
$buildLog = Join-Path $logPath 'P82-build.log'
if (-not (Select-String -LiteralPath $buildLog -Pattern 'U00_BUILD_RESULT: Succeeded; errors=0' -Quiet)) {
    throw 'P82 build did not report success.'
}

$player = Join-Path $ProjectPath 'Builds\P82\Serenity.exe'
if (-not (Test-Path -LiteralPath $player)) { throw 'P82 Windows Player is missing.' }
New-Item -ItemType Directory -Force -Path $PlayerSmokeOutput | Out-Null
$oldSave = $env:SERENITY_SAVE_DIRECTORY
$oldSettings = $env:SERENITY_SETTINGS_PATH
$env:SERENITY_SAVE_DIRECTORY = Join-Path $PlayerSmokeOutput 'saves'
$env:SERENITY_SETTINGS_PATH = Join-Path $PlayerSmokeOutput 'settings\user-settings.json'
try {
    $playerLog = Join-Path $PlayerSmokeOutput 'Player.log'
    $process = Start-Process -FilePath $player -ArgumentList @('-logFile', $playerLog,
        '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720', '-force-d3d11',
        '-p82Smoke', '-p82SmokeOutput', $PlayerSmokeOutput) -WindowStyle Normal -PassThru
    $exitCode = Wait-P82Process $process $PlayerSmokeTimeoutSeconds 'P82 Player smoke'
    if ($null -ne $exitCode -and $exitCode -ne 0) { throw "P82 Player smoke exited $exitCode." }
    Assert-P82Log 'P82 Player smoke' $playerLog
} finally {
    $env:SERENITY_SAVE_DIRECTORY = $oldSave
    $env:SERENITY_SETTINGS_PATH = $oldSettings
}
$resultPath = Join-Path $PlayerSmokeOutput 'p82-player-smoke.json'
if (-not (Test-Path -LiteralPath $resultPath)) { throw 'P82 Player smoke result is missing.' }
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if (-not $result.passed) { throw "P82 Player smoke failed: $($result.error)" }
foreach ($name in @('p82-local-map.png', 'p82-selected-resource.png', 'p82-global-map.png')) {
    $screenshot = Join-Path $PlayerSmokeOutput $name
    if (-not (Test-Path -LiteralPath $screenshot) -or (Get-Item -LiteralPath $screenshot).Length -le 0) {
        throw "P82 Player smoke screenshot is missing or empty: $name"
    }
}
Write-Output "P82 verification passed. Evidence: $PlayerSmokeOutput"
