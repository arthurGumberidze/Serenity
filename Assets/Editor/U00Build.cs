using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

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
        Directory.CreateDirectory("Builds/Windows");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/Windows/Serenity.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        Debug.Log($"U00_BUILD_RESULT: {report.summary.result}; errors={report.summary.totalErrors}; warnings={report.summary.totalWarnings}");
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("U00 Windows Development build failed.");
    }
}
