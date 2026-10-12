param(
    [string]$ProjectPath,
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [int]$StepTimeoutSeconds = 1200,
    [switch]$SkipU29Regression
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path -Path $PSScriptRoot -Parent }
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$logPath = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Force -Path $logPath | Out-Null

function Assert-U30CleanLog([string]$Name, [string]$Path) {
    $patterns = @('error CS\d+', 'Scripts have compiler errors', 'Unhandled Exception',
        'NullReferenceException', 'AssertionException', 'Native Collection.*leak', 'Player crash')
    $fatal = Select-String -LiteralPath $Path -Pattern $patterns -ErrorAction SilentlyContinue
    if ($fatal) { throw "$Name log contains a fatal diagnostic: $($fatal[0].Line)" }
}

function Invoke-U30Unity([string]$Name, [string[]]$Arguments) {
    $stepLog = Join-Path $logPath "$Name.log"
    $base = @('-batchmode', '-nographics', '-projectPath', $ProjectPath, '-logFile', $stepLog)
    $process = Start-Process -FilePath $EditorPath -ArgumentList ($base + $Arguments) -WindowStyle Hidden -PassThru
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
    Assert-U30CleanLog $Name $stepLog
}

function Assert-U30TestResult([string]$Name, [int]$Minimum) {
    $path = Join-Path $logPath "$Name.xml"
    if (-not (Test-Path -LiteralPath $path)) { throw "$Name did not produce NUnit XML." }
    [xml]$result = Get-Content -LiteralPath $path -Raw
    if ($result.'test-run'.result -ne 'Passed' -or [int]$result.'test-run'.passed -lt $Minimum -or
        [int]$result.'test-run'.failed -ne 0 -or [int]$result.'test-run'.skipped -ne 0) {
        throw "$Name did not pass every required test."
    }
    Write-Output "$Name : $($result.'test-run'.passed) passed."
}

$parseErrors = $null
[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Run-Soak-U30.ps1'),
    [ref]$null, [ref]$parseErrors) | Out-Null
if ($parseErrors.Count -ne 0) { throw "Run-Soak-U30.ps1 parser errors: $($parseErrors[0].Message)" }

& (Join-Path $PSScriptRoot 'Test-U30Runner.ps1') -ProjectPath $ProjectPath
if ($LASTEXITCODE -ne 0) { throw 'U30 runner protocol tests failed.' }

Invoke-U30Unity 'U30-compile' @('-quit')
Invoke-U30Unity 'U30-editmode' @('-runTests', '-testPlatform', 'EditMode', '-testCategory', 'U30',
    '-testResults', (Join-Path $logPath 'U30-editmode.xml'))
Assert-U30TestResult 'U30-editmode' 26
Invoke-U30Unity 'U30-playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testCategory', 'U30',
    '-testResults', (Join-Path $logPath 'U30-playmode.xml'))
Assert-U30TestResult 'U30-playmode' 7

if (-not $SkipU29Regression) {
    & (Join-Path $PSScriptRoot 'Verify-U29.ps1') -ProjectPath $ProjectPath -EditorPath $EditorPath `
        -StepTimeoutSeconds $StepTimeoutSeconds -BuildPlayer
} else {
    Invoke-U30Unity 'U30-build' @('-quit', '-buildTarget', 'Win64', '-executeMethod', 'U00Build.WindowsDevelopment')
}

$player = Join-Path $ProjectPath 'Builds\Windows\Serenity.exe'
if (-not (Test-Path -LiteralPath $player)) { throw 'U30 verification did not produce the Windows player.' }
Write-Output 'U30 preparation verification passed.'
