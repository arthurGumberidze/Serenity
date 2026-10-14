[CmdletBinding()]
param(
    [string]$ProjectPath,
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [int]$StepTimeoutSeconds = 2400,
    [int]$PlayerSmokeTimeoutSeconds = 420,
    [string]$PlayerSmokeOutput
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path -Path $PSScriptRoot -Parent }
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$logPath = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Force -Path $logPath | Out-Null
if ([string]::IsNullOrWhiteSpace($PlayerSmokeOutput)) {
    $PlayerSmokeOutput = Join-Path $logPath ('P85-player-smoke-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
}
$PlayerSmokeOutput = [IO.Path]::GetFullPath($PlayerSmokeOutput)

function Wait-P85Process([Diagnostics.Process]$Process, [int]$TimeoutSeconds, [string]$Name) {
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

function Assert-P85Log([string]$Name, [string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "$Name log is missing: $Path" }
    $fatal = Select-String -LiteralPath $Path -Pattern @('error CS\d+', 'Scripts have compiler errors',
        'Unhandled Exception', 'executeMethod method .* threw exception', 'NullReferenceException',
        'MissingReferenceException', 'AssertionException', 'Native Collection.*leak', 'Player crash') `
        -ErrorAction SilentlyContinue
    if ($fatal) { throw "$Name log contains a fatal diagnostic: $($fatal[0].Line)" }
}

function Invoke-P85Unity([string]$Name, [string[]]$Arguments, [bool]$NoGraphics = $true) {
    $stepLog = Join-Path $logPath "$Name.log"
    Set-Content -LiteralPath $stepLog -Value ''
    $base = @('-batchmode')
    if ($NoGraphics) { $base += '-nographics' }
    $base += @('-projectPath', $ProjectPath, '-logFile', $stepLog)
    $process = Start-Process -FilePath $EditorPath -ArgumentList ($base + $Arguments) -WindowStyle Hidden -PassThru
    Write-Output "$Name : PID $($process.Id)"
    $exitCode = Wait-P85Process $process $StepTimeoutSeconds $Name
    if ($null -ne $exitCode -and $exitCode -ne 0) {
        Get-Content -LiteralPath $stepLog -Tail 180
        throw "$Name failed with exit code $exitCode."
    }
    Assert-P85Log $Name $stepLog
}

function Assert-P85Tests([string]$Name, [int]$Minimum) {
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
if ($parseErrors.Count -ne 0) { throw "Verify-P85.ps1 parser errors: $($parseErrors[0].Message)" }

Invoke-P85Unity 'P85-focused-editmode' @('-runTests', '-testPlatform', 'EditMode', '-testCategory', 'P85',
    '-testResults', (Join-Path $logPath 'P85-focused-editmode.xml'))
Assert-P85Tests 'P85-focused-editmode' 3
Invoke-P85Unity 'P85-focused-playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testCategory', 'P85',
    '-testResults', (Join-Path $logPath 'P85-focused-playmode.xml'))
Assert-P85Tests 'P85-focused-playmode' 2
Invoke-P85Unity 'P85-gpu-playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testCategory', 'P85',
    '-force-d3d11', '-testResults', (Join-Path $logPath 'P85-gpu-playmode.xml')) $false
Assert-P85Tests 'P85-gpu-playmode' 2

$editFilter = 'Game.Tests.Editor.GameplayUiTests;Game.Tests.Editor.P1RussianLocalizationTests;' +
    'Game.Tests.Editor.P2StartupTests;Game.Tests.Editor.P85BeautifulUiTests'
Invoke-P85Unity 'P85-affected-editmode' @('-runTests', '-testPlatform', 'EditMode', '-testFilter', $editFilter,
    '-testResults', (Join-Path $logPath 'P85-affected-editmode.xml'))
Assert-P85Tests 'P85-affected-editmode' 4
$playFilter = 'Game.Tests.PlayMode.P1RussianLocalizationPlayModeTests;' +
    'Game.Tests.PlayMode.P2MainMenuPlayModeTests;Game.Tests.PlayMode.GameplayUiPlayModeTests;' +
    'Game.Tests.PlayMode.GlobalMapPlayModeTests;Game.Tests.PlayMode.ThirdPersonControlPlayModeTests;' +
    'Game.Tests.PlayMode.P85BeautifulUiPlayModeTests'
Invoke-P85Unity 'P85-affected-playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testFilter', $playFilter,
    '-force-d3d11', '-testResults', (Join-Path $logPath 'P85-affected-playmode.xml')) $false
Assert-P85Tests 'P85-affected-playmode' 6

Invoke-P85Unity 'P85-build' @('-quit', '-buildTarget', 'Win64', '-executeMethod',
    'U00Build.WindowsDevelopment', '-serenityBuildPath', 'Builds/P85/Serenity.exe')
$buildLog = Join-Path $logPath 'P85-build.log'
if (-not (Select-String -LiteralPath $buildLog -Pattern 'U00_BUILD_RESULT: Succeeded; errors=0' -Quiet)) {
    throw 'P85 build did not report success.'
}

$player = Join-Path $ProjectPath 'Builds\P85\Serenity.exe'
if (-not (Test-Path -LiteralPath $player)) { throw 'P85 Windows Player is missing.' }
New-Item -ItemType Directory -Force -Path $PlayerSmokeOutput | Out-Null
$oldSave = $env:SERENITY_SAVE_DIRECTORY
$oldSettings = $env:SERENITY_SETTINGS_PATH
$env:SERENITY_SAVE_DIRECTORY = Join-Path $PlayerSmokeOutput 'saves'
$env:SERENITY_SETTINGS_PATH = Join-Path $PlayerSmokeOutput 'settings\user-settings.json'
try {
    $playerLog = Join-Path $PlayerSmokeOutput 'Player.log'
    $process = Start-Process -FilePath $player -ArgumentList @('-logFile', $playerLog,
        '-screen-fullscreen', '0', '-screen-mode', 'windowed', '-screen-width', '1280', '-screen-height', '720', '-force-d3d11',
        '-p85Smoke', '-p85SmokeOutput', $PlayerSmokeOutput) -WindowStyle Normal -PassThru
    $exitCode = Wait-P85Process $process $PlayerSmokeTimeoutSeconds 'P85 Player smoke'
    if ($null -ne $exitCode -and $exitCode -ne 0) { throw "P85 Player smoke exited $exitCode." }
    Assert-P85Log 'P85 Player smoke' $playerLog
} finally {
    $env:SERENITY_SAVE_DIRECTORY = $oldSave
    $env:SERENITY_SETTINGS_PATH = $oldSettings
}
$resultPath = Join-Path $PlayerSmokeOutput 'p85-player-smoke.json'
if (-not (Test-Path -LiteralPath $resultPath)) { throw 'P85 Player smoke result is missing.' }
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if (-not $result.passed) { throw "P85 Player smoke failed: $($result.error)" }
foreach ($name in @('p85-main-menu.png', 'p85-ruler-editor.png', 'p85-local-rts.png',
        'p85-global-map.png', 'p85-tps.png')) {
    $screenshot = Join-Path $PlayerSmokeOutput $name
    if (-not (Test-Path -LiteralPath $screenshot) -or (Get-Item -LiteralPath $screenshot).Length -le 0) {
        throw "P85 Player smoke screenshot is missing or empty: $name"
    }
    Add-Type -AssemblyName System.Drawing
    $image = [Drawing.Image]::FromFile($screenshot)
    try {
        if ($image.Width -ne 1280 -or $image.Height -ne 720) {
            throw "P85 Player smoke screenshot has wrong dimensions: $name is $($image.Width)x$($image.Height)."
        }
    } finally {
        $image.Dispose()
    }
}
Write-Output "P85 verification passed. Evidence: $PlayerSmokeOutput"
