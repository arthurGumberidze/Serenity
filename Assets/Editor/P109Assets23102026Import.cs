using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Imports only the reviewed, inert subset prepared from Assets/23102026.</summary>
public static class P109Assets23102026Import
{
    private const string Folder = "Artifacts/P109/assets-23102026-import";
    private const string NatureRoot = "Assets/3D Low Poly Nature Pack -assets model";

    public static void Import()
    {
        AssetDatabase.importPackageCompleted += _ => EditorApplication.delayCall += Finish;
        AssetDatabase.importPackageFailed += (_, error) => { Debug.LogError(error); EditorApplication.Exit(1); };
        AssetDatabase.importPackageCancelled += _ => EditorApplication.Exit(1);
        AssetDatabase.ImportPackage(Path.GetFullPath(Folder + "/ReviewedContent.unitypackage"), false);
    }

    public static void Finish()
    {
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var converted = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { NatureRoot }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null) continue;
                var texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") :
                    material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                var color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") :
                    material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
                material.shader = shader;
                material.shaderKeywords = Array.Empty<string>();
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", 0.12f);
                material.SetFloat("_Cull", 0f);
                if (texture != null && IsCutout(material.name))
                {
                    material.SetFloat("_AlphaClip", 1f);
                    material.SetFloat("_Cutoff", 0.35f);
                    material.EnableKeyword("_ALPHATEST_ON");
                }
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                converted++;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Medieval Action - FX Pack 2.0" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;
                importer.loadInBackground = true;
                importer.defaultSampleSettings = new AudioImporterSampleSettings
                {
                    compressionFormat = AudioCompressionFormat.Vorbis,
                    quality = path.Contains("ambient") || path.Contains("night") ? 0.45f : 0.65f,
                    loadType = path.Contains("ambient") || path.Contains("night")
                        ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory,
                    sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate
                };
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(Folder + "/unity-import-result.txt",
                $"Imported reviewed 23102026 subset; converted {converted} nature materials to URP. No vendor code, scenes or settings imported.");
            Debug.Log("P109_23102026_IMPORT_COMPLETE converted=" + converted);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static bool IsCutout(string name)
    {
        var value = name.ToLowerInvariant();
        return value.Contains("leaf") || value.Contains("leaves") || value.Contains("grass") ||
               value.Contains("flower") || value.Contains("plant") || value.Contains("mushroom") ||
               value.Contains("cattail") || value.Contains("lily");
    }
}
