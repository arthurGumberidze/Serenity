// Fault-injection executable for the PowerShell runner only. Never imported by Unity.
using System;
using System.IO;
using System.Threading;

internal static class WM15FakeUnity
{
    private static string Value(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, content);
    }

    private static int Main(string[] args)
    {
        if (Array.IndexOf(args, "--echo") >= 0)
        {
            foreach (var argument in args) Console.WriteLine(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(argument)));
            return 0;
        }
        if (Array.IndexOf(args, "--hang") >= 0) { Thread.Sleep(60000); return 0; }
        var childPidFile = Value(args, "--spawn-child");
        if (childPidFile != null)
        {
            var child = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                System.Reflection.Assembly.GetExecutingAssembly().Location, "--hang")
                { UseShellExecute = false, CreateNoWindow = true });
            Write(childPidFile, child.Id.ToString());
            if (Array.IndexOf(args, "--exit-parent") >= 0) return 0;
            Thread.Sleep(60000);
            return 0;
        }
        var holdFile = Value(args, "--hold-file");
        if (holdFile != null)
        {
            using (var stream = File.Open(holdFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Write(holdFile + ".ready", "ready");
                Thread.Sleep(1200);
            }
            return 0;
        }
        var log = Value(args, "-logFile");
        if (log != null) Write(log, "Fixture Unity/Player log.\n");
        var mode = Environment.GetEnvironmentVariable("WM15_FIXTURE_MODE") ?? "mixed";
        var project = Value(args, "-projectPath");
        if (project != null && (mode == "generated-report" || mode == "save-change" || mode == "save-added"))
            Write(Path.Combine(project, "protected-user-saves",
                mode == "generated-report" ? "TestResults.xml" : mode == "save-added" ? "new-save.xml" : "example.xml"), "changed by fixture");
        if (Value(args, "-executeMethod") == "U00Build.WindowsDevelopment")
        {
            if (mode == "build-fail") return 9;
            var player = Value(args, "-serenityBuildPath");
            Directory.CreateDirectory(Path.GetDirectoryName(player));
            File.Copy(System.Reflection.Assembly.GetExecutingAssembly().Location, player, false);
            var commit = new string('a', 40);
            Write(Path.Combine(Path.GetDirectoryName(player), "build-provenance.json"),
                "{\"source_commit\":\"" + commit + "\",\"unity_version\":\"6000.6.4f1\"}");
            Write(log, "U00_BUILD_RESULT: Succeeded; errors=0; warnings=0\n");
            return 0;
        }
        var output = Value(args, "-u31SmokeOutput");
        if (output != null)
        {
            Write(Path.Combine(output, "u31-player-smoke.json"),
                "{\"passed\":true,\"save_created\":true,\"load_completed\":true," +
                "\"player_character_count\":10,\"ai_character_count\":6,\"composition_root_count\":1," +
                "\"gameplay_ui_count\":1,\"ui_subscription_count\":4}");
            Write(Path.Combine(output, "u31-player-smoke.png"), "fixture-image");
            return 0;
        }
        var xml = Value(args, "-testResults");
        if (xml == null) return 0;
        var filter = Value(args, "-testFilter") ?? "";
        if (mode == "healthy" || mode == "generated-report" || mode == "save-change" || mode == "save-added") filter = "Good";
        if (filter.Contains("Crash")) return 7;
        if (filter.Contains("Hang")) { Thread.Sleep(60000); return 0; }
        if (filter.Contains("Missing")) return 0;
        if (filter.Contains("Malformed")) { Write(xml, "<test-run"); return 0; }
        if (filter.Contains("Zero"))
        {
            Write(xml, "<test-run result=\"Passed\" total=\"0\" passed=\"0\" failed=\"0\" skipped=\"0\" inconclusive=\"0\"/>");
            return 0;
        }
        var failed = filter.Contains("Failed") ? 1 : 0;
        var skipped = filter.Contains("Skipped") ? 1 : 0;
        Write(xml, "<test-run result=\"" + (failed > 0 ? "Failed" : "Passed") + "\" total=\"1\" passed=\"" +
            (1 - failed - skipped) + "\" failed=\"" + failed + "\" skipped=\"" + skipped + "\" inconclusive=\"0\"/>");
        return failed > 0 ? 2 : 0;
    }
}
