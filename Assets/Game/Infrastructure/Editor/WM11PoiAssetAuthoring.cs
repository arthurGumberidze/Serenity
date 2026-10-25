using System;
using System.Linq;
using Game.Presentation.Environment;
using UnityEditor;
using UnityEngine;

public static class WM11PoiAssetAuthoring
{
    private const string OutputPath = "Assets/Game/Resources/World/WorldMapPoiArt.asset";

    [MenuItem("Serenity/World Map V2/Author WM11 POI Catalog")]
    public static void Generate()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<WorldMapPoiArtCatalog>(OutputPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<WorldMapPoiArtCatalog>();
            AssetDatabase.CreateAsset(catalog, OutputPath);
        }

        var giantTree = Load("Assets/Synty/PolygonNature/Prefabs/Trees/SM_Tree_Generic_Giant_01.prefab");
        var monolith = Load("Assets/PolygonNatureBiomes/PNB_Tropical_Jungle/Prefabs/SM_Env_Monolith_01.prefab");
        var flowers = Load("Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Wildflowers_Patch_03.prefab");
        var butterflies = Load("Assets/PolygonNatureBiomes/PNB_Meadow_Forest/FX/FX_Prefabs/FX_Butterflies_Blue_01.prefab");
        catalog.Configure(new[]
        {
            Entry("poi_sacred_grove", "Assets/Game/Art/Prefabs/Buildings/Serenity_Shrine.prefab", 1f,
                accent: giantTree, accentCount: 5, accentRadius: 12f, effect: butterflies),
            new WorldMapPoiArtCatalog.Entry().ConfigureTerrainLandmark("poi_stone_circle"),
            Entry("poi_abandoned_camp", "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/Props/SM_Prop_Camp_Tent_01.prefab", 1f,
                accent: Load("Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/Props/SM_Prop_Camp_Fireplace_Stones_01.prefab"),
                accentCount: 1),
            new WorldMapPoiArtCatalog.Entry().ConfigureTerrainLandmark("poi_ancient_ruins"),
            Entry("poi_overlook", "Assets/Game/Art/Prefabs/Buildings/Serenity_Watchtower.prefab"),
            new WorldMapPoiArtCatalog.Entry().ConfigureTerrainLandmark("poi_rock_spire"),
            Entry("poi_cave", "Assets/Synty/PolygonNature/Prefabs/Rocks/SM_Rock_CaveEntrance_01.prefab", 1.2f),
            new WorldMapPoiArtCatalog.Entry().Configure("poi_giant_tree", giantTree, 1.35f),
            new WorldMapPoiArtCatalog.Entry().Configure("poi_secluded_glade", flowers, 1f, 7, 9f,
                effect: butterflies)
        });
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("WM11 POI catalog authored with " + catalog.Entries.Count + " audited existing asset mappings.");
    }

    private static WorldMapPoiArtCatalog.Entry Entry(string id, string path, float scale = 1f,
        GameObject accent = null, int accentCount = 0, float accentRadius = 0f, GameObject effect = null) =>
        new WorldMapPoiArtCatalog.Entry().Configure(id, Load(path), scale, accent: accent,
            accents: accentCount, accentRingRadius: accentRadius, effect: effect);

    private static GameObject Load(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Missing audited WM11 asset: " + path);
        var renderers = prefab.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0 || renderers.SelectMany(value => value.sharedMaterials).Any(value => value == null))
            throw new InvalidOperationException("WM11 asset has missing renderer/material: " + path);
        return prefab;
    }
}
