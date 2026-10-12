[CmdletBinding()]
param([string]$ProjectPath)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path -Path $PSScriptRoot -Parent }
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('serenity-u30-runner-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$player = Join-Path $testRoot 'U30FakePlayer.exe'
& $compiler /nologo /target:exe ('/out:' + $player) /r:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'Fixtures\U30FakePlayer.cs')
if ($LASTEXITCODE -ne 0) { throw 'Runner fixture compilation failed.' }
$reports = @()
$originalMode = $env:SERENITY_U30_FIXTURE_MODE
try {
    foreach ($mode in 'healthy','exception','partial','watchdog','exception_loop','watchdog_loop','corrupt') {
        $env:SERENITY_U30_FIXTURE_MODE = $mode
        $output = Join-Path $testRoot $mode
        $seconds = if ($mode -eq 'watchdog_loop') { 24 } elseif ($mode -eq 'exception_loop') { 18 } else { 8 }
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Run-Soak-U30.ps1') `
            -ProjectPath $ProjectPath -PlayerPath $player -Seed 3030 -TestMode -TestWallClockSeconds $seconds `
            -TestSegmentSeconds 2 -TestWatchdogSeconds 2 -TestRestartBackoffSeconds 1 -OutputFolder $output
        $exit = $LASTEXITCODE
        $result = Get-Content -LiteralPath (Join-Path $output 'soak-result.json') -Raw | ConvertFrom-Json
        $incidents = @(Get-Content -LiteralPath (Join-Path $output 'incidents.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
        $manifest = @(Get-Content -LiteralPath (Join-Path $output 'segment-manifest.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
        if ($result.actual_wall_clock_seconds -lt $seconds) { throw "$mode ended before its deadline." }
        if ($result.performance_samples -le 0 -or $result.coverage.production_steps -le 0) { throw "$mode lost segment evidence." }
        if ($result.incident_journal_sha256 -ne (Get-FileHash -LiteralPath (Join-Path $output 'incidents.jsonl')).Hash.ToLowerInvariant()) {
            throw "$mode journal hash mismatch."
        }
        if ($mode -eq 'healthy') {
            if ($exit -ne 0 -or -not $result.passed -or -not $result.checkpoint_resume_verified) { throw 'Healthy fixture failed.' }
            if (@($manifest | Where-Object { $_.exit_code -ne 0 }).Count -ne 0) { throw 'Healthy Player exit code was not retained.' }
        } else {
            if ($exit -eq 0 -or $result.passed -or $result.critical_errors -le 0) { throw "$mode did not fail closed." }
            if ($result.critical_errors -ne @($incidents | Where-Object critical).Count) { throw "$mode counted incidents twice or dropped them." }
        }
        if ($mode -in 'exception','partial','exception_loop') {
            $first = Get-Content -LiteralPath (Join-Path $output 'first-failure\failure.json') -Raw | ConvertFrom-Json
            if ($first.kind -ne 'recoverable_exception' -or $first.state_hash -ne 'fixture-hash' -or -not $first.exception) {
                throw "$mode lost the original gameplay failure."
            }
            $repro = Get-Content -LiteralPath (Join-Path $output 'first-failure\repro.ps1') -Raw
            if ($repro -notmatch 'ResumeFromCheckpoint' -or $repro -match '<absolute-external-path>') { throw "$mode reproduction is incomplete." }
            if (@($manifest | Where-Object { $_.end_reason -eq 'gameplay_exception' -and $_.sample_count -gt 0 }).Count -lt 1) {
                throw "$mode lost aborted-attempt performance."
            }
        }
        if ($mode -like '*_loop' -and $manifest.Count -lt 6) { throw "$mode did not retry beyond five failures." }
        if ($mode -eq 'watchdog' -and @($manifest | Where-Object { $_.end_reason -eq 'watchdog_restart' -and $_.sample_count -gt 0 }).Count -ne 1) {
            throw 'Watchdog discarded pre-stall samples.'
        }
        if ($mode -eq 'corrupt' -and @($incidents | Where-Object { $_.kind -eq 'checkpoint_validation' }).Count -lt 1) {
            throw 'Malformed checkpoint did not produce fallback evidence.'
        }
        $reports += [pscustomobject]@{ mode=$mode; passed=$true; exit=$exit; seconds=$result.actual_wall_clock_seconds;
            attempts=$manifest.Count; critical=$result.critical_errors; samples=$result.performance_samples; output=$output }
        Write-Output "$mode protocol test PASS"
    }
} finally { $env:SERENITY_U30_FIXTURE_MODE = $originalMode }
$reports | ConvertTo-Json -Depth 5
Write-Output "Runner protocol fixtures passed. Evidence: $testRoot"
$global:LASTEXITCODE = 0
