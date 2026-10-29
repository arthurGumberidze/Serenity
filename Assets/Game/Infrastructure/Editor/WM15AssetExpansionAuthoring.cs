using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Military;
using Game.Presentation.Buildings;
using Game.Presentation.Characters;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Game.Infrastructure.Editor
{
    /// <summary>Explicit authoring recipe using admitted local sources. Never runs on game startup.</summary>
    public static class WM15AssetExpansionAuthoring
    {
        public const string Folder="Assets/Game/Art/WM15Expansion";
        public const string Buildings="Assets/Game/Art/Config/BuildingPresentationCatalog.asset";
        public const string Characters="Assets/Game/Art/Config/CharacterPresentationCatalog.asset";
        private const string Rts="Assets/RTSToonBuildings/Prefabs/Buildings/";
        private const string City="Assets/SoftLeitner/CityBuilderThree/Models/";
        private const string Farm="Assets/LowPolyFarm/Prefabs/Props/";
        private static readonly Dictionary<Material,Material> materials=new();
        private static readonly Dictionary<string,string> secondModels=new()
        {
            ["primitive_shelter"]=City+"hut.fbx",["stone_age_hut"]=Folder+"/Sources/clan_house.prefab",
            ["stone_tool_workshop"]=City+"smith.fbx",["crafting_hall"]=Rts+"Blacksmith.prefab",
            ["clan_house"]=Rts+"House2.prefab",["communal_house"]=Rts+"House2.prefab",
            ["food_storage"]=Rts+"Granary3.prefab",["central_storehouse"]=Rts+"Storehouse.prefab",
            ["military_barracks"]=Rts+"Barracks.prefab",["fishing_camp"]=Rts+"Dock.prefab",
            ["meeting_area"]=City+"EntertainerCamp.fbx",["shrine"]=City+"obelisks.fbx",
            ["hide_workshop"]=City+"workshop.fbx",
            ["well"]="Assets/LowPolyFantasyVillage/Prefabs/Props/Well.prefab",
            ["watchtower"]=Folder+"/Sources/watchtower.prefab"
        };

        [MenuItem("Serenity/WM15/Author asset expansion and level II")]
        public static void Generate()
        {
            foreach(var sub in new[]{"Sources","Buildings","Uniforms","Materials"})Directory.CreateDirectory(Folder+"/"+sub);
            AssetDatabase.Refresh();materials.Clear();
            AuthorUniforms();AuthorBuildings();AssetDatabase.SaveAssets();
            Debug.Log("WM15_ASSET_EXPANSION_AUTHORED");
        }

        private static BuildingPresentationCatalog.Entry Copy(BuildingPresentationCatalog.Entry source)
        { var entry=new BuildingPresentationCatalog.Entry();JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source),entry);return entry; }
        private static BuildingPresentationCatalog.Entry.CostEntry Cost(string id,long n)=>new BuildingPresentationCatalog.Entry.CostEntry().Configure(id,n);

        private static void AuthorBuildings()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(Buildings);
            var baseEntries=catalog.Entries.Where(x=>!x.DefinitionId.EndsWith("_level2",StringComparison.Ordinal)).ToList();
            AddNew(baseEntries,"communal_house","Общинный дом", "clan_house",Rts+"House3.prefab",8,7,10,0);
            AddNew(baseEntries,"central_storehouse","Общинный склад","food_storage",Rts+"Storehouse.prefab",7,6,0,600);
            AddNew(baseEntries,"crafting_hall","Ремесленный двор","stone_tool_workshop",Rts+"LumberYard1.prefab",7,6,0,0);
            var entries=new List<BuildingPresentationCatalog.Entry>();
            var manifest=new List<string>{"building,level_1,level_2,work_minutes,cost"};
            foreach(var entry in baseEntries)
            {
                var snapshot=Folder+"/Sources/"+entry.DefinitionId+".prefab";
                if(AssetDatabase.LoadAssetAtPath<GameObject>(snapshot)!=null)continue;
                var copy=Object.Instantiate(entry.Prefab);PrefabUtility.SaveAsPrefabAsset(copy,snapshot);Object.DestroyImmediate(copy);
            }
            foreach(var original in baseEntries)
            {
                var id=original.DefinitionId;var def=original.CreateDefinition();
                var snapshot=Folder+"/Sources/"+id+".prefab";
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(snapshot);
                if(source==null)
                {
                    var copy=Object.Instantiate(original.Prefab);copy.name=id+" source";
                    source=PrefabUtility.SaveAsPrefabAsset(copy,snapshot);Object.DestroyImmediate(copy);
                }
                var one=AuthorBuildingView(source,def,id,1);
                var title=id=="communal_house"?"Общинный дом":def.DisplayName;
                var baseEntry=Copy(original).Configure(id,title,def.Category,def.Footprint.Width,def.Footprint.Depth,one,def.PlacementMode,def.AllowsOverlap);
                // Existing world seed scenes still reference this ID; its model is now the hut's second level.
                if(id=="clan_house")
                {
                    entries.Add(baseEntry.ConfigureUpgrades(Array.Empty<BuildingPresentationCatalog.Entry.UpgradeEntry>()));
                    AssetDatabase.DeleteAsset(Folder+"/Buildings/clan_house_L2.prefab");continue;
                }
                var two=AuthorBuildingView(source,def,id,2);
                var upgradeCost=def.ConstructionCost.Select(x=>Cost(x.ResourceId.ToString(),Math.Max(2,(long)Math.Ceiling(x.Quantity.Units*.75)))).ToArray();
                if(id=="stone_age_hut")upgradeCost=new[]{Cost("stone",18),Cost("wood_log",12),Cost("plant_fiber",6)};
                if(upgradeCost.Length==0)upgradeCost=new[]{Cost("wood_log",4),Cost("stone",2)};
                var minutes=Math.Max(30,(int)Math.Ceiling(TimeSpan.FromTicks(def.Construction.WorkRequiredTicks).TotalMinutes*1.25));
                baseEntry.ConfigureUpgrades(new[]{new BuildingPresentationCatalog.Entry.UpgradeEntry().Configure(id+"_level2",upgradeCost,minutes)});
                var upgraded=Copy(baseEntry).Configure(id+"_level2",id=="stone_age_hut"?"Каменный дом":title+" II",def.Category,def.Footprint.Width,def.Footprint.Depth,two,def.PlacementMode,def.AllowsOverlap)
                    .ConfigureUpgrades(Array.Empty<BuildingPresentationCatalog.Entry.UpgradeEntry>())
                    .ConfigureStructure((int)Math.Ceiling(def.MaxIntegrity*1.6),def.FireDamagePerThousand,def.CombatDamagePerThousand,def.Capturable)
                    .ConfigureMetadata(def.Era,"Усиленная постройка второго уровня. "+def.Description,def.IconKey,
                        def.RequiredTechnologies.Select(x=>x.ToString()).Concat(new[]{"organized_labor","woodworking"}).Distinct());
                if(def.Housing!=null)upgraded.ConfigureHousing(def.Housing.ResidentCapacity+Math.Max(2,def.Housing.ResidentCapacity/2));
                if(def.Storage!=null)upgraded.ConfigureEconomy(def.ConstructionCost.Select(x=>Cost(x.ResourceId.ToString(),x.Quantity.Units)),def.Storage.Capacity*2,def.Storage.AcceptedCategories);
                if(def.Workplace!=null)
                {
                    if(def.Production!=null)upgraded.ConfigureProduction(def.Workplace.MaximumWorkers+1,
                        def.Production.RecipeIds.Select(x=>x.ToString()),def.Production.InputCapacity*2,def.Production.OutputCapacity*2);
                    else upgraded.ConfigureWorkplace(def.Workplace.MaximumWorkers+1);
                }
                entries.Add(baseEntry);entries.Add(upgraded);
                manifest.Add(id+","+AssetDatabase.GetAssetPath(one)+","+AssetDatabase.GetAssetPath(two)+","+minutes+",\""+
                    string.Join(";",upgradeCost.Select(x=>x.CreateAmount().ResourceId+":"+x.CreateAmount().Quantity.Units))+"\"");
            }
            catalog.Configure(entries,catalog.DefaultDefinitionId.ToString());EditorUtility.SetDirty(catalog);
            Directory.CreateDirectory("Artifacts/WM15AssetExpansion");
            File.WriteAllLines("Artifacts/WM15AssetExpansion/building-levels.csv",manifest);
        }

        private static void AddNew(List<BuildingPresentationCatalog.Entry> entries,string id,string title,string basis,string model,int width,int depth,int residents,long capacity)
        {
            if(entries.Any(x=>x.DefinitionId==id))return;
            var original=entries.Single(x=>x.DefinitionId==basis);var def=original.CreateDefinition();
            var root=new GameObject(id);root.AddComponent<BuildingPresenter>();
            var box=root.AddComponent<BoxCollider>();box.size=new Vector3(width*.85f,3,depth*.85f);box.center=Vector3.up*1.5f;
            var stage=new GameObject("Finished work yard");stage.transform.SetParent(root.transform,false);
            stage.AddComponent<BuildingConstructionStageVisual>().Configure(3);
            Place(model,stage.transform,Vector3.zero,new Vector3(width*.76f,4,depth*.76f));
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Sources/"+id+".prefab");Object.DestroyImmediate(root);
            var entry=Copy(original).Configure(id,title,def.Category,width,depth,prefab)
                .ConfigureMetadata(BuildingEra.StoneAge,"Расширенная общинная постройка с отдельным рабочим пространством.","building_"+basis,new[]{"organized_labor"})
                .ConfigureEconomy(new[]{Cost("wood_log",32),Cost("stone",18),Cost("plant_fiber",12)},capacity,
                    capacity>0?Enum.GetValues(typeof(Game.Domain.Resources.ResourceCategory)).Cast<Game.Domain.Resources.ResourceCategory>():def.StorageCategories)
                .ConfigureConstruction(120).ConfigureStructure(1400);
            if(residents>0)entry.ConfigureHousing(residents);
            if(id=="crafting_hall"&&def.Production!=null)entry.ConfigureProduction(3,def.Production.RecipeIds.Select(x=>x.ToString()),120,120);
            entries.Add(entry);
        }

        private static GameObject AuthorBuildingView(GameObject source,BuildingDefinition def,string id,int level)
        {
            var root=Object.Instantiate(source);root.name=id+" level "+level;
            var stages=root.GetComponentsInChildren<BuildingConstructionStageVisual>(true);
            var finish=stages.OrderByDescending(x=>x.RequiredStage).FirstOrDefault()?.transform??root.transform;
            var replacement=level==2&&secondModels.TryGetValue(id,out var second)?second:
                id=="communal_house"&&level==1?Rts+"House3.prefab":
                id=="stone_age_hut"&&level==1?"Assets/LowPolyNaturePack/Prefabs/Props/House_01.prefab":
                id=="watchtower"&&level==1?"Assets/LowPolyFantasyVillage/Prefabs/Props/Tower.prefab":null;
            if(replacement!=null)
            {
                foreach(Transform child in finish.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
                Place(replacement,finish,Vector3.zero,new Vector3(def.Footprint.Width*.82f,5.2f,def.Footprint.Depth*.82f));
            }
            var decor=new GameObject("Authored work-site detail L"+level);decor.transform.SetParent(finish,false);
            Decorate(decor.transform,def,id,level);
            if(level==2)
            {
                if(id=="palisade"||id=="gate"||id=="rally_point")
                    foreach(Transform child in finish)if(child!=decor.transform)child.localScale=Vector3.Scale(child.localScale,new Vector3(1,1.25f,1));
                if(id=="primitive_path")
                    Place(City+"Props/Pebbles.fbx",decor.transform,Vector3.zero,new Vector3(.85f,.045f,.85f));
                if(id=="storage_basket")
                    Place(Farm+"Shelving_01.prefab",decor.transform,new Vector3(0,0,def.Footprint.Depth*.2f),new Vector3(def.Footprint.Width*.65f,1.8f,def.Footprint.Depth*.4f));
                if(id=="primitive_garden"||id=="primitive_crop_plot")
                    Place(Farm+"Scarecrow_01.prefab",decor.transform,new Vector3(0,0,def.Footprint.Depth*.3f),new Vector3(.9f,1.7f,.6f));
                if(id=="stone_age_kitchen"||id=="animal_pasture")
                    Place(Farm+"Market_Roof_01.prefab",decor.transform,new Vector3(0,0,def.Footprint.Depth*.15f),new Vector3(def.Footprint.Width*.8f,3,def.Footprint.Depth*.65f));
                if(id=="central_storehouse")
                    Place(City+"silo.fbx",decor.transform,new Vector3(-def.Footprint.Width*.31f,0,def.Footprint.Depth*.25f),new Vector3(1.25f,3.4f,1.25f));
                if(id=="chief_hut"||id=="hunting_camp"||id=="butcher_station")
                {
                    Place(Farm+"Market_Roof_01.prefab",decor.transform,new Vector3(def.Footprint.Width*.28f,0,0),
                        new Vector3(def.Footprint.Width*.35f,2.8f,def.Footprint.Depth*.65f));
                    Place(Farm+"Table_01.prefab",decor.transform,new Vector3(def.Footprint.Width*.28f,0,0),
                        new Vector3(def.Footprint.Width*.3f,.85f,def.Footprint.Depth*.4f));
                }
                if(id=="healer_hut")
                {
                    var table=Place(Farm+"Table_01.prefab",decor.transform,new Vector3(0,0,def.Footprint.Depth*.32f),new Vector3(def.Footprint.Width*.6f,.8f,.6f));
                    var top=BoundsOf(table.GetComponentsInChildren<Renderer>()).max.y;
                    for(var n=0;n<3;n++)Place("Assets/LowPolyFantasyVillage/Prefabs/Props/Bottle_0"+(n+1)+".prefab",decor.transform,
                        new Vector3((n-1)*.22f,top,def.Footprint.Depth*.32f),new Vector3(.18f,.3f,.18f));
                }
            }
            var path=Folder+"/Buildings/"+id+"_L"+level+".prefab";
            var result=PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);return result;
        }

        private static void Decorate(Transform parent,BuildingDefinition def,string id,int level)
        {
            var w=def.Footprint.Width;var d=def.Footprint.Depth;
            var size=Mathf.Min(w,d)*.21f;
            // Keep approaches in front clear. Decorations are non-colliding and inside the footprint.
            var a=new Vector3(-w*.32f,0,d*.29f);var b=new Vector3(w*.32f,0,d*.29f);
            var prop=id.Contains("storage")||id.Contains("storehouse")?"Basket_01.prefab":
                id.Contains("workshop")||id.Contains("crafting")?"Table_01.prefab":
                id.Contains("well")||id.Contains("fishing")?"Bucket_01.prefab":"Logs_01.prefab";
            if(id=="primitive_path" || id=="palisade" || id=="gate" || id=="rally_point")
            {
                if(level==2)Place(Farm+"Logs_01.prefab",parent,a,new Vector3(Mathf.Max(.18f,size),.24f,.35f));
                return;
            }
            Place(Farm+prop,parent,a,new Vector3(size,.8f,size));
            if(w>=3)Place(Farm+"Basket_01.prefab",parent,b,new Vector3(size*.65f,.65f,size*.65f));
            if(level==2)
            {
                Place(Farm+"Logs_01.prefab",parent,new Vector3(w*.32f,0,-d*.1f),new Vector3(size,.5f,size));
                Place(Farm+"Bucket_01.prefab",parent,new Vector3(-w*.32f,0,-d*.08f),new Vector3(size*.6f,.7f,size*.6f));
                if(id.Contains("drying")||id.Contains("hide"))
                    Place("Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/Props/SM_Prop_Camp_Tanning_Rack_01.prefab",parent,
                        new Vector3(0,0,d*.30f),new Vector3(w*.65f,2,d*.25f));
            }
        }

        private static GameObject Place(string path,Transform parent,Vector3 position,Vector3 maxSize)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(source==null)throw new FileNotFoundException("Required admitted art is missing",path);
            var value=Object.Instantiate(source,parent,false);value.name=source.name;
            var authoredStages=value.GetComponentsInChildren<BuildingConstructionStageVisual>(true);
            if(authoredStages.Length>0)
            {
                var final=authoredStages.Max(x=>x.RequiredStage);
                foreach(var stage in authoredStages)stage.gameObject.SetActive(stage.RequiredStage==final);
            }
            foreach(var component in value.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(component);
            foreach(var collider in value.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
            foreach(var animator in value.GetComponentsInChildren<Animator>(true))Object.DestroyImmediate(animator);
            foreach(var lod in value.GetComponentsInChildren<LODGroup>(true))Object.DestroyImmediate(lod);
            value.transform.localPosition=Vector3.zero;value.transform.localRotation=Quaternion.identity;value.transform.localScale=Vector3.one;
            var renderers=value.GetComponentsInChildren<Renderer>(true);
            var bounds=BoundsOf(renderers);
            var scale=Mathf.Min(maxSize.x/Mathf.Max(.01f,bounds.size.x),maxSize.y/Mathf.Max(.01f,bounds.size.y),maxSize.z/Mathf.Max(.01f,bounds.size.z));
            value.transform.localScale=Vector3.one*scale;bounds=BoundsOf(renderers);
            value.transform.position+=parent.TransformPoint(position)-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            foreach(var renderer in renderers)renderer.sharedMaterials=renderer.sharedMaterials.Select(CleanMaterial).ToArray();
            return value;
        }
        private static Bounds BoundsOf(Renderer[] renderers)
        {if(renderers.Length==0)throw new InvalidDataException("Art has no renderer");var b=renderers[0].bounds;foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds);return b;}
        private static Material CleanMaterial(Material source)
        {
            if(source==null)throw new InvalidDataException("Art material missing");
            if(source.shader!=null&&source.shader.name.StartsWith("Universal Render Pipeline/",StringComparison.Ordinal))return source;
            if(materials.TryGetValue(source,out var cached))return cached;
            var path=Folder+"/Materials/"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source))+".mat";
            var result=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(result==null){result=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(result,path);}
            var texture=source.HasProperty("_BaseMap")?source.GetTexture("_BaseMap"):source.HasProperty("_MainTex")?source.GetTexture("_MainTex"):null;
            result.SetTexture("_BaseMap",texture);result.SetColor("_BaseColor",Color.white);result.SetFloat("_Smoothness",.16f);result.enableInstancing=true;
            EditorUtility.SetDirty(result);materials[source]=result;return result;
        }

        private static void AuthorUniforms()
        {
            var entries=new List<MilitaryUniformCatalog.Entry>();
            foreach(var rank in new[]{1,4,5,6,7,8,9,10})
                entries.Add(new MilitaryUniformCatalog.Entry{MinimumRank=(MilitaryRank)rank,Male=Uniform(rank,false),Female=Uniform(rank,true)});
            var path=Folder+"/MilitaryUniforms.asset";var catalog=AssetDatabase.LoadAssetAtPath<MilitaryUniformCatalog>(path);
            if(catalog==null){catalog=ScriptableObject.CreateInstance<MilitaryUniformCatalog>();AssetDatabase.CreateAsset(catalog,path);}
            catalog.Entries=entries.ToArray();EditorUtility.SetDirty(catalog);
            var characters=AssetDatabase.LoadAssetAtPath<CharacterPresentationCatalog>(Characters);characters.MilitaryUniforms=catalog;EditorUtility.SetDirty(characters);
        }
        private static GameObject Uniform(int rank,bool female)
        {
            var sex=female?"Female":"Male";
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Polytope Studio/Lowpoly_Characters/Prefabs/Modular_Armors/PT_Lowpoly_Armors_"+sex+"_Moduar_Free.prefab");
            var root=Object.Instantiate(source);root.name="Serenity "+sex+" rank "+rank;
            foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(script);
            var suit=rank>=6?"_Armor_05_C_":"_Armor_01_A_";
            var materialPath=Folder+"/Materials/Uniform_"+rank+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(mat==null)
            {
                mat=new Material(source.GetComponentInChildren<Renderer>(true).sharedMaterial);
                AssetDatabase.CreateAsset(mat,materialPath);
            }
            mat.shader=Shader.Find("Serenity/MilitaryUniform");
            if(mat.shader==null)throw new InvalidDataException("Military uniform palette shader missing");
            var honour=Mathf.InverseLerp(4,10,rank);
            var metal=rank<4?new Color(.25f,.29f,.30f):Color.Lerp(new Color(.50f,.56f,.60f),new Color(.90f,.67f,.22f),honour);
            var cloth=rank switch {1=>new Color(.10f,.18f,.16f),4=>new Color(.09f,.22f,.40f),5=>new Color(.12f,.28f,.49f),6=>new Color(.40f,.055f,.045f),7=>new Color(.53f,.075f,.055f),8=>new Color(.64f,.10f,.065f),9=>new Color(.30f,.055f,.42f),_=>new Color(.55f,.09f,.48f)};
            Set(mat,"_Color",Color.white);Set(mat,"_BaseColor",Color.white);
            Set(mat,"_METAL1COLOR",metal);Set(mat,"_METAL2COLOR",Color.Lerp(metal,new Color(1,.83f,.34f),honour));Set(mat,"_METAL3COLOR",metal*.65f);
            Set(mat,"_CLOTH1COLOR",cloth);Set(mat,"_CLOTH2COLOR",Color.Lerp(cloth,new Color(.93f,.79f,.37f),honour*.2f));Set(mat,"_CLOTH3COLOR",cloth*.7f);
            Set(mat,"_LEATHER1COLOR",new Color(.25f,.12f,.045f));Set(mat,"_LEATHER2COLOR",new Color(.42f,.24f,.10f));Set(mat,"_LEATHER3COLOR",new Color(.13f,.07f,.025f));
            Set(mat,"_SKINCOLOR",new Color(.64f,.40f,.27f));Set(mat,"_HAIRCOLOR",new Color(.12f,.065f,.035f));Set(mat,"_EYESCOLOR",new Color(.08f,.12f,.16f));
            var active=0;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var name=renderer.name;var selected=name.Contains(suit)||name.EndsWith("_Armor_head_01",StringComparison.Ordinal);
                if(name.Contains("cape")&&rank<4)selected=false;
                if(!selected){Object.DestroyImmediate(renderer);continue;}
                renderer.enabled=true;
                renderer.gameObject.SetActive(true);renderer.sharedMaterial=mat;active++;
            }
            if(active<5)throw new InvalidDataException("Incomplete uniform: "+root.name+" parts="+active);
            EditorUtility.SetDirty(mat);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Uniforms/"+sex+"_"+rank+".prefab");Object.DestroyImmediate(root);return prefab;
        }
        private static void Set(Material m,string name,Color color){color.a=1;if(m.HasProperty(name))m.SetColor(name,color);}

        public static void CaptureReview()
        {
            Directory.CreateDirectory("Artifacts/WM15AssetExpansion/previews");
            var paths=Directory.GetFiles(Folder+"/Buildings","*.prefab").Concat(Directory.GetFiles(Folder+"/Uniforms","*.prefab"));
            foreach(var path in paths)
            {
                var preview=new PreviewRenderUtility();var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                model.GetComponent<BuildingZoneVisual>()?.Render(new[]{new GridCoordinate(0,0),new GridCoordinate(1,0),new GridCoordinate(2,0)},1f);
                var stages=model.GetComponentsInChildren<BuildingConstructionStageVisual>(true);var final=stages.Length==0?0:stages.Max(x=>x.RequiredStage);
                foreach(var stage in stages)stage.gameObject.SetActive(stage.RequiredStage==final);
                preview.AddSingleGO(model);var bounds=BoundsOf(model.GetComponentsInChildren<Renderer>().Where(x=>x.enabled).ToArray());
                var distance=bounds.size.magnitude*1.6f;preview.camera.transform.position=bounds.center+new Vector3(.65f,.48f,-1).normalized*distance;
                preview.camera.transform.LookAt(bounds.center);preview.camera.fieldOfView=35;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=distance*4;
                preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=Quaternion.Euler(45,30,0);preview.lights[1].intensity=.7f;preview.ambientColor=Color.gray;
                preview.BeginStaticPreview(new Rect(0,0,480,360));preview.Render(true);var image=preview.EndStaticPreview();
                File.WriteAllBytes("Artifacts/WM15AssetExpansion/previews/"+Path.GetFileNameWithoutExtension(path)+".png",image.EncodeToPNG());Object.DestroyImmediate(image);preview.Cleanup();
            }
        }
    }
}
