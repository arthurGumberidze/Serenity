using System;
using System.Collections.Generic;
using System.Linq;
using Game.Presentation.Environment;
using UnityEditor;
using UnityEngine;

public static class P109StarterNatureAuthoring
{
    private const string Vendor = "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/";
    private const string Output = "Assets/Game/Art/StarterNature";
    public static void BuildPlayable()
    {
        if (Application.unityVersion != "6000.6.4f1") throw new InvalidOperationException("Pinned Editor required");
        var catalog = UnityEngine.Resources.Load<StarterNatureCatalog>("World/StarterNature");
        if (catalog == null || catalog.Trees.Length != 4) throw new InvalidOperationException("Generate starter assets first");
        var scenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray();
        if (scenes.Any(x => x.StartsWith("Assets/PolygonNatureBiomes")))
            throw new InvalidOperationException("Vendor demo is not a gameplay scene");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/P109SyntyPlayable/Serenity.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        Debug.Log($"P109_SYNTY_PLAYABLE_BUILD: {report.summary.result}; errors={report.summary.totalErrors}; " +
            $"warnings={report.summary.totalWarnings}; guid={report.summary.guid}");
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new UnityEditor.Build.BuildFailedException("Playable build failed");
    }

    public static void Generate()
    {
        System.IO.Directory.CreateDirectory(Output);
        AssetDatabase.Refresh();
        var materials = new Dictionary<Material, Material>();
        var prefabs = new List<GameObject>();
        foreach (var name in new[] { "SM_Env_Tree_Meadow_01", "SM_Env_Tree_Meadow_02",
                     "SM_Env_Tree_Birch_01", "SM_Env_Tree_Birch_02",
                     "SM_Env_Rock_Small_01", "SM_Env_Grass_Tall_Clump_01" })
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Vendor + "Prefabs/" + name + ".prefab");
            if (source == null) throw new InvalidOperationException(name);
            var root = new GameObject(name + "_Stable");
            var group = source.GetComponentInChildren<LODGroup>();
            var renderers = group != null ? group.GetLODs()[0].renderers : source.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var part = new GameObject(renderer.name);
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = source.transform.InverseTransformPoint(renderer.transform.position);
                part.transform.localRotation = Quaternion.Inverse(source.transform.rotation) * renderer.transform.rotation;
                part.transform.localScale = renderer.transform.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials.Select(original =>
                {
                    if (materials.TryGetValue(original, out var existing)) return existing;
                    var path = Output + "/" + original.name + "_Stable.mat";
                    var stable = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (stable == null) { stable = new Material(original); AssetDatabase.CreateAsset(stable, path); }
                    stable.CopyPropertiesFromMaterial(original);
                    stable.enableInstancing = true;
                    foreach (var property in new[] { "_TotalWindAmount", "_WindAmount", "_JitterAmount",
                                 "_LeafBigNoiseAmount", "_LOD_FADE_CROSSFADE_out", "_LOD_FADE_CROSSFADE_in" })
                        if (stable.HasProperty(property)) stable.SetFloat(property, 0f);
                    stable.DisableKeyword("LOD_FADE_CROSSFADE");
                    EditorUtility.SetDirty(stable);
                    materials.Add(original, stable);
                    return stable;
                }).ToArray();
            }
            prefabs.Add(PrefabUtility.SaveAsPrefabAsset(root, Output + "/" + root.name + ".prefab"));
            UnityEngine.Object.DestroyImmediate(root);
        }
        const string catalogPath = "Assets/Game/Resources/World/StarterNature.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<StarterNatureCatalog>(catalogPath);
        if (catalog == null) { catalog = ScriptableObject.CreateInstance<StarterNatureCatalog>(); AssetDatabase.CreateAsset(catalog, catalogPath); }
        catalog.Trees = prefabs.Take(4).ToArray();
        catalog.ShoreProps = prefabs.Skip(4).ToArray();
        catalog.GroundLayers = new[] { "Grass_01", "Grass_Leaves_01", "Rock_Moss_01", "Grass_Clover_01", "Mud_Gravel_01" }
            .Select(name => AssetDatabase.LoadAssetAtPath<TerrainLayer>(Vendor + "Terrain/Terrain_Meadow_" + name + ".terrainlayer")).ToArray();
        if (catalog.GroundLayers.Any(x => x == null)) throw new InvalidOperationException("Missing terrain layer");
        var water = AssetDatabase.LoadAssetAtPath<Material>(Vendor + "Materials/Water_Lake_01.mat");
        catalog.WaterRipples = water.GetTexture("_DistortionMap");
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("P109_STARTER_NATURE_READY: " + prefabs.Count + " stable mesh tree variants");
    }
}
