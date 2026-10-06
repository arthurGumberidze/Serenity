param(
    [string]$ProjectPath = (Split-Path $PSScriptRoot -Parent),
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [int]$StepTimeoutSeconds = 900,
    [switch]$FullRegression,
    [switch]$ManagedPostgres,
    [switch]$GpuValidation,
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin'
)

$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$logPath = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Force -Path $logPath | Out-Null

function Invoke-Unity([string]$Name, [string[]]$Arguments, [bool]$NoGraphics = $true) {
    $stepLog = Join-Path $logPath "$Name.log"
    $base = @('-batchmode')
    if ($NoGraphics) { $base += '-nographics' }
    $base += @('-projectPath', $ProjectPath, '-logFile', $stepLog)
    $process = Start-Process -FilePath $EditorPath -ArgumentList ($base + $Arguments) -WindowStyle Hidden -PassThru
    Write-Output "$Name : PID $($process.Id)"
    Write-Output "$Name : Unity.exe $(($base + $Arguments) -join ' ')"
    if (-not $process.WaitForExit($StepTimeoutSeconds * 1000)) {
        $process.Refresh()
        if (Test-Path -LiteralPath $stepLog) { Get-Content -LiteralPath $stepLog -Tail 120 }
        Stop-Process -Id $process.Id -Force
        throw "$Name timed out; only its launched Unity PID was terminated."
    }
    if ($process.ExitCode -ne 0) {
        if (Test-Path -LiteralPath $stepLog) { Get-Content -LiteralPath $stepLog -Tail 180 }
        throw "$Name failed (exit $($process.ExitCode)); see Logs/$Name.log."
    }
}

function Assert-TestResult([string]$Name, [int]$Minimum) {
    [xml]$result = Get-Content -LiteralPath (Join-Path $logPath "$Name.xml") -Raw
    if ($result.'test-run'.result -ne 'Passed' -or [int]$result.'test-run'.passed -lt $Minimum -or
        [int]$result.'test-run'.failed -ne 0 -or [int]$result.'test-run'.skipped -ne 0) {
        throw "$Name did not pass every required test."
    }
    Write-Output "$Name : $($result.'test-run'.passed) passed, $($result.'test-run'.failed) failed, $($result.'test-run'.skipped) skipped."
}

$u14Params = @{
    ProjectPath = $ProjectPath
    EditorPath = $EditorPath
    StepTimeoutSeconds = $StepTimeoutSeconds
    PostgresBin = $PostgresBin
    FullRegression = [bool]$FullRegression
    ManagedPostgres = [bool]$ManagedPostgres
    GpuValidation = [bool]$GpuValidation
}
& "$PSScriptRoot/Verify-U14.ps1" @u14Params

Invoke-Unity 'U15-editmode' @('-runTests','-testPlatform','EditMode','-testCategory','U15',
    '-testResults',(Join-Path $logPath 'U15-editmode.xml'))
Assert-TestResult 'U15-editmode' 13
Invoke-Unity 'U15-playmode' @('-runTests','-testPlatform','PlayMode','-testCategory','U15',
    '-testResults',(Join-Path $logPath 'U15-playmode.xml'))
Assert-TestResult 'U15-playmode' 3
Invoke-Unity 'U15-u09-editmode' @('-runTests','-testPlatform','EditMode','-testCategory','U09',
    '-testResults',(Join-Path $logPath 'U15-u09-editmode.xml'))
Assert-TestResult 'U15-u09-editmode' 1
Invoke-Unity 'U15-u09-playmode' @('-runTests','-testPlatform','PlayMode','-testCategory','U09',
    '-testResults',(Join-Path $logPath 'U15-u09-playmode.xml'))
Assert-TestResult 'U15-u09-playmode' 1

if ($GpuValidation) {
    Invoke-Unity 'U15-playmode-gpu' @('-runTests','-testPlatform','PlayMode','-testCategory','U15',
        '-testResults',(Join-Path $logPath 'U15-playmode-gpu.xml')) $false
    Assert-TestResult 'U15-playmode-gpu' 3
}

Invoke-Unity 'U15-asset-validation' @('-quit','-executeMethod','Game.Infrastructure.Editor.U05AAssetFoundation.Validate')
Invoke-Unity 'U15-build' @('-quit','-buildTarget','Win64','-executeMethod','U00Build.WindowsDevelopment')
if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath 'Builds/Windows/Serenity.exe'))) {
    throw 'Windows player missing.'
}
Write-Output 'U15 validation passed.'
