Set-StrictMode -Version 2

function ConvertTo-Wm15Argument([AllowEmptyString()][string]$Value) {
    # Windows CommandLineToArgvW/CRT quoting, including embedded quotes and trailing backslashes.
    return '"' + [regex]::Replace([regex]::Replace($Value, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
}

function Write-Wm15Text([string]$Path, [string]$Text) {
    $temporary = $Path + '.tmp'
    for ($attempt=0; $attempt -lt 15; $attempt++) {
        try {
            [IO.File]::WriteAllText($temporary, $Text, [Text.UTF8Encoding]::new($false))
            if ([IO.File]::Exists($Path)) {
                try { [IO.File]::Replace($temporary, $Path, [System.Management.Automation.Language.NullString]::Value) }
                catch {
                    # Some AV/virtualized filesystems deny replace/delete but allow overwrite.
                    [IO.File]::Copy($temporary, $Path, $true)
                    # The fallback temp file is harmless and reused on the next update.
                }
            } else { [IO.File]::Move($temporary, $Path) }
            return
        } catch {
            if ($attempt -eq 14) { throw }
            Start-Sleep -Milliseconds 150
        }
    }
}

function Write-Wm15Json([string]$Path, [object]$Value) {
    Write-Wm15Text $Path ($Value | ConvertTo-Json -Depth 30)
}

function Add-Wm15Text([string]$Path, [string]$Text) {
    for ($attempt=0; $attempt -lt 15; $attempt++) {
        try { [IO.File]::AppendAllText($Path, $Text, [Text.UTF8Encoding]::new($false)); return }
        catch { if ($attempt -eq 14) { throw }; Start-Sleep -Milliseconds 150 }
    }
}

function Get-Wm15Sha256([string]$Path) {
    # Avoid PSModulePath differences when Windows PowerShell is launched from PowerShell 7.
    $hash = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($Path)
    try { return [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-', '') }
    finally { $stream.Dispose(); $hash.Dispose() }
}

function Test-Wm15ProjectBusy([string]$ProjectPath) {
    $lockPath = Join-Path $ProjectPath 'Temp/UnityLockfile'
    if (-not [IO.File]::Exists($lockPath)) { return $false }
    try {
        $handle = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $handle.Dispose()
        return $false
    } catch [IO.IOException] { return $true }
    catch [UnauthorizedAccessException] { return $true }
}

function Stop-Wm15OwnedProcess([Diagnostics.Process]$Process, [long]$StartedTicks) {
    if (-not ('Wm15OwnedProcessTree' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
public static class Wm15OwnedProcessTree
{
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Auto)]
    private struct Entry
    {
        public uint size, usage, pid; public IntPtr heap; public uint module, threads, parent;
        public int priority; public uint flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=260)] public string exe;
    }
    [DllImport("kernel32.dll", SetLastError=true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet=CharSet.Auto)] private static extern bool Process32First(IntPtr snapshot, ref Entry entry);
    [DllImport("kernel32.dll", CharSet=CharSet.Auto)] private static extern bool Process32Next(IntPtr snapshot, ref Entry entry);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    public static void Stop(int root, long startedTicks)
    {
        var parents = new Dictionary<int,int>();
        var snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot == new IntPtr(-1)) throw new InvalidOperationException("Cannot enumerate owned descendants.");
        try
        {
            var entry = new Entry { size=(uint)Marshal.SizeOf(typeof(Entry)) };
            if (Process32First(snapshot, ref entry))
                do { parents[(int)entry.pid]=(int)entry.parent; } while(Process32Next(snapshot, ref entry));
        } finally { CloseHandle(snapshot); }
        var owned = new List<int> { root };
        for (var i=0; i<owned.Count; i++)
            foreach(var pair in parents)
                if(pair.Value==owned[i] && !owned.Contains(pair.Key)) owned.Add(pair.Key);
        for (var i=owned.Count-1; i>=0; i--)
        {
            try
            {
                using(var process=Process.GetProcessById(owned[i]))
                    if(!process.HasExited && process.StartTime.ToUniversalTime().Ticks >= startedTicks)
                        process.Kill();
            }
            catch(ArgumentException) { } // Exited between enumeration and opening.
        }
    }
}
'@
    }
    [Wm15OwnedProcessTree]::Stop($Process.Id, $StartedTicks)
    if (-not $Process.WaitForExit(10000)) { throw "Could not stop owned PID $($Process.Id)." }
}

function Invoke-Wm15Process {
    param(
        [string]$FilePath, [string[]]$Arguments, [string]$WorkingDirectory,
        [string]$OutputPrefix, [int]$TimeoutSeconds, [int]$ProgressSeconds = 30,
        [scriptblock]$OnProgress, [hashtable]$Environment = @{}, [switch]$Visible
    )
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $process = $null
    $startedTicks = 0L
    $oldEnvironment = @{}
    try {
        foreach ($key in $Environment.Keys) {
            $oldEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
            [Environment]::SetEnvironmentVariable($key, [string]$Environment[$key], 'Process')
        }
        $quoted = @($Arguments | ForEach-Object { ConvertTo-Wm15Argument $_ }) -join ' '
        $process = Start-Process -FilePath $FilePath -ArgumentList $quoted -WorkingDirectory $WorkingDirectory `
            -RedirectStandardOutput ($OutputPrefix + '.stdout.log') `
            -RedirectStandardError ($OutputPrefix + '.stderr.log') `
            -WindowStyle $(if ($Visible) { 'Normal' } else { 'Hidden' }) -PassThru
        # Windows PowerShell 5.1 can lose ExitCode after a fast process exits unless its handle is retained.
        $null = $process.Handle
        $startedTicks = $process.StartTime.ToUniversalTime().Ticks
        $nextUpdate = 0.0
        while (-not $process.HasExited) {
            if ($timer.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
                # Only the process created above and its descendants; never kill an existing Editor.
                Stop-Wm15OwnedProcess $process $startedTicks
                return [pscustomobject]@{ ExitCode = $null; TimedOut = $true; Seconds = $timer.Elapsed.TotalSeconds }
            }
            if ($timer.Elapsed.TotalSeconds -ge $nextUpdate) {
                if ($OnProgress) { & $OnProgress $timer.Elapsed.TotalSeconds $process.Id }
                $nextUpdate = $timer.Elapsed.TotalSeconds + $ProgressSeconds
            }
            Start-Sleep -Milliseconds 250
            $process.Refresh()
        }
        # A crashed wrapper can leave a Player holding redirected pipes open. Reap descendants even
        # when the wrapper exited normally; never use unbounded WaitForExit() on inherited pipes.
        Stop-Wm15OwnedProcess $process $startedTicks
        return [pscustomobject]@{ ExitCode = $process.ExitCode; TimedOut = $false; Seconds = $timer.Elapsed.TotalSeconds }
    } finally {
        foreach ($key in $oldEnvironment.Keys) {
            [Environment]::SetEnvironmentVariable($key, $oldEnvironment[$key], 'Process')
        }
        if ($null -ne $process) {
            if (-not $process.HasExited) {
                Stop-Wm15OwnedProcess $process $startedTicks
            }
            $process.Dispose()
        }
    }
}

function Read-Wm15NUnit([string]$Path) {
    if (-not [IO.File]::Exists($Path)) { throw "Missing NUnit XML: $Path" }
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $reader = [Xml.XmlReader]::Create($Path, $settings)
    try { $document = [Xml.XmlDocument]::new(); $document.Load($reader) } finally { $reader.Dispose() }
    $root = $document.DocumentElement
    if ($root.Name -ne 'test-run') { throw 'Expected NUnit 3 test-run root.' }
    foreach ($attribute in @('total', 'passed', 'failed', 'skipped', 'inconclusive', 'result')) {
        if (-not $root.HasAttribute($attribute)) { throw "NUnit XML lacks $attribute." }
    }
    $counts = @{}
    foreach ($name in @('total', 'passed', 'failed', 'skipped', 'inconclusive')) {
        $count = 0
        if (-not [int]::TryParse($root.GetAttribute($name), [ref]$count) -or $count -lt 0) {
            throw "Invalid NUnit count: $name"
        }
        $counts[$name] = $count
    }
    if ($counts.total -eq 0 -or $counts.total -ne ($counts.passed + $counts.failed + $counts.skipped + $counts.inconclusive)) {
        throw 'NUnit XML is empty or has inconsistent totals.'
    }
    $status = if ($counts.failed -gt 0 -or $root.GetAttribute('result') -in @('Failed', 'Cancelled')) { 'FAIL' }
        elseif ($counts.skipped -gt 0 -or $counts.inconclusive -gt 0) { 'PARTIAL' }
        elseif ($root.GetAttribute('result') -eq 'Passed' -and $counts.passed -eq $counts.total) { 'PASS' }
        else { 'FAIL' }
    return [pscustomobject]@{ Status = $status; Counts = $counts; Detail = "tests=$($counts.total) passed=$($counts.passed) failed=$($counts.failed) skipped=$($counts.skipped) inconclusive=$($counts.inconclusive)" }
}

function Get-Wm15Fixtures([string]$ProjectPath) {
    foreach ($relative in @('Assets/Game/Tests/Editor', 'Assets/Tests/Editor', 'Assets/Game/Tests/PlayMode')) {
        $directory = Join-Path $ProjectPath $relative
        if (-not (Test-Path -LiteralPath $directory)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $directory -Filter '*.cs' -Recurse -File | Sort-Object FullName) {
            $source = [IO.File]::ReadAllText($file.FullName)
            $namespace = [regex]::Match($source, '(?m)^\s*namespace\s+([\w.]+)')
            foreach ($match in [regex]::Matches($source, '(?m)^\s*public\s+(?:(?:sealed|partial|abstract)\s+)*class\s+(\w*Tests)\b')) {
                if ($match.Value -match '\babstract\b') { continue }
                $name = $match.Groups[1].Value
                if ($namespace.Success) { $name = $namespace.Groups[1].Value + '.' + $name }
                [pscustomobject]@{
                    Platform = $(if ($relative -like '*PlayMode') { 'PlayMode' } else { 'EditMode' })
                    Filter = $name
                    Source = $file.FullName
                }
            }
        }
    }
}

function Get-Wm15LogProblems([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return @('Unity/Player log is missing.') }
    # Test logs contain intentionally asserted exceptions; NUnit owns test verdicts. This scan is for builds/Player only.
    return @(Select-String -LiteralPath $Path -Pattern 'error CS\d+|Scripts have compiler errors|Unhandled Exception|NullReferenceException|MissingReferenceException|AssertionException|Native Collection.*leak|Crash!!!|Fatal error' |
        Select-Object -First 8 | ForEach-Object { $_.Line.Trim() })
}

Export-ModuleMember -Function *-Wm15*
