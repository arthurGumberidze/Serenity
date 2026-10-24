#requires -Version 5.1
[CmdletBinding()]
param([switch]$DryRun)

$ErrorActionPreference = 'Stop'
$project = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$original = Join-Path $project 'Artifacts/WM15/night-20261024-002730-4c6d7c'
$output = Join-Path $project ('Artifacts/WM15/performance-repair-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6))
Write-Host "Performance repair report: $output"
& (Join-Path $PSScriptRoot 'Run-WM15.ps1') -ProjectPath $project -RepairFrom $original `
    -RepairStage Performance -RunDirectory $output -BusyWaitMinutes 0 -MaxHours 3 -DryRun:$DryRun
exit $LASTEXITCODE
