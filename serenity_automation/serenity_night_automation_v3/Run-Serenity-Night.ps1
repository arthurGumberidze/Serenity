param(
    [string]$Repo = "C:\serenity_game",
    [string]$StartTask = "U25",
    [string]$FinalTask = "U31",
    [int]$WaitForU25Hours = 2,
    [int]$PollSeconds = 60,
    [string]$Model = "",
    [string]$RunBase = "",
    [ValidateRange(8, 168)][int]$U30SoakHours = 8,
    [ValidateRange(1, 120)][int]$U30CheckpointMinutes = 15,
    [int]$U30Seed = 300030,
    [switch]$NoShutdown,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$PromptRoot = Join-Path $ScriptRoot "prompts"
$StageSchema = Join-Path $ScriptRoot "stage-result.schema.json"
$VerifySchema = Join-Path $ScriptRoot "verify-result.schema.json"
$U30PrepSchema = Join-Path $ScriptRoot "u30-prep-result.schema.json"
$U30SoakSchema = Join-Path $ScriptRoot "u30-soak-result.schema.json"
$TaskOrder = @("U26", "U27", "U28", "U29", "U30", "U30A", "U31")

if ([string]::IsNullOrWhiteSpace($RunBase)) {
    $root = if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        Join-Path ([System.IO.Path]::GetTempPath()) "SerenityAutomation"
    } else {
        Join-Path $env:LOCALAPPDATA "SerenityAutomation"
    }
    $RunBase = Join-Path $root "runs"
}

$RunStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$RunRoot = Join-Path $RunBase $RunStamp
New-Item -ItemType Directory -Force -Path $RunRoot | Out-Null
$Transcript = Join-Path $RunRoot "orchestrator.log"
Start-Transcript -Path $Transcript -Append | Out-Null

function Write-RunLog([string]$Message) {
    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$ts] $Message"
}

function Invoke-Git([string[]]$GitArgs) {
    $output = & git -C $Repo @GitArgs 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git $($GitArgs -join ' ') failed:`n$output" }
    return ($output -join "`n").Trim()
}

function Get-Head { Invoke-Git @("rev-parse", "HEAD") }
function Get-StatusPorcelain { Invoke-Git @("status", "--porcelain") }
function Test-WorkingTreeClean { [string]::IsNullOrWhiteSpace((Get-StatusPorcelain)) }
function Get-CommitSubject { Invoke-Git @("log", "-1", "--pretty=%s") }

function Get-TaskCommit([string]$TaskId) {
    $result = & git -C $Repo log --format=%H --extended-regexp --grep="^${TaskId}([: ]|$)" -1 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Unable to inspect commit for $TaskId`: $result" }
    return ($result -join "`n").Trim()
}

function Get-TaskStatus([string]$TaskId) {
    $graph = Join-Path $Repo "docs\TASK_GRAPH.md"
    if (!(Test-Path $graph)) { return "" }
    $line = Select-String -Path $graph -Pattern "^\|\s*$TaskId\s*\|" | Select-Object -First 1
    if ($null -eq $line) { return "" }
    $parts = $line.Line.Split("|")
    if ($parts.Count -lt 4) { return "" }
    return $parts[2].Trim()
}

function Get-TaskSequence {
    $last = [Array]::IndexOf($TaskOrder, $FinalTask)
    if ($last -lt 0) { throw "FinalTask must be one of: $($TaskOrder -join ', ')" }
    $first = if ($StartTask -eq "U25") { 0 } else { [Array]::IndexOf($TaskOrder, $StartTask) }
    if ($first -lt 0) { throw "StartTask must be U25 or one of: $($TaskOrder -join ', ')" }
    if ($first -gt $last) { throw "StartTask $StartTask is after FinalTask $FinalTask." }
    return @($TaskOrder[$first..$last])
}

function Test-U25Committed {
    $handoff = Join-Path $Repo "docs\U25_HANDOFF.md"
    (Test-Path $handoff) -and
    ((Get-TaskStatus "U25") -eq "DONE") -and
    (Test-WorkingTreeClean) -and
    (-not [string]::IsNullOrWhiteSpace((Get-TaskCommit "U25")))
}

function Stop-AndShutdown([string]$Reason, [int]$ExitCode = 1) {
    Write-RunLog "STOP: $Reason"
    @"
Time: $(Get-Date -Format o)
Reason: $Reason
HEAD: $(Get-Head)
Status:
$(Get-StatusPorcelain)
"@ | Set-Content -Encoding UTF8 (Join-Path $RunRoot "STOP_REASON.txt")

    if (-not $NoShutdown -and -not $DryRun) {
        Write-RunLog "PC shutdown scheduled in 60 seconds. Cancel with: shutdown /a"
        & shutdown.exe /s /t 60 /c "Serenity night automation stopped: $Reason"
    } else {
        Write-RunLog "Shutdown skipped."
    }
    Stop-Transcript | Out-Null
    exit $ExitCode
}

function Invoke-CodexRun([string]$Name, [string]$PromptText, [string]$SchemaPath) {
    $dir = Join-Path $RunRoot $Name
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $promptPath = Join-Path $dir "prompt.txt"
    $jsonlPath = Join-Path $dir "trace.jsonl"
    $stderrPath = Join-Path $dir "stderr.log"
    $answerPath = Join-Path $dir "answer.json"
    $PromptText | Set-Content -Encoding UTF8 $promptPath

    if ($DryRun) {
        Write-RunLog "DRY RUN: would execute $Name using $SchemaPath"
        return @{ ExitCode = 0; AnswerPath = $answerPath; TracePath = $jsonlPath; StderrPath = $stderrPath }
    }

    $args = @(
        "exec", "--json",
        "--sandbox", "workspace-write",
        "--ask-for-approval", "never",
        "--config", "model_reasoning_effort=high",
        "--output-schema", $SchemaPath,
        "--output-last-message", $answerPath
    )
    if (-not [string]::IsNullOrWhiteSpace($Model)) { $args += @("--model", $Model) }
    $args += "-"

    Write-RunLog "Starting Codex: $Name"
    Get-Content -Raw $promptPath | & codex @args 2> $stderrPath | Tee-Object -FilePath $jsonlPath | Out-Null
    $code = $LASTEXITCODE
    Write-RunLog "$Name exit code: $code"
    return @{ ExitCode = $code; AnswerPath = $answerPath; TracePath = $jsonlPath; StderrPath = $stderrPath }
}

function Read-JsonResult([string]$Path) {
    if (!(Test-Path $Path)) { throw "Missing structured result: $Path" }
    $raw = Get-Content -Raw $Path
    if ([string]::IsNullOrWhiteSpace($raw)) { throw "Empty structured result: $Path" }
    return $raw | ConvertFrom-Json
}

function New-VerifyPrompt([string]$TaskId, [string]$ExternalEvidencePath = "") {
    $external = if ([string]::IsNullOrWhiteSpace($ExternalEvidencePath)) { "" } else {
        "- inspect the external U30 structured result and supporting artifacts rooted at: $ExternalEvidencePath;"
    }
@"
You are the independent release verifier for Serenity task $TaskId.
Repository: $Repo

DO NOT implement the next task.
DO NOT modify tracked source code or project docs.

Verify from the repository itself that $TaskId is genuinely DONE:
- read AGENTS, TASK_GRAPH, NEXT_TASK, PROJECT_STATE, ARCHITECTURE, DECISIONS, the task prompt and ${TaskId}_HANDOFF;
- inspect the exact task commit(s) and diff;
- verify the task-specific Definition of Done rather than only the generic table row;
- verify focused test/build evidence and re-run only focused/critical checks where needed;
- verify working tree clean and a commit subject beginning with $TaskId exists;
- verify the next task has NOT been implemented;
- check duplicate canonical state, regenerated IDs, realtime SQL authority, GameObject-as-domain authority, resource duplication and weakened acceptance criteria;
$external
- after verification tracked source/docs and HEAD must remain unchanged.

Return ONLY schema JSON with task exactly "$TaskId".
verified=true only if every mandatory gate is satisfied.
If uncertain, verified=false with exact blocking_reason.
"@
}

function Get-ImplementationPrompt([string]$TaskId) {
    $path = Join-Path $PromptRoot "${TaskId}_PROMPT.txt"
    if (!(Test-Path $path)) { throw "Missing task prompt: $path" }
    $taskPrompt = Get-Content -Raw $path
@"
You are running Serenity task $TaskId autonomously overnight.
Repository: $Repo

IMPORTANT EXECUTION WRAPPER
- Work only in the repository above.
- Begin by checking git status and reading AGENTS.md plus current repository documentation referenced by the task prompt.
- Stop on any mandatory failure or usage/approval limitation.
- Do not push.
- At the very end return ONLY the JSON required by the supplied output schema, with task exactly "$TaskId" and the full 40-character lowercase HEAD commit hash.
- done=true only when implementation, required tests, Windows build, documentation, commit and clean working tree are complete.
- next_task_started must remain false.

----- TASK-SPECIFIC PROMPT START -----
$taskPrompt
----- TASK-SPECIFIC PROMPT END -----
"@
}

function Get-U30PreparationPrompt {
    $path = Join-Path $PromptRoot "U30_PROMPT.txt"
    $taskPrompt = Get-Content -Raw $path
@"
You are running ONLY the preparation phase of Serenity U30.
Repository: $Repo

Implement the U30 harness and exact Tools/Run-Soak-U30.ps1 interface described below. Run the required focused tests, real short soak, deterministic repeat, checkpoint/resume, watchdog/failure injection and Windows build.

This phase MUST:
- update TASK_GRAPH U30 to IN_PROGRESS, not DONE;
- keep NEXT_TASK on U30 and explicitly state that the external >=8h gate is pending;
- create U30_HANDOFF.md and SOAK_TESTING.md as preparation documents with exact commands;
- commit all implementation/docs with a subject beginning `U30`;
- leave a clean working tree;
- not start U30A or U31;
- return ONLY JSON matching the U30 preparation schema, with task=U30, prepared=true only if every preparation gate passes, and the full 40-character lowercase HEAD hash.

Do not wait eight hours and do not claim U30 DONE. The outer PowerShell orchestrator will run the long soak without Codex after this process exits.

----- U30 TASK PROMPT START -----
$taskPrompt
----- U30 TASK PROMPT END -----
"@
}

function Get-U30FinalizePrompt([string]$EvidencePath) {
@"
You are the U30 evidence finalizer, not a feature implementation agent.
Repository: $Repo
External soak result: $EvidencePath

Read AGENTS.md, current project docs, U30 prompt, U30_HANDOFF.md, SOAK_TESTING.md, the structured result and supporting logs/checkpoints in its folder.

Mandatory validation:
- actual and requested duration are both at least 28800 seconds;
- actual duration is not shorter than requested duration;
- timestamps are coherent with reported duration;
- passed=true;
- critical_errors, invariant_failures and watchdog_failures are zero;
- memory_runaway_detected=false;
- checkpoint_resume_verified=true;
- final_state_hash is present;
- the repository is clean before finalization and the U30 preparation commit exists;
- no evidence is invented and no task acceptance criterion is weakened.

If any gate fails, return done=false without committing or marking U30 DONE.

If every gate passes:
- update U30_HANDOFF.md with exact external evidence path, timestamps, duration, seed, hash and result;
- update PROJECT_STATE, TASK_GRAPH (U30 DONE), NEXT_TASK (U30A), DECISIONS and ARCHITECTURE only as required by the implemented harness/evidence;
- do not implement U30A/U31;
- commit with subject `U30: validate 8 hour soak`;
- leave the worktree clean;
- return ONLY stage schema JSON with task=U30, done=true and the full 40-character lowercase HEAD hash.
"@
}

function Assert-CommitMatches([string]$ReportedHash, [string]$ActualHash, [string]$Context) {
    if ([string]::IsNullOrWhiteSpace($ReportedHash) -or $ReportedHash -ne $ActualHash) {
        throw "$Context reported commit $ReportedHash but HEAD is $ActualHash."
    }
}

function Assert-StageHardGates([string]$TaskId, $Result, [string]$HeadBefore) {
    if ($Result.task -ne $TaskId) { throw "Result task '$($Result.task)' does not match $TaskId." }
    if (-not $Result.done) { throw "$TaskId NOT DONE: $($Result.blocking_reason)" }
    if (-not $Result.commit_created) { throw "$TaskId did not create commit." }
    if (-not $Result.working_tree_clean) { throw "$TaskId reported dirty tree." }
    if (-not $Result.required_tests_passed) { throw "$TaskId required tests failed." }
    if (-not $Result.build_passed) { throw "$TaskId build failed." }
    if ($Result.next_task_started) { throw "$TaskId started next task." }
    if (-not (Test-WorkingTreeClean)) { throw "$TaskId left repository dirty." }
    $headAfter = Get-Head
    if ($headAfter -eq $HeadBefore) { throw "$TaskId did not advance HEAD." }
    Assert-CommitMatches $Result.commit_hash $headAfter $TaskId
    $subject = Get-CommitSubject
    if ($subject -notmatch "^$TaskId\b") { throw "Unexpected HEAD subject: $subject" }
    if ((Get-TaskStatus $TaskId) -ne "DONE") { throw "$TaskId not DONE in TASK_GRAPH." }
}

function Assert-U30PreparationGates($Result, [string]$HeadBefore) {
    if ($Result.task -ne "U30" -or -not $Result.prepared) { throw "U30 preparation failed: $($Result.blocking_reason)" }
    if (-not $Result.commit_created -or -not $Result.working_tree_clean) { throw "U30 preparation commit/clean gate failed." }
    if (-not $Result.short_soak_passed -or -not $Result.checkpoint_resume_passed -or
        -not $Result.determinism_passed -or -not $Result.build_passed) {
        throw "U30 preparation tests/build gate failed."
    }
    if ($Result.next_task_started) { throw "U30 preparation started the next task." }
    if (-not (Test-WorkingTreeClean)) { throw "U30 preparation left repository dirty." }
    $headAfter = Get-Head
    if ($headAfter -eq $HeadBefore) { throw "U30 preparation did not advance HEAD." }
    Assert-CommitMatches $Result.commit_hash $headAfter "U30 preparation"
    if ((Get-CommitSubject) -notmatch "^U30\b") { throw "U30 preparation commit subject is invalid." }
    if ((Get-TaskStatus "U30") -ne "IN_PROGRESS") { throw "U30 preparation must leave TASK_GRAPH IN_PROGRESS." }
    $runner = Join-Path $Repo "Tools\Run-Soak-U30.ps1"
    if (!(Test-Path $runner)) { throw "U30 runner missing: $runner" }
    if ([string]::IsNullOrWhiteSpace($Result.runner_path)) { throw "U30 preparation did not report runner_path." }
}

function Assert-VerificationGates([string]$TaskId, $Result) {
    if ($Result.task -ne $TaskId) { throw "Verifier task '$($Result.task)' does not match $TaskId." }
    if (-not $Result.verified -or -not $Result.task_marked_done -or -not $Result.working_tree_clean -or
        -not $Result.commit_present -or -not $Result.required_tests_evidence_ok -or
        -not $Result.build_evidence_ok -or -not $Result.next_task_not_started) {
        throw "$TaskId verification failed: $($Result.blocking_reason) $($Result.summary)"
    }
}

function Invoke-IndependentVerification([string]$TaskId, [string]$ExternalEvidencePath = "") {
    $head = Get-Head
    $run = Invoke-CodexRun "$TaskId-verify" (New-VerifyPrompt $TaskId $ExternalEvidencePath) $VerifySchema
    if ($run.ExitCode -ne 0) { throw "$TaskId verifier failed/limit. Exit $($run.ExitCode)" }
    $result = Read-JsonResult $run.AnswerPath
    Assert-VerificationGates $TaskId $result
    if ((Get-Head) -ne $head -or -not (Test-WorkingTreeClean)) {
        throw "$TaskId verifier altered repository."
    }
}

function Invoke-U30ExternalSoak {
    if ($U30SoakHours -lt 8) { throw "Official U30 soak cannot be shorter than 8 hours." }
    $runner = Join-Path $Repo "Tools\Run-Soak-U30.ps1"
    if (!(Test-Path $runner)) { throw "U30 runner missing: $runner" }
    if ((Get-TaskStatus "U30") -ne "IN_PROGRESS") { throw "U30 must be IN_PROGRESS before external soak." }
    if (-not (Test-WorkingTreeClean)) { throw "Repository must be clean before external U30 soak." }

    $out = Join-Path $RunRoot "U30-external-soak"
    New-Item -ItemType Directory -Force -Path $out | Out-Null
    $consoleLog = Join-Path $out "orchestrator-console.log"
    Write-RunLog "Starting external U30 soak for $U30SoakHours wall-clock hours. Codex is not active during this process."
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $runner `
        -Seed $U30Seed `
        -WallClockHours $U30SoakHours `
        -CheckpointIntervalMinutes $U30CheckpointMinutes `
        -OutputFolder $out *>> $consoleLog
    $code = $LASTEXITCODE
    if ($code -ne 0) { throw "External U30 soak failed with exit code $code. See $consoleLog" }

    $resultPath = Join-Path $out "soak-result.json"
    $result = Read-JsonResult $resultPath
    $minimum = [long]$U30SoakHours * 3600L
    if (-not $result.passed) { throw "External U30 soak result reports passed=false: $($result.summary)" }
    if ([long]$result.requested_wall_clock_seconds -lt $minimum) { throw "U30 requested duration is below $minimum seconds." }
    if ([long]$result.actual_wall_clock_seconds -lt [long]$result.requested_wall_clock_seconds) { throw "U30 actual duration is shorter than requested." }
    if ([long]$result.actual_wall_clock_seconds -lt 28800L) { throw "U30 actual duration is below eight hours." }
    if ([int]$result.critical_errors -ne 0 -or [int]$result.invariant_failures -ne 0 -or
        [int]$result.watchdog_failures -ne 0 -or [bool]$result.memory_runaway_detected -or
        -not [bool]$result.checkpoint_resume_verified) {
        throw "U30 structured reliability gates failed."
    }
    if ([string]::IsNullOrWhiteSpace([string]$result.final_state_hash)) { throw "U30 final_state_hash missing." }
    $started = [DateTimeOffset]::Parse([string]$result.started_utc)
    $completed = [DateTimeOffset]::Parse([string]$result.completed_utc)
    if ($completed -le $started) { throw "U30 timestamps are invalid." }
    $timestampSeconds = [long][Math]::Floor(($completed - $started).TotalSeconds)
    if ([Math]::Abs($timestampSeconds - [long]$result.actual_wall_clock_seconds) -gt 300L) {
        throw "U30 timestamps and actual_wall_clock_seconds differ by more than five minutes."
    }
    Write-RunLog "External U30 soak passed. Evidence: $resultPath"
    return $resultPath
}

function Invoke-U30Task {
    $status = Get-TaskStatus "U30"
    if ($status -eq "TODO") {
        if (-not (Test-WorkingTreeClean)) { throw "Dirty repo before U30 preparation." }
        $headBefore = Get-Head
        $run = Invoke-CodexRun "U30-prepare" (Get-U30PreparationPrompt) $U30PrepSchema
        if ($run.ExitCode -ne 0) { throw "U30 preparation Codex failed/limit. Exit $($run.ExitCode)" }
        $result = Read-JsonResult $run.AnswerPath
        Assert-U30PreparationGates $result $headBefore
        Write-RunLog "U30 preparation committed; external soak begins."
    } elseif ($status -ne "IN_PROGRESS") {
        throw "U30 must be TODO or IN_PROGRESS before execution; actual status is '$status'."
    }

    $evidence = Invoke-U30ExternalSoak
    if (-not (Test-WorkingTreeClean)) { throw "External U30 soak altered the repository." }
    $headBeforeFinalize = Get-Head
    $run = Invoke-CodexRun "U30-finalize" (Get-U30FinalizePrompt $evidence) $StageSchema
    if ($run.ExitCode -ne 0) { throw "U30 finalizer failed/limit. Exit $($run.ExitCode)" }
    $result = Read-JsonResult $run.AnswerPath
    Assert-StageHardGates "U30" $result $headBeforeFinalize
    Invoke-IndependentVerification "U30" $evidence
}

try {
    if (!(Test-Path $Repo)) { throw "Repository not found: $Repo" }
    if (!(Test-Path (Join-Path $Repo ".git"))) { throw "Not a git repository: $Repo" }
    if (!(Get-Command git -ErrorAction SilentlyContinue)) { throw "git missing from PATH" }
    if (!(Get-Command codex -ErrorAction SilentlyContinue)) { throw "codex CLI missing from PATH" }
    if (!(Test-Path $PromptRoot)) { throw "prompts folder missing: $PromptRoot" }
    foreach ($schema in @($StageSchema, $VerifySchema, $U30PrepSchema, $U30SoakSchema)) {
        if (!(Test-Path $schema)) { throw "Schema missing: $schema" }
    }
    $tasks = Get-TaskSequence
    foreach ($task in $tasks) {
        $prompt = Join-Path $PromptRoot "${task}_PROMPT.txt"
        if (!(Test-Path $prompt)) { throw "Task prompt missing: $prompt" }
    }

    Write-RunLog "Serenity night automation v3 started."
    Write-RunLog "External run root: $RunRoot"
    Write-RunLog "Task sequence: $($tasks -join ' -> ')"
    Write-RunLog "U30 external soak: $U30SoakHours hour(s)"

    if ($DryRun) {
        Write-RunLog "DRY RUN. HEAD=$(Get-Head); clean=$(Test-WorkingTreeClean)"
        foreach ($task in $tasks) {
            Write-RunLog "$task status=$(Get-TaskStatus $task); prompt=OK"
        }
        Write-RunLog "Dry run does not invoke Codex, Unity, shutdown or the eight-hour soak."
        Stop-Transcript | Out-Null
        exit 0
    }

    if ($StartTask -eq "U25") {
        Write-RunLog "Waiting for committed U25. This script does not implement U25."
        $deadline = (Get-Date).AddHours($WaitForU25Hours)
        while (-not (Test-U25Committed)) {
            if ((Get-Date) -ge $deadline) { Stop-AndShutdown "Timed out waiting for U25 committed/DONE/clean." }
            Write-RunLog "U25 not ready. status=$(Get-TaskStatus 'U25'); clean=$(Test-WorkingTreeClean); taskCommit=$(Get-TaskCommit 'U25')"
            Start-Sleep -Seconds $PollSeconds
        }
        try { Invoke-IndependentVerification "U25" }
        catch { Stop-AndShutdown "U25 verification failed: $($_.Exception.Message)" }
        Write-RunLog "U25 VERIFIED."
    }

    foreach ($task in $tasks) {
        if ((Get-TaskStatus $task) -eq "DONE") {
            Write-RunLog "$task already DONE; verifying."
            try { Invoke-IndependentVerification $task }
            catch { Stop-AndShutdown "$task existing DONE failed verification: $($_.Exception.Message)" }
            continue
        }

        if ($task -eq "U30") {
            try { Invoke-U30Task }
            catch { Stop-AndShutdown "U30 failure: $($_.Exception.Message)" }
            Write-RunLog "U30 DONE + VERIFIED after real external soak."
            continue
        }

        if ((Get-TaskStatus $task) -ne "TODO") {
            Stop-AndShutdown "$task expected TODO or DONE but status is $(Get-TaskStatus $task)."
        }
        if (-not (Test-WorkingTreeClean)) { Stop-AndShutdown "Dirty repo before $task." }

        $headBefore = Get-Head
        $prompt = Get-ImplementationPrompt $task
        $run = Invoke-CodexRun "$task-implement" $prompt $StageSchema
        if ($run.ExitCode -ne 0) { Stop-AndShutdown "$task Codex failed/limit. Exit $($run.ExitCode)" }
        $result = Read-JsonResult $run.AnswerPath
        try { Assert-StageHardGates $task $result $headBefore }
        catch { Stop-AndShutdown "$task hard-gate failure: $($_.Exception.Message)" }

        Write-RunLog "$task hard gates passed; independent verifier starts."
        try { Invoke-IndependentVerification $task }
        catch { Stop-AndShutdown "$task verification failed: $($_.Exception.Message)" }
        Write-RunLog "$task DONE + VERIFIED."
    }

    Write-RunLog "Completed through $FinalTask."
    if (-not $NoShutdown) {
        Write-RunLog "Shutdown in 60 sec. Cancel: shutdown /a"
        & shutdown.exe /s /t 60 /c "Serenity automation completed through $FinalTask"
    }
    Stop-Transcript | Out-Null
    exit 0
}
catch {
    Stop-AndShutdown "Unhandled orchestrator error: $($_.Exception.Message)"
}
