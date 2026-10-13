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
    $PlayerSmokeOutput = Join-Path $logPath ('P7-player-smoke-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
}
$PlayerSmokeOutput = [IO.Path]::GetFullPath($PlayerSmokeOutput)

function Wait-P7Process([Diagnostics.Process]$Process, [int]$TimeoutSeconds, [string]$Name, [string]$StepLog) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $successfulBuildObservedAt = $null
    while (-not $Process.HasExited) {
        if ($Name -eq 'P7-build' -and (Test-Path -LiteralPath $StepLog) -and
            (Select-String -LiteralPath $StepLog -Pattern 'U00_BUILD_RESULT: Succeeded; errors=0' -Quiet)) {
            if ($null -eq $successfulBuildObservedAt) { $successfulBuildObservedAt = [DateTime]::UtcNow }
            elseif ([DateTime]::UtcNow -ge $successfulBuildObservedAt.AddSeconds(30)) {
                Stop-Process -Id $Process.Id
                $Process.WaitForExit(5000) | Out-Null
                Write-Host "$Name : terminated lingering Editor after confirmed successful build."
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

function Assert-P7Log([string]$Name, [string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "$Name log is missing: $Path" }
    $fatal = Select-String -LiteralPath $Path -Pattern @('error CS\d+', 'Scripts have compiler errors',
        'Unhandled Exception', 'executeMethod method .* threw exception', 'NullReferenceException',
        'MissingReferenceException', 'ArgumentOutOfRangeException', 'AssertionException', 'invariant failure',
        'Native Collection.*leak', 'Player crash') -ErrorAction SilentlyContinue
    if ($fatal) { throw "$Name log contains a fatal diagnostic: $($fatal[0].Line)" }
}

function Invoke-P7Unity([string]$Name, [string[]]$Arguments, [bool]$NoGraphics = $true) {
    $stepLog = Join-Path $logPath "$Name.log"
    Set-Content -LiteralPath $stepLog -Value ''
    $base = @('-batchmode')
    if ($NoGraphics) { $base += '-nographics' }
    $base += @('-projectPath', $ProjectPath, '-logFile', $stepLog)
    $process = Start-Process -FilePath $EditorPath -ArgumentList ($base + $Arguments) -WindowStyle Hidden -PassThru
    Write-Output "$Name : PID $($process.Id)"
    $exitCode = Wait-P7Process $process $StepTimeoutSeconds $Name $stepLog
    if ($null -ne $exitCode -and $exitCode -ne 0) {
        Get-Content -LiteralPath $stepLog -Tail 180
        throw "$Name failed with exit code $exitCode."
    }
    Assert-P7Log $Name $stepLog
}

function Assert-P7Tests([string]$Name, [int]$Minimum) {
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
if ($parseErrors.Count -ne 0) { throw "Verify-P7.ps1 parser errors: $($parseErrors[0].Message)" }

Invoke-P7Unity 'P7-focused-editmode' @('-runTests', '-testPlatform', 'EditMode', '-testCategory', 'P7',
    '-testResults', (Join-Path $logPath 'P7-focused-editmode.xml'))
Assert-P7Tests 'P7-focused-editmode' 5
Invoke-P7Unity 'P7-focused-playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testCategory', 'P7',
    '-testResults', (Join-Path $logPath 'P7-focused-playmode.xml'))
Assert-P7Tests 'P7-focused-playmode' 2
Invoke-P7Unity 'P7-gpu-playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testCategory', 'P7',
    '-force-d3d11', '-testResults', (Join-Path $logPath 'P7-gpu-playmode.xml')) $false
Assert-P7Tests 'P7-gpu-playmode' 2

$editFilter = 'Game.Tests.Editor.StrategicAiTests;Game.Tests.Editor.PersistenceTests;' +
    'Game.Tests.Editor.PlayerControlStateTests;Game.Tests.Editor.U30AComplianceTests;' +
    'Game.Tests.Editor.P1RussianLocalizationTests;Game.Tests.Editor.DynastyAndSuccessionTests;' +
    'Game.Tests.Editor.HealthAndFamilyTests'
Invoke-P7Unity 'P7-affected-editmode' @('-runTests', '-testPlatform', 'EditMode', '-testFilter', $editFilter,
    '-testResults', (Join-Path $logPath 'P7-affected-editmode.xml'))
Assert-P7Tests 'P7-affected-editmode' 1

$playFilter = 'Game.Tests.PlayMode.GameplayUiPlayModeTests;Game.Tests.PlayMode.U30ACompliancePlayModeTests;' +
    'Game.Tests.PlayMode.DynastySuccessionPlayModeTests;Game.Tests.PlayMode.HealthAndFamilyPlayModeTests'
Invoke-P7Unity 'P7-affected-playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testFilter', $playFilter,
    '-testResults', (Join-Path $logPath 'P7-affected-playmode.xml'))
Assert-P7Tests 'P7-affected-playmode' 1

Invoke-P7Unity 'P7-build' @('-quit', '-buildTarget', 'Win64', '-executeMethod',
    'U00Build.WindowsDevelopment', '-serenityBuildPath', 'Builds/P7/Serenity.exe')
$buildLog = Join-Path $logPath 'P7-build.log'
if (-not (Select-String -LiteralPath $buildLog -Pattern 'U00_BUILD_RESULT: Succeeded; errors=0' -Quiet)) {
    throw 'P7 build did not report success.'
}

$player = Join-Path $ProjectPath 'Builds\P7\Serenity.exe'
if (-not (Test-Path -LiteralPath $player)) { throw 'P7 Windows Player is missing.' }
New-Item -ItemType Directory -Force -Path $PlayerSmokeOutput | Out-Null
$oldSave = $env:SERENITY_SAVE_DIRECTORY
$oldSettings = $env:SERENITY_SETTINGS_PATH
$env:SERENITY_SAVE_DIRECTORY = Join-Path $PlayerSmokeOutput 'saves'
$env:SERENITY_SETTINGS_PATH = Join-Path $PlayerSmokeOutput 'settings\user-settings.json'
try {
    $playerLog = Join-Path $PlayerSmokeOutput 'Player.log'
    $process = Start-Process -FilePath $player -ArgumentList @('-logFile', $playerLog,
        '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720', '-force-d3d11',
        '-p7Smoke', '-p7SmokeSeed', '71007', '-p7SmokeOutput', $PlayerSmokeOutput) -WindowStyle Normal -PassThru
    $exitCode = Wait-P7Process $process $PlayerSmokeTimeoutSeconds 'P7 Player smoke' $playerLog
    if ($null -ne $exitCode -and $exitCode -ne 0) { throw "P7 Player smoke exited $exitCode." }
    Assert-P7Log 'P7 Player smoke' $playerLog
} finally {
    $env:SERENITY_SAVE_DIRECTORY = $oldSave
    $env:SERENITY_SETTINGS_PATH = $oldSettings
}
$resultPath = Join-Path $PlayerSmokeOutput 'p7-player-smoke.json'
if (-not (Test-Path -LiteralPath $resultPath)) { throw 'P7 Player smoke result is missing.' }
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if (-not $result.passed) { throw "P7 Player smoke failed: $($result.error)" }
foreach ($name in @('p7-defended-battle.png', 'p7-occupation-progress.png',
    'p7-occupation-interrupted.png', 'p7-victory-continues.png', 'p7-defeat.png',
    'p7-recovered-after-load.png')) {
    $path = Join-Path $PlayerSmokeOutput $name
    if (-not (Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -le 0) {
        throw "P7 Player smoke screenshot is missing or empty: $path"
    }
}
Write-Output "P7 verification passed. Evidence: $PlayerSmokeOutput"
