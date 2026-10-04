param(
    [string]$ProjectPath = (Split-Path $PSScriptRoot -Parent),
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [switch]$ManagedTestCluster,
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin'
)
$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
& "$PSScriptRoot/Restore-NuGet.ps1" -ProjectPath $ProjectPath
$logPath = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Force $logPath | Out-Null
$environmentKeys = @('DYNASTYGAME_PG_HOST','DYNASTYGAME_PG_PORT','DYNASTYGAME_PG_DATABASE',
    'DYNASTYGAME_PG_TEST_DATABASE','DYNASTYGAME_PG_USER','DYNASTYGAME_PG_PASSWORD','SERENITY_PG_EXPECT_FRESH','PGPASSWORD')
$previousEnvironment = @{}
foreach ($key in $environmentKeys) { $previousEnvironment[$key] = [Environment]::GetEnvironmentVariable($key) }
$clusterStarted = $false
function Run-Unity([string]$Name, [string[]]$Extra) {
    $arguments = @('-batchmode','-nographics','-projectPath',('"'+$ProjectPath+'"'),'-logFile',('"'+(Join-Path $logPath "$Name.log")+'"')) + $Extra
    $process = Start-Process $EditorPath -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "$Name failed (exit $($process.ExitCode)); see Logs/$Name.log." }
}
function Check-Tests([string]$Name, [int]$Minimum) {
    [xml]$result = Get-Content (Join-Path $logPath "$Name.xml") -Raw
    if ($result.'test-run'.result -ne 'Passed' -or [int]$result.'test-run'.passed -lt $Minimum -or [int]$result.'test-run'.skipped -ne 0) {
        throw "$Name did not pass all required tests."
    }
    Write-Output "$Name : $($result.'test-run'.passed) passed, $($result.'test-run'.failed) failed, $($result.'test-run'.skipped) skipped."
}
try {
    if ($ManagedTestCluster) {
        # Always a fresh, private loopback cluster; never start, stop or delete a user's cluster.
        $clusterPath = Join-Path $ProjectPath ('.local/pg-u04a-' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Force $clusterPath | Out-Null
        $env:DYNASTYGAME_PG_HOST = '127.0.0.1'
        $env:DYNASTYGAME_PG_DATABASE = 'dynasty_game_dev'
        $env:DYNASTYGAME_PG_TEST_DATABASE = 'dynasty_game_test'
        $env:DYNASTYGAME_PG_USER = 'u04a_' + [Guid]::NewGuid().ToString('N')
        $env:DYNASTYGAME_PG_PASSWORD = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
        $env:PGPASSWORD = $env:DYNASTYGAME_PG_PASSWORD
        $passwordFile = Join-Path $clusterPath 'init-password.local.config'
        [IO.File]::WriteAllText($passwordFile, $env:DYNASTYGAME_PG_PASSWORD, [Text.UTF8Encoding]::new($false))
        try {
            & "$PostgresBin/initdb.exe" -D "$clusterPath/data" -U $env:DYNASTYGAME_PG_USER --auth=scram-sha-256 --encoding=UTF8 --locale=C --pwfile=$passwordFile *> (Join-Path $logPath 'U04A-initdb.log')
            if ($LASTEXITCODE -ne 0) { throw 'Private test cluster initialization failed; see Logs/U04A-initdb.log.' }
        } finally { Remove-Item -LiteralPath $passwordFile -ErrorAction SilentlyContinue }
        $started = $false
        for ($attempt = 1; $attempt -le 5 -and -not $started; $attempt++) {
            $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
            $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
            $env:DYNASTYGAME_PG_PORT = [string]$port
            $pgArgs = @('-D',('"'+$clusterPath+'/data"'),'-l',('"'+$logPath+'/U04A-postgres.log"'),'-o',('"-h 127.0.0.1 -p '+$port+'"'),'-w','start')
            $pgProcess = Start-Process "$PostgresBin/pg_ctl.exe" -ArgumentList $pgArgs -WindowStyle Hidden -PassThru
            $pgProcess.WaitForExit() # Unlike Start-Process -Wait, this does not wait for the detached server child.
            $started = $pgProcess.ExitCode -eq 0
        }
        if (-not $started) { throw 'Private test PostgreSQL failed to start after five loopback-port attempts.' }
        $clusterStarted = $true
        & "$PostgresBin/createdb.exe" -h 127.0.0.1 -p $port -U $env:DYNASTYGAME_PG_USER dynasty_game_test
        if ($LASTEXITCODE -ne 0) { throw 'Separate test database creation failed.' }
        $env:SERENITY_PG_EXPECT_FRESH = '1'
        Write-Output 'Fresh local SCRAM-authenticated PostgreSQL test cluster ready (credentials omitted).'
    }
    Run-Unity 'U04A-core-tests' @('-runTests','-testPlatform','EditMode','-testCategory','!PostgresIntegration','-testResults',('"'+$logPath+'/U04A-core-tests.xml"'))
    Check-Tests 'U04A-core-tests' 86
    $configured = -not [string]::IsNullOrWhiteSpace($env:DYNASTYGAME_PG_TEST_DATABASE)
    if ($configured) {
        Run-Unity 'U04A-postgres-tests' @('-runTests','-testPlatform','EditMode','-testCategory','PostgresIntegration','-testResults',('"'+$logPath+'/U04A-postgres-tests.xml"'))
        Check-Tests 'U04A-postgres-tests' 25
    } else { Write-Warning 'PostgreSQL integration NOT CONFIGURED; U04A cannot be marked DONE. Use -ManagedTestCluster or set environment variables.' }
    Run-Unity 'U04A-build' @('-quit','-buildTarget','Win64','-executeMethod','U00Build.WindowsDevelopment')
    if (-not (Test-Path "$ProjectPath/Builds/Windows/Serenity.exe")) { throw 'Windows player missing.' }
    if (-not $configured) { throw 'Core/build complete, but mandatory PostgreSQL validation remains unverified.' }
    Write-Output 'U04A validation passed: core tests, real PostgreSQL tests, Windows Mono Development build.'
} finally {
    if ($clusterStarted) {
        $stop = Start-Process "$PostgresBin/pg_ctl.exe" -ArgumentList @('-D',('"'+$clusterPath+'/data"'),'-m','fast','-w','stop') -WindowStyle Hidden -Wait -PassThru
        if ($stop.ExitCode -ne 0) { Write-Warning 'Private test cluster did not stop; inspect its ignored .local directory.' }
    }
    foreach ($key in $environmentKeys) { [Environment]::SetEnvironmentVariable($key, $previousEnvironment[$key]) }
}
