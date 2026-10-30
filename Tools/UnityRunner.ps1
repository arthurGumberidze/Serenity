[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('start', 'status', 'compile', 'test', 'build', 'stop')]
    [string]$Action = 'status',
    [string]$ProjectPath,
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [string]$OutputPath,
    [string]$BuildPath,
    [string[]]$TestNames,
    [string[]]$GroupNames,
    [string[]]$CategoryNames,
    [string[]]$AssemblyNames,
    [int]$TimeoutSeconds = 900,
    [switch]$QuitOwnedEditor
)

$ErrorActionPreference = 'Stop'
$expectedUnityVersion = '6000.6.4f1'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Split-Path -Path $PSScriptRoot -Parent
}
$ProjectPath = [IO.Path]::GetFullPath($ProjectPath)
$runnerRoot = Join-Path $ProjectPath 'Library\SerenityPersistentRunner'
$requestRoot = Join-Path $runnerRoot 'requests'
$responseRoot = Join-Path $runnerRoot 'responses'
$statePath = Join-Path $runnerRoot 'state.json'
$enabledPath = Join-Path $runnerRoot 'enabled'
$launcherPath = Join-Path $runnerRoot 'launcher.json'
$editorLogPath = Join-Path $ProjectPath 'Logs\PersistentUnityRunner\Editor.log'
$editorInstancePath = Join-Path $ProjectPath 'Library\EditorInstance.json'
$projectLockPath = Join-Path $ProjectPath 'Temp\UnityLockfile'
$script:CommandExitCode = 2

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    try { return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json } catch { return $null }
}

function Test-RunnerState($State) {
    if (-not (Test-Path -LiteralPath $enabledPath) -or $null -eq $State -or $State.unity_version -ne $expectedUnityVersion) { return $false }
    if ([IO.Path]::GetFullPath([string]$State.project_path).TrimEnd('\') -ne $ProjectPath.TrimEnd('\')) { return $false }
    try {
        if ($State.heartbeat_utc -is [DateTime]) { $heartbeat = $State.heartbeat_utc.ToUniversalTime() }
        else { $heartbeat = [DateTime]::Parse([string]$State.heartbeat_utc, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime() }
    } catch { return $false }
    if (((Get-Date).ToUniversalTime() - $heartbeat).TotalSeconds -gt 8) { return $false }
    $instance = Get-ProjectEditorInstance
    if ($null -eq $instance -or [int]$instance.process_id -ne [int]$State.process_id) { return $false }
    return $true
}

function Get-ProjectEditorInstance {
    $instance = Read-JsonFile $editorInstancePath
    if ($null -eq $instance -or [string]$instance.version -ne $expectedUnityVersion) { return $null }
    $editorProcess = Get-Process -Id ([int]$instance.process_id) -ErrorAction SilentlyContinue
    if ($null -eq $editorProcess -or $editorProcess.ProcessName -ne 'Unity') { return $null }
    return $instance
}

function Wait-Runner([int]$Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    do {
        $state = Read-JsonFile $statePath
        if (Test-RunnerState $state) { return $state }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    return $null
}

function Start-Runner {
    New-Item -ItemType Directory -Force -Path $runnerRoot, $requestRoot, $responseRoot, (Split-Path $editorLogPath -Parent) | Out-Null
    [IO.File]::WriteAllText($enabledPath, ([DateTime]::UtcNow.ToString('O')), [Text.UTF8Encoding]::new($false))
    $state = Wait-Runner 5
    if (Test-RunnerState $state) { return $state }

    $existing = Get-ProjectEditorInstance
    if ($null -eq $existing -and -not (Test-Path -LiteralPath $projectLockPath)) {
        if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity Editor was not found at '$UnityPath'." }
        $versionFile = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
        if ((Get-Content -LiteralPath $versionFile -Raw) -notmatch [Regex]::Escape($expectedUnityVersion)) {
            throw "ProjectVersion.txt is not pinned to Unity $expectedUnityVersion."
        }
        $process = Start-Process -FilePath $UnityPath -ArgumentList @(
            '-projectPath', $ProjectPath,
            '-logFile', $editorLogPath,
            '-serenityPersistentRunner'
        ) -WindowStyle Hidden -PassThru
        [pscustomobject]@{
            process_id = $process.Id
            project_path = $ProjectPath
            started_utc = [DateTime]::UtcNow.ToString('O')
        } | ConvertTo-Json | Set-Content -LiteralPath $launcherPath -Encoding utf8NoBOM
        Write-Host "Started Unity $expectedUnityVersion runner process $($process.Id)."
    } else {
        $existingPid = if ($null -ne $existing) { $existing.process_id } else { 'unknown' }
        Write-Host "Waiting for the already open project Editor process $existingPid to activate the runner."
    }

    $state = Wait-Runner $TimeoutSeconds
    if (-not (Test-RunnerState $state)) {
        throw "The persistent Unity runner did not become ready within $TimeoutSeconds seconds. See '$editorLogPath'."
    }
    return $state
}

function New-CommandOutput([string]$CommandName) {
    if ([string]::IsNullOrWhiteSpace($OutputPath)) {
        $stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss-fff')
        return Join-Path $ProjectPath "Artifacts\PersistentUnityRunner\$stamp-$CommandName"
    }
    if ([IO.Path]::IsPathRooted($OutputPath)) { return [IO.Path]::GetFullPath($OutputPath) }
    return [IO.Path]::GetFullPath((Join-Path $ProjectPath $OutputPath))
}

function Submit-Command([string]$CommandName) {
    $state = Start-Runner
    $commandOutput = New-CommandOutput $CommandName
    New-Item -ItemType Directory -Force -Path $commandOutput, $requestRoot, $responseRoot | Out-Null
    $id = ([DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff') + '-' + [Guid]::NewGuid().ToString('N'))
    $resultPath = Join-Path $commandOutput 'result.json'
    $logPath = Join-Path $commandOutput 'command.log'
    $xmlPath = if ($CommandName -eq 'test') { Join-Path $commandOutput 'test-results.xml' } else { $null }
    $resolvedBuildPath = $null
    if ($CommandName -eq 'build') {
        if ([string]::IsNullOrWhiteSpace($BuildPath)) {
            $resolvedBuildPath = Join-Path $commandOutput 'Serenity.exe'
        } elseif ([IO.Path]::IsPathRooted($BuildPath)) {
            $resolvedBuildPath = [IO.Path]::GetFullPath($BuildPath)
        } else {
            $resolvedBuildPath = [IO.Path]::GetFullPath((Join-Path $ProjectPath $BuildPath))
        }
    }
    foreach ($oldEvidence in @($resultPath, $logPath, $xmlPath)) {
        if (-not [string]::IsNullOrWhiteSpace($oldEvidence) -and (Test-Path -LiteralPath $oldEvidence)) {
            Remove-Item -LiteralPath $oldEvidence -Force
        }
    }

    $request = [ordered]@{
        id = $id
        command = $CommandName
        created_utc = [DateTime]::UtcNow.ToString('O')
        result_path = $resultPath
        log_path = $logPath
        xml_path = $xmlPath
        build_path = $resolvedBuildPath
        test_names = @($TestNames)
        group_names = @($GroupNames)
        category_names = @($CategoryNames)
        assembly_names = @($AssemblyNames)
        quit_editor = [bool]($CommandName -eq 'shutdown' -and $QuitOwnedEditor)
    }
    $tempPath = Join-Path $requestRoot ($id + '.tmp')
    $requestPath = Join-Path $requestRoot ($id + '.json')
    $request | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $tempPath -Encoding utf8NoBOM
    Move-Item -LiteralPath $tempPath -Destination $requestPath
    Write-Host "Queued $CommandName command $id on Editor process $($state.process_id)."

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        if (Test-Path -LiteralPath $resultPath) {
            $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
            $result | ConvertTo-Json -Depth 8
            $script:CommandExitCode = [int]$result.exit_code
            return
        }
        $responsePath = Join-Path $responseRoot ($id + '.json')
        if (Test-Path -LiteralPath $responsePath) {
            $result = Get-Content -LiteralPath $responsePath -Raw | ConvertFrom-Json
            $result | ConvertTo-Json -Depth 8
            $script:CommandExitCode = [int]$result.exit_code
            return
        }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)

    $timeoutResult = [ordered]@{
        id = $id
        command = $CommandName
        status = 'timeout'
        exit_code = 2
        message = "No authoritative Editor response arrived within $TimeoutSeconds seconds."
        editor_process_id = $state.process_id
        finished_utc = [DateTime]::UtcNow.ToString('O')
        log_path = $logPath
        xml_path = $xmlPath
        build_path = $resolvedBuildPath
    }
    $timeoutResult | ConvertTo-Json | Set-Content -LiteralPath $resultPath -Encoding utf8NoBOM
    $timeoutResult | ConvertTo-Json -Depth 5
    $script:CommandExitCode = 2
}

switch ($Action) {
    'start' {
        $state = Start-Runner
        $state | ConvertTo-Json -Depth 5
        exit 0
    }
    'status' {
        $state = Read-JsonFile $statePath
        if (Test-RunnerState $state) { $state | ConvertTo-Json -Depth 5; exit 0 }
        [pscustomobject]@{ ready = $false; message = 'No live persistent Unity runner.' } | ConvertTo-Json
        exit 1
    }
    'compile' { Submit-Command 'compile'; exit $script:CommandExitCode }
    'test' { Submit-Command 'test'; exit $script:CommandExitCode }
    'build' { Submit-Command 'build'; exit $script:CommandExitCode }
    'stop' {
        $launcher = Read-JsonFile $launcherPath
        $state = Read-JsonFile $statePath
        if ($QuitOwnedEditor -and ($null -eq $launcher -or $null -eq $state -or [int]$launcher.process_id -ne [int]$state.process_id)) {
            throw 'Refusing to quit an Editor that was not launched by this runner. Omit -QuitOwnedEditor to disable only the runner.'
        }
        Submit-Command 'shutdown'
        exit $script:CommandExitCode
    }
}
