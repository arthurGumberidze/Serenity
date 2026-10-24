#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$ProjectPath,
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [string]$RunDirectory,
    [string]$ProtectedSaveDirectory,
    [string]$RepairFrom,
    [ValidateSet('Tests','Performance','Protection')][string]$RepairStage = 'Tests',
    [ValidateRange(1, 7200)][int]$TestTimeoutSeconds = 1200,
    [ValidateRange(1, 14400)][int]$BuildTimeoutSeconds = 3600,
    [ValidateRange(1, 3600)][int]$PlayerTimeoutSeconds = 300,
    [ValidateRange(0, 180)][int]$BusyWaitMinutes = 30,
    [ValidateRange(0.01, 48)][double]$MaxHours = 10,
    [ValidateRange(1, 120)][int]$ProgressSeconds = 30,
    [ValidateRange(0, 2)][int]$InfrastructureRetries = 1,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2
Import-Module (Join-Path $PSScriptRoot 'WM15.Runner.psm1') -Force
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent }
$ProjectPath = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\', '/')
if ([string]::IsNullOrWhiteSpace($ProtectedSaveDirectory)) {
    $ProtectedSaveDirectory = if ([string]::IsNullOrWhiteSpace($env:SERENITY_SAVE_DIRECTORY)) {
        Join-Path $env:USERPROFILE 'AppData/LocalLow/Serenity/Serenity'
    } else { $env:SERENITY_SAVE_DIRECTORY }
}
$ProtectedSaveDirectory = [IO.Path]::GetFullPath($ProtectedSaveDirectory)
if (-not (Test-Path -LiteralPath $ProjectPath -PathType Container)) { throw "Project directory missing: $ProjectPath" }
if ([string]::IsNullOrWhiteSpace($RunDirectory)) {
    $RunDirectory = Join-Path $ProjectPath ('Artifacts/WM15/night-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6))
}
$RunDirectory = [IO.Path]::GetFullPath($RunDirectory)
if ((Test-Path -LiteralPath $RunDirectory) -and @(Get-ChildItem -LiteralPath $RunDirectory -Force).Count -gt 0) {
    throw "Use a new or empty results directory: $RunDirectory"
}
New-Item -ItemType Directory -Force -Path $RunDirectory | Out-Null
$results = [Collections.Generic.List[object]]::new()
$steps = [Collections.Generic.List[object]]::new()
$timer = [Diagnostics.Stopwatch]::StartNew()
$index = 0
$currentName = 'initializing'
$fatal = $null
$mutex = $null
$ownsMutex = $false
$powerRequested = $false
$protected = @()
$journal = Join-Path $RunDirectory 'steps.jsonl'
$utf8 = [Text.UTF8Encoding]::new($false)
$statePath = Join-Path $RunDirectory 'STATUS.json'
$reportPath = Join-Path $RunDirectory 'REPORT.md'
$playerPath = Join-Path $RunDirectory 'build/Serenity.exe'
$buildPassed = $false
$unityReady = $false
$sourceCommit = ''
$exitCode = 2
$cleanupIssues = [Collections.Generic.List[object]]::new()

function Write-ProgressLine([string]$Message) {
    $line = '[{0}] [{1}/{2}] {3}' -f (Get-Date -Format 'HH:mm:ss'), $index, $steps.Count, $Message
    Write-Host $line
    Add-Wm15Text (Join-Path $RunDirectory 'progress.log') ($line + [Environment]::NewLine)
}

function Save-Status([string]$State) {
    Write-Wm15Json $statePath ([ordered]@{
        state = $State; updatedUtc = [DateTime]::UtcNow.ToString('O'); current = $currentName
        completed = $results.Count; planned = $steps.Count; elapsedMinutes = [Math]::Round($timer.Elapsed.TotalMinutes, 1)
        failed = @($results | Where-Object { $_.status -in @('FAIL','TIMEOUT','ERROR') }).Count
        report = $reportPath; output = $RunDirectory
    })
}

function Save-Report([string]$State) {
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('# WM15 offline validation')
    $lines.Add('')
    $lines.Add("State: **$State**. Updated UTC: $([DateTime]::UtcNow.ToString('O')).")
    $lines.Add("Elapsed: $([Math]::Round($timer.Elapsed.TotalMinutes, 1)) minutes. Completed: $($results.Count)/$($steps.Count).")
    $lines.Add("Source commit: $sourceCommit")
    $lines.Add('')
    $lines.Add('| Check | Status | Seconds | Detail |')
    $lines.Add('| --- | --- | ---: | --- |')
    foreach ($result in $results) {
        $detail = ([string]$result.detail).Replace('|', '\|').Replace("`r", ' ').Replace("`n", ' ')
        $lines.Add("| $($result.name) | $($result.status) | $($result.seconds) | $detail |")
    }
    if ($null -ne $fatal) { $lines.Add(''); $lines.Add("Runner error: $fatal") }
    $lines.Add('')
    $lines.Add('## Review by a person')
    $lines.Add('')
    $lines.Add('- Confirm the tested world generation. The existing Player uses the active compact world; proposed V2 has dedicated WM tests and is not automatically activated by this runner.')
    $lines.Add('- Inspect the start, river crossings, four map edges, lakes, ridge, marsh, vegetation, POIs, and green/amber/red construction previews.')
    $lines.Add('- Review screenshots and performance reports. A desktop must remain unlocked and the GPU available for rendered performance gates.')
    $lines.Add('- User visual acceptance is PENDING. Script success alone does not mark WM15 DONE.')
    $lines.Add('')
    $lines.Add('Each step has its own folder with stdout/stderr, Unity log and XML/JSON where available. steps.jsonl is flushed after each completed step. STATUS.json is refreshed while work continues.')
    Write-Wm15Text $reportPath ($lines -join [Environment]::NewLine)
    Write-Wm15Json (Join-Path $RunDirectory 'results.json') ([ordered]@{
        state = $State; sourceCommit = $sourceCommit; manualAcceptance = 'PENDING'
        steps = @($results.ToArray()); runnerError = $fatal; cleanupIssues = @($cleanupIssues.ToArray())
    })
}

function Add-Step([string]$Name, [string]$Kind, [string]$Filter = '', [string]$Platform = '') {
    $steps.Add([pscustomobject]@{ Name = $Name; Kind = $Kind; Filter = $Filter; Platform = $Platform })
}

function Invoke-Child([string]$Executable, [string[]]$Arguments, [int]$Timeout, [string]$Prefix, [hashtable]$Environment = @{}, [bool]$Visible = $false) {
    $remaining = [Math]::Floor($MaxHours * 3600 - $timer.Elapsed.TotalSeconds)
    if ($remaining -le 0) { throw 'Overall time budget exhausted.' }
    $effectiveTimeout = [int][Math]::Min($Timeout, $remaining)
    $progress = {
        param($seconds, $pidValue)
        Write-ProgressLine ("RUN {0} +{1:mm\:ss} PID={2}" -f $currentName, [TimeSpan]::FromSeconds($seconds), $pidValue)
        Save-Status 'RUNNING'
    }
    $child = Invoke-Wm15Process -FilePath $Executable -Arguments $Arguments -WorkingDirectory $ProjectPath `
        -OutputPrefix $Prefix -TimeoutSeconds $effectiveTimeout -ProgressSeconds $ProgressSeconds `
        -OnProgress $progress -Environment $Environment -Visible:$Visible
    Write-Wm15Json ($Prefix + '.process.json') $child
    if ($child.Cleanup.Diagnostics.Count -gt 0 -or -not $child.Cleanup.Complete) {
        $cleanupIssues.Add([pscustomobject]@{step=$currentName; evidence=($Prefix + '.process.json'); cleanup=$child.Cleanup})
    }
    return $child
}

function Get-Protection {
    # Protect authored map/config and the inherited dirty tracked files, without hashing gigabytes of unchanged art.
    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($relative in @('ProjectSettings/ProjectVersion.txt','ProjectSettings/EditorBuildSettings.asset','Packages/manifest.json','Packages/packages-lock.json')) {
        $paths.Add((Join-Path $ProjectPath $relative)) | Out-Null
    }
    foreach ($relative in @('docs/world_map', 'Assets/Game/Resources/World')) {
        $path = Join-Path $ProjectPath $relative
        if (Test-Path -LiteralPath $path) {
            Get-ChildItem -LiteralPath $path -Filter '*.json' -File -Recurse | ForEach-Object { $paths.Add($_.FullName) | Out-Null }
        }
    }
    if (Get-Command git -ErrorAction SilentlyContinue) {
        $dirtyPaths = @(& git -C $ProjectPath -c core.quotepath=false diff --name-only HEAD -- Assets Packages ProjectSettings)
        if ($LASTEXITCODE -ne 0) { throw 'Cannot record dirty tracked file protection.' }
        foreach ($path in $dirtyPaths) { if ($path) { $paths.Add((Join-Path $ProjectPath $path)) | Out-Null } }
    }
    # Existing real saves are read only and fingerprinted; children get an isolated save-directory override.
    $saveRoot = $ProtectedSaveDirectory
    if (Test-Path -LiteralPath $saveRoot) {
        Get-ChildItem -LiteralPath $saveRoot -File -Recurse | Where-Object {
            -not (Test-Wm15GeneratedTestArtifact $_.FullName $saveRoot)
        } | ForEach-Object { $paths.Add($_.FullName) | Out-Null }
    }
    foreach ($path in $paths | Sort-Object) {
        [pscustomobject]@{ path = $path; hash = $(if (Test-Path -LiteralPath $path -PathType Leaf) { Get-Wm15Sha256 $path } else { 'MISSING' }) }
    }
}

try {
    Add-Step 'preflight' 'preflight'
    Add-Step 'protected-inputs-before' 'before'
    $fixtures = @(Get-Wm15Fixtures $ProjectPath | Sort-Object Platform, Filter -Unique)
    $repairReport = $null
    if ($RepairFrom) {
        $repairRoot = [IO.Path]::GetFullPath($RepairFrom)
        if ($RunDirectory.StartsWith($repairRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -or $RunDirectory -eq $repairRoot) { throw 'Repair output must be outside original evidence.' }
        $repairReport = Get-Content -LiteralPath (Join-Path $repairRoot 'results.json') -Raw | ConvertFrom-Json
        $selected = [Collections.Generic.List[object]]::new()
        foreach ($failedStep in $repairReport.steps | Where-Object { $_.status -in @('FAIL','ERROR','TIMEOUT') -and $_.name -match '^(EditMode|PlayMode)-' }) {
            $platform = $failedStep.name.Split('-')[0]
            $files = @(Get-ChildItem -LiteralPath $failedStep.directory -Filter 'attempt-*.xml' | Sort-Object Name)
            if ($files.Count -eq 0) { throw "Repair needs explicit case evidence for $($failedStep.name); no broad fallback." }
            [xml]$xml = Get-Content -LiteralPath $files[-1].FullName -Raw
            $nodes = @($xml.SelectNodes('//test-case[@result="Failed"]'))
            # Cleanup ERROR can accompany a valid all-passed XML (HealthAndFamily: two cases).
            if ($nodes.Count -eq 0 -and $failedStep.status -eq 'ERROR') { $nodes = @($xml.SelectNodes('//test-case')) }
            foreach ($node in $nodes) {
                if (-not $node.fullname) { throw 'Repair XML case lacks fullname.' }
                $selected.Add([pscustomobject]@{Platform=$platform; Filter=[string]$node.fullname; Source=$files[-1].FullName})
            }
        }
        $fixtures = @($selected | Sort-Object Platform,Filter -Unique)
        if ($fixtures.Count -eq 0 -and $RepairStage -eq 'Tests') { throw 'No failed cases selected; refusing full-suite fallback.' }
    }
    Write-Wm15Json (Join-Path $RunDirectory 'fixtures.json') @($fixtures)
    # World-map tests first; persistence and integration next, then every other discovered fixture.
    $fixtures = @($fixtures | Sort-Object @{Expression={ if ($_.Filter -match '\.WM\d') { 0 } elseif ($_.Filter -match 'Persistence|U31|P109') { 1 } else { 2 } }}, Platform, Filter)
    if (-not $RepairFrom -or $RepairStage -eq 'Tests') {
        Add-Step 'compile' 'compile'
        foreach ($fixture in $fixtures) {
            $label = if ($RepairFrom) { $fixture.Platform + '-repair-case-' + $steps.Count } else { $fixture.Platform + '-' + $fixture.Filter }
            Add-Step $label 'test' $fixture.Filter $fixture.Platform
        }
    }
    if (-not $RepairFrom -or $RepairStage -eq 'Performance') {
        Add-Step 'windows-build' 'build'
        if (-not $RepairFrom) { Add-Step 'player-save-load-smoke' 'smoke' }
        foreach ($population in @(100,300,500)) {
            if (-not $RepairFrom -or @($repairReport.steps | Where-Object { $_.name -eq "performance-$population" -and $_.status -in @('FAIL','ERROR','TIMEOUT') }).Count -eq 1) {
                Add-Step "performance-$population" 'performance' ([string]$population)
            }
        }
    }
    Add-Step 'protected-inputs-after' 'after'
    Add-Step 'git-diff-check' 'git'
    Write-Wm15Json (Join-Path $RunDirectory 'plan.json') @($steps.ToArray())

    $sha = [Security.Cryptography.SHA256]::Create()
    try { $key = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($ProjectPath.ToLowerInvariant()))).Replace('-', '').Substring(0, 24) }
    finally { $sha.Dispose() }
    $mutex = [Threading.Mutex]::new($false, ('Local\Serenity-WM15-' + $key))
    try { $ownsMutex = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $ownsMutex = $true }
    if (-not $ownsMutex) { throw 'Another WM15 runner already owns this project. Its work is left running.' }

    if ($DryRun) {
        foreach ($step in $steps) { $index++; Write-ProgressLine "PLAN $($step.Name)" }
        $currentName = 'dry run complete'
        Save-Status 'DRY_RUN'
        Save-Report 'DRY_RUN'
        $exitCode = 0
    } else {
        if (-not ('Wm15Power' -as [type])) {
            Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public static class Wm15Power { [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags); }'
        }
        $powerRequested = [Wm15Power]::SetThreadExecutionState([uint32]2147483651) -ne 0
        Write-ProgressLine "START output=$RunDirectory; tests=$($fixtures.Count); errors will be recorded and remaining checks will continue."
        foreach ($step in $steps) {
            $index++
            $currentName = $step.Name
            $stepTimer = [Diagnostics.Stopwatch]::StartNew()
            $status = 'PASS'
            $detail = ''
            $counts = $null
            $directory = Join-Path $RunDirectory ('{0:D3}-{1}' -f $index, $step.Name)
            New-Item -ItemType Directory -Force -Path $directory | Out-Null
            Write-ProgressLine "START $currentName"
            Save-Status 'RUNNING'
            try {
                if ($step.Kind -eq 'preflight') {
                    $waitTimer = [Diagnostics.Stopwatch]::StartNew()
                    while (Test-Wm15ProjectBusy $ProjectPath) {
                        if ($waitTimer.Elapsed.TotalMinutes -ge $BusyWaitMinutes -or $timer.Elapsed.TotalHours -ge $MaxHours) { throw 'Existing Unity Editor still owns the project; runtime checks will be BLOCKED.' }
                        Write-ProgressLine 'WAIT existing Unity Editor owns project; close it to begin.'
                        Save-Status 'WAITING_FOR_EDITOR'
                        Start-Sleep -Seconds ([Math]::Min($ProgressSeconds, 10))
                    }
                    if (-not (Test-Path -LiteralPath $EditorPath -PathType Leaf)) { throw "Editor missing: $EditorPath" }
                    $versionPath = Join-Path $ProjectPath 'ProjectSettings/ProjectVersion.txt'
                    if (-not (Test-Path -LiteralPath $versionPath) -or [IO.File]::ReadAllText($versionPath) -notmatch '(?m)^m_EditorVersion: 6000\.6\.4f1\s*$') { throw 'Project is not pinned to Unity 6000.6.4f1.' }
                    if ($fixtures.Count -eq 0) { throw 'No test fixtures discovered.' }
                    $unityReady = $true
                    $detail = "Unity available; discovered $($fixtures.Count) fixtures; sleep prevention=$powerRequested."
                } elseif ($step.Kind -eq 'before') {
                    if (Get-Command git -ErrorAction SilentlyContinue) {
                        $sourceCommit = (@(& git -C $ProjectPath rev-parse HEAD) -join '').Trim()
                        if ($LASTEXITCODE -ne 0) { throw 'Cannot read source commit.' }
                        & git -C $ProjectPath status --short | Out-File -LiteralPath (Join-Path $directory 'git-status.txt') -Encoding utf8
                    }
                    $protected = @(Get-Protection)
                    Write-Wm15Json (Join-Path $RunDirectory 'protected-before.json') @($protected)
                    $detail = "SHA-256 baseline: $($protected.Count) files."
                } elseif ($step.Kind -eq 'after') {
                    if ($protected.Count -eq 0) { $status = 'BLOCKED'; $detail = 'No input baseline was available.' }
                    else {
                        $differences = @()
                        foreach ($entry in $protected) {
                            $hash = if (Test-Path -LiteralPath $entry.path -PathType Leaf) { Get-Wm15Sha256 $entry.path } else { 'MISSING' }
                            if ($hash -ne $entry.hash) { $differences += [pscustomobject]@{ path=$entry.path; before=$entry.hash; after=$hash } }
                        }
                        $baselinePaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
                        foreach ($entry in $protected) { $baselinePaths.Add($entry.path) | Out-Null }
                        foreach ($entry in @(Get-Protection)) {
                            if (-not $baselinePaths.Contains($entry.path)) {
                                $differences += [pscustomobject]@{path=$entry.path; before='MISSING'; after=$entry.hash}
                            }
                        }
                        Write-Wm15Json (Join-Path $directory 'changed-files.json') @($differences)
                        $detail = "Checked $($protected.Count) files; changed=$($differences.Count)."
                        if ($differences.Count -gt 0) { $status = 'FAIL' }
                        if ($sourceCommit) {
                            $headNow = (@(& git -C $ProjectPath rev-parse HEAD) -join '').Trim()
                            if ($headNow -ne $sourceCommit) { $status = 'FAIL'; $detail += ' HEAD changed during the run; evidence spans multiple commits.' }
                        }
                    }
                } elseif ($step.Kind -eq 'git') {
                    $run = Invoke-Child 'git.exe' @('-C', $ProjectPath, 'diff', '--check') 60 (Join-Path $directory 'git')
                    if ($run.TimedOut) { $status = 'TIMEOUT' } elseif ($run.ExitCode -ne 0) { $status = 'FAIL' }
                    $detail = 'git diff --check; see git.stdout.log.'
                } elseif (@($cleanupIssues | Where-Object { -not $_.cleanup.Complete }).Count -gt 0) {
                    $status = 'BLOCKED'; $detail = 'Owned process cleanup unresolved; see cleanupIssues/process.json. No overlapping runtime check launched.'
                } elseif (-not $unityReady) {
                    $status = 'BLOCKED'; $detail = 'Unity preflight failed; independent static checks still run.'
                } elseif ($timer.Elapsed.TotalHours -ge $MaxHours) {
                    $status = 'BLOCKED'; $detail = 'Overall time budget exhausted; this check was not run.'
                } elseif (Test-Wm15ProjectBusy $ProjectPath) {
                    $status = 'BLOCKED'; $detail = 'Another Editor acquired project; existing process left untouched.'
                } elseif ($step.Kind -eq 'test' -and $step.Filter -match '(^|\.)PostgresIntegrationTests$' -and [string]::IsNullOrWhiteSpace($env:DYNASTYGAME_PG_TEST_DATABASE)) {
                    $status = 'PARTIAL'; $detail = 'Local PostgreSQL integration is not configured; no database/network connection attempted.'
                } elseif ($step.Kind -in @('compile','test','build')) {
                    $environment = @{
                        SERENITY_SAVE_DIRECTORY = (Join-Path $directory 'isolated-saves')
                        SERENITY_WM14_OUTPUT_DIRECTORY = (Join-Path $directory 'wm14-profile')
                    }
                    for ($attempt = 0; $attempt -le $InfrastructureRetries; $attempt++) {
                        $prefix = Join-Path $directory ("attempt-$attempt")
                        $log = $prefix + '.unity.log'
                        $xml = $prefix + '.xml'
                        $arguments = @('-batchmode','-projectPath',$ProjectPath,'-logFile',$log)
                        $timeout = $TestTimeoutSeconds
                        if ($step.Kind -eq 'test') {
                            # Rendered tests need a real graphics device. No -quit: Unity Test Runner exits itself.
                            $arguments += @('-force-d3d11','-runTests','-testPlatform',$step.Platform,'-testFilter',$step.Filter,'-testResults',$xml)
                            if ([string]::IsNullOrWhiteSpace($env:DYNASTYGAME_PG_TEST_DATABASE)) { $arguments += @('-testCategory','!PostgresIntegration') }
                        } elseif ($step.Kind -eq 'build') {
                            New-Item -ItemType Directory -Force -Path (Join-Path $ProjectPath 'Logs') | Out-Null
                            $arguments += @('-quit','-nographics','-buildTarget','Win64','-executeMethod','U00Build.WindowsDevelopment','-serenityBuildPath',$playerPath)
                            $timeout = $BuildTimeoutSeconds
                        } else { $arguments += @('-quit','-nographics') }
                        $run = Invoke-Child $EditorPath $arguments $timeout $prefix $environment
                        if ($run.TimedOut) { $status = 'TIMEOUT'; $detail = "Exceeded $timeout seconds; own process tree terminated."; break }
                        if ($step.Kind -eq 'test' -and (Test-Path -LiteralPath $xml)) {
                            $nunit = Read-Wm15NUnit $xml
                            $status = $nunit.Status; $detail = $nunit.Detail; $counts = $nunit.Counts
                            if ($run.ExitCode -ne 0) { $status = 'FAIL'; $detail += "; Unity exit=$($run.ExitCode)" }
                            break
                        }
                        $problems = @(Get-Wm15LogProblems $log)
                        if ($step.Kind -ne 'test' -and $run.ExitCode -eq 0 -and $problems.Count -eq 0) {
                            if ($step.Kind -eq 'build') {
                                $provenance = Join-Path (Split-Path $playerPath -Parent) 'build-provenance.json'
                                if (-not (Test-Path -LiteralPath $playerPath) -or -not (Test-Path -LiteralPath $provenance) -or [IO.File]::ReadAllText($log) -notmatch 'U00_BUILD_RESULT: Succeeded; errors=0') { throw 'Fresh successful Player/provenance/build marker missing.' }
                                $metadata = Get-Content -LiteralPath $provenance -Raw | ConvertFrom-Json
                                if ($metadata.source_commit -ne $sourceCommit -or $metadata.unity_version -ne '6000.6.4f1') { throw 'Player provenance mismatches run source/Unity.' }
                                $buildPassed = $true
                            }
                            $detail = "Unity exit=0; $($step.Kind) completed."; break
                        }
                        $status = 'FAIL'; $detail = "Unity exit=$($run.ExitCode); $($problems -join '; ')"
                        if ($step.Kind -eq 'test') { $detail += '; NUnit XML missing.' }
                        if ($attempt -lt $InfrastructureRetries) { Write-ProgressLine "RETRY infrastructure failure: $currentName"; $status = 'PASS' }
                    }
                } elseif (-not $buildPassed) {
                    $status = 'BLOCKED'; $detail = 'Fresh build failed; no older Player is substituted.'
                } elseif ($step.Kind -eq 'smoke') {
                    $log = Join-Path $directory 'player.log'
                    $arguments = @('-logFile',$log,'-screen-fullscreen','0','-screen-width','1600','-screen-height','900','-u31Smoke','-u31SmokeSeed','310031','-u31SmokeOutput',$directory)
                    $run = Invoke-Child $playerPath $arguments $PlayerTimeoutSeconds (Join-Path $directory 'player') @{SERENITY_SAVE_DIRECTORY=(Join-Path $directory 'isolated-saves')}
                    if ($run.TimedOut) { $status='TIMEOUT'; $detail='Player smoke timed out.' }
                    else {
                        $smoke = Get-Content -LiteralPath (Join-Path $directory 'u31-player-smoke.json') -Raw | ConvertFrom-Json
                        $problems = @(Get-Wm15LogProblems $log)
                        $image = Join-Path $directory 'u31-player-smoke.png'
                        if ($run.ExitCode -ne 0 -or -not $smoke.passed -or -not $smoke.save_created -or -not $smoke.load_completed -or $smoke.player_character_count -ne 10 -or $smoke.ai_character_count -ne 6 -or $smoke.composition_root_count -ne 1 -or $smoke.gameplay_ui_count -ne 1 -or $smoke.ui_subscription_count -ne 4 -or $problems.Count -gt 0 -or -not (Test-Path -LiteralPath $image) -or (Get-Item -LiteralPath $image).Length -eq 0) { throw 'Player save/load/counts/screenshot/log validation failed.' }
                        $detail='Fresh Player save/load smoke passed; compact-world screenshot recorded.'
                    }
                } elseif ($step.Kind -eq 'performance') {
                    $performanceScript = Join-Path (Split-Path $PSScriptRoot -Parent) 'Run-U29-Performance.ps1'
                    $output = Join-Path $directory 'matrix'
                    $arguments = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$performanceScript,'-PlayerPath',$playerPath,'-OutputDirectory',$output,'-Populations',$step.Filter,'-Repeats','3','-WarmupSeconds','60','-MeasurementSeconds','300','-ProcessTimeoutSeconds','540','-BaseCommit',$sourceCommit.Substring(0,12),'-ContinueOnFailure')
                    $run = Invoke-Child "$env:SystemRoot/System32/WindowsPowerShell/v1.0/powershell.exe" $arguments 1800 (Join-Path $directory 'matrix') @{SERENITY_SAVE_DIRECTORY=(Join-Path $directory 'isolated-saves')}
                    if ($run.TimedOut) { $status='TIMEOUT'; $detail='Performance matrix timeout; owned descendants terminated.' }
                    else {
                        $matrix = Get-Content -LiteralPath (Join-Path $output 'U29-final-summary.json') -Raw | ConvertFrom-Json
                        if ($matrix.repeatCount -ne 3 -or $matrix.smoke -or @($matrix.populations).Count -ne 1 -or $matrix.populations[0].population -ne [int]$step.Filter -or $matrix.populations[0].completedRepeats -ne 3) { throw 'Performance evidence incomplete; see matrix report.' }
                        if (-not $matrix.passed) { $status = 'FAIL' }
                        elseif ($run.ExitCode -ne 0) { throw "Performance reports PASS but wrapper exit=$($run.ExitCode)." }
                        $detail="population=$($step.Filter); 3 repeats; worst p95=$($matrix.populations[0].worstP95Ms) ms. Active Player generation, not full proposed-V2 visual acceptance."
                    }
                }
            } catch { $status = 'ERROR'; $detail = $_.Exception.Message }
            $result = [ordered]@{
                name=$currentName; status=$status; seconds=[Math]::Round($stepTimer.Elapsed.TotalSeconds,2)
                detail=$detail; directory=$directory; testCounts=$counts; finishedUtc=[DateTime]::UtcNow.ToString('O')
            }
            $results.Add([pscustomobject]$result)
            Add-Wm15Text $journal (($result | ConvertTo-Json -Depth 10 -Compress) + [Environment]::NewLine)
            Write-ProgressLine "$status $currentName ($($result.seconds)s) $detail"
            Save-Status 'RUNNING'
            Save-Report 'RUNNING'
        }
        $bad = @($results | Where-Object { $_.status -in @('FAIL','TIMEOUT','ERROR') }).Count + @($cleanupIssues | Where-Object { -not $_.cleanup.Complete }).Count
        $incomplete = @($results | Where-Object { $_.status -in @('BLOCKED','PARTIAL') }).Count
        $state = if ($bad -gt 0) { 'FAIL' } elseif ($incomplete -gt 0) { 'PARTIAL' } else { 'AUTOMATED_PASS' }
        $currentName = 'complete'
        Save-Status $state
        Save-Report $state
        Write-ProgressLine "FINISHED $state; report=$reportPath"
        $exitCode = if ($state -eq 'AUTOMATED_PASS') { 0 } elseif ($state -eq 'PARTIAL') { 2 } else { 1 }
    }
} catch {
    $fatal = $_.Exception.ToString()
    $currentName = 'runner error'
    try { Save-Status 'RUNNER_ERROR'; Save-Report 'RUNNER_ERROR'; Write-ProgressLine "RUNNER_ERROR $fatal" }
    catch { Write-Host "Cannot write final report: $($_.Exception.Message)" }
    $exitCode = 3
} finally {
    if ($powerRequested) { [Wm15Power]::SetThreadExecutionState([uint32]2147483648) | Out-Null }
    if ($ownsMutex) { $mutex.ReleaseMutex() }
    if ($null -ne $mutex) { $mutex.Dispose() }
}
exit $exitCode
