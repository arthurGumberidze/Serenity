using System;
using System.Linq;
using Game.Presentation.Environment;
using UnityEditor;
using UnityEngine;

public static class WM15FeedbackAuthoring
{
    public static void BuildPlayer()
    {
        Generate();
        var build = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("U00Build"))
            .First(type => type != null);
        build.GetMethod("WindowsDevelopment").Invoke(null, null);
    }

    public static void Generate()
    {
        // Keep GPU-instancing variants in Player builds even though draw materials are cloned at runtime.
        const string vegetationMaterialPath = "Assets/Game/Resources/World/WorldMapVegetation.mat";
        var vegetationMaterial = AssetDatabase.LoadAssetAtPath<Material>(vegetationMaterialPath);
        if (vegetationMaterial == null)
        {
            vegetationMaterial = new Material(Shader.Find("Serenity/World Map Vegetation"));
            AssetDatabase.CreateAsset(vegetationMaterial, vegetationMaterialPath);
        }
        vegetationMaterial.enableInstancing = true;
        EditorUtility.SetDirty(vegetationMaterial);
        foreach (var path in new[] {
            "Assets/Synty/PolygonNature/Materials/Misc/Tree_Dead_01.mat",
            "Assets/Synty/PolygonNature/Materials/LODS/Leaves_Base_LOD_01.mat",
            "Assets/Synty/PolygonNature/Materials/Leaves/Leaves_01_Base.mat",
            "Assets/Synty/PolygonNature/Materials/Alts/PolygonNature_Tree_Trunk_01.mat",
            "Assets/Synty/PolygonNature/Materials/Alts/PolygonNature_01.mat",
            "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Materials/BASEMat_01.mat",
            "Assets/PolygonNatureBiomes/PNB_Tropical_Jungle/Materials/BASEMat_01.mat" })
            RepairAlbedo(path);
        var bearMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Blink/Art/Animals/Stylized/Bear/Bear_Materials/Bear_4.mat");
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Blink/Art/Animals/Stylized/Bear/Bear_Textures/Stylized_Bear_Albedo4.png");
        bearMaterial.shader = Shader.Find("Universal Render Pipeline/Lit");
        bearMaterial.SetTexture("_BaseMap", albedo);
        bearMaterial.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(bearMaterial);
        var wildlife = Resources.Load<WorldMapWildlifeArtCatalog>("World/WorldMapWildlifeArt");
        var bear = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Blink/Art/Animals/Stylized/Bear/Bear_Prefabs/Bear_4.prefab");
        wildlife.For("bear").Configure("bear", bear, 1f);
        EditorUtility.SetDirty(wildlife);
        var resourceArt = Resources.Load<WorldMapResourceArtCatalog>("World/WorldMapResourceArt");
        const string stoneMaterialPath = "Assets/Game/Art/WorldMapV2/WorldMapStone.mat";
        var stoneMaterial = AssetDatabase.LoadAssetAtPath<Material>(stoneMaterialPath);
        if (stoneMaterial == null)
        {
            stoneMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(stoneMaterial, stoneMaterialPath);
        }
        stoneMaterial.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
            AssetDatabase.GUIDToAssetPath("a195a893970f3ab4990bd06e18a0b308")));
        stoneMaterial.SetColor("_BaseColor", new Color(.58f, .55f, .49f));
        stoneMaterial.SetFloat("_Smoothness", .1f);
        EditorUtility.SetDirty(stoneMaterial);
        var stone = PrefabUtility.LoadPrefabContents("Assets/Game/Art/CompactWorld/StoneOutcrop.prefab");
        foreach (var renderer in stone.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => stoneMaterial).ToArray();
        var stonePrefab = PrefabUtility.SaveAsPrefabAsset(stone, "Assets/Game/Art/WorldMapV2/StoneOutcropV2.prefab");
        PrefabUtility.UnloadPrefabContents(stone);
        resourceArt.Entries.Single(e => e.ResourceTypeId == "stone").Configure("stone", stonePrefab);
        var quarry = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SoftLeitner/CityBuilderThree/Models/GraniteMine.fbx");
        if (quarry == null || bear == null || albedo == null) throw new InvalidOperationException("Missing local art.");
        var quarryInstance = UnityEngine.Object.Instantiate(quarry);
        foreach (var renderer in quarryInstance.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterials = renderer.sharedMaterials.Select(material =>
                material != null && material.name == "White" ? stoneMaterial : material).ToArray();
        quarry = PrefabUtility.SaveAsPrefabAsset(quarryInstance, "Assets/Game/Art/WorldMapV2/StoneQuarry.prefab");
        UnityEngine.Object.DestroyImmediate(quarryInstance);
        resourceArt.Configure(resourceArt.Entries.Where(e => e.ResourceTypeId != "stone_quarry")
            .Concat(new[] { new WorldMapResourceArtCatalog.Entry().Configure("stone_quarry", quarry) }));
        EditorUtility.SetDirty(resourceArt);
        AssetDatabase.SaveAssets();
        Debug.Log("WM15: local bear and granite quarry catalog references authored.");
    }

    private static void RepairAlbedo(string path)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null || !material.HasProperty("_BaseMap") || material.GetTexture("_BaseMap") != null) return;
        var saved = new SerializedObject(material).FindProperty("m_SavedProperties.m_TexEnvs");
        foreach (var key in new[] { "_Albedo_Map", "_Main_Texture", "_MainTex", "_BaseTexture", "_LeafTex" })
        for (var i = 0; i < saved.arraySize; i++)
        {
            var entry = saved.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("first").stringValue != key) continue;
            var value = entry.FindPropertyRelative("second");
            var texture = value.FindPropertyRelative("m_Texture").objectReferenceValue as Texture;
            if (texture == null) continue;
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", value.FindPropertyRelative("m_Scale").vector2Value);
            material.SetTextureOffset("_BaseMap", value.FindPropertyRelative("m_Offset").vector2Value);
            EditorUtility.SetDirty(material);
            return;
        }
        throw new InvalidOperationException("Missing scenery albedo: " + path);
    }
}
