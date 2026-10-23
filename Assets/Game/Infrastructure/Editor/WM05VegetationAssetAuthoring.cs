using System;
using System.IO;
using System.Linq;
using Game.Presentation.Environment;
using UnityEditor;
using UnityEngine;

public static class WM05VegetationAssetAuthoring
{
    private const string Vendor = "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/";
    private const string StableMaterials = "Assets/Game/Art/StarterNature/";
    private const string Output = "Assets/Game/Art/WorldMapV2/Vegetation";

    public static void Generate()
    {
        if (Application.unityVersion != "6000.6.4f1")
            throw new InvalidOperationException("WM05 requires the pinned Unity Editor.");
        Directory.CreateDirectory(Output);
        AssetDatabase.Refresh();

        var trees = new[]
        {
            "SM_Env_Tree_Meadow_01", "SM_Env_Tree_Meadow_02",
            "SM_Env_Tree_Birch_01", "SM_Env_Tree_Birch_02"
        }.Select(CreateColliderFreeMeshLodPrefab).ToArray();
        var shrubs = Load("SM_Env_Bush_01", "SM_Env_Bush_02", "SM_Env_Bush_03");
        var ground = Load("SM_Env_Grass_Tall_Clump_01", "SM_Env_Grass_Med_Clump_01",
            "SM_Env_Ground_Cover_01", "SM_Env_Ground_Cover_02");
        var props = Load("SM_Env_Rock_Small_01", "SM_Prop_Leaves_Branch_01");

        var catalog = AssetDatabase.LoadAssetAtPath<StarterNatureCatalog>(
            "Assets/Game/Resources/World/StarterNature.asset");
        if (catalog == null) throw new InvalidOperationException("StarterNature catalog is missing.");
        catalog.WorldMapTrees = trees;
        catalog.WorldMapShrubs = shrubs;
        catalog.WorldMapGroundCover = ground;
        catalog.WorldMapSmallProps = props;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("WM05_VEGETATION_ASSETS_READY: trees=4 shrubs=3 ground=4 props=2; mesh LOD retained; colliders stripped");
    }

    private static GameObject CreateColliderFreeMeshLodPrefab(string name)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Vendor + name + ".prefab");
        if (source == null) throw new InvalidOperationException("Missing vegetation prefab " + name);
        var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
        root.name = name + "_WorldMapV2";
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(collider);
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is BillboardRenderer || renderer.name.EndsWith("LOD3", StringComparison.Ordinal) ||
                renderer.sharedMaterials.Any(material => material != null &&
                    material.name.StartsWith("Card_", StringComparison.Ordinal)))
            {
                renderer.enabled = false;
                continue;
            }
            renderer.sharedMaterials = renderer.sharedMaterials.Select(StableMaterial).ToArray();
        }
        foreach (var group in root.GetComponentsInChildren<LODGroup>(true))
        {
            group.fadeMode = LODFadeMode.CrossFade;
            group.animateCrossFading = true;
            var lods = group.GetLODs().Select(lod =>
            {
                lod.fadeTransitionWidth = 0.3f;
                lod.renderers = lod.renderers.Where(renderer => renderer != null &&
                    renderer.enabled && !(renderer is BillboardRenderer)).ToArray();
                return lod;
            }).Where(lod => lod.renderers.Length > 0).ToArray();
            if (lods.Length < 2)
                throw new InvalidOperationException(name + " does not retain at least two mesh LODs.");
            lods[lods.Length - 1].screenRelativeTransitionHeight = 0.015f;
            group.SetLODs(lods);
            group.RecalculateBounds();
        }
        var result = PrefabUtility.SaveAsPrefabAsset(root, Output + "/" + root.name + ".prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return result;
    }

    private static Material StableMaterial(Material source)
    {
        if (source == null) return null;
        var stable = AssetDatabase.LoadAssetAtPath<Material>(StableMaterials + source.name + "_Stable.mat");
        if (stable == null)
            throw new InvalidOperationException("No approved stable material exists for " + source.name);
        return stable;
    }

    private static GameObject[] Load(params string[] names) => names.Select(name =>
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Vendor + name + ".prefab");
        if (asset == null) throw new InvalidOperationException("Missing vegetation prefab " + name);
        return asset;
    }).ToArray();
}
