param([string]$ProjectPath = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = 'C:\Program Files\dotnet\dotnet.exe' }
if (-not (Test-Path $dotnet)) { throw '.NET SDK required for NuGetForUnity CLI restore.' }
$previousRollForward = $env:DOTNET_ROLL_FORWARD
Push-Location $ProjectPath
try {
    # CLI 4.5.0 targets .NET 9; allow the installed .NET 10 runtime without installing another runtime.
    $env:DOTNET_ROLL_FORWARD = 'Major'
    & $dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'NuGetForUnity CLI restore failed.' }
    & $dotnet nugetforunity restore $ProjectPath
    if ($LASTEXITCODE -ne 0) { throw 'Unity NuGet package restore failed.' }
    # CLI 4.5.0 writes analyzer PluginImporter metadata without a version. Unity 6000.6
    # requires version 2. Upgrade only generated metadata, preserving import settings/GUIDs.
    foreach ($meta in Get-ChildItem (Join-Path $ProjectPath 'Assets/Packages') -Recurse -Filter '*.dll.meta') {
        $content = [IO.File]::ReadAllText($meta.FullName)
        if ($content.Contains('PluginImporter:') -and $content -notmatch 'serializedVersion:') {
            [IO.File]::WriteAllText($meta.FullName, $content.Replace('PluginImporter:', "PluginImporter:`n  serializedVersion: 2"))
        }
    }
} finally { $env:DOTNET_ROLL_FORWARD = $previousRollForward; Pop-Location }
