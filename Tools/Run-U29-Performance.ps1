param(
    [Parameter(Mandatory = $true)]
    [string]$PlayerPath,
    [string]$OutputDirectory,
    [int[]]$Populations = @(100, 300, 500, 1000),
    [int]$Repeats = 3,
    [double]$WarmupSeconds = 60,
    [double]$MeasurementSeconds = 300,
    [int]$ProcessTimeoutSeconds = 900,
    [long]$Seed = 20261006,
    [string]$BaseCommit,
    [switch]$Smoke,
    [switch]$RecordOnly,
    [switch]$ContinueOnFailure,
    [switch]$DetailedDiagnostics,
    [switch]$CaptureTrace,
    [bool]$MaintainPlayerForeground = $true
)

$ErrorActionPreference = 'Stop'
if ($CaptureTrace -and (-not $Smoke -or -not $DetailedDiagnostics)) { throw 'Native trace is diagnostic-only; use Smoke and DetailedDiagnostics.' }
$PlayerPath = (Resolve-Path -LiteralPath $PlayerPath).Path
$repo = (Resolve-Path -LiteralPath (Split-Path $PSScriptRoot -Parent)).Path
if ([string]::IsNullOrWhiteSpace($BaseCommit)) {
    $BaseCommit = (& git -C $repo rev-parse --short=12 HEAD).Trim()
}
if ($Smoke) {
    if (-not $PSBoundParameters.ContainsKey('Repeats')) { $Repeats = 1 }
    if (-not $PSBoundParameters.ContainsKey('WarmupSeconds')) { $WarmupSeconds = 2 }
    if (-not $PSBoundParameters.ContainsKey('MeasurementSeconds')) { $MeasurementSeconds = 5 }
    if (-not $PSBoundParameters.ContainsKey('Populations')) { $Populations = @(100) }
}
if (-not $Smoke -and ($Repeats -lt 3 -or $WarmupSeconds -lt 60 -or $MeasurementSeconds -lt 300)) {
    throw 'The final U29 matrix requires at least 3 repeats, 60 seconds warm-up and 300 seconds measurement.'
}
foreach ($population in $Populations) {
    if ($population -notin @(100, 300, 500, 1000)) { throw "Unsupported U29 population: $population" }
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
    $OutputDirectory = Join-Path $repo "Logs/U29/$stamp"
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) {
    if ((Get-ChildItem -LiteralPath $OutputDirectory -Force | Measure-Object).Count -ne 0) {
        throw "U29 output directory is not empty: $OutputDirectory"
    }
} else {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}

function Get-TierMix([int]$Population) {
    return [pscustomobject]@{ tier1 = [int]($Population / 4); tier2 = [int]($Population / 2); tier3 = [int]($Population / 4) }
}

function Get-FileSha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Test-MonotonicGrowth([long[]]$Values, [long]$Limit) {
    if ($Values.Count -lt 3) { return $false }
    $minimumStep = [Math]::Max(1MB, [long]($Limit / 8))
    for ($i = 1; $i -lt $Values.Count; $i++) {
        if (($Values[$i] - $Values[$i - 1]) -le $minimumStep) { return $false }
    }
    return ($Values[-1] - $Values[0]) -gt $Limit
}

if ($MaintainPlayerForeground -and -not ('U29ForegroundWindow' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class U29ForegroundWindow
{
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
}
'@
}

function Wait-U29Player([Diagnostics.Process]$Process, [int]$TimeoutSeconds) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $nextFocus = [DateTime]::MinValue
    while (-not $Process.HasExited) {
        if ([DateTime]::UtcNow -ge $deadline) {
            $Process.Refresh()
            Stop-Process -Id $Process.Id -Force
            throw "U29 Player timed out; only launched PID $($Process.Id) was terminated."
        }
        if ($MaintainPlayerForeground -and [DateTime]::UtcNow -ge $nextFocus) {
            $Process.Refresh()
            if ($Process.MainWindowHandle -ne [IntPtr]::Zero) {
                [U29ForegroundWindow]::ShowWindowAsync($Process.MainWindowHandle, 9) | Out-Null
                [U29ForegroundWindow]::SetForegroundWindow($Process.MainWindowHandle) | Out-Null
            }
            $nextFocus = [DateTime]::UtcNow.AddSeconds(2)
        }
        Start-Sleep -Milliseconds 500
    }
    $Process.WaitForExit()
}

function Assert-RepeatResult([pscustomobject]$Result, [string]$RepeatDirectory, [int]$Population,
    [int]$Repeat, [pscustomobject]$Mix, [bool]$Mandatory, [bool]$AllowPerformanceFailure,
    [datetime]$StartedUtc) {
    if (-not $Result) { throw 'Missing U29 result.' }
    if ($Result.schemaVersion -ne 1 -or $Result.harnessVersion -ne 1) { throw 'Unsupported U29 result version.' }
    if ($Result.population -ne $Population -or $Result.repeatIndex -ne $Repeat -or
        $Result.requestedTier1 -ne $Mix.tier1 -or $Result.requestedTier2 -ne $Mix.tier2 -or
        $Result.requestedTier3 -ne $Mix.tier3) { throw 'U29 result config/tier mismatch.' }
    if ($Result.baseCommit -ne $BaseCommit) { throw 'U29 result base commit mismatch.' }
    if ([string]::IsNullOrWhiteSpace($Result.buildGuid)) { throw 'U29 result build GUID is missing.' }
    if ($Mandatory -and ($Result.batchMode -or -not $Result.graphicsDeviceActive -or -not $Result.uiEnabled -or
        -not $Result.cameraActive)) { throw 'Mandatory U29 result lacks graphics/UI/camera evidence.' }
    if ($Mandatory -and ($Result.warmupSeconds -lt 60 -or $Result.measurementSeconds -lt 300 -or
        $Result.repeatCount -lt 3)) { throw 'Mandatory U29 duration/repeat evidence is too short.' }
    if ($Result.countsEnd.canonicalCharacters -ne $Population -or
        $Result.countsEnd.distinctStableIds -ne $Population -or $Result.countsEnd.tier1 -ne $Mix.tier1 -or
        $Result.countsEnd.tier2 -ne $Mix.tier2 -or $Result.countsEnd.tier3 -ne $Mix.tier3 -or
        $Result.countsEnd.tier1Presenters -ne $Mix.tier1 -or $Result.countsEnd.tier2Entities -ne $Mix.tier2 -or
        $Result.countsEnd.tier3Records -ne $Mix.tier3) { throw 'U29 actual population/tier invariant failed.' }
    if (-not $Result.save.exactIdentityTierRoundTrip -or $Result.save.restoredCharacters -ne $Population) {
        throw 'U29 save observation did not preserve exact population/tier identity.'
    }
    $rawPath = Join-Path $RepeatDirectory $Result.rawFramesFile
    if (-not (Test-Path -LiteralPath $rawPath)) { throw 'U29 raw frames file is missing.' }
    if ((Get-Item -LiteralPath $rawPath).LastWriteTimeUtc -lt $StartedUtc.AddSeconds(-2)) { throw 'Stale U29 raw frames file.' }
    if ((Get-FileSha256 $rawPath) -ne $Result.rawFramesSha256) { throw 'U29 raw frames hash mismatch.' }
    $visualPath = Join-Path $RepeatDirectory $Result.visualEvidenceFile
    if (-not (Test-Path -LiteralPath $visualPath) -or
        (Get-FileSha256 $visualPath) -ne $Result.visualEvidenceSha256) {
        throw 'U29 visual evidence is missing or its hash mismatches.'
    }
    $rawLines = (Get-Content -LiteralPath $rawPath | Measure-Object -Line).Lines - 1
    if ($rawLines -ne $Result.frame.sampleCount) { throw 'U29 raw frame/sample count mismatch.' }
    if ($Result.frame.p95Ms -le 0 -or [double]::IsNaN($Result.frame.p95Ms) -or
        [double]::IsInfinity($Result.frame.p95Ms)) { throw 'U29 frame statistics are invalid.' }
    if ($Mandatory -and -not $AllowPerformanceFailure -and $Result.frame.p95Ms -gt 33.33) { throw "U29 p95 gate failed: $($Result.frame.p95Ms) ms" }
    if ($Mandatory -and -not $AllowPerformanceFailure -and ($Result.memory.runawayManagedGrowth -or $Result.memory.runawayWorkingSetGrowth -or
        $Result.memory.recurringMajorGcStorm)) { throw 'U29 memory/GC gate failed.' }
    if (-not $AllowPerformanceFailure -and (-not $Result.passed -or $Result.failureReasons.Count -ne 0)) {
        throw 'U29 Player result reported failure.'
    }
}

$allPopulationSummaries = @()
$runnerFailures = [Collections.Generic.List[object]]::new()
foreach ($population in $Populations) {
    $mix = Get-TierMix $population
    $configId = "u29-v1-p$population-seed$Seed"
    $populationDirectory = Join-Path $OutputDirectory "population-$population"
    New-Item -ItemType Directory -Path $populationDirectory -Force | Out-Null
    $results = @()
    for ($repeat = 1; $repeat -le $Repeats; $repeat++) {
        try {
        $repeatDirectory = Join-Path $populationDirectory "repeat-$repeat"
        New-Item -ItemType Directory -Path $repeatDirectory -Force | Out-Null
        $playerLog = Join-Path $repeatDirectory 'player.log'
        $arguments = @(
            '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '900',
            '-logFile', $playerLog,
            '-u29Benchmark', '-u29Population', "$population",
            '-u29Tier1', "$($mix.tier1)", '-u29Tier2', "$($mix.tier2)", '-u29Tier3', "$($mix.tier3)",
            '-u29Repeat', "$repeat", '-u29RepeatCount', "$Repeats",
            '-u29WarmupSeconds', $WarmupSeconds.ToString([Globalization.CultureInfo]::InvariantCulture),
            '-u29MeasurementSeconds', $MeasurementSeconds.ToString([Globalization.CultureInfo]::InvariantCulture),
            '-u29Seed', "$Seed", '-u29ConfigId', $configId, '-u29BaseCommit', $BaseCommit,
            '-u29OutputDirectory', $repeatDirectory
        )
        if ($Smoke) { $arguments += '-u29Smoke' }
        if ($DetailedDiagnostics) { $arguments += '-u29Diagnostics' }
        if ($CaptureTrace) { $arguments += '-u29Trace' }
        $startedUtc = [DateTime]::UtcNow
        $process = Start-Process -FilePath $PlayerPath -ArgumentList $arguments -PassThru
        Write-Output "U29 Player population=$population repeat=$repeat PID=$($process.Id)"
        Wait-U29Player $process $ProcessTimeoutSeconds
        if ($process.ExitCode -ne 0 -and -not $RecordOnly -and -not $ContinueOnFailure) {
            if (Test-Path -LiteralPath $playerLog) { Get-Content -LiteralPath $playerLog -Tail 160 }
            throw "U29 Player exited $($process.ExitCode) for population=$population repeat=$repeat."
        }
        $summaryPath = Join-Path $repeatDirectory 'summary.json'
        if (-not (Test-Path -LiteralPath $summaryPath)) { throw 'U29 Player did not produce summary.json.' }
        if ((Get-Item -LiteralPath $summaryPath).LastWriteTimeUtc -lt $startedUtc.AddSeconds(-2)) { throw 'Stale U29 summary.' }
        $result = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
        $results += $result
        Assert-RepeatResult $result $repeatDirectory $population $repeat $mix (-not $Smoke) `
            ([bool]($RecordOnly -or $ContinueOnFailure)) $startedUtc
        $fatal = Select-String -LiteralPath $playerLog -Pattern 'error CS\d+|Scripts have compiler errors|Unhandled Exception|NullReferenceException|AssertionException|There are 2 audio listeners|Jobs safety|allocator.*leak|Native Collection.*leak|device lost|Player crash|timeout' -CaseSensitive:$false
        if ($fatal) { throw "U29 Player log contains a fatal diagnostic: $($fatal[0].Line)" }
        Write-Output ("U29 population={0} repeat={1}: p50={2:N3} p95={3:N3} p99={4:N3} max={5:N3} ms" -f
            $population, $repeat, $result.frame.p50Ms, $result.frame.p95Ms, $result.frame.p99Ms, $result.frame.maximumMs)
        } catch {
            $runnerFailures.Add([pscustomobject]@{
                population = $population
                repeatIndex = $repeat
                message = $_.Exception.Message
            })
            Write-Warning "U29 population=$population repeat=$repeat failed validation: $($_.Exception.Message)"
            if (-not $ContinueOnFailure) { throw }
        }
    }

    if (-not $Smoke) {
        if (($results.repeatIndex | Select-Object -Unique).Count -ne $Repeats) {
            $runnerFailures.Add([pscustomobject]@{ population = $population; repeatIndex = 0; message = 'Duplicate/missing repeat index.' })
            if (-not $ContinueOnFailure) { throw 'Duplicate/missing repeat index.' }
        }
        if ($results.Count -gt 0 -and (($results.buildGuid | Select-Object -Unique).Count -ne 1 -or
            ($results.hardware.processor | Select-Object -Unique).Count -ne 1 -or
            ($results.hardware.graphicsDevice | Select-Object -Unique).Count -ne 1 -or
            ($results.hardware.operatingSystem | Select-Object -Unique).Count -ne 1)) {
            $runnerFailures.Add([pscustomobject]@{ population = $population; repeatIndex = 0; message = 'U29 repeat hardware/build metadata mismatch.' })
            if (-not $ContinueOnFailure) { throw 'U29 repeat hardware/build metadata mismatch.' }
        }
    }
    $managedEnds = @($results | ForEach-Object { [long]$_.memory.managedHeapEndBytes })
    $workingEnds = @($results | ForEach-Object { [long]$_.memory.workingSetEndBytes })
    $memoryTrendPassed = -not (Test-MonotonicGrowth $managedEnds 32MB) -and
        -not (Test-MonotonicGrowth $workingEnds 128MB)
    if (-not $memoryTrendPassed -and -not $RecordOnly -and -not $ContinueOnFailure) { throw "U29 cross-repeat memory trend failed for population $population." }
    $worst = $results | Sort-Object { [double]$_.frame.p95Ms } -Descending | Select-Object -First 1
    $populationFailures = @($runnerFailures | Where-Object { $_.population -eq $population })
    $populationSummary = [ordered]@{
        schemaVersion = 1
        harnessVersion = 1
        generatedUtc = [DateTime]::UtcNow.ToString('O')
        configId = $configId
        buildGuid = if ($null -ne $worst) { $worst.buildGuid } else { $null }
        population = $population
        tier1 = $mix.tier1
        tier2 = $mix.tier2
        tier3 = $mix.tier3
        repeats = $results
        completedRepeats = $results.Count
        worstRepeatIndex = if ($null -ne $worst) { $worst.repeatIndex } else { 0 }
        worstP95Ms = if ($null -ne $worst) { $worst.frame.p95Ms } else { 0 }
        memoryTrendPassed = $memoryTrendPassed
        passed = [bool]($results.Count -eq $Repeats -and
            ($results | Where-Object { -not $_.passed }).Count -eq 0 -and $memoryTrendPassed -and
            $populationFailures.Count -eq 0)
        failureReasons = @($results | ForEach-Object { $_.failureReasons } | Select-Object -Unique) +
            @($populationFailures | ForEach-Object { $_.message } | Select-Object -Unique)
    }
    $populationSummaryPath = Join-Path $populationDirectory 'aggregate.json'
    $populationSummary | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $populationSummaryPath -Encoding utf8
    $allPopulationSummaries += $populationSummary
}

$final = [ordered]@{
    schemaVersion = 1
    harnessVersion = 1
    generatedUtc = [DateTime]::UtcNow.ToString('O')
    baseCommit = $BaseCommit
    smoke = [bool]$Smoke
    warmupSeconds = $WarmupSeconds
    measurementSeconds = $MeasurementSeconds
    repeatCount = $Repeats
    populations = $allPopulationSummaries
    runnerFailures = @($runnerFailures)
    passed = [bool](($allPopulationSummaries | Where-Object { -not $_.passed }).Count -eq 0 -and
        $runnerFailures.Count -eq 0)
}
$finalPath = Join-Path $OutputDirectory 'U29-final-summary.json'
$final | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $finalPath -Encoding utf8
Write-Output "U29 performance matrix completed: $finalPath"
$reportScript = Join-Path $PSScriptRoot 'Write-U29-PerformanceReport.ps1'
$reportFailure = $null
try {
    # A diagnostic capture must retain FAIL in the report without aborting its detailed analysis.
    # The mandatory matrix and all runner/artifact failures remain fail-closed.
    & $reportScript -ResultsDirectory $OutputDirectory -ExpectedPopulations $Populations -ExpectedRepeats $Repeats `
        -DoNotFailOnVerdict:([bool]($Smoke -and $DetailedDiagnostics))
} catch {
    $reportFailure = $_
    Write-Warning $_.Exception.Message
}
if (-not $final.passed -or $null -ne $reportFailure) {
    throw "U29 performance matrix failed. See $OutputDirectory\U29-DIAGNOSTIC-REPORT.md"
}
