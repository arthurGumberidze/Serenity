param(
    [string]$ProjectPath = (Split-Path $PSScriptRoot -Parent),
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [string]$RunDirectory,
    [int]$StepTimeoutSeconds = 1800,
    [int]$PlayerTimeoutSeconds = 900,
    [switch]$PerformanceOnly,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
if (-not (Test-Path -LiteralPath $EditorPath)) { throw "Unity Editor not found: $EditorPath" }

if ([string]::IsNullOrWhiteSpace($RunDirectory)) {
    $stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
    $RunDirectory = Join-Path $ProjectPath "Logs/U29/offline-$stamp"
}
$RunDirectory = [IO.Path]::GetFullPath($RunDirectory)
if (Test-Path -LiteralPath $RunDirectory) {
    if ((Get-ChildItem -LiteralPath $RunDirectory -Force | Measure-Object).Count -ne 0) {
        throw "Offline run directory must be new or empty: $RunDirectory"
    }
} else {
    New-Item -ItemType Directory -Path $RunDirectory -Force | Out-Null
}

$performanceDirectory = Join-Path $RunDirectory 'performance'
$transcriptPath = Join-Path $RunDirectory 'offline-run.log'
$statusPath = Join-Path $RunDirectory 'STATUS.txt'
$reportPath = Join-Path $performanceDirectory 'U29-DIAGNOSTIC-REPORT.md'
$verifyScript = Join-Path $PSScriptRoot 'Verify-U29.ps1'
$performanceScript = Join-Path $PSScriptRoot 'Run-U29-Performance.ps1'
$reportScript = Join-Path $PSScriptRoot 'Write-U29-PerformanceReport.ps1'
$playerPath = Join-Path $ProjectPath 'Builds/Windows/Serenity.exe'

foreach ($script in @($verifyScript, $performanceScript, $reportScript)) {
    if (-not (Test-Path -LiteralPath $script)) { throw "Required U29 script is missing: $script" }
}

if ($DryRun) {
    @(
        'state=DRY_RUN_OK'
        "updatedUtc=$([DateTime]::UtcNow.ToString('O'))"
        "runDirectory=$RunDirectory"
        "performanceOnly=$([bool]$PerformanceOnly)"
        "playerPath=$playerPath"
        "editorPath=$EditorPath"
    ) | Set-Content -LiteralPath $statusPath -Encoding UTF8
    Write-Output 'U29 offline dry run passed. No Unity or Player process was started.'
    Write-Output "Planned run directory: $RunDirectory"
    exit 0
}

$conflicts = @(Get-Process -Name Unity,Serenity -ErrorAction SilentlyContinue)
if ($conflicts.Count -gt 0) {
    $details = $conflicts | ForEach-Object { "$($_.ProcessName) PID=$($_.Id)" }
    throw "Close existing Unity/Serenity processes before the isolated offline run: $($details -join ', ')"
}

if (-not ('U29PowerRequest' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class U29PowerRequest
{
    [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
}
'@
}

function Set-OfflineStatus([string]$State, [string]$Message) {
    @(
        "state=$State"
        "updatedUtc=$([DateTime]::UtcNow.ToString('O'))"
        "message=$Message"
        "runDirectory=$RunDirectory"
        "liveLog=$transcriptPath"
        "finalReport=$reportPath"
    ) | Set-Content -LiteralPath $statusPath -Encoding UTF8
}

$runFailed = $false
$failureMessage = $null
$reportFailure = $null
$transcriptStarted = $false
$executionStateContinuous = [Convert]::ToUInt32('80000000', 16)
$executionStateRequired = [Convert]::ToUInt32('80000003', 16)

try {
    Start-Transcript -LiteralPath $transcriptPath -Append | Out-Null
    $transcriptStarted = $true
    [U29PowerRequest]::SetThreadExecutionState($executionStateRequired) | Out-Null
    Set-OfflineStatus 'RUNNING' 'U29 is running locally. Do not use the PC: the Player window must remain foreground.'

    Write-Output 'U29 OFFLINE VALIDATION'
    Write-Output "Run directory: $RunDirectory"
    Write-Output 'No Codex/API calls are used by this process, so the long run consumes no model-token allowance.'
    Write-Output 'The script prevents sleep and re-asserts the visible Player foreground during measurements.'
    Write-Output 'Do not interact with the PC until STATUS.txt changes from RUNNING.'
    Write-Output 'Expected duration: roughly 90-120 minutes on the current machine.'

    if ($PerformanceOnly) {
        if (-not (Test-Path -LiteralPath $playerPath)) {
            throw "-PerformanceOnly requires an existing Player build: $playerPath"
        }
        Set-OfflineStatus 'RUNNING' 'Performance-only matrix is running locally.'
        & $performanceScript -PlayerPath $playerPath -OutputDirectory $performanceDirectory `
            -ProcessTimeoutSeconds $PlayerTimeoutSeconds -ContinueOnFailure
    } else {
        Set-OfflineStatus 'RUNNING' 'Compile, tests, GPU validation and build are running; performance matrix follows.'
        & $verifyScript -ProjectPath $ProjectPath -EditorPath $EditorPath `
            -StepTimeoutSeconds $StepTimeoutSeconds -FullRegression -GpuValidation -BuildPlayer `
            -RunPerformanceGates -PerformanceOutputDirectory $performanceDirectory `
            -PerformanceProcessTimeoutSeconds $PlayerTimeoutSeconds
    }
} catch {
    $runFailed = $true
    $failureMessage = $_.Exception.Message
    Write-Error -ErrorAction Continue "U29 offline run failed: $failureMessage"
} finally {
    try {
        if (-not (Test-Path -LiteralPath $performanceDirectory)) {
            New-Item -ItemType Directory -Path $performanceDirectory -Force | Out-Null
        }
        $verificationDirectory = if ($PerformanceOnly) { $null } else { Join-Path $ProjectPath 'Logs' }
        & $reportScript -ResultsDirectory $performanceDirectory -OutputPath $reportPath `
            -VerificationLogDirectory $verificationDirectory
    } catch {
        $reportFailure = $_.Exception.Message
        Write-Warning "Diagnostic report verdict/error: $reportFailure"
    }

    [U29PowerRequest]::SetThreadExecutionState($executionStateContinuous) | Out-Null
    $finalState = if (-not $runFailed -and $null -eq $reportFailure) { 'PASS' } else { 'FAIL' }
    $finalMessage = if ($finalState -eq 'PASS') {
        'All verification and performance gates passed. Review the diagnostic report.'
    } else {
        (@($failureMessage, $reportFailure) |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Select-Object -Unique) -join ' | '
    }
    Set-OfflineStatus $finalState $finalMessage
    Write-Output "Final state: $finalState"
    Write-Output "Status: $statusPath"
    Write-Output "Report: $reportPath"
    if ($transcriptStarted) { Stop-Transcript | Out-Null }
}

if ($runFailed -or $null -ne $reportFailure) { exit 29 }
exit 0
