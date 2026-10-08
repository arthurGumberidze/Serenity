SERENITY NIGHT AUTOMATION v3
============================

PURPOSE
-------
Runs the remaining Stone Age MVP roadmap sequentially with a fresh Codex CLI process for implementation and independent verification of each task.

Order:
U25 manual verification (when StartTask=U25)
U26 strategic AI
U27 gameplay UI
U28 deterministic events
U29 performance gates
U30 three-phase real soak
U30A pre-U31 FRS compliance patch
U31 MVP integration gate

SAFETY / HARD GATES
-------------------
- No push.
- Dirty tree, failed test/build, bad commit, malformed structured result, verifier rejection or non-zero Codex exit stops the chain.
- Each completed implementation is checked by a fresh verifier.
- Implementation and verifier must leave the repository clean.
- Logs are stored OUTSIDE the Git repository by default under:
  %LOCALAPPDATA%\SerenityAutomation\runs\<timestamp>\
- On an error/stop Windows shutdown is scheduled in 120 seconds; after final success it is scheduled in 60 seconds. `-NoShutdown` disables both.

PREREQUISITE
------------
The automation package itself must be committed (or live outside the repository), and the repository must be clean before a real run. Do not keep an untracked copy under C:\serenity_game.
Codex CLI must be installed, authenticated and available on PATH. On Windows the orchestrator prefers codex.cmd so the npm codex.ps1 shim is not blocked by PowerShell Execution Policy.

Before any agent starts, the orchestrator reads `codex exec --help`, selects the installed CLI's supported unattended approval mode (`--approve-for-me` on codex-cli 0.160.0, with legacy `--sandbox workspace-write --ask-for-approval never` fallback), and validates the complete common argument set in a captured child process against an intentionally absent schema. Capturing native stderr prevents the expected diagnostic from becoming a terminating PowerShell error. This catches conflicting flags without model inference. `--approve-for-me` already selects workspace-write in 0.160.0 and must not be combined with an explicit `--sandbox`. Unsupported CLI versions stop before U26.

PROMPTS
-------
prompts\U26_PROMPT.txt
prompts\U27_PROMPT.txt
prompts\U28_PROMPT.txt
prompts\U29_PROMPT.txt
prompts\U30_PROMPT.txt
prompts\U30A_PROMPT.txt
prompts\U31_PROMPT.txt

The repository TASK_GRAPH/NEXT_TASK remain authoritative. Prompt files constrain implementation and acceptance details.

DRY RUN
-------
cd C:\serenity_game\serenity_automation\serenity_night_automation_v3
Set-ExecutionPolicy -Scope Process Bypass
.\Run-Serenity-Night.ps1 -DryRun -NoShutdown

REAL RUN WITHOUT AUTO SHUTDOWN
------------------------------
.\Run-Serenity-Night.ps1 -NoShutdown

NIGHT RUN
---------
.\Run-Serenity-Night.ps1

RESUME FROM A TASK
------------------
.\Run-Serenity-Night.ps1 -StartTask U28 -NoShutdown
.\Run-Serenity-Night.ps1 -StartTask U30A -NoShutdown

U25 WAIT
--------
The default StartTask is U25. U25 is not implemented by this script; it waits for an existing committed U25 and independently verifies it. Default wait is 2 hours.

.\Run-Serenity-Night.ps1 -WaitForU25Hours 3

U30 REAL EIGHT-HOUR SOAK
------------------------
U30 is intentionally three-phase:

1. Codex implements the harness, passes the short soak/build, commits a clean U30 preparation state and leaves U30 IN_PROGRESS.
2. The orchestrator runs Tools\Run-Soak-U30.ps1 as an ordinary PowerShell/Unity process for at least 8 wall-clock hours. Codex is not active during this time, so the soak itself does not consume Codex tokens.
3. After the structured result passes, a fresh Codex finalizer audits the evidence, marks U30 DONE and commits.

Override only to make the run longer:
.\Run-Serenity-Night.ps1 -U30SoakHours 10

Values below 8 are rejected for a real run.

MODEL
-----
By default the script uses the Codex CLI configured/default model with high reasoning effort.

.\Run-Serenity-Night.ps1 -Model "<model-id>"

CUSTOM EXTERNAL LOG ROOT
------------------------
.\Run-Serenity-Night.ps1 -RunBase "D:\SerenityAutomationRuns"

Each Codex run stores prompt.txt, trace.jsonl, stderr.log and answer.json. U30 external soak stores its logs, checkpoints and soak-result.json under the same external run root.

SHUTDOWN
--------
Cancel a scheduled shutdown with:
shutdown /a

IMPORTANT
---------
The script automates Codex CLI, not desktop chat creation. Every `codex exec` is a fresh agent run. The external U30 soak deliberately runs without Codex.
Current compatibility validation: codex-cli 0.160.0 uses `--approve-for-me`; the removed `--ask-for-approval` spelling must not be hard-coded for that version.
