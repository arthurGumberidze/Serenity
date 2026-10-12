[CmdletBinding()]
param(
    [int]$Seed = 20261012,
    [int]$WallClockHours = 8,
    [int]$CheckpointIntervalMinutes = 5,
    [Parameter(Mandatory = $true)]
    [string]$OutputFolder,
    [string]$ResumeFromCheckpoint,
    [string]$ProjectPath,
    [string]$PlayerPath,
    [switch]$TestMode,
    [int]$TestWallClockSeconds = 30,
    [int]$TestSegmentSeconds = 10,
    [int]$TestWatchdogSeconds = 4,
    [ValidateSet('None', 'Invariant', 'Exception', 'Watchdog')]
    [string]$InjectFailure = 'None'
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Split-Path -Path $PSScriptRoot -Parent
}
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$OutputFolder = [IO.Path]::GetFullPath($OutputFolder)
$projectPrefix = $ProjectPath.TrimEnd('\') + '\'
if ($OutputFolder.Equals($ProjectPath, [StringComparison]::OrdinalIgnoreCase) -or
    $OutputFolder.StartsWith($projectPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'U30 output must be outside the Git worktree.'
}
if ($CheckpointIntervalMinutes -lt 1) { throw 'CheckpointIntervalMinutes must be at least 1.' }
if (-not $TestMode -and $WallClockHours -lt 8) { throw 'The official U30 gate requires WallClockHours >= 8.' }
if ($TestMode -and ($TestWallClockSeconds -lt 6 -or $TestSegmentSeconds -lt 2)) {
    throw 'U30 test mode requires at least 6 wall seconds and 2-second segments.'
}
if ([string]::IsNullOrWhiteSpace($PlayerPath)) {
    $PlayerPath = Join-Path $ProjectPath 'Builds\Windows\Serenity.exe'
}
$PlayerPath = (Resolve-Path -LiteralPath $PlayerPath).Path
if ([string]::IsNullOrWhiteSpace($ResumeFromCheckpoint)) { $ResumeFromCheckpoint = $null }
elseif (-not [IO.Path]::IsPathRooted($ResumeFromCheckpoint)) { throw 'ResumeFromCheckpoint must be absolute.' }
else { $ResumeFromCheckpoint = (Resolve-Path -LiteralPath $ResumeFromCheckpoint).Path }

# Keep the system awake for the lifetime of this PowerShell process without changing the
# user's persistent power plan. The display is intentionally allowed to turn off.
if (-not ('U30NativePower' -as [type])) {
    Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
public static class U30NativePower
{
    [DllImport("kernel32.dll")]
    public static extern uint SetThreadExecutionState(uint flags);
}
'@
}
$u30Continuous = [uint32]2147483648
$u30SystemRequired = [uint32]1
if ([U30NativePower]::SetThreadExecutionState($u30Continuous -bor $u30SystemRequired) -eq 0) {
    throw 'Windows rejected the U30 system-awake request.'
}

if (Test-Path -LiteralPath $OutputFolder) {
    if ((Get-ChildItem -LiteralPath $OutputFolder -Force | Measure-Object).Count -ne 0) {
        throw "U30 output folder is not empty: $OutputFolder"
    }
} else {
    New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null
}
$segmentsFolder = Join-Path $OutputFolder 'segments'
$checkpointRoot = Join-Path $OutputFolder 'checkpoints'
$incidentJournal = Join-Path $OutputFolder 'incidents.jsonl'
$segmentManifest = Join-Path $OutputFolder 'segment-manifest.jsonl'
$resultPath = Join-Path $OutputFolder 'soak-result.json'
New-Item -ItemType Directory -Path $segmentsFolder -Force | Out-Null
New-Item -ItemType Directory -Path $checkpointRoot -Force | Out-Null
[IO.File]::WriteAllText($incidentJournal, '', [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($segmentManifest, '', [Text.UTF8Encoding]::new($false))

$requestedSeconds64 = if ($TestMode) { [long]$TestWallClockSeconds } else { [long]$WallClockHours * 3600L }
if ($requestedSeconds64 -gt [int]::MaxValue) { throw 'Requested soak duration is too large.' }
$requestedSeconds = [int]$requestedSeconds64
$plannedSegmentSeconds = if ($TestMode) { $TestSegmentSeconds } else { 1800 }
$checkpointSeconds64 = if ($TestMode) { 2L } else { [long]$CheckpointIntervalMinutes * 60L }
if ($checkpointSeconds64 -gt [int]::MaxValue) { throw 'Checkpoint interval is too large.' }
$checkpointSeconds = [int]$checkpointSeconds64
$watchdogSeconds = if ($TestMode) { $TestWatchdogSeconds } else { [Math]::Max(30, $checkpointSeconds * 2) }
$startedUtc = [DateTime]::UtcNow
$timer = [Diagnostics.Stopwatch]::StartNew()
$segmentIndex = 0
$criticalErrors = 0
$invariantFailures = 0
$watchdogFailures = 0
$recoverableErrors = 0
$playerRestarts = 0
$segmentsCompleted = 0
$productiveSeconds = 0.0
$performanceSamples = [long]0
$memoryRunaway = $false
$resumeVerified = $false
$latestCheckpoint = $ResumeFromCheckpoint
$finalStateHash = ''
$firstFailureUtc = ''
$lastFailureUtc = ''
$worstP95 = 0.0
$maximumFrame = 0.0
$coverage = [ordered]@{
    production_steps = [long]0; research_steps = [long]0; dynasty_checks = [long]0
    health_steps = [long]0; weather_days = [long]0; military_checks = [long]0
    physical_battle_steps = [long]0; abstract_battle_steps = [long]0; fire_steps = [long]0
    capture_attempts = [long]0; global_travel_steps = [long]0; strategic_ai_steps = [long]0
    event_steps = [long]0
}
$injectionUsed = $false
$immediateStop = $false
$consecutiveNoProgressSegments = 0

function Get-U30Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Write-U30JsonLine([string]$Path, [object]$Value) {
    $line = $Value | ConvertTo-Json -Compress -Depth 16
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($line + "`n")
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Append, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
}

function Register-U30Failure([string]$Kind, [string]$Message, [int]$Segment, [string]$Checkpoint) {
    $now = [DateTime]::UtcNow.ToString('O')
    if ([string]::IsNullOrWhiteSpace($script:firstFailureUtc)) { $script:firstFailureUtc = $now }
    $script:lastFailureUtc = $now
    $record = [ordered]@{
        schema_version = 1; utc = $now; kind = $Kind; critical = $true; segment_index = $Segment
        message = $Message; checkpoint = [string]$Checkpoint
    }
    Write-U30JsonLine $incidentJournal $record
    $bundle = Join-Path $OutputFolder 'first-failure'
    if (-not (Test-Path -LiteralPath $bundle)) {
        New-Item -ItemType Directory -Path $bundle -Force | Out-Null
        [IO.File]::WriteAllText((Join-Path $bundle 'failure.json'),
            ($record | ConvertTo-Json -Depth 16), [Text.UTF8Encoding]::new($false))
        $repro = "& '$PSScriptRoot\Run-Soak-U30.ps1' -Seed $Seed -WallClockHours 8 " +
            "-CheckpointIntervalMinutes $CheckpointIntervalMinutes -OutputFolder '<absolute-external-path>'"
        [IO.File]::WriteAllText((Join-Path $bundle 'repro.ps1'), $repro + "`r`n",
            [Text.UTF8Encoding]::new($false))
    }
}

function Get-U30VerifiedCheckpoint {
    $metadataFiles = @(Get-ChildItem -LiteralPath $checkpointRoot -Filter 'checkpoint.json' -Recurse -File |
        Sort-Object { [long]((Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json).sequence) } -Descending)
    foreach ($metadataFile in $metadataFiles) {
        try {
            $metadata = Get-Content -LiteralPath $metadataFile.FullName -Raw | ConvertFrom-Json
            $snapshot = [string]$metadata.snapshot_file
            $tiers = [string]$metadata.tiers_file
            if (-not $metadata.independently_verified -or -not (Test-Path -LiteralPath $snapshot) -or
                -not (Test-Path -LiteralPath $tiers)) { throw 'Checkpoint bundle is incomplete.' }
            if ((Get-U30Sha256 $snapshot) -ne ([string]$metadata.snapshot_sha256).ToLowerInvariant() -or
                (Get-U30Sha256 $tiers) -ne ([string]$metadata.tiers_sha256).ToLowerInvariant()) {
                throw 'Checkpoint SHA-256 mismatch.'
            }
            return $snapshot
        } catch {
            $script:criticalErrors++
            $script:recoverableErrors++
            Register-U30Failure 'checkpoint_validation' $_.Exception.Message $segmentIndex $metadataFile.FullName
        }
    }
    return $null
}

function Append-U30PlayerIncidents([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return }
    $content = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8)
    if ([string]::IsNullOrWhiteSpace($content)) { return }
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($content.TrimEnd() + "`n")
    $stream = [IO.File]::Open($incidentJournal, [IO.FileMode]::Append, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
}

while ($productiveSeconds -lt $requestedSeconds -and -not $immediateStop) {
    $segmentIndex++
    if ($segmentIndex -gt 1) { $playerRestarts++ }
    $remaining = $requestedSeconds - $productiveSeconds
    $segmentSeconds = [Math]::Max(1, [Math]::Min($plannedSegmentSeconds, [Math]::Ceiling($remaining)))
    $segmentDirectory = Join-Path $segmentsFolder ('segment-{0:D4}' -f $segmentIndex)
    New-Item -ItemType Directory -Path $segmentDirectory -Force | Out-Null
    $stdoutPath = Join-Path $segmentDirectory 'player.stdout.log'
    $stderrPath = Join-Path $segmentDirectory 'player.stderr.log'
    $playerLog = Join-Path $segmentDirectory 'player.log'
    $heartbeatPath = Join-Path $segmentDirectory 'heartbeats.jsonl'
    $segmentResultPath = Join-Path $segmentDirectory 'segment-result.json'
    $segmentStarted = [DateTime]::UtcNow

    $arguments = @(
        '-batchmode', '-logFile', ('"' + $playerLog + '"'), '-u30Soak',
        '-u30Seed', $Seed, '-u30SegmentIndex', $segmentIndex,
        '-u30SegmentSeconds', $segmentSeconds.ToString([Globalization.CultureInfo]::InvariantCulture),
        '-u30CheckpointIntervalSeconds', $checkpointSeconds,
        '-u30OutputDirectory', ('"' + $segmentDirectory + '"'),
        '-u30CheckpointRoot', ('"' + $checkpointRoot + '"'), '-u30StepsPerSecond', 4
    )
    if (-not [string]::IsNullOrWhiteSpace($latestCheckpoint)) {
        $arguments += @('-u30ResumeFromCheckpoint', ('"' + $latestCheckpoint + '"'))
    }
    if (-not $injectionUsed -and $InjectFailure -ne 'None') {
        $injectionUsed = $true
        $arguments += @('-u30InjectionAfterSeconds', 2)
        if ($InjectFailure -eq 'Invariant') { $arguments += '-u30InjectInvariantFailure' }
        elseif ($InjectFailure -eq 'Exception') { $arguments += '-u30InjectRecoverableException' }
        elseif ($InjectFailure -eq 'Watchdog') { $arguments += '-u30InjectWatchdogStall' }
    }

    $process = Start-Process -FilePath $PlayerPath -ArgumentList $arguments -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
    $lastHeartbeatUtc = [DateTime]::UtcNow
    $lastHeartbeatLength = -1L
    $watchdogTriggered = $false
    while (-not $process.HasExited) {
        if (Test-Path -LiteralPath $heartbeatPath) {
            $item = Get-Item -LiteralPath $heartbeatPath
            if ($item.Length -ne $lastHeartbeatLength) {
                $lastHeartbeatLength = $item.Length
                $lastHeartbeatUtc = [DateTime]::UtcNow
            }
        }
        if (([DateTime]::UtcNow - $lastHeartbeatUtc).TotalSeconds -gt $watchdogSeconds) {
            $watchdogTriggered = $true
            Stop-Process -Id $process.Id -Force
            $process.WaitForExit()
            $watchdogFailures++
            $criticalErrors++
            $recoverableErrors++
            Register-U30Failure 'watchdog' "No Player heartbeat for $watchdogSeconds seconds." $segmentIndex $latestCheckpoint
            break
        }
        Start-Sleep -Milliseconds 500
        $process.Refresh()
    }
    $process.WaitForExit()
    $exitCode = $process.ExitCode
    $segmentEnded = [DateTime]::UtcNow
    $segmentResult = $null
    if (Test-Path -LiteralPath $segmentResultPath) {
        try { $segmentResult = Get-Content -LiteralPath $segmentResultPath -Raw | ConvertFrom-Json }
        catch {
            $criticalErrors++
            $recoverableErrors++
            Register-U30Failure 'segment_result_parse' $_.Exception.Message $segmentIndex $latestCheckpoint
        }
    } elseif (-not $watchdogTriggered) {
        $criticalErrors++
        $recoverableErrors++
        Register-U30Failure 'player_crash' "Player exited $exitCode without segment-result.json." $segmentIndex $latestCheckpoint
    }

    if ($segmentResult) {
        $segmentsCompleted++
        $criticalErrors += [int]$segmentResult.critical_errors
        $invariantFailures += [int]$segmentResult.invariant_failures
        $recoverableErrors += [int]$segmentResult.recoverable_errors
        $memoryRunaway = $memoryRunaway -or [bool]$segmentResult.memory_runaway_detected
        $resumeVerified = $resumeVerified -or [bool]$segmentResult.checkpoint_resume_verified
        $productiveSeconds += [double]$segmentResult.productive_simulation_seconds
        if ([double]$segmentResult.productive_simulation_seconds -gt 0) { $consecutiveNoProgressSegments = 0 }
        else { $consecutiveNoProgressSegments++ }
        $performanceSamples += [long]$segmentResult.performance_samples
        if (-not [string]::IsNullOrWhiteSpace([string]$segmentResult.final_state_hash)) {
            $finalStateHash = [string]$segmentResult.final_state_hash
        }
        if ($segmentResult.performance) {
            $worstP95 = [Math]::Max($worstP95, [double]$segmentResult.performance.p95_ms)
            $maximumFrame = [Math]::Max($maximumFrame, [double]$segmentResult.performance.max_ms)
        }
        if ($segmentResult.coverage) {
            foreach ($name in @($coverage.Keys)) {
                $coverage[$name] = [long]$coverage[$name] + [long]$segmentResult.coverage.$name
            }
        }
        Append-U30PlayerIncidents ([string]$segmentResult.incident_journal)
        if (-not [bool]$segmentResult.passed -and [int]$segmentResult.critical_errors -eq 0 -and
            [int]$segmentResult.invariant_failures -eq 0 -and -not [bool]$segmentResult.memory_runaway_detected) {
            $criticalErrors++
            Register-U30Failure 'segment_gate' ([string]$segmentResult.summary) $segmentIndex ([string]$segmentResult.latest_checkpoint)
        }
    }
    else {
        $consecutiveNoProgressSegments++
    }

    $verified = Get-U30VerifiedCheckpoint
    if (-not [string]::IsNullOrWhiteSpace($verified)) { $latestCheckpoint = $verified }
    elseif ([string]::IsNullOrWhiteSpace($latestCheckpoint)) {
        $criticalErrors++
        Register-U30Failure 'no_recovery_checkpoint' 'No verified checkpoint or fresh-start recovery path is available.' $segmentIndex ''
        $immediateStop = $true
    }

    Write-U30JsonLine $segmentManifest ([ordered]@{
        schema_version = 1; segment_index = $segmentIndex; started_utc = $segmentStarted.ToString('O')
        completed_utc = $segmentEnded.ToString('O'); start_checkpoint = [string]$ResumeFromCheckpoint
        end_checkpoint = [string]$latestCheckpoint; pid = $process.Id; exit_code = $exitCode
        end_reason = if ($watchdogTriggered) { 'watchdog_restart' } elseif ($segmentResult) { 'segment_complete' } else { 'player_exit' }
        sample_count = if ($segmentResult) { [long]$segmentResult.performance_samples } else { 0 }
        p50_ms = if ($segmentResult) { [double]$segmentResult.performance.p50_ms } else { 0 }
        p95_ms = if ($segmentResult) { [double]$segmentResult.performance.p95_ms } else { 0 }
        p99_ms = if ($segmentResult) { [double]$segmentResult.performance.p99_ms } else { 0 }
        max_ms = if ($segmentResult) { [double]$segmentResult.performance.max_ms } else { 0 }
    })
    $ResumeFromCheckpoint = $latestCheckpoint

    if ($watchdogTriggered -or (-not $segmentResult) -or $exitCode -ne 0) {
        $backoff = [Math]::Min(30, [Math]::Pow(2, [Math]::Min(4, $recoverableErrors)))
        if ($consecutiveNoProgressSegments -ge 5) {
            $criticalErrors++
            Register-U30Failure 'recovery_exhausted' 'Five consecutive segments made no productive simulation progress.' $segmentIndex $latestCheckpoint
            $immediateStop = $true
        } elseif ($productiveSeconds -lt $requestedSeconds) {
            Start-Sleep -Seconds $backoff
        }
    }
}

$timer.Stop()
$completedUtc = [DateTime]::UtcNow
$actualSeconds = [int][Math]::Floor($timer.Elapsed.TotalSeconds)
$durationPassed = $actualSeconds -ge $requestedSeconds
$coveragePassed = $coverage.production_steps -gt 0 -and $coverage.research_steps -gt 0 -and
    $coverage.dynasty_checks -gt 0 -and $coverage.health_steps -gt 0 -and $coverage.weather_days -gt 0 -and
    $coverage.military_checks -gt 0 -and ($coverage.physical_battle_steps + $coverage.abstract_battle_steps) -gt 0 -and
    $coverage.fire_steps -gt 0 -and $coverage.capture_attempts -gt 0 -and $coverage.global_travel_steps -gt 0 -and
    $coverage.strategic_ai_steps -gt 0 -and $coverage.event_steps -gt 0
$productivePassed = $productiveSeconds -ge $requestedSeconds
$passed = $durationPassed -and $productivePassed -and -not $immediateStop -and $criticalErrors -eq 0 -and
    $invariantFailures -eq 0 -and $watchdogFailures -eq 0 -and -not $memoryRunaway -and
    $resumeVerified -and -not [string]::IsNullOrWhiteSpace($finalStateHash) -and
    $segmentsCompleted -gt 0 -and $performanceSamples -gt 0 -and $coveragePassed
$summary = if ($passed) {
    "U30 soak completed; worst segment p95 ${worstP95} ms, maximum ${maximumFrame} ms."
} else {
    "U30 soak failed closed; inspect the incident journal and segment manifest."
}
$result = [ordered]@{
    passed = $passed; seed = $Seed; requested_wall_clock_seconds = [int]$requestedSeconds
    actual_wall_clock_seconds = $actualSeconds; started_utc = $startedUtc.ToString('O')
    completed_utc = $completedUtc.ToString('O'); critical_errors = $criticalErrors
    invariant_failures = $invariantFailures; watchdog_failures = $watchdogFailures
    memory_runaway_detected = $memoryRunaway; checkpoint_resume_verified = $resumeVerified
    final_state_hash = $finalStateHash; summary = $summary; recoverable_errors = $recoverableErrors
    player_restarts = $playerRestarts; segments_completed = $segmentsCompleted
    productive_simulation_seconds = [Math]::Round($productiveSeconds, 3)
    performance_samples = $performanceSamples; coverage = $coverage; first_failure_utc = $firstFailureUtc
    last_failure_utc = $lastFailureUtc; incident_journal_path = $incidentJournal
    incident_journal_sha256 = Get-U30Sha256 $incidentJournal; segment_manifest_path = $segmentManifest
    segment_manifest_sha256 = Get-U30Sha256 $segmentManifest; latest_checkpoint = [string]$latestCheckpoint
    worst_segment_p95_ms = [Math]::Round($worstP95, 6); maximum_frame_ms = [Math]::Round($maximumFrame, 6)
}
$temporaryResult = $resultPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
[IO.File]::WriteAllText($temporaryResult, ($result | ConvertTo-Json -Depth 16), [Text.UTF8Encoding]::new($false))
if (Test-Path -LiteralPath $resultPath) { [IO.File]::Replace($temporaryResult, $resultPath, $null) }
else { [IO.File]::Move($temporaryResult, $resultPath) }

[void][U30NativePower]::SetThreadExecutionState($u30Continuous)
if ($passed) { exit 0 }
exit 1
