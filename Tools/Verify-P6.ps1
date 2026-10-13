[CmdletBinding()]
param(
    [string]$ProjectPath,
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [int]$StepTimeoutSeconds = 1200,
    [int]$PlayerSmokeTimeoutSeconds = 180,
    [string]$PlayerSmokeOutput
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path -Path $PSScriptRoot -Parent }
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$logPath = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Force -Path $logPath | Out-Null
if ([string]::IsNullOrWhiteSpace($PlayerSmokeOutput)) {
    $PlayerSmokeOutput = Join-Path $logPath ('P6-player-smoke-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
}
$PlayerSmokeOutput = [IO.Path]::GetFullPath($PlayerSmokeOutput)

function Wait-P6Process([Diagnostics.Process]$Process, [int]$TimeoutSeconds, [string]$Name, [string]$StepLog) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $successfulBuildObservedAt = $null
    while (-not $Process.HasExited) {
        if ($Name -eq 'P6-build' -and (Test-Path -LiteralPath $StepLog) -and
            (Select-String -LiteralPath $StepLog -Pattern 'U00_BUILD_RESULT: Succeeded; errors=0' -Quiet)) {
            if ($null -eq $successfulBuildObservedAt) { $successfulBuildObservedAt = [DateTime]::UtcNow }
            elseif ([DateTime]::UtcNow -ge $successfulBuildObservedAt.AddSeconds(30)) {
                Stop-Process -Id $Process.Id
                $Process.WaitForExit(5000) | Out-Null
                Write-Output "$Name : terminated lingering Editor after confirmed successful build."
                return 0
            }
        }
        if ([DateTime]::UtcNow -ge $deadline) {
            Stop-Process -Id $Process.Id -Force
            throw "$Name timed out; only PID $($Process.Id) was terminated."
        }
        Start-Sleep -Milliseconds 500
        $Process.Refresh()
    }
    return $Process.ExitCode
}

function Assert-P6Log([string]$Name, [string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "$Name log is missing: $Path" }
    $fatal = Select-String -LiteralPath $Path -Pattern @('error CS\d+', 'Scripts have compiler errors',
        'Unhandled Exception', 'executeMethod method .* threw exception', 'NullReferenceException',
        'MissingReferenceException', 'AssertionException', 'invariant failure',
        'Native Collection.*leak', 'Player crash') -ErrorAction SilentlyContinue
    if ($fatal) { throw "$Name log contains a fatal diagnostic: $($fatal[0].Line)" }
}

function Invoke-P6Unity([string]$Name, [string[]]$Arguments, [bool]$NoGraphics = $true) {
    $stepLog = Join-Path $logPath "$Name.log"
    Set-Content -LiteralPath $stepLog -Value ''
    $base = @('-batchmode')
    if ($NoGraphics) { $base += '-nographics' }
    $base += @('-projectPath', $ProjectPath, '-logFile', $stepLog)
    $process = Start-Process -FilePath $EditorPath -ArgumentList ($base + $Arguments) -WindowStyle Hidden -PassThru
    Write-Output "$Name : PID $($process.Id)"
    $exitCode = Wait-P6Process $process $StepTimeoutSeconds $Name $stepLog
    if ($null -ne $exitCode -and $exitCode -ne 0) {
        Get-Content -LiteralPath $stepLog -Tail 180
        throw "$Name failed with exit code $exitCode."
    }
    Assert-P6Log $Name $stepLog
}

function Assert-P6Tests([string]$Name, [int]$Minimum) {
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
if ($parseErrors.Count -ne 0) { throw "Verify-P6.ps1 parser errors: $($parseErrors[0].Message)" }

Invoke-P6Unity 'P6-focused-editmode' @('-runTests', '-testPlatform', 'EditMode', '-testCategory', 'P6',
    '-testResults', (Join-Path $logPath 'P6-focused-editmode.xml'))
Assert-P6Tests 'P6-focused-editmode' 3
Invoke-P6Unity 'P6-focused-playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testCategory', 'P6',
    '-testResults', (Join-Path $logPath 'P6-focused-playmode.xml'))
Assert-P6Tests 'P6-focused-playmode' 2
Invoke-P6Unity 'P6-gpu-playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testCategory', 'P6',
    '-force-d3d11', '-testResults', (Join-Path $logPath 'P6-gpu-playmode.xml')) $false
Assert-P6Tests 'P6-gpu-playmode' 2

$editFilter = 'Game.Tests.P1RussianLocalizationTests;Game.Tests.P2StartupTests;' +
    'Game.Tests.P4AppearanceTests;Game.Tests.P5InhabitableRegionTests;Game.Tests.PersistenceTests;' +
    'Game.Tests.ProductionAndFarmingTests;Game.Tests.ResearchFrameworkTests;Game.Tests.EventSystemTests;' +
    'Game.Tests.GlobalMapTests;Game.Tests.WorkGroupAndJobTests'
Invoke-P6Unity 'P6-affected-editmode' @('-runTests', '-testPlatform', 'EditMode', '-testFilter', $editFilter,
    '-testResults', (Join-Path $logPath 'P6-affected-editmode.xml'))
Assert-P6Tests 'P6-affected-editmode' 1

$playFilter = 'Game.Tests.PlayMode.GameplayUiPlayModeTests;Game.Tests.PlayMode.P1RussianUiPlayModeTests;' +
    'Game.Tests.PlayMode.P2StartupPlayModeTests;Game.Tests.PlayMode.P4RulerCreationPlayModeTests;' +
    'Game.Tests.PlayMode.P5InhabitableRegionPlayModeTests;Game.Tests.PlayMode.ProductionPlayModeTests;' +
    'Game.Tests.PlayMode.ResearchPlayModeTests;Game.Tests.PlayMode.EventSystemPlayModeTests;' +
    'Game.Tests.PlayMode.GlobalMapPlayModeTests;Game.Tests.PlayMode.WorkGroupPlayModeTests'
Invoke-P6Unity 'P6-affected-playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testFilter', $playFilter,
    '-testResults', (Join-Path $logPath 'P6-affected-playmode.xml'))
Assert-P6Tests 'P6-affected-playmode' 1

Invoke-P6Unity 'P6-build' @('-quit', '-buildTarget', 'Win64', '-executeMethod',
    'U00Build.WindowsDevelopment', '-serenityBuildPath', 'Builds/P6/Serenity.exe')
$buildLog = Join-Path $logPath 'P6-build.log'
if (-not (Select-String -LiteralPath $buildLog -Pattern 'U00_BUILD_RESULT: Succeeded; errors=0' -Quiet)) {
    throw 'P6 build did not report success.'
}

$player = Join-Path $ProjectPath 'Builds\P6\Serenity.exe'
if (-not (Test-Path -LiteralPath $player)) { throw 'P6 Windows Player is missing.' }
New-Item -ItemType Directory -Force -Path $PlayerSmokeOutput | Out-Null
$oldSave = $env:SERENITY_SAVE_DIRECTORY
$oldSettings = $env:SERENITY_SETTINGS_PATH
$env:SERENITY_SAVE_DIRECTORY = Join-Path $PlayerSmokeOutput 'saves'
$env:SERENITY_SETTINGS_PATH = Join-Path $PlayerSmokeOutput 'settings\user-settings.json'
try {
    $playerLog = Join-Path $PlayerSmokeOutput 'Player.log'
    $process = Start-Process -FilePath $player -ArgumentList @('-logFile', $playerLog,
        '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720', '-force-d3d11',
        '-p6Smoke', '-p6SmokeSeed', '61006', '-p6SmokeOutput', $PlayerSmokeOutput) -WindowStyle Normal -PassThru
    $exitCode = Wait-P6Process $process $PlayerSmokeTimeoutSeconds 'P6 Player smoke' $playerLog
    if ($null -ne $exitCode -and $exitCode -ne 0) { throw "P6 Player smoke exited $exitCode." }
    Assert-P6Log 'P6 Player smoke' $playerLog
} finally {
    $env:SERENITY_SAVE_DIRECTORY = $oldSave
    $env:SERENITY_SETTINGS_PATH = $oldSettings
}
$resultPath = Join-Path $PlayerSmokeOutput 'p6-player-smoke.json'
if (-not (Test-Path -LiteralPath $resultPath)) { throw 'P6 Player smoke result is missing.' }
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if (-not $result.passed) { throw "P6 Player smoke failed: $($result.error)" }
foreach ($name in @('p6-onboarding-start.png', 'p6-settlement-food.png', 'p6-event-result.png',
    'p6-scouting-map.png', 'p6-after-load.png')) {
    $path = Join-Path $PlayerSmokeOutput $name
    if (-not (Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -le 0) {
        throw "P6 Player smoke screenshot is missing or empty: $path"
    }
}
Write-Output "P6 verification passed. Evidence: $PlayerSmokeOutput"
