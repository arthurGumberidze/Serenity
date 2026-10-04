param(
    [string]$ProjectPath = (Split-Path $PSScriptRoot -Parent),
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [switch]$ManagedTestCluster
)

$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$logPath = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Force -Path $logPath | Out-Null

if (Test-Path -LiteralPath (Join-Path $ProjectPath 'Assets/serenity_games_assets')) {
    throw 'Forbidden bulk staging copy detected under Assets.'
}

$required = @(
    'docs/ASSET_REGISTRY.md',
    'docs/MANUAL_ASSET_DOWNLOADS.md',
    'docs/STONE_AGE_ASSET_GAPS.md',
    'Assets/Scenes/Development/StoneAgeAssetGallery.unity'
)
foreach ($relativePath in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath $relativePath))) {
        throw "Required U05A artifact is missing: $relativePath"
    }
}

$generateArgs = @(
    '-batchmode', '-nographics', '-projectPath', ('"' + $ProjectPath + '"'),
    '-logFile', ('"' + (Join-Path $logPath 'U05A-generate.log') + '"'),
    '-quit', '-executeMethod', 'Game.Infrastructure.Editor.U05AAssetFoundation.Build'
)
$generate = Start-Process $EditorPath -ArgumentList $generateArgs -WindowStyle Hidden -Wait -PassThru
if ($generate.ExitCode -ne 0) { throw "U05A generation failed (exit $($generate.ExitCode)); see Logs/U05A-generate.log." }

& "$PSScriptRoot/Verify-U05.ps1" -ProjectPath $ProjectPath -EditorPath $EditorPath -ManagedTestCluster:$ManagedTestCluster
if ($LASTEXITCODE -ne 0) { throw 'U05 regression gate failed.' }

[xml]$editResult = Get-Content (Join-Path $logPath 'U04A-core-tests.xml') -Raw
if ($editResult.'test-run'.result -ne 'Passed' -or [int]$editResult.'test-run'.passed -lt 96 -or [int]$editResult.'test-run'.skipped -ne 0) {
    throw 'U05A EditMode suite did not pass all required tests.'
}

$validateArgs = @(
    '-batchmode', '-nographics', '-projectPath', ('"' + $ProjectPath + '"'),
    '-logFile', ('"' + (Join-Path $logPath 'U05A-validate.log') + '"'),
    '-quit', '-executeMethod', 'Game.Infrastructure.Editor.U05AAssetFoundation.Validate'
)
$validate = Start-Process $EditorPath -ArgumentList $validateArgs -WindowStyle Hidden -Wait -PassThru
if ($validate.ExitCode -ne 0) { throw "U05A validation failed (exit $($validate.ExitCode)); see Logs/U05A-validate.log." }

Write-Output "U05A-editmode-tests : $($editResult.'test-run'.passed) passed, $($editResult.'test-run'.failed) failed, $($editResult.'test-run'.skipped) skipped."
Write-Output 'U05A validation passed: scoped asset generation, all inherited tests, PostgreSQL integration, LocalGameplay PlayMode tests, and Windows Mono Development build.'
