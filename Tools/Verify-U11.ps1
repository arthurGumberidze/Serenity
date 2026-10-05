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
        $activity = if (Test-Path -LiteralPath $stepLog) { (Get-Item -LiteralPath $stepLog).LastWriteTime } else { 'no log' }
        Write-Warning "$Name timed out; PID=$($process.Id); responding=$($process.Responding); last log activity=$activity"
        if (Test-Path -LiteralPath $stepLog) { Get-Content -LiteralPath $stepLog -Tail 100 }
        Stop-Process -Id $process.Id -Force
        throw "$Name timed out; only its launched Unity PID was terminated."
    }
    if ($process.ExitCode -ne 0) {
        if (Test-Path -LiteralPath $stepLog) { Get-Content -LiteralPath $stepLog -Tail 100 }
        throw "$Name failed (exit $($process.ExitCode)); see Logs/$Name.log."
    }
}

function Invoke-BoundedProcess([string]$Name, [string]$FilePath, [string[]]$Arguments, [int]$TimeoutSeconds = 120) {
    $stdout = Join-Path $logPath "$Name.stdout.log"
    $stderr = Join-Path $logPath "$Name.stderr.log"
    $process = Start-Process -FilePath $FilePath -ArgumentList $Arguments -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        Stop-Process -Id $process.Id -Force
        throw "$Name timed out; only PID $($process.Id) was terminated."
    }
    if ($process.ExitCode -ne 0) {
        if (Test-Path -LiteralPath $stdout) { Get-Content -LiteralPath $stdout -Tail 50 }
        if (Test-Path -LiteralPath $stderr) { Get-Content -LiteralPath $stderr -Tail 50 }
        throw "$Name failed (exit $($process.ExitCode))."
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

$environmentKeys = @('DYNASTYGAME_PG_HOST','DYNASTYGAME_PG_PORT','DYNASTYGAME_PG_DATABASE',
    'DYNASTYGAME_PG_TEST_DATABASE','DYNASTYGAME_PG_USER','DYNASTYGAME_PG_PASSWORD','SERENITY_PG_EXPECT_FRESH','PGPASSWORD')
$previousEnvironment = @{}
foreach ($key in $environmentKeys) { $previousEnvironment[$key] = [Environment]::GetEnvironmentVariable($key) }
$clusterStarted = $false
$clusterPath = $null

try {
    if ($FullRegression) {
        Invoke-Unity 'U11-core-tests' @('-runTests','-testPlatform','EditMode','-testCategory','!PostgresIntegration',
            '-testResults',(Join-Path $logPath 'U11-core-tests.xml'))
        Assert-TestResult 'U11-core-tests' 173
        Invoke-Unity 'U11-all-playmode' @('-runTests','-testPlatform','PlayMode',
            '-testResults',(Join-Path $logPath 'U11-all-playmode.xml'))
        Assert-TestResult 'U11-all-playmode' 21
    }

    Invoke-Unity 'U11-editmode' @('-runTests','-testPlatform','EditMode','-testCategory','U11',
        '-testResults',(Join-Path $logPath 'U11-editmode.xml'))
    Assert-TestResult 'U11-editmode' 11
    Invoke-Unity 'U11-playmode' @('-runTests','-testPlatform','PlayMode','-testCategory','U11',
        '-testResults',(Join-Path $logPath 'U11-playmode.xml'))
    Assert-TestResult 'U11-playmode' 3
    Invoke-Unity 'U11-u10-editmode' @('-runTests','-testPlatform','EditMode','-testCategory','U10',
        '-testResults',(Join-Path $logPath 'U11-u10-editmode.xml'))
    Assert-TestResult 'U11-u10-editmode' 14
    Invoke-Unity 'U11-u10-playmode' @('-runTests','-testPlatform','PlayMode','-testCategory','U10',
        '-testResults',(Join-Path $logPath 'U11-u10-playmode.xml'))
    Assert-TestResult 'U11-u10-playmode' 5
    Invoke-Unity 'U11-u09-editmode' @('-runTests','-testPlatform','EditMode','-testCategory','U09',
        '-testResults',(Join-Path $logPath 'U11-u09-editmode.xml'))
    Assert-TestResult 'U11-u09-editmode' 10
    Invoke-Unity 'U11-u09-playmode' @('-runTests','-testPlatform','PlayMode','-testCategory','U09',
        '-testResults',(Join-Path $logPath 'U11-u09-playmode.xml'))
    Assert-TestResult 'U11-u09-playmode' 4

    if ($ManagedPostgres) {
        & "$PSScriptRoot/Restore-NuGet.ps1" -ProjectPath $ProjectPath
        $clusterPath = Join-Path $ProjectPath ('.local/pg-u11-' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Force -Path $clusterPath | Out-Null
        $env:DYNASTYGAME_PG_HOST = '127.0.0.1'
        $env:DYNASTYGAME_PG_DATABASE = 'dynasty_game_dev'
        $env:DYNASTYGAME_PG_TEST_DATABASE = 'dynasty_game_test'
        $env:DYNASTYGAME_PG_USER = 'u11_' + [Guid]::NewGuid().ToString('N')
        $env:DYNASTYGAME_PG_PASSWORD = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
        $env:PGPASSWORD = $env:DYNASTYGAME_PG_PASSWORD
        $passwordFile = Join-Path $clusterPath 'init-password.local.config'
        [IO.File]::WriteAllText($passwordFile, $env:DYNASTYGAME_PG_PASSWORD, [Text.UTF8Encoding]::new($false))
        try {
            Invoke-BoundedProcess 'U11-initdb' "$PostgresBin/initdb.exe" @('-D',(Join-Path $clusterPath 'data'),
                '-U',$env:DYNASTYGAME_PG_USER,'--auth=scram-sha-256','--encoding=UTF8','--locale=C',"--pwfile=$passwordFile") 180
        } finally { Remove-Item -LiteralPath $passwordFile -ErrorAction SilentlyContinue }
        $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
        $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
        $env:DYNASTYGAME_PG_PORT = [string]$port
        Invoke-BoundedProcess 'U11-pg-start' "$PostgresBin/pg_ctl.exe" @('-D',(Join-Path $clusterPath 'data'),
            '-l',(Join-Path $logPath 'U11-postgres-server.log'),'-o',('"-h 127.0.0.1 -p ' + $port + '"'),'-w','start') 120
        $clusterStarted = $true
        Invoke-BoundedProcess 'U11-createdb' "$PostgresBin/createdb.exe" @('-h','127.0.0.1','-p',[string]$port,
            '-U',$env:DYNASTYGAME_PG_USER,'dynasty_game_test') 120
        $env:SERENITY_PG_EXPECT_FRESH = '1'
        Invoke-Unity 'U11-postgres-tests' @('-runTests','-testPlatform','EditMode','-testCategory','PostgresIntegration',
            '-testResults',(Join-Path $logPath 'U11-postgres-tests.xml'))
        Assert-TestResult 'U11-postgres-tests' 25
    }

    Invoke-Unity 'U11-asset-validation' @('-quit','-executeMethod','Game.Infrastructure.Editor.U05AAssetFoundation.Validate')
    if ($GpuValidation) {
        Invoke-Unity 'U11-playmode-gpu' @('-runTests','-testPlatform','PlayMode','-testCategory','U11',
            '-testResults',(Join-Path $logPath 'U11-playmode-gpu.xml')) $false
        Assert-TestResult 'U11-playmode-gpu' 3
    }
    Invoke-Unity 'U11-build' @('-quit','-buildTarget','Win64','-executeMethod','U00Build.WindowsDevelopment')
    if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath 'Builds/Windows/Serenity.exe'))) {
        throw 'Windows player missing.'
    }
    Write-Output 'U11 validation passed.'
}
finally {
    if ($clusterStarted -and $clusterPath) {
        try {
            Invoke-BoundedProcess 'U11-pg-stop' "$PostgresBin/pg_ctl.exe" @('-D',(Join-Path $clusterPath 'data'),'-m','fast','-w','stop') 120
        } catch { Write-Warning $_ }
    }
    foreach ($key in $environmentKeys) { [Environment]::SetEnvironmentVariable($key, $previousEnvironment[$key]) }
}
