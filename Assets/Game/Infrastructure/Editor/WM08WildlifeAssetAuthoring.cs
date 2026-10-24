using System;
using System.Linq;
using Game.Presentation.Environment;
using UnityEditor;
using UnityEngine;

public static class WM08WildlifeAssetAuthoring
{
    private const string OutputPath = "Assets/Game/Resources/World/WorldMapWildlifeArt.asset";

    [MenuItem("Serenity/World Map V2/Author WM08 Wildlife Catalog")]
    public static void Generate()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<WorldMapWildlifeArtCatalog>(OutputPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<WorldMapWildlifeArtCatalog>();
            AssetDatabase.CreateAsset(catalog, OutputPath);
        }

        catalog.Configure(new[]
        {
            Entry("deer", "Assets/Game/Art/CompactWorld/HuntingDeer.prefab", 1f),
            Entry("boar", "Assets/Game/Art/Prefabs/Animals/Serenity_Boar.prefab", 1f),
            // No imported hare/fox/wolf models exist. These closest animated project assets are
            // disposable visual fallbacks only; canonical species never share identity or state.
            Entry("hare", "Assets/ithappy/Animals_FREE/Prefabs/Kitty_001.prefab", 0.55f),
            Entry("fox", "Assets/ithappy/Animals_FREE/Prefabs/Dog_001.prefab", 0.7f),
            Entry("wolf", "Assets/ithappy/Animals_FREE/Prefabs/Dog_001.prefab", 1.05f),
            Entry("bear", "Assets/Blink/Art/Animals/Stylized/Bear/Bear_Prefabs/Bear_4.prefab", 1f),
            Entry("waterfowl", "Assets/Toon Farm Pack/Prefabs/Animals/Ducks/TFP_Duck_01A.prefab", 0.8f, true)
        });
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("WM08 wildlife catalog authored with " + catalog.Entries.Count + " audited existing assets.");
    }

    private static WorldMapWildlifeArtCatalog.Entry Entry(string id, string path, float scale,
        bool allowStaticAnimator = false)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Missing audited WM08 asset: " + path);
        var renderers = prefab.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("WM08 asset has no renderer: " + path);
        if (renderers.SelectMany(value => value.sharedMaterials).Any(value => value == null))
            throw new InvalidOperationException("WM08 asset has a missing material: " + path);
        // Prefer an exact imported species mesh even when the source pack ships it as a static prefab
        // (the Toon Farm duck has no controller). Other catalog entries retain their imported animation.
        var animator = prefab.GetComponentInChildren<Animator>(true);
        if (!allowStaticAnimator && (animator == null || animator.runtimeAnimatorController == null))
            throw new InvalidOperationException("WM08 animated asset has no controller: " + path);
        return new WorldMapWildlifeArtCatalog.Entry().Configure(id, prefab, scale);
    }
}
