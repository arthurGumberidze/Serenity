param(
    [string]$ProjectPath=(Split-Path $PSScriptRoot -Parent),
    [string]$EditorPath='C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [ValidateRange(1,3600)][int]$WarmupSeconds=180,
    [ValidateRange(1,3600)][int]$MeasurementSeconds=900,
    [switch]$SkipBuild,
    [switch]$CaptureTrace,
    [switch]$DryRun
)
$ErrorActionPreference='Stop'
$ProjectPath=(Resolve-Path -LiteralPath $ProjectPath).Path
$out=Join-Path $ProjectPath ('Logs/U29/diagnostic-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
$player=Join-Path $ProjectPath 'Builds/Windows/Serenity.exe'
if($DryRun){Write-Output "Diagnostic only: build=$(-not $SkipBuild), population=1000, warmup=$WarmupSeconds, measurement=$MeasurementSeconds, output=$out";exit 0}
if(@(Get-Process -Name Unity,Serenity -ErrorAction SilentlyContinue).Count){throw 'Close Unity and Serenity before running diagnostics.'}
New-Item -ItemType Directory -Path $out|Out-Null
$status=Join-Path $out 'STATUS.txt'
$exitCode=0
$started=$false
if(-not ('U29DiagnosticPower' -as [type])){
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class U29DiagnosticPower {
    [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
}
'@
}
try{
    Start-Transcript -LiteralPath (Join-Path $out 'diagnostic-run.log')|Out-Null
    $started=$true
    'RUNNING'|Set-Content -LiteralPath $status
    [U29DiagnosticPower]::SetThreadExecutionState([Convert]::ToUInt32('80000003',16))|Out-Null
    Write-Output "Local diagnostic only. No model/API calls. Output: $out"
    Write-Output "Do not use the PC while Player runs. Duration: $($WarmupSeconds+$MeasurementSeconds) seconds plus build. This cannot complete U29."
    if(-not $SkipBuild){
        $log=Join-Path $out 'build.log'
        $args=@('-batchmode','-nographics','-projectPath',('"'+$ProjectPath+'"'),'-logFile',('"'+$log+'"'),'-quit','-buildTarget','Win64','-executeMethod','U00Build.WindowsDevelopment')
        $p=Start-Process -FilePath $EditorPath -ArgumentList $args -WindowStyle Hidden -PassThru
        if(-not $p.WaitForExit(1200000)){Stop-Process -Id $p.Id -Force;throw 'Diagnostic build timed out.'}
        if($p.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch 'U00_BUILD_RESULT: Succeeded; errors=0')){throw "Diagnostic build failed: $log"}
    }
    $matrix=Join-Path $out 'performance'
    $runnerFailure=$null
    try{
        & (Join-Path $PSScriptRoot 'Run-U29-Performance.ps1') -PlayerPath $player -OutputDirectory $matrix `
            -Populations 1000 -Repeats 1 -WarmupSeconds $WarmupSeconds -MeasurementSeconds $MeasurementSeconds `
            -ProcessTimeoutSeconds ($WarmupSeconds+$MeasurementSeconds+300) -Smoke -DetailedDiagnostics -CaptureTrace:$CaptureTrace
    }catch{$runnerFailure=$_}
    $repeat=Join-Path $matrix 'population-1000/repeat-1'
    # Even when integrity/runtime validation fails, salvage analysis from complete raw artifacts.
    if(Test-Path -LiteralPath (Join-Path $repeat 'diagnostic-metadata.txt')){
        & (Join-Path $PSScriptRoot 'Write-U29-FrameDiagnostics.ps1') -RepeatDirectory $repeat
    }
    if($null -ne $runnerFailure){throw $runnerFailure}
    # Require the newly built diagnostic artifacts: an old Player cannot silently pass.
    $meta=Get-Content -LiteralPath (Join-Path $repeat 'diagnostic-metadata.txt') -Raw
    if($meta -notmatch 'frames=[1-9][0-9]*'){throw 'Missing detailed diagnostic samples.'}
    if($CaptureTrace){
        $trace=Join-Path $repeat 'native-trace.raw'
        if(-not (Test-Path -LiteralPath $trace)){throw 'Missing native profiler trace.'}
        'EXPORTING_TRACE (Player closed; PC is available)'|Set-Content -LiteralPath $status
        $traceOutput=Join-Path $repeat 'native-export'
        $traceLog=Join-Path $out 'native-export.log'
        $args=@('-batchmode','-nographics','-projectPath',('"'+$ProjectPath+'"'),'-logFile',('"'+$traceLog+'"'),
            '-quit','-executeMethod','U29ProfileExport.Export','-u29ProfilePath',('"'+$trace+'"'),'-u29ProfileOutput',('"'+$traceOutput+'"'))
        $p=Start-Process -FilePath $EditorPath -ArgumentList $args -WindowStyle Hidden -PassThru
        if(-not $p.WaitForExit(300000)){Stop-Process -Id $p.Id -Force;throw 'Native trace export timed out.'}
        if($p.ExitCode -ne 0 -or -not (Select-String -LiteralPath $traceLog -SimpleMatch 'U29_PROFILE_EXPORT_COMPLETE:')){throw "Native trace export failed: $traceLog"}
    }
    'DIAGNOSTIC_COMPLETE (not U29 PASS)'|Set-Content -LiteralPath $status
    $performanceReport=Get-Content -LiteralPath (Join-Path $matrix 'U29-DIAGNOSTIC-REPORT.json') -Raw|ConvertFrom-Json
    Write-Output "Capture completed. Performance verdict: $($performanceReport.verdict) (unchanged)."
    Write-Output "Complete. Send this directory for analysis: $out"
}catch{
    $exitCode=29
    ('FAILED: '+$_.Exception.Message)|Set-Content -LiteralPath $status
    Write-Error -ErrorAction Continue $_
}finally{
    [U29DiagnosticPower]::SetThreadExecutionState([Convert]::ToUInt32('80000000',16))|Out-Null
    if($started){Stop-Transcript|Out-Null}
}
exit $exitCode
