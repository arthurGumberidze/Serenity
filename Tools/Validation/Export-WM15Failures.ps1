#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$SourceDirectory,
      [Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath($SourceDirectory).TrimEnd('\')
$output = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')
if ($output -eq $source -or $output.StartsWith($source + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Export must not write inside original evidence.'
}
New-Item -ItemType Directory -Force $output | Out-Null
$report = Get-Content -LiteralPath (Join-Path $source 'results.json') -Raw | ConvertFrom-Json
$cases = @(foreach ($step in $report.steps) {
    foreach ($file in Get-ChildItem -LiteralPath $step.directory -Filter 'attempt-*.xml' -ErrorAction SilentlyContinue) {
        [xml]$xml = Get-Content -LiteralPath $file.FullName -Raw
        foreach ($case in $xml.SelectNodes('//test-case[@result="Failed"]')) {
            [pscustomobject]@{
                step=$step.name; stepStatus=$step.status; fullname=[string]$case.fullname
                message=$case.failure.message.InnerText; stack=$case.failure.'stack-trace'.InnerText
                xml=$file.FullName; sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            }
        }
    }
})
$cases | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'failed-cases.json') -Encoding UTF8
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('# Original WM15 failed cases')
$lines.Add("Source commit: $($report.sourceCommit). Failed test cases: $($cases.Count).")
foreach ($case in $cases) {
    $lines.Add(''); $lines.Add('## ' + $case.fullname)
    $lines.Add("Step: $($case.step) ($($case.stepStatus)). XML: $($case.xml)")
    $lines.Add('SHA256: ' + $case.sha256)
    $lines.Add('```text'); $lines.Add($case.message); $lines.Add($case.stack); $lines.Add('```')
}
$lines | Set-Content -LiteralPath (Join-Path $output 'failed-cases.md') -Encoding UTF8
Write-Host "Exported $($cases.Count) failed cases without modifying source evidence."
