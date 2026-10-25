using System;
using System.Linq;
using System.IO;
using Game.Presentation.Characters;
using UnityEditor;
using UnityEngine;

public static class WM15OctoberAssetRepair
{
    public static void Repair()
    {
        const string path = "Assets/Game/Art/ThirdParty/UMA3/Races/Slots/UMA30_Eyelashes/UMA30_Eyelashes_UMA_EYELASHES_overlay.asset";
        var overlay = AssetDatabase.LoadMainAssetAtPath(path);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/ThirdParty/UMA3/Textures/UMA_EYELASH_4.png");
        if (overlay == null || texture == null) throw new InvalidOperationException("Admitted UMA eyelashes are missing.");
        var data = new SerializedObject(overlay);
        var list = data.FindProperty("_textureList");
        if (list == null) throw new InvalidOperationException("UMA overlay texture schema changed.");
        list.arraySize = Math.Max(1, list.arraySize);
        list.GetArrayElementAtIndex(0).objectReferenceValue = texture;
        var names = data.FindProperty("textureNames");
        if (names != null) { names.arraySize = list.arraySize; names.GetArrayElementAtIndex(0).stringValue = texture.name; }
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(overlay);
        AssetDatabase.SaveAssets();
        Debug.Log("WM15_REPAIR eyelashes now reference admitted UMA_EYELASH_4 texture.");
        RepairToolsAndUniforms();
        const string terrainPath = "Assets/Game/Resources/World/RegionalTerrain.mat";
        var terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(terrainPath);
        if (terrainMaterial == null)
        {
            terrainMaterial = new Material(Shader.Find("Serenity/WorldTerrain"));
            AssetDatabase.CreateAsset(terrainMaterial, terrainPath);
        }
        // A serialized reference keeps the instancing variants needed by runtime-created terrains.
        terrainMaterial.enableInstancing = true;
        terrainMaterial.EnableKeyword("_NORMALMAP");
        terrainMaterial.EnableKeyword("_TERRAIN_INSTANCED_PERPIXEL_NORMAL");
        EditorUtility.SetDirty(terrainMaterial); AssetDatabase.SaveAssets();
    }

    private static void RepairToolsAndUniforms()
    {
        const string directory = "Assets/Game/Art/WM15October";
        Directory.CreateDirectory(directory); AssetDatabase.Refresh();
        var catalog = AssetDatabase.LoadAssetAtPath<CharacterPresentationCatalog>("Assets/Game/Art/Config/CharacterPresentationCatalog.asset");
        foreach (var tool in new[]{catalog.WorkAxe,catalog.WorkPickaxe,catalog.WorkHammer})
        {
            var path = AssetDatabase.GetAssetPath(tool);
            var instance = PrefabUtility.LoadPrefabContents(path);
            foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                if (AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(directory+"/",StringComparison.Ordinal)) continue;
                var meshPath = directory+"/"+tool.name+"_"+filter.sharedMesh.name+".asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(mesh == null) { mesh=UnityEngine.Object.Instantiate(filter.sharedMesh); AssetDatabase.CreateAsset(mesh,meshPath); }
                filter.sharedMesh=mesh;
            }
            PrefabUtility.SaveAsPrefabAsset(instance,path); PrefabUtility.UnloadPrefabContents(instance);
        }
        catalog.MaleTrainedResident = Uniform(catalog.MaleResident,"Male",directory);
        catalog.FemaleTrainedResident = Uniform(catalog.FemaleResident,"Female",directory);
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
    }
    private static GameObject Uniform(GameObject resident,string sex,string directory)
    {
        // Reuse the imported Polytope citizen's face and rig; replace only the tunic with its matching cloth kit.
        var instance = UnityEngine.Object.Instantiate(resident);
        var kit = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Polytope Studio/Lowpoly_Characters/Prefabs/Modular_Armors/PT_Lowpoly_Armors_"+sex+"_Moduar_Free.prefab");
        var cloth=kit.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(x=>x.name=="PT_"+sex+"_Armor_cloth_00_body");
        var upper=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(x=>x.name.Contains("upper"));
        var bones=instance.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
        if(cloth.bones.Any(b=>b!=null && !bones.ContainsKey(b.name))) throw new InvalidOperationException("Uniform bones differ from admitted resident rig.");
        var meshPath=directory+"/"+sex+"_TrainingCloth.asset";
        var clothMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(clothMesh==null) { clothMesh=UnityEngine.Object.Instantiate(cloth.sharedMesh); AssetDatabase.CreateAsset(clothMesh,meshPath); }
        upper.sharedMesh=clothMesh;
        upper.bones=cloth.bones.Select(b=>b==null?null:bones[b.name]).ToArray();
        upper.rootBone=cloth.rootBone==null?null:bones[cloth.rootBone.name];
        upper.localBounds=cloth.localBounds;
        upper.sharedMaterials=cloth.sharedMaterials.Select((source,i)=>
        {
            var path=directory+"/"+sex+"_TrainingCloth_"+i+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null)
            {
                material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor",new Color(.62f,.72f,.9f));
                material.SetFloat("_Smoothness",.12f); AssetDatabase.CreateAsset(material,path);
            }
            const string textureSource="Assets/Polytope Studio/Lowpoly_Characters/Sources/Modular_Armors/Textures/PT_Armors_Base_Texture.png";
            var texturePath=directory+"/TrainingClothAtlas.png";
            if(AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath)==null) AssetDatabase.CopyAsset(textureSource,texturePath);
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(texture==null) throw new InvalidOperationException("Training tunic atlas missing.");
            material.SetTexture("_BaseMap",texture); EditorUtility.SetDirty(material);
            return material;
        }).ToArray();
        var prefab=PrefabUtility.SaveAsPrefabAsset(instance,directory+"/"+sex+"_TrainedResident.prefab");
        UnityEngine.Object.DestroyImmediate(instance); return prefab;
    }
}
