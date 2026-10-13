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
    $PlayerSmokeOutput = Join-Path $logPath ('P4-player-smoke-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
}
$PlayerSmokeOutput = [IO.Path]::GetFullPath($PlayerSmokeOutput)

function Wait-P4Process([Diagnostics.Process]$Process, [int]$TimeoutSeconds, [string]$Name) {
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

function Assert-P4Log([string]$Name, [string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "$Name log is missing: $Path" }
    $fatal = Select-String -LiteralPath $Path -Pattern @('error CS\d+', 'Scripts have compiler errors',
        'Unhandled Exception', 'executeMethod method .* threw exception', 'NullReferenceException',
        'MissingReferenceException', 'AssertionException', 'Player crash') -ErrorAction SilentlyContinue
    if ($fatal) { throw "$Name log contains a fatal diagnostic: $($fatal[0].Line)" }
}

function Invoke-P4Unity([string]$Name, [string[]]$Arguments, [bool]$NoGraphics = $true) {
    $stepLog = Join-Path $logPath "$Name.log"
    Set-Content -LiteralPath $stepLog -Value ''
    $base = @('-batchmode')
    if ($NoGraphics) { $base += '-nographics' }
    $base += @('-projectPath', $ProjectPath, '-logFile', $stepLog)
    $process = Start-Process -FilePath $EditorPath -ArgumentList ($base + $Arguments) -WindowStyle Hidden -PassThru
    Write-Output "$Name : PID $($process.Id)"
    $exitCode = Wait-P4Process $process $StepTimeoutSeconds $Name
    if ($null -ne $exitCode -and $exitCode -ne 0) {
        Get-Content -LiteralPath $stepLog -Tail 160
        throw "$Name failed with exit code $exitCode."
    }
    Assert-P4Log $Name $stepLog
}

function Assert-P4Tests([string]$Name, [int]$Minimum) {
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

Invoke-P4Unity 'P4-generate' @('-quit', '-executeMethod', 'Game.Infrastructure.Editor.P4AppearanceFoundation.Build')
Invoke-P4Unity 'P4-focused-editmode' @('-runTests', '-testPlatform', 'EditMode', '-testCategory', 'P4',
    '-testResults', (Join-Path $logPath 'P4-focused-editmode.xml'))
Assert-P4Tests 'P4-focused-editmode' 4
Invoke-P4Unity 'P4-focused-playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testCategory', 'P4',
    '-testResults', (Join-Path $logPath 'P4-focused-playmode.xml'))
Assert-P4Tests 'P4-focused-playmode' 2
Invoke-P4Unity 'P4-gpu-playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testCategory', 'P4',
    '-force-d3d11', '-testResults', (Join-Path $logPath 'P4-gpu-playmode.xml')) $false
Assert-P4Tests 'P4-gpu-playmode' 2

$editFilter = 'Game.Tests.Editor.PersistenceTests;Game.Tests.Editor.DynastyAndSuccessionTests;' +
    'Game.Tests.Editor.PlayerControlStateTests;Game.Tests.Editor.P4AppearanceTests'
Invoke-P4Unity 'P4-affected-editmode' @('-runTests', '-testPlatform', 'EditMode', '-testFilter', $editFilter,
    '-testResults', (Join-Path $logPath 'P4-affected-editmode.xml'))
Assert-P4Tests 'P4-affected-editmode' 59

$playFilter = 'Game.Tests.PlayMode.P2MainMenuPlayModeTests;Game.Tests.PlayMode.P3ReferenceCampPlayModeTests;' +
    'Game.Tests.PlayMode.CharacterPresentationPlayModeTests;Game.Tests.PlayMode.TierManagerPlayModeTests;' +
    'Game.Tests.PlayMode.GameplayUiPlayModeTests;Game.Tests.PlayMode.P4RulerCreationPlayModeTests'
Invoke-P4Unity 'P4-affected-playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testFilter', $playFilter,
    '-testResults', (Join-Path $logPath 'P4-affected-playmode.xml'))
Assert-P4Tests 'P4-affected-playmode' 19

Invoke-P4Unity 'P4-build' @('-quit', '-buildTarget', 'Win64', '-executeMethod',
    'U00Build.WindowsDevelopment', '-serenityBuildPath', 'Builds/P4/Serenity.exe')
$buildLog = Join-Path $logPath 'P4-build.log'
if (-not (Select-String -LiteralPath $buildLog -Pattern 'U00_BUILD_RESULT: Succeeded; errors=0' -Quiet)) {
    throw 'P4 build did not report success.'
}

$player = Join-Path $ProjectPath 'Builds\P4\Serenity.exe'
if (-not (Test-Path -LiteralPath $player)) { throw 'P4 Windows Player is missing.' }
New-Item -ItemType Directory -Force -Path $PlayerSmokeOutput | Out-Null
$oldSave = $env:SERENITY_SAVE_DIRECTORY
$oldSettings = $env:SERENITY_SETTINGS_PATH
$env:SERENITY_SAVE_DIRECTORY = Join-Path $PlayerSmokeOutput 'saves'
$env:SERENITY_SETTINGS_PATH = Join-Path $PlayerSmokeOutput 'settings\user-settings.json'
try {
    $playerLog = Join-Path $PlayerSmokeOutput 'Player.log'
    $process = Start-Process -FilePath $player -ArgumentList @('-logFile', $playerLog,
        '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720', '-force-d3d11',
        '-p4Smoke', '-p4SmokeSeed', '44009', '-p4SmokeOutput', $PlayerSmokeOutput) -WindowStyle Normal -PassThru
    $exitCode = Wait-P4Process $process $PlayerSmokeTimeoutSeconds 'P4 Player smoke'
    if ($null -ne $exitCode -and $exitCode -ne 0) { throw "P4 Player smoke exited $exitCode." }
    Assert-P4Log 'P4 Player smoke' $playerLog
} finally {
    $env:SERENITY_SAVE_DIRECTORY = $oldSave
    $env:SERENITY_SETTINGS_PATH = $oldSettings
}
$resultPath = Join-Path $PlayerSmokeOutput 'p4-player-smoke.json'
if (-not (Test-Path -LiteralPath $resultPath)) { throw 'P4 Player smoke result is missing.' }
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if (-not $result.passed) { throw "P4 Player smoke failed: $($result.error)" }
Write-Output "P4 verification passed. Evidence: $PlayerSmokeOutput"
