[CmdletBinding()]
param(
    [string]$ProjectPath,
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [int]$StepTimeoutSeconds = 3600,
    [int]$PlayerSmokeTimeoutSeconds = 360,
    [string]$EvidenceRoot
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path -Path $PSScriptRoot -Parent }
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$logPath = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Force -Path $logPath | Out-Null
if ([string]::IsNullOrWhiteSpace($EvidenceRoot)) {
    $EvidenceRoot = Join-Path $logPath ('P96-player-smoke-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
}
$EvidenceRoot = [IO.Path]::GetFullPath($EvidenceRoot)

function Wait-P96Process([Diagnostics.Process]$Process, [int]$TimeoutSeconds, [string]$Name) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while (Get-Process -Id $Process.Id -ErrorAction SilentlyContinue) {
        if ([DateTime]::UtcNow -ge $deadline) {
            Stop-Process -Id $Process.Id -Force
            throw "$Name timed out; only PID $($Process.Id) was terminated."
        }
        Start-Sleep -Milliseconds 500
    }
    try { $Process.Refresh(); return $Process.ExitCode } catch { return $null }
}

function Assert-P96Log([string]$Name, [string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "$Name log is missing: $Path" }
    $fatal = Select-String -LiteralPath $Path -Pattern @('error CS\d+', 'Scripts have compiler errors',
        'Unhandled Exception', 'executeMethod method .* threw exception', 'NullReferenceException',
        'MissingReferenceException', 'AssertionException', 'invariant failure', 'Player crash') -ErrorAction SilentlyContinue
    if ($fatal) { throw "$Name log contains a fatal diagnostic: $($fatal[0].Line)" }
}

function Invoke-P96Unity([string]$Name, [string[]]$Arguments) {
    $stepLog = Join-Path $logPath "$Name.log"
    Set-Content -LiteralPath $stepLog -Value ''
    $base = @('-batchmode', '-nographics', '-projectPath', $ProjectPath, '-logFile', $stepLog)
    $process = Start-Process -FilePath $EditorPath -ArgumentList ($base + $Arguments) -WindowStyle Hidden -PassThru
    Write-Output "$Name : PID $($process.Id)"
    $exitCode = Wait-P96Process $process $StepTimeoutSeconds $Name
    if ($null -ne $exitCode -and $exitCode -ne 0) {
        Get-Content -LiteralPath $stepLog -Tail 180
        throw "$Name failed with exit code $exitCode."
    }
    Assert-P96Log $Name $stepLog
}

function Assert-P96Tests([string]$Name, [int]$Minimum) {
    $path = Join-Path $logPath "$Name.xml"
    if (-not (Test-Path -LiteralPath $path)) { throw "$Name did not produce NUnit XML." }
    [xml]$result = Get-Content -LiteralPath $path -Raw
    $run = $result.'test-run'
    if ($run.result -ne 'Passed' -or [int]$run.passed -lt $Minimum -or
        [int]$run.failed -ne 0 -or [int]$run.skipped -ne 0) {
        throw "$Name failed (passed=$($run.passed) failed=$($run.failed) skipped=$($run.skipped))."
    }
    Write-Output "$Name : $($run.passed) passed."
}

function Invoke-P96Player([string]$Name, [int]$Width, [int]$Height) {
    $player = Join-Path $ProjectPath 'Builds\P96\Serenity.exe'
    $output = Join-Path $EvidenceRoot $Name
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    $oldSave = $env:SERENITY_SAVE_DIRECTORY
    $oldSettings = $env:SERENITY_SETTINGS_PATH
    $env:SERENITY_SAVE_DIRECTORY = Join-Path $output 'saves'
    $env:SERENITY_SETTINGS_PATH = Join-Path $output 'settings\user-settings.json'
    try {
        $playerLog = Join-Path $output 'Player.log'
        $arguments = @('-logFile', $playerLog, '-screen-fullscreen', '0', '-screen-width', $Width,
            '-screen-height', $Height, '-force-d3d11', '-p96Smoke', '-p96SmokeSeed', '96006',
            '-p96SmokeWidth', $Width, '-p96SmokeHeight', $Height, '-p96SmokeOutput', $output)
        $process = Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Normal -PassThru
        Write-Output "$Name : visible Player PID $($process.Id)"
        $exitCode = Wait-P96Process $process $PlayerSmokeTimeoutSeconds "$Name Player smoke"
        if ($null -ne $exitCode -and $exitCode -ne 0) { throw "$Name Player smoke exited $exitCode." }
        Assert-P96Log "$Name Player smoke" $playerLog
    } finally {
        $env:SERENITY_SAVE_DIRECTORY = $oldSave
        $env:SERENITY_SETTINGS_PATH = $oldSettings
    }
    $resultPath = Join-Path $output 'p96-player-smoke.json'
    if (-not (Test-Path -LiteralPath $resultPath)) { throw "$Name Player smoke result is missing." }
    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    if (-not $result.technical_passed) { throw "$Name technical smoke failed: $($result.error)" }
    if (-not $result.subjective_visual_acceptance) { throw "$Name did not pass subjective visual acceptance." }
    if ($result.character_asset_gap -or $result.aggregate_crowd_caveat -or
        -not $result.stone_age_wardrobe -or -not $result.clean_uma_prefab) {
        throw "$Name retained a character visual caveat or failed the UMA wardrobe/prefab assertions."
    }
    if ([int]$result.width -ne $Width -or [int]$result.height -ne $Height) {
        throw "$Name rendered $($result.width)x$($result.height), expected ${Width}x${Height}."
    }
    foreach ($screenshot in @('p96-new-game-editor.png', 'p96-rts-work.png', 'p96-tps-settlement.png',
            'p96-global-map.png', 'p96-rts-rain.png', 'p96-rts-winter-night.png', 'p96-rts-after-load.png')) {
        $path = Join-Path $output $screenshot
        if (-not (Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -le 0) {
            throw "$Name screenshot is missing or empty: $screenshot"
        }
    }
    Write-Output "$Name : technical and visual acceptance passed. Evidence: $output"
}

$parseErrors = $null
[Management.Automation.Language.Parser]::ParseFile($PSCommandPath, [ref]$null, [ref]$parseErrors) | Out-Null
if ($parseErrors.Count -ne 0) { throw "Verify-P96.ps1 parser errors: $($parseErrors[0].Message)" }

Invoke-P96Unity 'P96-focused-playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testCategory', 'P96',
    '-testResults', (Join-Path $logPath 'P96-focused-playmode.xml'))
Assert-P96Tests 'P96-focused-playmode' 2
Invoke-P96Unity 'P96-affected-editmode' @('-runTests', '-testPlatform', 'EditMode',
    '-testCategory', 'P84|P93|P94|P95', '-testResults', (Join-Path $logPath 'P96-affected-editmode.xml'))
Assert-P96Tests 'P96-affected-editmode' 12
Invoke-P96Unity 'P96-affected-playmode' @('-runTests', '-testPlatform', 'PlayMode',
    '-testCategory', 'P84|P93|P94|P95', '-testResults', (Join-Path $logPath 'P96-affected-playmode.xml'))
Assert-P96Tests 'P96-affected-playmode' 10
Invoke-P96Unity 'P96-build' @('-quit', '-executeMethod', 'U00Build.WindowsDevelopment',
    '-serenityBuildPath', 'Builds/P96/Serenity.exe')
$buildLog = Join-Path $logPath 'P96-build.log'
if (-not (Select-String -LiteralPath $buildLog -Pattern 'U00_BUILD_RESULT: Succeeded; errors=0' -Quiet)) {
    throw 'P96 build did not report success.'
}

Invoke-P96Player '1280x720' 1280 720
Invoke-P96Player '1600x900' 1600 900
Write-Output "P9.6 technical and visual verification passed at both resolutions. Evidence: $EvidenceRoot"
