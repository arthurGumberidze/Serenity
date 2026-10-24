#requires -Version 5.1
[CmdletBinding()]
param([int]$Repeats = 2)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2
Import-Module (Join-Path $PSScriptRoot 'WM15.Runner.psm1') -Force
$root = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) ('Artifacts/WM15/runner-tests/serenity WM15 runner ' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $root | Out-Null
$compiler = Join-Path $env:SystemRoot 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$fakeEditor = Join-Path $root 'Fake Unity.exe'
& $compiler /nologo /target:exe ('/out:' + $fakeEditor) (Join-Path $PSScriptRoot 'Fixtures/WM15FakeUnity.cs')
if ($LASTEXITCODE -ne 0) { throw 'Cannot compile fault-injection executable.' }
$checks = [Collections.Generic.List[object]]::new()
function Assert([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message }; $checks.Add($Message) }
function Write-Text([string]$Path, [string]$Text) {
    New-Item -ItemType Directory -Force -Path (Split-Path $Path -Parent) | Out-Null
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
}
function Invoke-Runner([string]$Name, [string[]]$Extra = @(), [string]$Mode = 'mixed', [string]$Editor = $fakeEditor) {
    $output = Join-Path $root $Name
    $arguments = @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $runnerDirectory 'Run-WM15.ps1'),
        '-ProjectPath',$project,'-EditorPath',$Editor,'-RunDirectory',$output,'-ProtectedSaveDirectory',(Join-Path $project 'protected-user-saves'),'-TestTimeoutSeconds','2',
        '-BuildTimeoutSeconds','10','-PlayerTimeoutSeconds','10','-BusyWaitMinutes','0','-ProgressSeconds','1',
        '-InfrastructureRetries','1') + $Extra
    $run = Invoke-Wm15Process -FilePath "$env:SystemRoot/System32/WindowsPowerShell/v1.0/powershell.exe" `
        -Arguments $arguments -WorkingDirectory $root -OutputPrefix (Join-Path $root $Name) -TimeoutSeconds 100 `
        -Environment @{WM15_FIXTURE_MODE=$Mode; PATH=($fakeBin + ';' + $env:PATH)}
    if ($run.TimedOut) { throw "Self-test $Name hung: $output" }
    $report = Get-Content -LiteralPath (Join-Path $output 'results.json') -Raw | ConvertFrom-Json
    [pscustomobject]@{ Process=$run; Report=$report; Output=$output }
}

try {
    # Run a copied runner against a tiny temporary project. The real checkout/Editor is never used.
    $runnerDirectory = Join-Path $root 'Tools/Validation'
    New-Item -ItemType Directory -Force -Path $runnerDirectory | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Run-WM15.ps1'),(Join-Path $PSScriptRoot 'WM15.Runner.psm1') -Destination $runnerDirectory
    $project = Join-Path $root 'Project with spaces'
    Write-Text (Join-Path $project 'ProjectSettings/ProjectVersion.txt') "m_EditorVersion: 6000.6.4f1`n"
    Write-Text (Join-Path $project 'protected-user-saves/example.xml') '<protected-save />'
    Write-Text (Join-Path $project 'protected-user-saves/TestResults.xml') '<test-run />'
    $saveRoot = Join-Path $project 'protected-user-saves'
    Assert (Test-Wm15GeneratedTestArtifact (Join-Path $saveRoot 'TestResults.xml') $saveRoot) 'Only the root Unity generated NUnit report is excluded.'
    Assert (-not (Test-Wm15GeneratedTestArtifact (Join-Path $saveRoot 'slot/TestResults.xml') $saveRoot)) 'A nested save named TestResults.xml remains protected.'
    Assert (-not (Test-Wm15GeneratedTestArtifact (Join-Path $saveRoot 'example.xml') $saveRoot)) 'Real XML saves remain protected.'
    foreach ($name in @('Crash','Failed','Good','Hang','Malformed','Missing','Skipped','Zero')) {
        Write-Text (Join-Path $project "Assets/Game/Tests/Editor/$($name)Tests.cs") "namespace Fixture {`npublic class $($name)Tests { }`n}"
    }
    # Private git shim: deterministic provenance, no mutation or dependency on a real repo.
    $fakeBin = Join-Path $root 'bin'
    Write-Text (Join-Path $fakeBin 'git.cmd') "@echo off`r`necho aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa`r`nexit /b 0`r`n"
    # git.exe is called by the real runner's diff check; copy the stub executable to implement that step.
    Copy-Item -LiteralPath $fakeEditor -Destination (Join-Path $fakeBin 'git.exe')
    # Use a local performance shim, exercising the child script/report contract without a GPU.
    Write-Text (Join-Path $root 'Tools/Run-U29-Performance.ps1') @'
param($PlayerPath,$OutputDirectory,[int[]]$Populations,$Repeats,$WarmupSeconds,$MeasurementSeconds,$ProcessTimeoutSeconds,$BaseCommit,[switch]$ContinueOnFailure)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$population = $Populations[0]
$passed = $env:WM15_FIXTURE_MODE -ne 'mixed' -or $population -ne 100
@{passed=$passed;repeatCount=3;smoke=$false;populations=@(@{population=$population;completedRepeats=3;worstP95Ms=20})} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'U29-final-summary.json')
if (-not $passed) { exit 1 }
'@
    # Replace git shim with one executable supporting the exact native calls used by the runner.
    $gitSource = Join-Path $root 'Git.cs'
    Write-Text $gitSource @'
using System;
class Git { static int Main(string[] args) { if(Array.IndexOf(args,"rev-parse")>=0) Console.WriteLine(new string('a',40)); return 0; } }
'@
    & $compiler /nologo /target:exe ('/out:' + (Join-Path $fakeBin 'git.exe')) $gitSource
    if ($LASTEXITCODE -ne 0) { throw 'Cannot compile git shim.' }
    # Native argument handling covers spaces, Unicode, quotes, empty values and trailing backslashes.
    $arguments = @('--echo','two words','кириллица','embedded"quote','','C:\trailing space\','C:\slash\\"quote')
    $echo = Invoke-Wm15Process $fakeEditor $arguments $root (Join-Path $root 'args') 10
    $actual = @(Get-Content -LiteralPath (Join-Path $root 'args.stdout.log') | ForEach-Object { [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($_)) })
    Assert ($echo.ExitCode -eq 0 -and $actual.Count -eq $arguments.Count) "Native quoting preserves argument count (exit=$($echo.ExitCode), actual=$($actual.Count), expected=$($arguments.Count))."
    for ($i=0; $i -lt $arguments.Count; $i++) { Assert ($actual[$i] -ceq $arguments[$i]) "Native argument round trip $i." }

    $unrelated = Start-Process -FilePath $fakeEditor -ArgumentList '--hang' -WindowStyle Hidden -PassThru
    $null = $unrelated.Handle
    try {
        $childPidFile = Join-Path $root 'descendant.pid'
        $tree = Invoke-Wm15Process $fakeEditor @('--spawn-child',$childPidFile) $root (Join-Path $root 'tree') 2
        $childId = [int](Get-Content -LiteralPath $childPidFile -Raw)
        $childStillRunning = Get-Process -Id $childId -ErrorAction SilentlyContinue
        Assert ($tree.TimedOut -and $null -eq $childStillRunning) 'Timeout terminates the owned descendant process as well as its parent.'
        Assert (-not $unrelated.HasExited) 'Timeout never terminates an unrelated process of the same executable.'
    } finally { if (-not $unrelated.HasExited) { $unrelated.Kill(); $unrelated.WaitForExit() }; $unrelated.Dispose() }

    $orphanPidFile = Join-Path $root 'orphan.pid'
    $denied = Start-Process -FilePath $fakeEditor -ArgumentList '--hang' -WindowStyle Hidden -PassThru
    $null = $denied.Handle
    try {
        $ticks = $denied.StartTime.ToUniversalTime().Ticks
        $cleanup = Stop-Wm15OwnedProcess $denied $ticks $denied.Id
        Assert (-not $cleanup.Complete -and $cleanup.UnresolvedPids.Contains($denied.Id) -and -not $denied.HasExited) 'Access denied on a live owned process is explicit and unresolved.'
        $cleanup = Stop-Wm15OwnedProcess $denied $ticks (-$denied.Id)
        Assert ($cleanup.Complete -and $cleanup.Diagnostics.Count -eq 1 -and $denied.HasExited) 'Exit/access-denied race keeps a diagnostic without losing the child verdict.'
    } finally { if (-not $denied.HasExited) { $denied.Kill(); $denied.WaitForExit(10000) | Out-Null }; $denied.Dispose() }
    $orphan = Invoke-Wm15Process $fakeEditor @('--spawn-child',$orphanPidFile,'--exit-parent') $root (Join-Path $root 'orphan') 3
    $orphanId = [int](Get-Content -LiteralPath $orphanPidFile -Raw)
    Assert (-not $orphan.TimedOut -and $null -eq (Get-Process -Id $orphanId -ErrorAction SilentlyContinue)) 'Exited wrapper cannot leave a Player or an inherited-pipe hang behind.'

    $lockedReport = Join-Path $root 'locked-report.json'
    Write-Wm15Json $lockedReport @{state='before'}
    $holder = Start-Process -FilePath $fakeEditor -ArgumentList (@('--hold-file',$lockedReport) | ForEach-Object { ConvertTo-Wm15Argument $_ }) -WindowStyle Hidden -PassThru
    $null = $holder.Handle
    try {
        $readyDeadline = [DateTime]::UtcNow.AddSeconds(5)
        while (-not (Test-Path -LiteralPath ($lockedReport + '.ready'))) {
            if ([DateTime]::UtcNow -ge $readyDeadline) { throw 'File-lock fixture did not start.' }
            Start-Sleep -Milliseconds 50
        }
        Write-Wm15Json $lockedReport @{state='after'}
        $state = Get-Content -LiteralPath $lockedReport -Raw | ConvertFrom-Json
        Assert ($state.state -eq 'after') 'Transient report-reader locks are retried without aborting the run.'
    } finally { if (-not $holder.HasExited) { $holder.Kill(); $holder.WaitForExit() }; $holder.Dispose() }

    $lockDirectory = Join-Path $project 'Temp'
    New-Item -ItemType Directory -Force -Path $lockDirectory | Out-Null
    $lock = [IO.File]::Open((Join-Path $lockDirectory 'UnityLockfile'),[IO.FileMode]::Create,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    try {
        Assert (Test-Wm15ProjectBusy $project) 'Live Unity file lock is detected.'
        $busy = Invoke-Runner 'busy'
        Assert ($busy.Report.state -eq 'FAIL' -and @($busy.Report.steps | Where-Object status -eq 'BLOCKED').Count -ge 8) 'Busy Editor blocks children, still writes final report.'
        Assert ($busy.Report.steps[-1].name -eq 'git-diff-check') 'Static checks continue after busy preflight failure.'
    } finally { $lock.Dispose() }
    Assert (-not (Test-Wm15ProjectBusy $project)) 'Stale unlocked UnityLockfile does not block.'

    $sha = [Security.Cryptography.SHA256]::Create()
    try { $mutexKey = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($project.ToLowerInvariant()))).Replace('-', '').Substring(0,24) }
    finally { $sha.Dispose() }
    $owner = [Threading.Mutex]::new($false, ('Local\Serenity-WM15-' + $mutexKey))
    $null = $owner.WaitOne(0)
    try {
        $duplicate = Invoke-Runner 'duplicate-runner'
        Assert ($duplicate.Process.ExitCode -eq 3 -and $duplicate.Report.state -eq 'RUNNER_ERROR' -and @($duplicate.Report.steps).Count -eq 0) 'Project mutex prevents a second runner from starting checks.'
    } finally { $owner.ReleaseMutex(); $owner.Dispose() }

    for ($round=1; $round -le $Repeats; $round++) {
        $mixed = Invoke-Runner "mixed-$round"
        $steps = @($mixed.Report.steps)
        Assert ($mixed.Process.ExitCode -eq 1 -and $mixed.Report.state -eq 'FAIL') "Round $round returns nonzero after failures."
        foreach ($expected in @(
            @('CrashTests','FAIL'), @('FailedTests','FAIL'), @('GoodTests','PASS'), @('HangTests','TIMEOUT'),
            @('MalformedTests','ERROR'), @('MissingTests','FAIL'), @('SkippedTests','PARTIAL'), @('ZeroTests','ERROR'),
            @('windows-build','PASS'), @('player-save-load-smoke','PASS'), @('performance-100','FAIL'),
            @('performance-300','PASS'), @('performance-500','PASS'), @('protected-inputs-after','PASS'), @('git-diff-check','PASS')
        )) {
            $found = @($steps | Where-Object { $_.name.EndsWith($expected[0]) })
            Assert ($found.Count -eq 1 -and $found[0].status -eq $expected[1]) "Round $round continues through $($expected[0]) => $($expected[1])."
        }
        $plan = Get-Content -LiteralPath (Join-Path $mixed.Output 'plan.json') -Raw | ConvertFrom-Json
        $journal = @(Get-Content -LiteralPath (Join-Path $mixed.Output 'steps.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
        Assert ($steps.Count -eq @($plan).Count -and $journal.Count -eq @($plan).Count) "Round $round journals every planned step."
        $crash = $steps | Where-Object name -like '*CrashTests'
        Assert (Test-Path -LiteralPath (Join-Path $crash.directory 'attempt-1.stderr.log')) "Round $round retries missing-XML crash."
        $failed = $steps | Where-Object name -like '*FailedTests'
        Assert (-not (Test-Path -LiteralPath (Join-Path $failed.directory 'attempt-1.stderr.log'))) "Round $round does not retry assertion failures."
    }
    $healthy = Invoke-Runner 'healthy' @() 'healthy'
    Assert ($healthy.Process.ExitCode -eq 0 -and $healthy.Report.state -eq 'AUTOMATED_PASS') 'Healthy complete run passes with manual acceptance still pending.'
    Assert ($healthy.Report.manualAcceptance -eq 'PENDING') 'Automatic success never pretends human acceptance.'
    $generated = Invoke-Runner 'generated-report' @() 'generated-report'
    Assert (($generated.Report.steps | Where-Object name -eq 'protected-inputs-after').status -eq 'PASS') 'Regenerated Unity root TestResults.xml does not accuse a save mutation.'
    $changedSave = Invoke-Runner 'save-change' @() 'save-change'
    Assert (($changedSave.Report.steps | Where-Object name -eq 'protected-inputs-after').status -eq 'FAIL') 'A changed real save is still detected independently of test artifacts.'
    $addedSave = Invoke-Runner 'save-added' @() 'save-added'
    Assert (($addedSave.Report.steps | Where-Object name -eq 'protected-inputs-after').status -eq 'FAIL') 'A newly added real save is detected as well as changes to existing saves.'
    $brokenBuild = Invoke-Runner 'broken-build' @() 'build-fail'
    Assert (@($brokenBuild.Report.steps | Where-Object { $_.name -match 'smoke|performance-' -and $_.status -eq 'BLOCKED' }).Count -eq 4) 'Failed build blocks smoke and all matrices without using stale Player.'
    Assert ($brokenBuild.Report.steps[-1].name -eq 'git-diff-check') 'End-of-run checks survive failed build.'
    $missingEditor = Invoke-Runner 'missing-editor' @() 'healthy' (Join-Path $root 'missing.exe')
    Assert ($missingEditor.Report.steps[-1].name -eq 'git-diff-check' -and $missingEditor.Process.ExitCode -ne 0) 'Missing Editor produces complete blocked/static report.'
    $dry = Invoke-Runner 'dry-run' @('-DryRun')
    Assert ($dry.Report.state -eq 'DRY_RUN' -and $dry.Process.ExitCode -eq 0 -and @($dry.Report.steps).Count -eq 0) 'Dry run plans checks without starting fake Unity.'
    $repairSource = Join-Path $root 'repair-source'
    $repairStep = Join-Path $repairSource 'failed-step'
    Write-Text (Join-Path $repairStep 'attempt-0.xml') '<test-run><test-case fullname="Fixture.FailedCase" result="Failed"/><test-case fullname="Fixture.PassedCase" result="Passed"/></test-run>'
    Write-Wm15Json (Join-Path $repairSource 'results.json') @{steps=@(@{name='EditMode-Fixture';status='FAIL';directory=$repairStep})}
    $repair = Invoke-Runner 'repair-plan' @('-DryRun','-RepairFrom',$repairSource)
    $repairPlan = @(Get-Content (Join-Path $repair.Output 'plan.json') -Raw | ConvertFrom-Json)
    $repairCases = @($repairPlan | Where-Object Kind -eq 'test')
    Assert ($repair.Process.ExitCode -eq 0 -and $repairCases.Count -eq 1 -and $repairCases[0].Filter -eq 'Fixture.FailedCase') 'Repair selects only the exact failed fullname, never the passed neighbour.'
    Assert (@($repairPlan | Where-Object {$_.Kind -in @('build','performance','smoke')}).Count -eq 0) 'Repair test stage cannot silently rerun build or Player gates.'
    $rejectedOutput = Invoke-Wm15Process -FilePath "$env:SystemRoot/System32/WindowsPowerShell/v1.0/powershell.exe" `
        -Arguments @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $runnerDirectory 'Run-WM15.ps1'),'-ProjectPath',$project,'-RunDirectory',$dry.Output,'-DryRun') `
        -WorkingDirectory $root -OutputPrefix (Join-Path $root 'reject-existing') -TimeoutSeconds 10
    Assert ($rejectedOutput.ExitCode -ne 0 -and (Test-Path -LiteralPath (Join-Path $dry.Output 'plan.json'))) 'Existing evidence cannot be overwritten by another run.'
    Write-Wm15Json (Join-Path $root 'self-test-results.json') @{checks=$checks.Count;passed=$true;repeats=$Repeats}
    Write-Host "PASS: $($checks.Count) runner assertions; evidence: $root"
} catch {
    Write-Host "FAIL: $($_.Exception.Message); evidence: $root"
    throw
}
