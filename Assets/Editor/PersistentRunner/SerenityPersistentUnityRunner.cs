using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
public static class SerenityPersistentUnityRunner
{
    private const string RunnerVersion = "1";
    private static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "SerenityPersistentRunner"));
    private static readonly string EnabledPath = Path.Combine(Root, "enabled");
    private static readonly string RequestsPath = Path.Combine(Root, "requests");
    private static readonly string StatePath = Path.Combine(Root, "state.json");
    private static readonly string ActivePath = Path.Combine(Root, "active.json");
    private static readonly string CompileFinishedPath = Path.Combine(Root, "compile-finished");
    private static readonly string CompileDiagnosticsPath = Path.Combine(Root, "compile-diagnostics.log");
    private static readonly int ProcessId = Process.GetCurrentProcess().Id;

    private static RunnerRequest active;
    private static DateTime activeStartedUtc;
    private static TestRunnerApi testApi;
    private static RunnerCallbacks testCallbacks;
    private static double nextPoll;
    private static double nextHeartbeat;

    static SerenityPersistentUnityRunner()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(RequestsPath);
        // EditorInstance.json can still describe the previous process during InitializeOnLoad.
        // Retry on the main thread; build/import workers never acquire the primary identity.
        EditorApplication.update += InitializePrimary;
    }

    private static void InitializePrimary()
    {
        if (!IsPrimaryEditorProcess()) return;
        EditorApplication.update -= InitializePrimary;
        CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
        CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
        CompilationPipeline.compilationFinished -= OnCompilationFinished;
        CompilationPipeline.compilationFinished += OnCompilationFinished;
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        RestoreActiveCommand();
    }

    private static bool IsPrimaryEditorProcess()
    {
        var instancePath = Path.Combine(Application.dataPath, "..", "Library", "EditorInstance.json");
        try
        {
            if (!File.Exists(instancePath)) return false;
            var instance = JsonUtility.FromJson<EditorInstance>(File.ReadAllText(instancePath));
            return instance != null && instance.process_id == ProcessId;
        }
        catch
        {
            return false;
        }
    }

    private static void RestoreActiveCommand()
    {
        if (!File.Exists(ActivePath)) return;
        try
        {
            active = JsonUtility.FromJson<RunnerRequest>(File.ReadAllText(ActivePath));
            if (active == null || string.IsNullOrWhiteSpace(active.id))
            {
                File.Delete(ActivePath);
                active = null;
                return;
            }

            activeStartedUtc = ParseUtc(active.started_utc, DateTime.UtcNow);
            if (active.editor_process_id != ProcessId)
            {
                FinishFailure("The Editor process ended before the command completed.", 2);
                return;
            }

            if (active.command == "test")
                RegisterTestCallbacks();
            else if (active.command != "compile")
                FinishFailure("The Editor domain reloaded during a non-resumable command.", 2);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            active = null;
        }
    }

    private static void Update()
    {
        var now = EditorApplication.timeSinceStartup;
        if (now >= nextHeartbeat)
        {
            nextHeartbeat = now + 1d;
            WriteState();
        }

        if (!File.Exists(EnabledPath) || now < nextPoll) return;
        nextPoll = now + 0.25d;

        if (active != null)
        {
            if (active.command == "compile" && (File.Exists(CompileFinishedPath) || active.compile_refresh_completed) &&
                !EditorApplication.isCompiling && !EditorApplication.isUpdating)
                FinishCompile();
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        var requestPath = Directory.GetFiles(RequestsPath, "*.json").OrderBy(path => path, StringComparer.Ordinal).FirstOrDefault();
        if (requestPath == null) return;
        try
        {
            var request = JsonUtility.FromJson<RunnerRequest>(File.ReadAllText(requestPath));
            File.Delete(requestPath);
            Begin(request);
        }
        catch (Exception exception)
        {
            File.Delete(requestPath);
            Debug.LogException(exception);
        }
    }

    private static void Begin(RunnerRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.id) || string.IsNullOrWhiteSpace(request.command))
            return;

        active = request;
        active.command = active.command.Trim().ToLowerInvariant();
        active.editor_process_id = ProcessId;
        active.started_utc = DateTime.UtcNow.ToString("O");
        activeStartedUtc = DateTime.UtcNow;
        WriteActive();
        AppendLog($"BEGIN {active.command} id={active.id}; unity={Application.unityVersion}; pid={ProcessId}");
        Debug.Log($"SERENITY_RUNNER_COMMAND_BEGIN id={active.id} command={active.command}");

        try
        {
            switch (active.command)
            {
                case "compile":
                    if (File.Exists(CompileFinishedPath)) File.Delete(CompileFinishedPath);
                    if (File.Exists(CompileDiagnosticsPath)) File.Delete(CompileDiagnosticsPath);
                    AppendLog("Refreshing the Asset Database and compiling only changed scripts.");
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    active.compile_refresh_completed = true;
                    WriteActive();
                    break;
                case "test":
                    StartTests();
                    break;
                case "build":
                    RunBuild();
                    break;
                case "shutdown":
                    RunShutdown();
                    break;
                default:
                    FinishFailure($"Unknown runner command '{active.command}'.", 2);
                    break;
            }
        }
        catch (Exception exception)
        {
            FinishFailure(exception.ToString(), 1);
        }
    }

    private static void StartTests()
    {
        if (string.IsNullOrWhiteSpace(active.xml_path))
            throw new InvalidOperationException("A test command requires xml_path.");

        Directory.CreateDirectory(Path.GetDirectoryName(active.xml_path));
        RegisterTestCallbacks();
        if (!Enum.TryParse(active.test_mode, true, out TestMode requestedMode))
            requestedMode = TestMode.EditMode;
        var filter = new Filter
        {
            testMode = requestedMode,
            testNames = EmptyToNull(active.test_names),
            groupNames = EmptyToNull(active.group_names),
            categoryNames = EmptyToNull(active.category_names),
            assemblyNames = EmptyToNull(active.assembly_names)
        };
        var settings = new ExecutionSettings(filter) { runSynchronously = false };
        var runId = testApi.Execute(settings);
        active.test_run_id = runId;
        WriteActive();
        AppendLog($"{requestedMode} test run scheduled; run_id={runId}.");
    }

    private static string[] EmptyToNull(string[] value)
    {
        return value == null || value.Length == 0 ? null : value.Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
    }

    private static void RegisterTestCallbacks()
    {
        testApi = ScriptableObject.CreateInstance<TestRunnerApi>();
        testCallbacks = new RunnerCallbacks();
        testApi.RegisterCallbacks(testCallbacks, 1000);
    }

    private static void RunBuild()
    {
        if (string.IsNullOrWhiteSpace(active.build_path))
            throw new InvalidOperationException("A build command requires build_path.");
        var buildType = Type.GetType("U00Build, Assembly-CSharp-Editor", true);
        var buildMethod = buildType.GetMethod("WindowsDevelopment", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
        if (buildMethod == null) throw new MissingMethodException("U00Build.WindowsDevelopment(string) was not found.");
        BuildReport report;
        try
        {
            report = (BuildReport)buildMethod.Invoke(null, new object[] { active.build_path });
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            throw exception.InnerException;
        }
        var summary = report.summary;
        Finish(new RunnerResult
        {
            status = summary.result == BuildResult.Succeeded ? "succeeded" : "failed",
            exit_code = summary.result == BuildResult.Succeeded ? 0 : 1,
            message = $"Build {summary.result}; errors={summary.totalErrors}; warnings={summary.totalWarnings}.",
            build_path = Path.GetFullPath(active.build_path),
            errors = (int)summary.totalErrors,
            warnings = (int)summary.totalWarnings
        });
    }

    private static void RunShutdown()
    {
        var quit = active.quit_editor;
        if (File.Exists(EnabledPath)) File.Delete(EnabledPath);
        Finish(new RunnerResult { status = "succeeded", exit_code = 0, message = quit ? "Runner stopped; owned Editor will exit." : "Runner stopped; Editor remains open." });
        if (quit) EditorApplication.delayCall += () => EditorApplication.Exit(0);
    }

    private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
    {
        if (active == null || active.command != "compile") return;
        var lines = messages.Select(message => string.Format(CultureInfo.InvariantCulture,
            "{0}|{1}|{2}|{3}|{4}", message.type, message.file, message.line, message.column,
            (message.message ?? string.Empty).Replace("\r", " ").Replace("\n", " "))).ToArray();
        if (lines.Length > 0) File.AppendAllLines(CompileDiagnosticsPath, lines, new UTF8Encoding(false));
    }

    private static void OnCompilationFinished(object context)
    {
        if (active == null || active.command != "compile") return;
        File.WriteAllText(CompileFinishedPath, DateTime.UtcNow.ToString("O"), new UTF8Encoding(false));
    }

    private static void FinishCompile()
    {
        var lines = File.Exists(CompileDiagnosticsPath) ? File.ReadAllLines(CompileDiagnosticsPath) : Array.Empty<string>();
        var errors = lines.Count(line => line.StartsWith("Error|", StringComparison.Ordinal));
        if (EditorUtility.scriptCompilationFailed && errors == 0) errors = 1;
        var warnings = lines.Count(line => line.StartsWith("Warning|", StringComparison.Ordinal));
        foreach (var line in lines) AppendLog(line);
        Finish(new RunnerResult
        {
            status = errors == 0 ? "succeeded" : "failed",
            exit_code = errors == 0 ? 0 : 1,
            message = $"Compilation finished; errors={errors}; warnings={warnings}.",
            errors = errors,
            warnings = warnings
        });
    }

    private static void FinishFailure(string message, int exitCode)
    {
        Finish(new RunnerResult { status = "failed", exit_code = exitCode, message = message, errors = 1 });
    }

    private static void Finish(RunnerResult result)
    {
        if (active == null) return;
        CleanupTestCallbacks();
        result.id = active.id;
        result.command = active.command;
        result.runner_version = RunnerVersion;
        result.unity_version = Application.unityVersion;
        result.editor_process_id = ProcessId;
        result.started_utc = active.started_utc;
        result.finished_utc = DateTime.UtcNow.ToString("O");
        result.duration_seconds = Math.Max(0d, (DateTime.UtcNow - activeStartedUtc).TotalSeconds);
        result.result_path = active.result_path;
        result.log_path = active.log_path;
        if (string.IsNullOrWhiteSpace(result.xml_path)) result.xml_path = active.xml_path;
        if (string.IsNullOrWhiteSpace(result.build_path)) result.build_path = active.build_path;
        AppendLog($"END {active.command} status={result.status}; exit_code={result.exit_code}; message={result.message}");
        Debug.Log($"SERENITY_RUNNER_COMMAND_END id={active.id} command={active.command} status={result.status} exit={result.exit_code}");
        WriteJsonAtomic(active.result_path, result);
        var responsePath = Path.Combine(Root, "responses", active.id + ".json");
        WriteJsonAtomic(responsePath, result);
        active = null;
        if (File.Exists(ActivePath)) File.Delete(ActivePath);
        if (File.Exists(CompileFinishedPath)) File.Delete(CompileFinishedPath);
        WriteState();
    }

    private static void CleanupTestCallbacks()
    {
        if (testApi != null && testCallbacks != null)
            testApi.UnregisterCallbacks(testCallbacks);
        if (testApi != null)
            UnityEngine.Object.DestroyImmediate(testApi);
        testApi = null;
        testCallbacks = null;
    }

    private static void WriteActive()
    {
        WriteJsonAtomic(ActivePath, active);
    }

    private static void AppendLog(string message)
    {
        if (active == null || string.IsNullOrWhiteSpace(active.log_path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(active.log_path));
        File.AppendAllText(active.log_path, $"{DateTime.UtcNow:O} {message}{Environment.NewLine}", new UTF8Encoding(false));
    }

    private static void WriteState()
    {
        if (!File.Exists(EnabledPath)) return;
        WriteJsonAtomic(StatePath, new RunnerState
        {
            runner_version = RunnerVersion,
            project_path = Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
            unity_version = Application.unityVersion,
            process_id = ProcessId,
            ready = !EditorApplication.isCompiling && !EditorApplication.isUpdating,
            busy = active != null,
            active_command_id = active?.id,
            active_command = active?.command,
            heartbeat_utc = DateTime.UtcNow.ToString("O")
        });
    }

    private static void WriteJsonAtomic<T>(string path, T value)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temp, JsonUtility.ToJson(value, true), new UTF8Encoding(false));
        if (File.Exists(path)) File.Delete(path);
        File.Move(temp, path);
    }

    private static DateTime ParseUtc(string value, DateTime fallback)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result) ? result.ToUniversalTime() : fallback;
    }

    [Serializable]
    private sealed class EditorInstance
    {
        public int process_id;
    }

    [Serializable]
    private sealed class RunnerRequest
    {
        public string id;
        public string command;
        public string created_utc;
        public string started_utc;
        public string result_path;
        public string log_path;
        public string xml_path;
        public string build_path;
        public string[] test_names;
        public string[] group_names;
        public string[] category_names;
        public string[] assembly_names;
        public string test_mode;
        public bool quit_editor;
        public bool compile_refresh_completed;
        public int editor_process_id;
        public string test_run_id;
    }

    [Serializable]
    private sealed class RunnerState
    {
        public string runner_version;
        public string project_path;
        public string unity_version;
        public int process_id;
        public bool ready;
        public bool busy;
        public string active_command_id;
        public string active_command;
        public string heartbeat_utc;
    }

    [Serializable]
    private sealed class RunnerResult
    {
        public string id;
        public string command;
        public string status;
        public int exit_code;
        public string message;
        public string runner_version;
        public string unity_version;
        public int editor_process_id;
        public string started_utc;
        public string finished_utc;
        public double duration_seconds;
        public string result_path;
        public string log_path;
        public string xml_path;
        public string build_path;
        public int passed;
        public int failed;
        public int skipped;
        public int inconclusive;
        public int errors;
        public int warnings;
    }

    private sealed class RunnerCallbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun)
        {
            if (active == null || active.command != "test") return;
            AppendLog($"Test run started; selected_test_cases={testsToRun.TestCaseCount}.");
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            if (active == null || active.command != "test") return;
            try
            {
                TestRunnerApi.SaveResultToFile(result, active.xml_path);
                if (!File.Exists(active.xml_path) || new FileInfo(active.xml_path).Length == 0)
                    throw new IOException("The Unity Test Framework did not write the requested NUnit XML result.");
                var total = result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount;
                var succeeded = result.FailCount == 0 && result.InconclusiveCount == 0 && total > 0;
                Finish(new RunnerResult
                {
                    status = succeeded ? "succeeded" : "failed",
                    exit_code = succeeded ? 0 : 1,
                    message = $"{(string.IsNullOrWhiteSpace(active.test_mode) ? "EditMode" : active.test_mode)} tests finished; total={total}; passed={result.PassCount}; failed={result.FailCount}; skipped={result.SkipCount}; inconclusive={result.InconclusiveCount}.",
                    xml_path = Path.GetFullPath(active.xml_path),
                    passed = result.PassCount,
                    failed = result.FailCount,
                    skipped = result.SkipCount,
                    inconclusive = result.InconclusiveCount,
                    errors = result.FailCount + result.InconclusiveCount
                });
            }
            catch (Exception exception)
            {
                FinishFailure(exception.ToString(), 1);
            }
        }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (active == null || active.command != "test" || !string.Equals(result.TestStatus.ToString(), "Failed", StringComparison.Ordinal)) return;
            AppendLog($"FAILED {result.FullName}: {result.Message}");
        }
    }
}
