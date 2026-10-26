using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Presentation.Buildings;
using Game.Presentation.Environment;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Infrastructure.Editor
{
    public static class WM15PropAuthoring
    {
        private const string Folder = "Assets/Game/Art/WM15Replacement";
        private const string Rts = "Assets/RTSToonBuildings/Prefabs/Props/";
        private const string Farm = "Assets/LowPolyFarm/Prefabs/";
        private const string Generic = "Assets/Synty/PolygonGeneric/Prefabs/Props/";
        private const string Fish = Folder + "/Imported/Fishing/LowPolyTropicalCity/Prefabs/";
        private const string Fx = Folder + "/Imported/NatureFX/GreenBugGames/All Nature VFX/Prefabs/";
        private static readonly List<WorldPropCatalog.Entry> entries = new();
        private static readonly Dictionary<Material, Material> materials = new();

        public static void Generate()
        {
            Directory.CreateDirectory(Folder + "/Views"); Directory.CreateDirectory(Folder + "/Materials");
            AssetDatabase.Refresh(); entries.Clear(); materials.Clear();
            Model("stone", Rts + "StonePile.prefab", new Vector3(1.3f,.65f,1.3f));
            Model("logs", Farm + "Props/Logs_01.prefab", new Vector3(1.25f,.7f,1.4f));
            Model("basket", Farm + "Props/Basket_01.prefab", new Vector3(.75f,.6f,.75f));
            Model("sack", Rts + "Sack.prefab", new Vector3(.8f,.7f,.8f));
            Model("berries", Rts + "Berries.prefab", new Vector3(.8f,.5f,.8f));
            Model("meat", Generic + "SM_Gen_Prop_Food_Meat_01.prefab", new Vector3(.75f,.35f,.75f));
            Model("bucket", Farm + "Props/Bucket_01.prefab", new Vector3(.65f,.65f,.65f));
            Model("gold", Rts + "CrateOfGold.prefab", new Vector3(.95f,.65f,.95f));
            Model("ore", "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Rocks/PT_Ore_Rock_01.prefab", new Vector3(2.4f,1.7f,2.4f));
            Model("ore_split", "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Rocks/PT_Ore_Rock_01_split.prefab", new Vector3(2.4f,1.1f,2.4f));
            Model("rock", "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Rocks/PT_Generic_Rock_01.prefab", new Vector3(2f,1.5f,2f));
            Model("branches", "Assets/LowPolyNatureGradientPack/Prefabs/Branch_01.prefab", new Vector3(.95f,.25f,.65f));
            Model("carrot", Farm + "Plants/Carrot_01.prefab", new Vector3(.45f,.3f,.45f));
            Model("cabbage", Farm + "Plants/Cabbage_01.prefab", new Vector3(.6f,.4f,.6f));
            Model("bone", Folder + "/Imported/Bones/LowPolyDungeons/Prefabs/Props/Bone_01.prefab", new Vector3(.65f,.2f,.4f));
            Model("fish", Fish + "Food/Fish_01.prefab", new Vector3(.22f,.18f,.65f), principalAxis: 2, centered: true);
            Model("fishing_rod", Fish + "Props/FishingRod_01.prefab", new Vector3(.3f,2.1f,.3f), principalAxis: 1);
            Model("carry_log", "Assets/Synty/PolygonNature/Prefabs/Trees/SM_Tree_Log_01.prefab", new Vector3(.95f,.25f,.25f), principalAxis: 0, centered: true);
            Model("carry_basket", Farm + "Props/Basket_01.prefab", new Vector3(.55f,.4f,.45f), centered: true);
            Model("soil", Farm + "Landscape/Mud_03.prefab", new Vector3(1f,.08f,1f), stretch: true);
            Model("trough", "Assets/Toon Farm Pack/Prefabs/Props/Exterior Props/TFP_Trough_01A.prefab", new Vector3(1.1f,.55f,.65f));
            Model("pier", "Assets/Toon Farm Pack/Prefabs/Props/Exterior Props/TFP_Wooden_Pier_01A.prefab", new Vector3(2.5f,2.5f,9f), principalAxis: 2, stretch: true, pier: true);
            Model("tanning", "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/Props/SM_Prop_Camp_Tanning_Rack_01.prefab", new Vector3(2.8f,2.5f,1.8f));
            Model("table", Farm + "Props/Table_01.prefab", new Vector3(2.1f,.85f,1.1f));
            Model("frame", "Assets/RTSToonBuildings/Prefabs/Construction/Construction_House.prefab", new Vector3(3f,2f,3f));
            Model("foundation", "Assets/RTSToonBuildings/Prefabs/Construction/Construction1x1_01.prefab", new Vector3(1f,.4f,1f));
            Model("camp_ring", "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/Props/SM_Prop_Camp_Fireplace_Stones_01.prefab", new Vector3(1.6f,.35f,1.6f));
            DryingFrame();
            foreach (var pair in new[] { ("rain","Rain"),("snow","Snow1"),("fire","Fire"),("fog","Fog"),("leaves","Leaf"),("butterfly","Butterfly"),("bee","Bee") })
                Effect(pair.Item1, Fx + pair.Item2 + ".prefab");
            Effect("blood", "Assets/Synty/PolygonGeneric/Prefabs/FX/FX_Blood_Splatter_01.prefab");
            Effect("smoke", "Assets/PolygonNatureBiomes/PNB_Tropical_Jungle/FX/FX_Prefabs/FX_Smoke_Large_01.prefab");
            var mappings = new Dictionary<string,string> {
                {"wood_log","logs"},{"stone","stone"},{"plant_fiber","sack"},{"hide","sack"},
                {"carcass","meat"},{"bone","bone"},{"cured_hide","sack"},{"berries","berries"},
                {"iron_ore","stone"},{"gold_ore","gold"},{"meat","meat"},{"fish","fish"},
                {"cooked_meat","meat"},{"cooked_fish","fish"},{"cooked_berries","basket"},
                {"dried_meat","meat"},{"water","bucket"},{"branches","branches"},{"mushrooms","basket"},
                {"medicinal_plants","basket"},{"clay","sack"},{"flint","stone"},{"copper_ore","stone"},
                {"oil","bucket"},{"carrot","carrot"},{"cabbage","cabbage"} };
            foreach (var pair in mappings)
            {
                var source = entries.Single(x=>x.Key==pair.Value);
                entries.Add(new WorldPropCatalog.Entry { Key="pile_"+pair.Key, Prefab=source.Prefab, Source=source.Source });
            }
            const string path = "Assets/Game/Resources/World/WorldProps.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<WorldPropCatalog>(path);
            if(catalog==null) { catalog=ScriptableObject.CreateInstance<WorldPropCatalog>(); AssetDatabase.CreateAsset(catalog,path); }
            catalog.Entries=entries.ToArray(); EditorUtility.SetDirty(catalog);
            UpgradeBuildings(); AssetDatabase.SaveAssets();
            Preview();
            Debug.Log("WM15_PROPS_AUTHORED entries="+entries.Count);
        }

        private static Material OwnedMaterial(Material source, bool particle = false)
        {
            if(source==null) throw new InvalidDataException("Source prop has a missing material");
            if(materials.TryGetValue(source,out var existing))return existing;
            var id=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
            var path=Folder+"/Materials/"+source.name+"_"+id.Substring(0,Math.Min(8,id.Length))+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader=Shader.Find(particle?"Universal Render Pipeline/Particles/Unlit":"Universal Render Pipeline/Lit");
            var texture=SourceTexture(source);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            material.shader=shader;
            material.SetTexture("_BaseMap",texture);
            material.SetColor("_BaseColor", texture!=null ? Color.white : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
            material.SetFloat("_Smoothness",.1f);
            if(particle)
            {
                material.SetFloat("_Surface",1); material.SetFloat("_Blend",0); material.SetFloat("_ZWrite",0);
                material.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue=3000;
            }
            EditorUtility.SetDirty(material);materials[source]=material;return material;
        }
        private static Texture SourceTexture(Material source)
        {
            // Pipeline conversion may leave the original atlas in a saved property no longer exposed by the shader.
            var saved=new SerializedObject(source).FindProperty("m_SavedProperties.m_TexEnvs");
            foreach(var name in new[]{"_BaseMap","_MainTex","_BaseTexture","_Albedo_Map"})
            {
                if(source.HasProperty(name) && source.GetTexture(name)!=null)return source.GetTexture(name);
                for(var i=0;saved!=null && i<saved.arraySize;i++)
                {
                    var pair=saved.GetArrayElementAtIndex(i);
                    if(pair.FindPropertyRelative("first").stringValue!=name)continue;
                    var texture=pair.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                    if(texture!=null)return texture;
                }
            }
            return null;
        }
        private static Bounds BoundsOf(GameObject model)
        {
            var renderers=model.GetComponentsInChildren<MeshRenderer>(true);
            if(renderers.Length==0)throw new InvalidDataException("No model meshes: "+model.name);
            var b=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))b.Encapsulate(renderer.bounds);return b;
        }
        private static void Model(string key,string path,Vector3 size,int principalAxis=-1,bool centered=false,bool stretch=false,bool pier=false)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(source==null)throw new FileNotFoundException(path);
            var root=new GameObject(key);var model=Object.Instantiate(source,root.transform);model.name="Authored mesh";
            foreach(var group in model.GetComponentsInChildren<LODGroup>(true))
            {
                var lods=group.GetLODs();var keep=lods.Length>0?new HashSet<Renderer>(lods[0].renderers):new HashSet<Renderer>();
                foreach(var renderer in lods.Skip(1).SelectMany(x=>x.renderers).Distinct())if(renderer!=null&&!keep.Contains(renderer))Object.DestroyImmediate(renderer.gameObject);
                Object.DestroyImmediate(group);
            }
            foreach(var component in model.GetComponentsInChildren<Component>(true).Reverse())
                if(component!=null && !(component is Transform) && !(component is MeshFilter) && !(component is MeshRenderer))Object.DestroyImmediate(component);
            foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>(true))renderer.sharedMaterials=renderer.sharedMaterials.Select(x=>OwnedMaterial(x)).ToArray();
            var b=BoundsOf(root);
            if(principalAxis>=0)
            {
                var axis=b.size.x>b.size.y?(b.size.x>b.size.z?0:2):(b.size.y>b.size.z?1:2);
                var from=axis==0?Vector3.right:axis==1?Vector3.up:Vector3.forward;
                var to=principalAxis==0?Vector3.right:principalAxis==1?Vector3.up:Vector3.forward;
                model.transform.rotation=Quaternion.FromToRotation(from,to)*model.transform.rotation;b=BoundsOf(root);
            }
            var scale=new Vector3(size.x/Mathf.Max(.001f,b.size.x),size.y/Mathf.Max(.001f,b.size.y),size.z/Mathf.Max(.001f,b.size.z));
            if(pier)scale.y=scale.x; // Preserve board thickness instead of stretching the whole model vertically.
            if(key=="fishing_rod")scale=Vector3.one*scale.y; // The line adapter attaches at the 2.1 m tip.
            model.transform.localScale=Vector3.Scale(model.transform.localScale,stretch?scale:Vector3.one*Mathf.Min(scale.x,scale.y,scale.z));
            b=BoundsOf(root);
            var floor=centered?b.center.y:b.min.y;
            if(pier)
            {
                var deck=model.GetComponentsInChildren<MeshRenderer>().OrderByDescending(x=>x.bounds.size.x*x.bounds.size.z/Mathf.Max(.08f,x.bounds.size.y)).First();
                floor=deck.bounds.max.y;
                Debug.Log("PIER_DECK "+deck.name+" "+floor+" bounds="+b.size);
            }
            model.transform.position-=new Vector3(b.center.x,floor,b.center.z);
            Save(key,path,root);Object.DestroyImmediate(root);
        }
        private static void Effect(string key,string path)
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path)); root.name=key;
            foreach(var child in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(script);
            foreach(var light in root.GetComponentsInChildren<Light>(true))Object.DestroyImmediate(light);
            foreach(var collider in root.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
            foreach(var renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                var primary=renderer.sharedMaterials.FirstOrDefault(x=>x!=null);
                if(primary==null)throw new InvalidDataException("Missing particle material: "+path+"/"+renderer.name);
                renderer.sharedMaterials=renderer.sharedMaterials.Select(x=>OwnedMaterial(x!=null?x:primary,true)).ToArray();
            }
            var systems=root.GetComponentsInChildren<ParticleSystem>(true);
            foreach(var particles in systems)
            {
                particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=particles.main;main.maxParticles=Math.Min(main.maxParticles,(key=="rain"?2600:key=="snow"?1800:160)/Math.Max(1,systems.Length));
                main.playOnAwake=true;
                if(key=="blood")main.loop=false;
            }
            Save(key,path,root);Object.DestroyImmediate(root);
        }
        private static void Save(string key,string source,GameObject root)
        {
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Views/"+key+".prefab");
            if(prefab==null)throw new InvalidDataException("Failed to save prop view: "+key);
            entries.Add(new WorldPropCatalog.Entry{Key=key,Prefab=prefab,Source=source});
        }
        private static GameObject Place(string key,Transform parent,Vector3 position,Vector3 scale)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(entries.Single(x=>x.Key==key).Prefab,parent);
            go.transform.localPosition=position;go.transform.localScale=scale;return go;
        }
        private static void Clear(Transform parent)
        {for(var i=parent.childCount-1;i>=0;i--)Object.DestroyImmediate(parent.GetChild(i).gameObject);}
        private static void DryingFrame()
        {
            // The farm Dryer is a clothesline. Assemble a food rack from imported timber instead.
            var root=new GameObject("drying_frame");
            foreach(var x in new[]{-1.05f,1.05f})
            {
                var post=Place("carry_log",root.transform,new Vector3(x,1.1f,0),new Vector3(2.2f/.95f,.65f,.65f));
                post.transform.localRotation=Quaternion.Euler(0,0,90);
            }
            Place("carry_log",root.transform,new Vector3(0,2.05f,0),new Vector3(2.6f/.95f,.65f,.65f));
            Save("drying_frame","Assembly from imported SM_Tree_Log_01",root);Object.DestroyImmediate(root);
        }
        private static void UpgradeBuildings()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(U08BuildingFoundation.CatalogPath);
            foreach(var entry in catalog.Entries)
            {
                var path=AssetDatabase.GetAssetPath(entry.Prefab);
                if(!path.StartsWith("Assets/Game/Art/Prefabs/Buildings/"))continue;
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var stages=root.GetComponentsInChildren<BuildingConstructionStageVisual>(true);
                    if(stages.Length==0)continue;
                    var final=stages.Max(x=>x.RequiredStage);
                    var box=root.GetComponent<BoxCollider>();var size=box!=null?box.size:new Vector3(3,2,3);
                    // Roads and existing modular fortifications keep their functional cell hierarchy.
                    if(entry.DefinitionId=="palisade"||entry.DefinitionId=="gate"||entry.DefinitionId=="primitive_path")continue;
                    foreach(var stage in stages.Where(x=>x.RequiredStage<final))
                    {
                        Clear(stage.transform);
                        var key=entry.DefinitionId=="campfire"?"camp_ring":stage.RequiredStage==1?"foundation":"frame";
                        Place(key,stage.transform,Vector3.zero, key=="frame" ? Vector3.one*Mathf.Min(size.x/3f,size.z/3f) : new Vector3(size.x*.85f,1,size.z*.85f));
                    }
                    var finish=stages.First(x=>x.RequiredStage==final).transform;
                    if(entry.DefinitionId=="hide_workshop") {Clear(finish);Place("tanning",finish,Vector3.zero,Vector3.one);}
                    if(entry.DefinitionId=="drying_rack")
                    {
                        Clear(finish);Place("drying_frame",finish,Vector3.zero,Vector3.one);
                        for(var i=-2;i<=2;i++)Place("meat",finish,new Vector3(i*.38f,1.7f,0),Vector3.one*.55f).transform.localRotation=Quaternion.Euler(0,0,90);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
        }
        public static void Preview()
        {
            Directory.CreateDirectory("Artifacts/WM15Replacement/previews");
            var catalog=AssetDatabase.LoadAssetAtPath<WorldPropCatalog>("Assets/Game/Resources/World/WorldProps.asset");
            foreach(var entry in catalog.Entries.Where(x=>new[]{"stone","rock","logs","meat","fish","fishing_rod","pier","drying_frame","tanning","soil","trough","frame"}.Contains(x.Key)))
            {
                var preview=new PreviewRenderUtility();var model=Object.Instantiate(entry.Prefab);var b=BoundsOf(model);
                preview.AddSingleGO(model);var distance=b.size.magnitude*1.7f;
                preview.camera.transform.position=b.center+new Vector3(.7f,.6f,-1f).normalized*distance;
                preview.camera.transform.LookAt(b.center);preview.camera.fieldOfView=35;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=distance*4;
                preview.lights[0].intensity=1.5f;preview.lights[0].transform.rotation=Quaternion.Euler(45,30,0);preview.lights[1].intensity=.6f;preview.ambientColor=Color.gray;
                preview.BeginStaticPreview(new Rect(0,0,640,480));preview.Render(true);var image=preview.EndStaticPreview();
                File.WriteAllBytes("Artifacts/WM15Replacement/previews/"+entry.Key+".png",image.EncodeToPNG());Object.DestroyImmediate(image);preview.Cleanup();
            }
        }
    }
}
