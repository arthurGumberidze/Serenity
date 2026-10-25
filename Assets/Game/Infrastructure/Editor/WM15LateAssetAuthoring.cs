using System;
using System.IO;
using System.Linq;
using Game.Presentation.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class WM15LateAssetAuthoring
{
    private const string Root = "Assets/Game/Art/WM15Late";
    public static void Author()
    {
        Directory.CreateDirectory(Root+"/Prepared"); AssetDatabase.Refresh();
        foreach (var path in AssetDatabase.FindAssets("t:Texture2D",new[]{Root+"/Imported"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
            importer.maxTextureSize=2048; importer.mipmapEnabled=true; importer.textureCompression=TextureImporterCompression.Compressed;
            if (path.Contains("_N.png") || path.Contains("_Normal") || path.Contains("_norm")) importer.textureType=TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
        foreach (var path in AssetDatabase.FindAssets("t:Material",new[]{Root+"/Imported/Stylized"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            var albedo=SavedTexture(material,"_Albedo") ?? SavedTexture(material,"_MainTex");
            var normal=SavedTexture(material,"_NormalMap");
            var particle=path.Contains("Particles");
            material.shader=Shader.Find(particle ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Lit");
            material.shaderKeywords=Array.Empty<string>(); material.SetTexture("_BaseMap",albedo);
            material.SetColor("_BaseColor",Color.white); material.enableInstancing=true;
            material.SetFloat("_Cull",0); material.SetFloat("_Smoothness",.12f);
            if (normal != null && !particle) { material.SetTexture("_BumpMap",normal); material.EnableKeyword("_NORMALMAP"); }
            if (particle)
            {
                material.SetFloat("_Surface",1); material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha); material.SetFloat("_ZWrite",0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue=3000;
            }
            else if(path.Contains("Vegetation"))
            { material.SetFloat("_AlphaClip",1); material.SetFloat("_Cutoff",.4f); material.EnableKeyword("_ALPHATEST_ON"); material.renderQueue=2450; }
            EditorUtility.SetDirty(material);
        }
        var snowRock=MakeMaterial("SnowRock",new Color(.28f,.36f,.40f),false);
        var snowTree=MakeMaterial("SnowTree",new Color(.12f,.24f,.19f),true);
        var assetPath="Assets/Game/Resources/World/LateLandscape.asset";
        var catalog=AssetDatabase.LoadAssetAtPath<LateLandscapeCatalog>(assetPath);
        if(catalog==null) {catalog=ScriptableObject.CreateInstance<LateLandscapeCatalog>();AssetDatabase.CreateAsset(catalog,assetPath);}
        catalog.SnowTree=Make("SnowyTree.prefab",9f,snowTree);
        catalog.SnowRocks=new[]{Make("SnowyRock1.prefab",3f,snowRock),Make("SnowyCliff.prefab",9f,snowRock),Make("GlacierWall.prefab",7f,snowRock)};
        catalog.IceFloe=Make("IceFloeLow.prefab",.65f,snowRock);
        catalog.Flowers=new[]{Make("Flower_01_a.prefab",.7f),Make("Flower_01_h.prefab",.7f),Make("Flower_02_b.prefab",.65f),Make("Flower_07_c.prefab",.9f)};
        catalog.MarshPlants=new[]{Make("Plant_03.prefab",.8f),Make("Plant_04_e.prefab",1.1f)};
        catalog.FallingLeaves=Make("Particles_Zone_FallingLeaves.prefab",0);
        catalog.Fireflies=Make("Particles_Fireflies.prefab",0);
        catalog.Grass=Texture("GrassUV02.png"); catalog.GrassNormal=Texture("GrassUV02_N.png");
        catalog.ForestFloor=Texture("GroundDryLeaves01.png"); catalog.ForestNormal=Texture("GroundDryLeaves01_N.png");
        catalog.Pebbles=Texture("GroundStones01.png"); catalog.PebblesNormal=Texture("GroundStones01_N.png");
        catalog.WaterWaves=Texture("MediumWaves_tex.png");
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        Debug.Log("WM15_LATE_ASSETS authored snow trees/rocks, flowers, marsh plants, leaves/fireflies, ground textures and AQUAS waves.");
    }
    private static Texture SavedTexture(Material material,string name)
    {
        var list=new SerializedObject(material).FindProperty("m_SavedProperties.m_TexEnvs");
        for(var i=0;i<list.arraySize;i++)
        {var entry=list.GetArrayElementAtIndex(i);if(entry.FindPropertyRelative("first").stringValue==name)
            return entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;}
        return null;
    }
    private static Texture2D Texture(string name)
    {
        var path=Directory.GetFiles(Root+"/Imported",name,SearchOption.AllDirectories).First().Replace('\\','/');
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? throw new InvalidOperationException(path);
    }
    private static Material MakeMaterial(string name,Color color,bool tree)
    {
        var path=Root+"/Prepared/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null) {mat=new Material(Shader.Find("Serenity/SnowProps"));AssetDatabase.CreateAsset(mat,path);}
        mat.SetColor("_BaseColor",color); mat.SetColor("_SnowColor",new Color(.85f,.94f,.99f));
        mat.SetFloat("_Tree",tree ? 1:0);mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
    }
    private static GameObject Make(string name,float height,Material forced=null)
    {
        var source=Directory.GetFiles(Root+"/Imported",name,SearchOption.AllDirectories).First().Replace('\\','/');
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(source);
        if(prefab==null) throw new InvalidOperationException("Cannot load "+source);
        var holder=new GameObject("Late_"+Path.GetFileNameWithoutExtension(name));
        var model=UnityEngine.Object.Instantiate(prefab,holder.transform);
        model.transform.localPosition=Vector3.zero;
        foreach(var t in model.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(script);
        foreach(var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
        foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            if(forced!=null) renderer.sharedMaterials=Enumerable.Repeat(forced,renderer.sharedMaterials.Length).ToArray();
        var meshes=model.GetComponentsInChildren<Renderer>(true).Where(r=>r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
        if(height>0 && meshes.Length>0)
        {
            var bounds=meshes[0].bounds;foreach(var renderer in meshes)bounds.Encapsulate(renderer.bounds);
            model.transform.localScale*=height/Mathf.Max(.01f,bounds.size.y);
            bounds=meshes[0].bounds;foreach(var renderer in meshes)bounds.Encapsulate(renderer.bounds);
            model.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        }
        foreach(var particles in model.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.maxParticles=36;main.loop=true;main.startLifetime=6f;
            main.startSize=name.Contains("Fireflies") ? .035f:.07f;
            var emission=particles.emission;emission.rateOverTime=4;
            var shape=particles.shape;shape.scale=Vector3.one*4f;shape.radius=2f;
        }
        var result=PrefabUtility.SaveAsPrefabAsset(holder,Root+"/Prepared/"+holder.name+".prefab");
        UnityEngine.Object.DestroyImmediate(holder);return result;
    }
}
