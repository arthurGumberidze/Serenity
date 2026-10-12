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
    $PlayerSmokeOutput = 'C:\serenity_u31_player_smoke_' + [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss')
}
$PlayerSmokeOutput = [IO.Path]::GetFullPath($PlayerSmokeOutput)

function Assert-U31Log([string]$Name, [string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "$Name log is missing: $Path" }
    $patterns = @('error CS\d+', 'Scripts have compiler errors', 'Unhandled Exception',
        'NullReferenceException', 'MissingReferenceException', 'AssertionException',
        'invariant failure', 'Native Collection.*leak', 'Player crash')
    $fatal = Select-String -LiteralPath $Path -Pattern $patterns -ErrorAction SilentlyContinue
    if ($fatal) { throw "$Name log contains a fatal diagnostic: $($fatal[0].Line)" }
}

function Invoke-U31Unity([string]$Name, [string[]]$Arguments, [bool]$NoGraphics = $true) {
    $stepLog = Join-Path $logPath "$Name.log"
    $base = @('-batchmode')
    if ($NoGraphics) { $base += '-nographics' }
    $base += @('-projectPath', $ProjectPath, '-logFile', $stepLog)
    $process = Start-Process -FilePath $EditorPath -ArgumentList ($base + $Arguments) `
        -WindowStyle Hidden -PassThru
    Write-Output "$Name : PID $($process.Id)"
    if (-not $process.WaitForExit($StepTimeoutSeconds * 1000)) {
        $process.Refresh()
        if (Test-Path -LiteralPath $stepLog) { Get-Content -LiteralPath $stepLog -Tail 160 }
        Stop-Process -Id $process.Id -Force
        throw "$Name timed out; only its launched Unity PID was terminated."
    }
    if ($process.ExitCode -ne 0) {
        if (Test-Path -LiteralPath $stepLog) { Get-Content -LiteralPath $stepLog -Tail 200 }
        throw "$Name failed with exit code $($process.ExitCode)."
    }
    Assert-U31Log $Name $stepLog
    Write-Output "$Name : Unity exit 0."
}

function Assert-U31TestResult([string]$Name, [int]$Minimum) {
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
if ($parseErrors.Count -ne 0) { throw "Verify-U31.ps1 parser errors: $($parseErrors[0].Message)" }

Invoke-U31Unity 'U31-compile' @('-quit')
Invoke-U31Unity 'U31-focused-editmode' @('-runTests', '-testPlatform', 'EditMode',
    '-testCategory', 'U31', '-testResults', (Join-Path $logPath 'U31-focused-editmode.xml'))
Assert-U31TestResult 'U31-focused-editmode' 4
Invoke-U31Unity 'U31-focused-playmode' @('-runTests', '-testPlatform', 'PlayMode',
    '-testFilter', 'Game.Tests.PlayMode.U31IntegrationPlayModeTests',
    '-testResults', (Join-Path $logPath 'U31-focused-playmode.xml'))
Assert-U31TestResult 'U31-focused-playmode' 2
Invoke-U31Unity 'U31-gpu-playmode' @('-runTests', '-testPlatform', 'PlayMode',
    '-testFilter', 'Game.Tests.PlayMode.U31GpuPlayModeTests',
    '-force-d3d11', '-testResults', (Join-Path $logPath 'U31-gpu-playmode.xml')) $false
Assert-U31TestResult 'U31-gpu-playmode' 1
Invoke-U31Unity 'U31-critical-editmode' @('-runTests', '-testPlatform', 'EditMode',
    '-testCategory', 'U04;U09;U17;U20;U21;U22;U23;U24;U25;U26;U27;U28;U30A',
    '-testResults', (Join-Path $logPath 'U31-critical-editmode.xml'))
Assert-U31TestResult 'U31-critical-editmode' 1
Invoke-U31Unity 'U31-critical-playmode' @('-runTests', '-testPlatform', 'PlayMode',
    '-testCategory', 'U04;U09;U17;U20;U21;U22;U23;U24;U25;U26;U27;U28;U30A',
    '-testResults', (Join-Path $logPath 'U31-critical-playmode.xml'))
Assert-U31TestResult 'U31-critical-playmode' 1
Invoke-U31Unity 'U31-persistence' @('-runTests', '-testPlatform', 'EditMode',
    '-testFilter', 'Game.Tests.Editor.PersistenceTests',
    '-testResults', (Join-Path $logPath 'U31-persistence.xml'))
Assert-U31TestResult 'U31-persistence' 35

if ([string]::IsNullOrWhiteSpace($env:DYNASTYGAME_PG_TEST_DATABASE)) {
    Write-Output 'U31-postgresql : SKIPPED_NOT_CONFIGURED'
} else {
    Invoke-U31Unity 'U31-postgresql' @('-runTests', '-testPlatform', 'EditMode',
        '-testCategory', 'PostgresIntegration',
        '-testResults', (Join-Path $logPath 'U31-postgresql.xml'))
    Assert-U31TestResult 'U31-postgresql' 1
}

Invoke-U31Unity 'U31-build' @('-quit', '-buildTarget', 'Win64',
    '-executeMethod', 'U00Build.WindowsDevelopment', '-serenityBuildPath', 'Builds/U31/Serenity.exe')
$player = Join-Path $ProjectPath 'Builds\U31\Serenity.exe'
if (-not (Test-Path -LiteralPath $player)) { throw 'U31 Windows development player is missing.' }

New-Item -ItemType Directory -Force -Path $PlayerSmokeOutput | Out-Null
$playerLog = Join-Path $PlayerSmokeOutput 'player.log'
$previousSaveDirectory = $env:SERENITY_SAVE_DIRECTORY
$env:SERENITY_SAVE_DIRECTORY = Join-Path $PlayerSmokeOutput 'saves'
try {
    $playerProcess = Start-Process -FilePath $player -ArgumentList @('-logFile', $playerLog,
        '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '900',
        '-u31Smoke', '-u31SmokeSeed', '310031', '-u31SmokeOutput', $PlayerSmokeOutput) `
        -WindowStyle Hidden -PassThru
    Write-Output "U31-player-smoke : PID $($playerProcess.Id)"
    if (-not $playerProcess.WaitForExit($PlayerSmokeTimeoutSeconds * 1000)) {
        Stop-Process -Id $playerProcess.Id -Force
        throw 'U31 player smoke timed out; only its launched Player PID was terminated.'
    }
    if ($playerProcess.ExitCode -ne 0) {
        if (Test-Path -LiteralPath $playerLog) { Get-Content -LiteralPath $playerLog -Tail 200 }
        throw "U31 player smoke failed with exit code $($playerProcess.ExitCode)."
    }
} finally {
    $env:SERENITY_SAVE_DIRECTORY = $previousSaveDirectory
}
Assert-U31Log 'U31-player-smoke' $playerLog
$smokeResultPath = Join-Path $PlayerSmokeOutput 'u31-player-smoke.json'
if (-not (Test-Path -LiteralPath $smokeResultPath)) { throw 'U31 player smoke result is missing.' }
$smoke = Get-Content -LiteralPath $smokeResultPath -Raw | ConvertFrom-Json
if (-not $smoke.passed -or $smoke.player_character_count -ne 10 -or $smoke.ai_character_count -ne 6 -or
    $smoke.composition_root_count -ne 1 -or $smoke.gameplay_ui_count -ne 1 -or
    $smoke.ui_subscription_count -ne 4 -or -not $smoke.save_created -or -not $smoke.load_completed) {
    throw 'U31 player smoke result failed its required assertions.'
}
$screenshot = Join-Path $PlayerSmokeOutput 'u31-player-smoke.png'
if (-not (Test-Path -LiteralPath $screenshot) -or (Get-Item -LiteralPath $screenshot).Length -le 0) {
    throw 'U31 player smoke screenshot is missing or empty.'
}
Write-Output "U31-player-smoke : passed; $smokeResultPath"
Write-Output "U31-build : passed; $player"
Write-Output 'U31 verification passed.'
