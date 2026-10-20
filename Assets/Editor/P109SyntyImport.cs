using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Explicit, scoped vendor import audit. Never opens/saves gameplay scenes.</summary>
public static class P109SyntyImport
{
    private const string Root = "Assets/PolygonNatureBiomes";

    public static void ImportAndAudit()
    {
        if (Directory.Exists(Root))
            throw new InvalidOperationException("Refusing to overwrite an existing vendor import.");
        // ImportPackage is queued. In batch mode -quit would terminate before the
        // import runs; finish only after the explicit completion event instead.
        AssetDatabase.importPackageCompleted += OnImported;
        AssetDatabase.importPackageFailed += OnFailed;
        AssetDatabase.importPackageCancelled += OnCancelled;
        AssetDatabase.ImportPackage(Path.GetFullPath(
            "Artifacts/P109/synty-import/MeadowForest-URP-reviewed.unitypackage"), false);
    }

    private static void OnImported(string package)
    {
        EditorApplication.delayCall += () =>
        {
            try { Audit(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        };
    }

    private static void OnFailed(string package, string error)
    {
        Debug.LogError("P109_SYNTY_IMPORT_FAILED: " + error);
        EditorApplication.Exit(1);
    }

    private static void OnCancelled(string package) => OnFailed(package, "Cancelled");

    public static void UpgradeImportedMaterialsAndAudit()
    {
        var upgraders = MaterialUpgrader.FetchAllUpgradersForPipeline(typeof(UniversalRenderPipelineAsset));
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Root }))
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            var name = material.shader.name;
            if (name.StartsWith("SyntyStudios/", StringComparison.Ordinal) ||
                name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal)) continue;
            if (name == "Standard")
            {
                var message = "";
                if (!MaterialUpgrader.Upgrade(material, upgraders, MaterialUpgrader.UpgradeFlags.None, ref message))
                    throw new InvalidOperationException("Cannot upgrade " + material.name + ": " + message);
            }
            else if (name.StartsWith("Legacy Shaders/Particles/Additive", StringComparison.Ordinal) ||
                     name == "Unlit/Transparent")
            {
                bool additive = name.Contains("Additive");
                var texture = material.GetTexture("_MainTex");
                var scale = material.GetTextureScale("_MainTex");
                var offset = material.GetTextureOffset("_MainTex");
                var tint = material.HasProperty("_TintColor") ? material.GetColor("_TintColor") : Color.white;
                material.shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                material.shaderKeywords = Array.Empty<string>();
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", scale);
                material.SetTextureOffset("_BaseMap", offset);
                material.SetColor("_BaseColor", tint);
                material.SetFloat("_Surface", 1);
                material.SetFloat("_Blend", additive ? 2 : 0);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                material.SetFloat("_ZWrite", 0);
                material.SetFloat("_Cull", (float)CullMode.Off);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
                material.SetShaderPassEnabled("ShadowCaster", false);
            }
            else throw new InvalidOperationException("Unreviewed legacy material: " + material.name + " / " + name);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }
        Audit();
    }

    public static void BuildVerification()
    {
        Audit();
        // Include the demo only in this diagnostic build so its meshes/shaders are
        // exercised by the build pipeline without changing saved build settings.
        var scenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path)
            .Concat(new[] { Root + "/PNB_Meadow_Forest/Scene/Demo.unity" }).ToArray();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/P109SyntyImportVerification/Serenity.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        Debug.Log($"P109_SYNTY_BUILD: {report.summary.result}; errors={report.summary.totalErrors}; warnings={report.summary.totalWarnings}; guid={report.summary.guid}");
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("Synty import verification build failed.");
    }

    public static void Audit()
    {
        if (Application.unityVersion != "6000.6.4f1")
            throw new InvalidOperationException("Use the pinned Unity Editor.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var paths = AssetDatabase.FindAssets("", new[] { Root })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x).ToArray();
        var materials = paths.Where(x => x.EndsWith(".mat"))
            .Select(AssetDatabase.LoadAssetAtPath<Material>).ToArray();
        var report = new Report
        {
            unityVersion = Application.unityVersion,
            prefabCount = paths.Count(x => x.EndsWith(".prefab")),
            modelCount = paths.Count(x => x.EndsWith(".fbx")),
            materialCount = materials.Length,
            shaders = materials.Where(x => x != null).GroupBy(x => x.shader != null ? x.shader.name : "MISSING")
                .Select(x => x.Key + ": " + x.Count()).OrderBy(x => x).ToArray(),
            missingMaterials = materials.Count(x => x == null || x.shader == null),
            shaderMessages = paths.Where(x => x.EndsWith(".shader"))
                .Select(AssetDatabase.LoadAssetAtPath<Shader>).Where(x => x != null)
                .SelectMany(x => ShaderUtil.GetShaderMessages(x).Select(m => x.name + ": " + m.severity + ": " + m.message))
                .ToArray(),
            enabledScenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray()
        };
        Directory.CreateDirectory("Artifacts/P109/synty-import");
        File.WriteAllText("Artifacts/P109/synty-import/unity-audit.json", JsonUtility.ToJson(report, true));
        Debug.Log("P109_SYNTY_IMPORT_AUDIT " + JsonUtility.ToJson(report));
        if (report.prefabCount < 190 || report.modelCount < 226 || report.missingMaterials != 0)
            throw new InvalidOperationException("Incomplete Synty import; see unity-audit.json.");
        if (report.shaderMessages.Any(x => x.Contains(": Error:")))
            throw new InvalidOperationException("Imported shader errors; see unity-audit.json.");
        if (report.enabledScenes.Any(x => x.StartsWith(Root, StringComparison.Ordinal)))
            throw new InvalidOperationException("Vendor demo must not replace gameplay startup.");
    }

    [Serializable]
    private sealed class Report
    {
        public string unityVersion;
        public int prefabCount, modelCount, materialCount, missingMaterials;
        public string[] shaders, shaderMessages, enabledScenes;
    }
}
