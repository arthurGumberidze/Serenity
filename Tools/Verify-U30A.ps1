[CmdletBinding()]
param(
    [string]$ProjectPath,
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [int]$StepTimeoutSeconds = 1200,
    [string]$ShortSoakOutput
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path -Path $PSScriptRoot -Parent }
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$logPath = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Force -Path $logPath | Out-Null
if ([string]::IsNullOrWhiteSpace($ShortSoakOutput)) {
    $ShortSoakOutput = 'C:\serenity_u30a_short_soak_' + [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss')
}
$ShortSoakOutput = [IO.Path]::GetFullPath($ShortSoakOutput)

function Assert-U30ALog([string]$Name, [string]$Path) {
    $patterns = @('error CS\d+', 'Scripts have compiler errors', 'Unhandled Exception',
        'NullReferenceException', 'MissingReferenceException', 'AssertionException',
        'invariant failure', 'Native Collection.*leak', 'Player crash')
    $fatal = Select-String -LiteralPath $Path -Pattern $patterns -ErrorAction SilentlyContinue
    if ($fatal) { throw "$Name log contains a fatal diagnostic: $($fatal[0].Line)" }
}

function Invoke-U30AUnity([string]$Name, [string[]]$Arguments, [bool]$NoGraphics = $true) {
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
    Assert-U30ALog $Name $stepLog
    Write-Output "$Name : Unity exit 0."
}

function Assert-U30ATestResult([string]$Name, [int]$Minimum, [bool]$AllowSkipped = $false) {
    $path = Join-Path $logPath "$Name.xml"
    if (-not (Test-Path -LiteralPath $path)) { throw "$Name did not produce NUnit XML." }
    [xml]$result = Get-Content -LiteralPath $path -Raw
    $skipped = [int]$result.'test-run'.skipped
    if ($result.'test-run'.result -ne 'Passed' -or [int]$result.'test-run'.passed -lt $Minimum -or
        [int]$result.'test-run'.failed -ne 0 -or (-not $AllowSkipped -and $skipped -ne 0)) {
        throw "$Name did not pass every required test."
    }
    Write-Output "$Name : $($result.'test-run'.passed) passed, $($result.'test-run'.failed) failed, $skipped skipped."
}

foreach ($script in @('Verify-U30A.ps1', 'Run-Soak-U30.ps1')) {
    $parseErrors = $null
    [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $script),
        [ref]$null, [ref]$parseErrors) | Out-Null
    if ($parseErrors.Count -ne 0) { throw "$script parser errors: $($parseErrors[0].Message)" }
}

Invoke-U30AUnity 'U30A-compile' @('-quit')
Invoke-U30AUnity 'U30A-focused-editmode' @('-runTests', '-testPlatform', 'EditMode',
    '-testCategory', 'U30A', '-testResults', (Join-Path $logPath 'U30A-focused-editmode.xml'))
Assert-U30ATestResult 'U30A-focused-editmode' 8
Invoke-U30AUnity 'U30A-focused-playmode' @('-runTests', '-testPlatform', 'PlayMode',
    '-testCategory', 'U30A', '-testResults', (Join-Path $logPath 'U30A-focused-playmode.xml'))
Assert-U30ATestResult 'U30A-focused-playmode' 5
Invoke-U30AUnity 'U30A-critical-editmode' @('-runTests', '-testPlatform', 'EditMode',
    '-testCategory', 'U04;U09;U17;U20;U21;U22;U23;U24;U25;U26;U27;U28',
    '-testResults', (Join-Path $logPath 'U30A-critical-editmode.xml'))
Assert-U30ATestResult 'U30A-critical-editmode' 1
Invoke-U30AUnity 'U30A-critical-playmode' @('-runTests', '-testPlatform', 'PlayMode',
    '-testCategory', 'U04;U09;U17;U20;U21;U22;U23;U24;U25;U26;U27;U28',
    '-testResults', (Join-Path $logPath 'U30A-critical-playmode.xml'))
Assert-U30ATestResult 'U30A-critical-playmode' 1
Invoke-U30AUnity 'U30A-persistence' @('-runTests', '-testPlatform', 'EditMode',
    '-testFilter', 'Game.Tests.Editor.PersistenceTests',
    '-testResults', (Join-Path $logPath 'U30A-persistence.xml'))
Assert-U30ATestResult 'U30A-persistence' 35
Invoke-U30AUnity 'U30A-gpu-playmode' @('-runTests', '-testPlatform', 'PlayMode',
    '-testCategory', 'U30A', '-testResults', (Join-Path $logPath 'U30A-gpu-playmode.xml')) $false
Assert-U30ATestResult 'U30A-gpu-playmode' 5

if ([string]::IsNullOrWhiteSpace($env:DYNASTYGAME_PG_TEST_DATABASE)) {
    Write-Output 'U30A-postgresql : SKIPPED_NOT_CONFIGURED'
} else {
    Invoke-U30AUnity 'U30A-postgresql' @('-runTests', '-testPlatform', 'EditMode',
        '-testCategory', 'PostgresIntegration',
        '-testResults', (Join-Path $logPath 'U30A-postgresql.xml'))
    Assert-U30ATestResult 'U30A-postgresql' 1
}

Invoke-U30AUnity 'U30A-build' @('-quit', '-buildTarget', 'Win64',
    '-executeMethod', 'U00Build.WindowsDevelopment', '-serenityBuildPath', 'Builds/U30A/Serenity.exe')
$player = Join-Path $ProjectPath 'Builds\U30A\Serenity.exe'
if (-not (Test-Path -LiteralPath $player)) { throw 'U30A Windows development player is missing.' }

& (Join-Path $PSScriptRoot 'Run-Soak-U30.ps1') -ProjectPath $ProjectPath -PlayerPath $player `
    -OutputFolder $ShortSoakOutput -TestMode -TestWallClockSeconds 30 -TestSegmentSeconds 10 `
    -TestWatchdogSeconds 10
if ($LASTEXITCODE -ne 0) { throw 'U30A short soak failed.' }
$soakResult = Join-Path $ShortSoakOutput 'soak-result.json'
if (-not (Test-Path -LiteralPath $soakResult)) { throw 'U30A short soak result is missing.' }
$soak = Get-Content -LiteralPath $soakResult -Raw | ConvertFrom-Json
if (-not $soak.passed) { throw 'U30A short soak result is not passed.' }
Write-Output "U30A-short-soak : passed; $soakResult"
Write-Output 'U30A verification passed.'
