using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Environment verification only; no runtime gameplay dependencies.
public static class U00Build
{
    public static void WindowsDevelopment()
    {
        if (Application.unityVersion != "6000.6.4f1")
            throw new BuildFailedException("Use the pinned Unity 6000.6.4f1 Editor.");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            throw new BuildFailedException("Windows x64 build support is missing.");

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0 || scenes.Any(s => !File.Exists(s)))
            throw new BuildFailedException("An enabled, existing scene is required.");

        PlayerSettings.companyName = "Serenity";
        PlayerSettings.productName = "Serenity";
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        EditorSettings.serializationMode = SerializationMode.ForceText;
        AssetDatabase.SaveAssets();
        var buildPath = CommandLineValue("-serenityBuildPath") ?? "Builds/Windows/Serenity.exe";
        var buildDirectory = Path.GetDirectoryName(buildPath);
        if (string.IsNullOrWhiteSpace(buildDirectory))
            throw new BuildFailedException("The Windows build path requires a parent directory.");
        Directory.CreateDirectory(buildDirectory);
        var sourceCommit = Git("rev-parse HEAD").Trim();
        var sourceManifest = SourceManifest();
        File.WriteAllText("Logs/U30-build-source-before.txt", sourceManifest, new UTF8Encoding(false));
        var sourceFingerprint = Hash(Encoding.UTF8.GetBytes(sourceManifest));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        Debug.Log($"U00_BUILD_RESULT: {report.summary.result}; errors={report.summary.totalErrors}; warnings={report.summary.totalWarnings}");
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("U00 Windows Development build failed.");
        AssetDatabase.SaveAssets();
        var finalManifest = SourceManifest();
        File.WriteAllText("Logs/U30-build-source-after.txt", finalManifest, new UTF8Encoding(false));
        if (sourceFingerprint != Hash(Encoding.UTF8.GetBytes(finalManifest)))
            throw new BuildFailedException("Source changed during build; rebuild for immutable provenance.");
        File.WriteAllText(Path.Combine(buildDirectory, "build-provenance.json"), JsonUtility.ToJson(new BuildProvenance
        {
            source_commit = sourceCommit, source_fingerprint_sha256 = sourceFingerprint,
            build_guid = report.summary.guid.ToString(), unity_version = Application.unityVersion,
            created_utc = DateTime.UtcNow.ToString("O")
        }, true), new UTF8Encoding(false));
    }

    [Serializable]
    private sealed class BuildProvenance
    {
        public string source_commit, source_fingerprint_sha256, build_guid, unity_version, created_utc;
    }

    private static string Git(string arguments)
    {
        using (var process = Process.Start(new ProcessStartInfo("git", arguments)
        { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true }))
        {
            var text = process.StandardOutput.ReadToEnd(); process.WaitForExit();
            if (process.ExitCode != 0) throw new BuildFailedException("Git provenance query failed.");
            return text;
        }
    }

    private static string SourceManifest()
    {
        // Commit ID alone does not identify a normal uncommitted edit/test/build.
        var paths = Git("ls-files -z --cached --others --exclude-standard").Split('\0')
            .Where(x => x.StartsWith("Assets/", StringComparison.Ordinal) ||
                x.StartsWith("Packages/", StringComparison.Ordinal) || x.StartsWith("ProjectSettings/", StringComparison.Ordinal))
            .Distinct().OrderBy(x => x, StringComparer.Ordinal);
        using (var hash = new SHA256CryptoServiceProvider())
        {
            var manifest = new StringBuilder();
            foreach (var path in paths)
            {
                var digest = "deleted";
                if (File.Exists(path))
                    using (var stream = File.OpenRead(path)) digest = BitConverter.ToString(hash.ComputeHash(stream));
                manifest.Append(path).Append('|').Append(digest).Append('\n');
            }
            return manifest.ToString();
        }
    }

    private static string Hash(byte[] bytes)
    {
        using (var hash = new SHA256CryptoServiceProvider())
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
    }

    private static string CommandLineValue(string key)
    {
        var arguments = Environment.GetCommandLineArgs();
        for (var index = 0; index < arguments.Length - 1; index++)
            if (string.Equals(arguments[index], key, StringComparison.Ordinal))
                return arguments[index + 1];
        return null;
    }
}
