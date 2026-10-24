using System;
using System.Linq;
using Game.Presentation.Environment;
using UnityEditor;
using UnityEngine;

public static class WM06ResourceAssetAuthoring
{
    private const string OutputPath = "Assets/Game/Resources/World/WorldMapResourceArt.asset";

    [MenuItem("Serenity/World Map V2/Author WM06 Resource Catalog")]
    public static void Generate()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<WorldMapResourceArtCatalog>(OutputPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<WorldMapResourceArtCatalog>();
            AssetDatabase.CreateAsset(catalog, OutputPath);
        }

        catalog.Configure(new[]
        {
            Entry("wood", "Assets/Game/Art/WorldMapV2/Vegetation/SM_Env_Tree_Meadow_01_WorldMapV2.prefab"),
            Entry("branches", "Assets/LowPolyNaturePack/Prefabs/Nature/Branch_02.prefab"),
            Entry("berries", "Assets/TerrainDemoScene_URP/Prefabs/Foliage/Red_Bush/Red_Bush.prefab"),
            Entry("mushrooms", "Assets/LowPolyNaturePack/Prefabs/Nature/Mushroom_04.prefab"),
            Entry("medicinal_plants", "Assets/TerrainDemoScene_URP/Prefabs/Details/Heather_A.prefab"),
            Entry("plant_fiber", "Assets/LowPolyNaturePack/Prefabs/Nature/Reeds_01.prefab"),
            Entry("stone", "Assets/Game/Art/WorldMapV2/StoneOutcropV2.prefab"),
            Entry("stone_quarry", "Assets/Game/Art/WorldMapV2/StoneQuarry.prefab"),
            Entry("flint", "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_A_01.prefab"),
            Entry("clay", "Assets/LowPolyFarm/Prefabs/Landscape/Mud_04.prefab"),
            Entry("fish", "Assets/PolygonNatureBiomes/PNB_Tropical_Jungle/FX/FX_Prefabs/FX_Fish_Small_01.prefab")
        });
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("WM06 resource catalog authored with " + catalog.Entries.Count + " audited existing assets.");
    }

    private static WorldMapResourceArtCatalog.Entry Entry(string id, string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Missing audited WM06 asset: " + path);
        if (prefab.GetComponentsInChildren<Renderer>(true).Length == 0)
            throw new InvalidOperationException("WM06 asset has no renderer: " + path);
        if (prefab.GetComponentsInChildren<Renderer>(true).SelectMany(value => value.sharedMaterials)
            .Any(value => value == null))
            throw new InvalidOperationException("WM06 asset has a missing material: " + path);
        return new WorldMapResourceArtCatalog.Entry().Configure(id, prefab);
    }
}
