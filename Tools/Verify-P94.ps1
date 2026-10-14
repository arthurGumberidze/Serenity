[CmdletBinding()]
param(
    [string]$ProjectPath,
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [int]$StepTimeoutSeconds = 3600,
    [int]$PlayerSmokeTimeoutSeconds = 300,
    [string]$PlayerSmokeOutput
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path -Path $PSScriptRoot -Parent }
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$logPath = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Force -Path $logPath | Out-Null
if ([string]::IsNullOrWhiteSpace($PlayerSmokeOutput)) {
    $PlayerSmokeOutput = Join-Path $logPath ('P94-player-smoke-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
}
$PlayerSmokeOutput = [IO.Path]::GetFullPath($PlayerSmokeOutput)

function Wait-P94Process([Diagnostics.Process]$Process, [int]$TimeoutSeconds, [string]$Name) {
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

function Assert-P94Log([string]$Name, [string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "$Name log is missing: $Path" }
    $fatal = Select-String -LiteralPath $Path -Pattern @('error CS\d+', 'Scripts have compiler errors',
        'Unhandled Exception', 'executeMethod method .* threw exception', 'NullReferenceException',
        'MissingReferenceException', 'AssertionException', 'invariant failure', 'Player crash') -ErrorAction SilentlyContinue
    if ($fatal) { throw "$Name log contains a fatal diagnostic: $($fatal[0].Line)" }
}

function Invoke-P94Unity([string]$Name, [string[]]$Arguments, [bool]$NoGraphics = $true) {
    $stepLog = Join-Path $logPath "$Name.log"
    Set-Content -LiteralPath $stepLog -Value ''
    $base = @('-batchmode')
    if ($NoGraphics) { $base += '-nographics' }
    $base += @('-projectPath', $ProjectPath, '-logFile', $stepLog)
    $process = Start-Process -FilePath $EditorPath -ArgumentList ($base + $Arguments) -WindowStyle Hidden -PassThru
    Write-Output "$Name : PID $($process.Id)"
    $exitCode = Wait-P94Process $process $StepTimeoutSeconds $Name
    if ($null -ne $exitCode -and $exitCode -ne 0) {
        Get-Content -LiteralPath $stepLog -Tail 180
        throw "$Name failed with exit code $exitCode."
    }
    Assert-P94Log $Name $stepLog
}

function Assert-P94Tests([string]$Name, [int]$Minimum) {
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
if ($parseErrors.Count -ne 0) { throw "Verify-P94.ps1 parser errors: $($parseErrors[0].Message)" }

Invoke-P94Unity 'P94-generate' @('-quit', '-executeMethod',
    'Game.Infrastructure.Editor.P94StoneAgeSettlementBuilder.Build')
Invoke-P94Unity 'P94-focused-editmode' @('-runTests', '-testPlatform', 'EditMode', '-testCategory', 'P94',
    '-testResults', (Join-Path $logPath 'P94-focused-editmode.xml'))
Assert-P94Tests 'P94-focused-editmode' 3
Invoke-P94Unity 'P94-focused-playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testCategory', 'P94',
    '-testResults', (Join-Path $logPath 'P94-focused-playmode.xml'))
Assert-P94Tests 'P94-focused-playmode' 3
Invoke-P94Unity 'P94-affected-editmode' @('-runTests', '-testPlatform', 'EditMode',
    '-testCategory', 'P83|P92|P93',
    '-testResults', (Join-Path $logPath 'P94-affected-editmode.xml'))
Assert-P94Tests 'P94-affected-editmode' 12
Invoke-P94Unity 'P94-affected-playmode' @('-runTests', '-testPlatform', 'PlayMode',
    '-testCategory', 'P83|P92|P93',
    '-testResults', (Join-Path $logPath 'P94-affected-playmode.xml'))
Assert-P94Tests 'P94-affected-playmode' 9

Invoke-P94Unity 'P94-build' @('-quit', '-executeMethod',
    'U00Build.WindowsDevelopment', '-serenityBuildPath', 'Builds/P94/Serenity.exe')
$buildLog = Join-Path $logPath 'P94-build.log'
if (-not (Select-String -LiteralPath $buildLog -Pattern 'U00_BUILD_RESULT: Succeeded; errors=0' -Quiet)) {
    throw 'P94 build did not report success.'
}

$player = Join-Path $ProjectPath 'Builds\P94\Serenity.exe'
if (-not (Test-Path -LiteralPath $player)) { throw 'P94 Windows Player is missing.' }
New-Item -ItemType Directory -Force -Path $PlayerSmokeOutput | Out-Null
$oldSave = $env:SERENITY_SAVE_DIRECTORY
$oldSettings = $env:SERENITY_SETTINGS_PATH
$env:SERENITY_SAVE_DIRECTORY = Join-Path $PlayerSmokeOutput 'saves'
$env:SERENITY_SETTINGS_PATH = Join-Path $PlayerSmokeOutput 'settings\user-settings.json'
try {
    $playerLog = Join-Path $PlayerSmokeOutput 'Player.log'
    $process = Start-Process -FilePath $player -ArgumentList @('-logFile', $playerLog,
        '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720', '-force-d3d11',
        '-p94Smoke', '-p94SmokeOutput', $PlayerSmokeOutput) -WindowStyle Normal -PassThru
    $exitCode = Wait-P94Process $process $PlayerSmokeTimeoutSeconds 'P94 Player smoke'
    if ($null -ne $exitCode -and $exitCode -ne 0) { throw "P94 Player smoke exited $exitCode." }
    Assert-P94Log 'P94 Player smoke' $playerLog
} finally {
    $env:SERENITY_SAVE_DIRECTORY = $oldSave
    $env:SERENITY_SETTINGS_PATH = $oldSettings
}
$resultPath = Join-Path $PlayerSmokeOutput 'p94-player-smoke.json'
if (-not (Test-Path -LiteralPath $resultPath)) { throw 'P94 Player smoke result is missing.' }
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if (-not $result.passed) { throw "P94 Player smoke failed: $($result.error)" }
foreach ($name in @('p94-rts-work.png', 'p94-tps-settlement.png')) {
    $screenshot = Join-Path $PlayerSmokeOutput $name
    if (-not (Test-Path -LiteralPath $screenshot) -or (Get-Item -LiteralPath $screenshot).Length -le 0) {
        throw "P94 Player smoke screenshot is missing or empty: $name"
    }
}
Write-Output "P94 verification passed. Evidence: $PlayerSmokeOutput"
