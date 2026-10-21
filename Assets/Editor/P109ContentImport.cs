using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit art-only batch import; never changes project settings or gameplay scenes.</summary>
public static class P109ContentImport
{
    [Serializable] private sealed class Entry { public string path; public string package; }
    [Serializable] private sealed class Manifest { public Entry[] admitted; }
    private static string Folder => "Artifacts/P109/compact-world/" +
        (Environment.GetCommandLineArgs().Contains("-p109BundleArt") ? "bundle/" : "");

    public static void Import()
    {
        AssetDatabase.importPackageCompleted += _ => EditorApplication.delayCall += Finish;
        AssetDatabase.importPackageFailed += (_, error) => { Debug.LogError(error); EditorApplication.Exit(1); };
        AssetDatabase.importPackageCancelled += _ => EditorApplication.Exit(1);
        AssetDatabase.ImportPackage(Path.GetFullPath(Folder + "ReviewedContent.unitypackage"), false);
    }

    public static void Finish()
    {
        try
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder + "prepared-content.json"));
            var count = 0;
            foreach (var entry in manifest.admitted.Where(x => x.path.EndsWith(".mat")))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(entry.path);
                if (material == null) throw new InvalidOperationException("Missing material " + entry.path);
                // Convert only newly admitted materials; the existing Meadow/Core shaders stay untouched.
                if (material.shader != null && material.shader.name.StartsWith("Universal Render Pipeline/")) continue;
                var serialized = new SerializedObject(material);
                var textures = serialized.FindProperty("m_SavedProperties.m_TexEnvs");
                Texture main = null;
                for (var i = 0; textures != null && i < textures.arraySize; i++)
                {
                    var element = textures.GetArrayElementAtIndex(i);
                    var name = element.FindPropertyRelative("first").stringValue;
                    if (name == "_MainTex" || name == "_BaseMap" || name == "_BaseColorMap")
                        main ??= element.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                }
                var color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") :
                    material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                material.shader = Shader.Find("Universal Render Pipeline/Lit");
                material.shaderKeywords = Array.Empty<string>();
                material.SetTexture("_BaseMap", main);
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", 0.15f);
                material.SetFloat("_Cull", 0f);
                if (main != null)
                {
                    material.SetFloat("_AlphaClip", 1);
                    material.SetFloat("_Cutoff", 0.35f);
                    material.EnableKeyword("_ALPHATEST_ON");
                }
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                count++;
            }
            Debug.Log("P109_CONTENT_IMPORTED " + manifest.admitted.Length + "; converted materials=" + count);
            File.WriteAllText(Folder + "unity-import-result.txt", "Imported " + manifest.admitted.Length +
                " art entries. Converted " + count + " newly imported materials to URP. No vendor scripts/settings/scenes imported.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
