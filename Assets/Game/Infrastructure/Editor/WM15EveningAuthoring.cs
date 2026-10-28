using System;
using System.IO;
using System.Linq;
using Game.Presentation.Buildings;
using Game.Domain.Buildings;
using Game.Domain.Resources;
using Game.Presentation.Environment;
using Game.Presentation.Production;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Infrastructure.Editor
{
    public static class WM15EveningAuthoring
    {
        private const string Folder = "Assets/Game/Art/WM15Evening";
        public static void Generate()
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            AddWildCattle();
            LoopAnimalClips();
            PrepareCropsAndPasture();
            PrepareQuarry();
            ScaleBuildings();
            AssetDatabase.SaveAssets();
            Debug.Log("WM15_EVENING_AUTHORING complete");
        }


        private static GameObject CleanModel(string source, string name)
        {
            var original=AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if(original==null) throw new InvalidDataException(source);
            var root=Object.Instantiate(original); root.name=name;
            foreach(var lod in root.GetComponentsInChildren<LODGroup>(true))
            {
                var levels=lod.GetLODs();
                for(var i=1;i<levels.Length;i++) foreach(var renderer in levels[i].renderers)
                    if(renderer!=null) Object.DestroyImmediate(renderer.gameObject);
                Object.DestroyImmediate(lod);
            }
            foreach(var c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach(var c in root.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(c);
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))
            { r.gameObject.SetActive(true); r.enabled=true; r.sharedMaterials=r.sharedMaterials.Select(x=>x!=null && x.shader.name.StartsWith("Serenity/") ? x : WM15PropAuthoring.OwnedMaterial(x)).ToArray(); }
            root.SetActive(true); return root;
        }
        private static GameObject Save(GameObject root,string name)
        {
            var result=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab");
            Object.DestroyImmediate(root);return result;
        }
        private static void AddWildCattle()
        {
            var catalog=Resources.Load<WorldMapWildlifeArtCatalog>("World/WorldMapWildlifeArt");
            if(catalog.For("cow")!=null)return;
            var root=CleanModel("Assets/Toon Farm Pack/Prefabs/Animals/Cows/TFP_Bull_01A.prefab","Wild mountain cattle");
            var animator=root.GetComponentInChildren<Animator>() ?? root.AddComponent<Animator>();
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"/cow.controller") ?? AnimatorController.CreateAnimatorControllerAtPath(Folder+"/cow.controller");
            foreach(var state in controller.layers[0].stateMachine.states.ToArray())controller.layers[0].stateMachine.RemoveState(state.state);
            foreach(var parameter in controller.parameters.ToArray())controller.RemoveParameter(parameter);
            controller.AddParameter("Moving",AnimatorControllerParameterType.Bool);
            var machine=controller.layers[0].stateMachine;
            var idle=machine.AddState("Idle"); var walk=machine.AddState("Walk");machine.defaultState=idle;
            var clips=AssetDatabase.LoadAllAssetsAtPath("Assets/Toon Farm Pack/Models/TFP_Bull_01.fbx").OfType<AnimationClip>().ToArray();
            idle.motion=clips.First(x=>x.name=="RIG_Bull_01_Idle_01");
            walk.motion=clips.First(x=>x.name=="RIG_Bull_01_Walk");
            var go=idle.AddTransition(walk);go.hasExitTime=false;go.duration=.15f;go.AddCondition(AnimatorConditionMode.If,0,"Moving");
            var stop=walk.AddTransition(idle);stop.hasExitTime=false;stop.duration=.15f;stop.AddCondition(AnimatorConditionMode.IfNot,0,"Moving");
            animator.runtimeAnimatorController=controller; animator.applyRootMotion=false;
            var renderers=root.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            root.transform.localScale*=1.6f/Mathf.Max(.1f,bounds.size.y);
            var prefab=Save(root,"WildCow");
            catalog.Configure(catalog.Entries.Concat(new[]{new WorldMapWildlifeArtCatalog.Entry().Configure("cow",prefab,1f)}));
            EditorUtility.SetDirty(catalog);
        }
        private static void PrepareCropsAndPasture()
        {
            var crops=Resources.Load<CropArtCatalog>("World/CropArt");
            foreach(var field in new[]{"Carrot","Cabbage","Fiber","Berries"})
            {
                var info=typeof(CropArtCatalog).GetField(field);var old=(GameObject)info.GetValue(crops);
                if(AssetDatabase.GetAssetPath(old).StartsWith(Folder))continue;
                info.SetValue(crops,Save(CleanModel(AssetDatabase.GetAssetPath(old),field),"Crop_"+field));
            }
            crops.PastureShelter=Save(CleanModel("Assets/RTSToonBuildings/Prefabs/Buildings/Stables.prefab","Pasture shelter"),"PastureShelter");
            EditorUtility.SetDirty(crops);
            var catalog=AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(U08BuildingFoundation.CatalogPath);
            var garden=catalog.Entries.First(x=>x.DefinitionId=="primitive_garden");
            var entry=catalog.Entries.FirstOrDefault(x=>x.DefinitionId=="animal_pasture") ??
                JsonUtility.FromJson<BuildingPresentationCatalog.Entry>(JsonUtility.ToJson(garden));
            entry.Configure("animal_pasture","Загон для скота",BuildingCategory.Food,8,8,garden.Prefab,BuildingPlacementMode.Grid,true)
                .ConfigureMetadata(BuildingEra.StoneAge,"Просторный загон с навесом и кормушкой. Растяните от 8×8 до 24×24 клеток.","building_primitive_garden")
                .ConfigurePlacement(BuildingSpatialForm.Zone,BuildingPlacementConstraint.OwnedTerritory,4500)
                .ConfigureEconomy(new[]
                {
                    new BuildingPresentationCatalog.Entry.CostEntry().Configure("wood_log", 8),
                    new BuildingPresentationCatalog.Entry.CostEntry().Configure("plant_fiber", 6)
                }, 256, new[] { ResourceCategory.Food, ResourceCategory.Other })
                .ConfigureWorkplace(2);
            catalog.Configure(catalog.Entries.Where(x=>x.DefinitionId!="animal_pasture").Concat(new[]{entry}),catalog.DefaultDefinitionId.ToString());EditorUtility.SetDirty(catalog);
        }
        public static void PrepareMountainArt()
        {
            PrepareQuarry();
            var cave=CleanModel("Assets/Synty/PolygonNature/Prefabs/Rocks/SM_Rock_CaveEntrance_01.prefab","Snowy mountain cave");
            var snow=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Art/WM15Late/Prepared/SnowRock.mat");
            foreach(var renderer in cave.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>snow).ToArray();
            var prefab=Save(cave,"SnowCave");
            var catalog=Resources.Load<WorldMapPoiArtCatalog>("World/WorldMapPoiArt");
            var serialized=new SerializedObject(catalog);
            var entries=serialized.FindProperty("entries");
            for(var i=0;i<entries.arraySize;i++)
            {
                var entry=entries.GetArrayElementAtIndex(i);
                if(entry.FindPropertyRelative("poiId").stringValue!="poi_cave")continue;
                entry.FindPropertyRelative("primaryPrefab").objectReferenceValue=prefab;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("WM15 mountain art prepared");
        }

        private static void PrepareQuarry()
        {
            var catalog=Resources.Load<WorldMapResourceArtCatalog>("World/WorldMapResourceArt");
            var cliff=CleanModel("Assets/Game/Art/WM15Late/Prepared/Late_SnowyCliff.prefab","Snow covered quarry face");
            var prefab=Save(cliff,"SnowQuarry");
            catalog.Configure(catalog.Entries.Where(x=>x.ResourceTypeId!="stone_quarry").Concat(new[]{new WorldMapResourceArtCatalog.Entry().Configure("stone_quarry",prefab)}));
            EditorUtility.SetDirty(catalog);
        }

        private static void LoopAnimalClips()
        {
            var catalog = Resources.Load<WorldMapWildlifeArtCatalog>("World/WorldMapWildlifeArt");
            foreach (var entry in catalog.Entries)
            {
                var animator = entry.Prefab.GetComponentInChildren<Animator>(true);
                var controller = animator?.runtimeAnimatorController as AnimatorController;
                if (controller == null) continue;
                foreach (var state in controller.layers[0].stateMachine.states)
                {
                    if (!(state.state.motion is AnimationClip source)) continue;
                    var path = Folder + "/" + entry.SpeciesId + "_" + state.state.name + ".anim";
                    var copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (copy == null) { copy = Object.Instantiate(source); AssetDatabase.CreateAsset(copy,path); }
                    var settings = AnimationUtility.GetAnimationClipSettings(copy);
                    settings.loopTime = true; settings.loopBlend = true;
                    AnimationUtility.SetAnimationClipSettings(copy,settings);
                    state.state.motion = copy; EditorUtility.SetDirty(state.state); EditorUtility.SetDirty(copy);
                    Debug.Log("WM15_ANIMAL_CLIP " + entry.SpeciesId + " " + state.state.name + " " + source.name + " loop=" + copy.isLooping);
                }
                EditorUtility.SetDirty(controller);
            }
        }

        private static void ScaleBuildings()
        {
            var catalog = Resources.Load<BuildingPresentationCatalog>("Buildings/BuildingPresentationCatalog");
            if (catalog == null) catalog = AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(U08BuildingFoundation.CatalogPath);
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("entries");
            for (var i=0;i<entries.arraySize;i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var id = entry.FindPropertyRelative("definitionId").stringValue;
                if (id=="fishing_camp")
                {
                    entry.FindPropertyRelative("maximumHeightDeltaMillimetres").intValue=4500;
                    var recipes = entry.FindPropertyRelative("productionRecipeIds");
                    if (!Enumerable.Range(0,recipes.arraySize).Any(n=>recipes.GetArrayElementAtIndex(n).stringValue=="fish_breed"))
                    { recipes.InsertArrayElementAtIndex(recipes.arraySize); recipes.GetArrayElementAtIndex(recipes.arraySize-1).stringValue="fish_breed"; }
                }
                // Re-running authoring never compounds sizes. Each identity gets its own prefab copy.
                var path = Folder + "/Building_" + id + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
                var prefab = entry.FindPropertyRelative("prefab").objectReferenceValue as GameObject;
                if (prefab == null) continue;
                var multiplier = new[] { "military_barracks", "stone_tool_workshop", "training_ground", "hunting_camp", "butcher_station", "food_storage" }.Contains(id) ? 2.25f : 1.5f;
                // Drawn plots/roads have their actual requested dimensions; fence/gate modules must remain compatible.
                if (new[] { "primitive_garden", "primitive_crop_plot", "primitive_path", "palisade", "gate", "animal_pasture" }.Contains(id)) continue;
                var root = Object.Instantiate(prefab); root.name = "Serenity " + id;
                root.transform.localScale *= multiplier;
                PrefabUtility.SaveAsPrefabAsset(root,path); Object.DestroyImmediate(root);
                entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var dimension in new[] { "footprintWidth", "footprintDepth" })
                {
                    var field = entry.FindPropertyRelative(dimension);
                    field.intValue = Mathf.CeilToInt(field.intValue * multiplier);
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(catalog);
        }
    }
}
