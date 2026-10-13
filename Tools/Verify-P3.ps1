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
    $PlayerSmokeOutput = Join-Path $logPath ('P3-player-smoke-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
}
$PlayerSmokeOutput = [IO.Path]::GetFullPath($PlayerSmokeOutput)

function Assert-P3Log([string]$Name, [string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "$Name log is missing: $Path" }
    $patterns = @('error CS\d+', 'Scripts have compiler errors', 'Unhandled Exception',
        'executeMethod method .* threw exception',
        'NullReferenceException', 'MissingReferenceException', 'AssertionException',
        'invariant failure', 'Native Collection.*leak', 'Player crash')
    $fatal = Select-String -LiteralPath $Path -Pattern $patterns -ErrorAction SilentlyContinue
    if ($fatal) { throw "$Name log contains a fatal diagnostic: $($fatal[0].Line)" }
}

function Wait-P3Process([System.Diagnostics.Process]$Process, [int]$TimeoutSeconds, [string]$Name) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while (Get-Process -Id $Process.Id -ErrorAction SilentlyContinue) {
        if ([DateTime]::UtcNow -ge $deadline) {
            Stop-Process -Id $Process.Id -Force
            throw "$Name timed out; only its launched process PID was terminated."
        }
        Start-Sleep -Milliseconds 500
    }
    try {
        $Process.Refresh()
        return $Process.ExitCode
    } catch {
        # Some Unity Windows exits invalidate the Process handle after the PID disappears.
        return $null
    }
}

function Invoke-P3Unity([string]$Name, [string[]]$Arguments, [bool]$NoGraphics = $true) {
    $stepLog = Join-Path $logPath "$Name.log"
    Set-Content -LiteralPath $stepLog -Value ''
    $base = @('-batchmode')
    if ($NoGraphics) { $base += '-nographics' }
    $base += @('-projectPath', $ProjectPath, '-logFile', $stepLog)
    $process = Start-Process -FilePath $EditorPath -ArgumentList ($base + $Arguments) -WindowStyle Hidden -PassThru
    Write-Output "$Name : PID $($process.Id)"
    if ($Name -eq 'P3-build') {
        $deadline = [DateTime]::UtcNow.AddSeconds($StepTimeoutSeconds)
        while ([DateTime]::UtcNow -lt $deadline) {
            if (Test-Path -LiteralPath $stepLog) {
                $result = Select-String -LiteralPath $stepLog -Pattern 'U00_BUILD_RESULT: Succeeded; errors=0' -Quiet
                if ($result) { break }
            }
            if (-not (Get-Process -Id $process.Id -ErrorAction SilentlyContinue)) {
                if (Test-Path -LiteralPath $stepLog) { Get-Content -LiteralPath $stepLog -Tail 200 }
                throw "$Name exited before reporting a successful U00 build result."
            }
            Start-Sleep -Milliseconds 500
        }
        if (-not $result) {
            if (Get-Process -Id $process.Id -ErrorAction SilentlyContinue) { Stop-Process -Id $process.Id -Force }
            throw "$Name timed out before reporting a successful U00 build result."
        }
        $exitCode = Wait-P3Process $process $StepTimeoutSeconds $Name
        if ($null -ne $exitCode -and $exitCode -ne 0) {
            Get-Content -LiteralPath $stepLog -Tail 200
            throw "$Name failed with exit code $exitCode after reporting its build result."
        }
        Assert-P3Log $Name $stepLog
        Write-Output "$Name : U00 build reported success."
        return
    }
    $exitCode = Wait-P3Process $process $StepTimeoutSeconds $Name
    if ($null -ne $exitCode -and $exitCode -ne 0) {
        if (Test-Path -LiteralPath $stepLog) { Get-Content -LiteralPath $stepLog -Tail 200 }
        throw "$Name failed with exit code $exitCode."
    }
    Assert-P3Log $Name $stepLog
    Write-Output "$Name : Unity exit 0."
}

function Assert-P3TestResult([string]$Name, [int]$Minimum) {
    $path = Join-Path $logPath "$Name.xml"
    if (-not (Test-Path -LiteralPath $path)) { throw "$Name did not produce NUnit XML." }
    [xml]$result = Get-Content -LiteralPath $path -Raw
    $passed = [int]$result.'test-run'.passed
    $failed = [int]$result.'test-run'.failed
    $skipped = [int]$result.'test-run'.skipped
    if ($result.'test-run'.result -ne 'Passed' -or $passed -lt $Minimum -or $failed -ne 0 -or $skipped -ne 0) {
        throw "$Name did not pass every required test (passed=$passed failed=$failed skipped=$skipped)."
    }
    Write-Output "$Name : $passed passed, $failed failed, $skipped skipped."
}

$parseErrors = $null
[Management.Automation.Language.Parser]::ParseFile($PSCommandPath, [ref]$null, [ref]$parseErrors) | Out-Null
if ($parseErrors.Count -ne 0) { throw "Verify-P3.ps1 parser errors: $($parseErrors[0].Message)" }

Invoke-P3Unity 'P3-generate' @('-quit', '-executeMethod', 'Game.Infrastructure.Editor.P3ReferenceSceneBuilder.Build')
Invoke-P3Unity 'P3-focused-editmode' @('-runTests', '-testPlatform', 'EditMode', '-testCategory', 'P3',
    '-testResults', (Join-Path $logPath 'P3-focused-editmode.xml'))
Assert-P3TestResult 'P3-focused-editmode' 3
Invoke-P3Unity 'P3-focused-playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testCategory', 'P3',
    '-testResults', (Join-Path $logPath 'P3-focused-playmode.xml'))
Assert-P3TestResult 'P3-focused-playmode' 3
Invoke-P3Unity 'P3-gpu-playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testCategory', 'P3',
    '-force-d3d11', '-testResults', (Join-Path $logPath 'P3-gpu-playmode.xml')) $false
Assert-P3TestResult 'P3-gpu-playmode' 3
Invoke-P3Unity 'P3-affected-editmode' @('-runTests', '-testPlatform', 'EditMode',
    '-testFilter', 'Game.Tests.Editor.U05AAssetFoundationTests;Game.Tests.Editor.BuildingFoundationTests;Game.Tests.Editor.WeatherGenerationTests;Game.Tests.Editor.WeatherModifierTests;Game.Tests.Editor.PlayerControlStateTests',
    '-testResults', (Join-Path $logPath 'P3-affected-editmode.xml'))
Assert-P3TestResult 'P3-affected-editmode' 1
Invoke-P3Unity 'P3-affected-playmode' @('-runTests', '-testPlatform', 'PlayMode',
    '-testFilter', 'Game.Tests.PlayMode.LocalGameplaySceneTests;Game.Tests.PlayMode.BuildingPlacementPlayModeTests;Game.Tests.PlayMode.WeatherPlayModeTests;Game.Tests.PlayMode.ThirdPersonControlPlayModeTests;Game.Tests.PlayMode.GameplayUiPlayModeTests',
    '-testResults', (Join-Path $logPath 'P3-affected-playmode.xml'))
Assert-P3TestResult 'P3-affected-playmode' 1
Invoke-P3Unity 'P3-build' @('-quit', '-buildTarget', 'Win64', '-executeMethod',
    'U00Build.WindowsDevelopment', '-serenityBuildPath', 'Builds/P3/Serenity.exe')

$player = Join-Path $ProjectPath 'Builds\P3\Serenity.exe'
if (-not (Test-Path -LiteralPath $player)) { throw 'P3 Windows development Player is missing.' }
New-Item -ItemType Directory -Force -Path $PlayerSmokeOutput | Out-Null
$playerLog = Join-Path $PlayerSmokeOutput 'Player.log'
$previousSaveDirectory = $env:SERENITY_SAVE_DIRECTORY
$previousSettingsPath = $env:SERENITY_SETTINGS_PATH
$env:SERENITY_SAVE_DIRECTORY = Join-Path $PlayerSmokeOutput 'saves'
$env:SERENITY_SETTINGS_PATH = Join-Path $PlayerSmokeOutput 'settings\user-settings.json'
try {
    $playerProcess = Start-Process -FilePath $player -ArgumentList @('-logFile', $playerLog,
        '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720', '-force-d3d11',
        '-p3Smoke', '-p3SmokeSeed', '310033', '-p3SmokeOutput', $PlayerSmokeOutput) `
        -WindowStyle Normal -PassThru
    Write-Output "P3-player-smoke : PID $($playerProcess.Id)"
    $playerExitCode = Wait-P3Process $playerProcess $PlayerSmokeTimeoutSeconds 'P3 Player smoke'
    if ($null -ne $playerExitCode -and $playerExitCode -ne 0) {
        if (Test-Path -LiteralPath $playerLog) { Get-Content -LiteralPath $playerLog -Tail 200 }
        throw "P3 Player smoke failed with exit code $playerExitCode."
    }
} finally {
    $env:SERENITY_SAVE_DIRECTORY = $previousSaveDirectory
    $env:SERENITY_SETTINGS_PATH = $previousSettingsPath
}
Assert-P3Log 'P3-player-smoke' $playerLog
$smokeResultPath = Join-Path $PlayerSmokeOutput 'p3-player-smoke.json'
if (-not (Test-Path -LiteralPath $smokeResultPath)) { throw 'P3 Player smoke result is missing.' }
$smoke = Get-Content -LiteralPath $smokeResultPath -Raw | ConvertFrom-Json
if (-not $smoke.passed -or -not $smoke.canonical_characters_linked -or
    -not $smoke.canonical_buildings_linked -or -not $smoke.stable_identity_preserved -or
    -not $smoke.rts_camera_rendered -or -not $smoke.tps_camera_rendered -or
    -not $smoke.clear_preview_applied -or -not $smoke.rain_preview_applied -or
    $smoke.rain_particles_alive -le 0) {
    throw 'P3 Player smoke result failed its required assertions.'
}
foreach ($name in @('p3-rts-clear.png', 'p3-tps-clear.png', 'p3-tps-rain.png')) {
    $path = Join-Path $PlayerSmokeOutput $name
    if (-not (Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -le 0) {
        throw "P3 Player smoke screenshot is missing or empty: $path"
    }
}
Write-Output "P3-player-smoke : passed; $smokeResultPath"
Write-Output "P3-build : passed; $player"
Write-Output 'P3 verification passed.'
