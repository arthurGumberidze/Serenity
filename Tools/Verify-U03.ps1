param(
    [string]$ProjectPath = (Split-Path $PSScriptRoot -Parent),
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
if (-not (Test-Path -LiteralPath $EditorPath)) { throw "Editor missing: $EditorPath" }
$logPath = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Force -Path $logPath | Out-Null

function Invoke-UnityCheck([string]$Name, [string[]]$ExtraArguments) {
    $arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $ProjectPath + '"'),
        '-logFile', ('"' + (Join-Path $logPath "$Name.log") + '"')) + $ExtraArguments
    $process = Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "$Name failed with exit code $($process.ExitCode). See Logs/$Name.log" }
    Write-Output "$Name exit code: $($process.ExitCode)"
}

Invoke-UnityCheck 'U03-tests' @('-runTests', '-testPlatform', 'EditMode',
    '-testResults', ('"' + (Join-Path $logPath 'U03-tests.xml') + '"'))
[xml]$results = Get-Content -LiteralPath (Join-Path $logPath 'U03-tests.xml') -Raw
if ($results.'test-run'.result -ne 'Passed' -or [int]$results.'test-run'.passed -lt 35) {
    throw 'U03 requires all existing tests and the fifteen time tests to pass.'
}
Invoke-UnityCheck 'U03-build' @('-quit', '-buildTarget', 'Win64', '-executeMethod', 'U00Build.WindowsDevelopment')
if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath 'Builds/Windows/Serenity.exe'))) {
    throw 'Windows player output is missing.'
}
Write-Output 'U03 verification passed: 35+ EditMode tests and Windows Development build.'
