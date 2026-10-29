using System;
using System.IO;
using System.Linq;
using Game.Domain.Buildings;
using Game.Presentation.Buildings;
using Game.Presentation.Environment;
using Game.Presentation.Production;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Infrastructure.Editor
{
    public static class WM15CurrentFeedbackAuthoring
    {
        private const string Folder = "Assets/Game/Art/WM15Current";
        [MenuItem("Tools/Serenity/WM15/Prepare current feedback")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var art=Resources.Load<CropArtCatalog>("World/CropArt");
            art.Sheep=Animal("Sheep/TFP_Sheep_01A","TFP_Sheep_01","Sheep");
            art.SheepBaby=Animal("Sheep/TFP_Lamb_01A","TFP_Lamb_01","Lamb");
            art.Cow=Animal("Cows/TFP_Bull_01A","TFP_Bull_01","Cow");
            art.Pig=Animal("Pigs/TFP_Piglet_01A","TFP_Piglet_01","Pig");
            art.Goat=Animal("Goats/TFP_Goat_01A","TFP_Goat_01","Goat");
            art.GoatBaby=Animal("Goats/TFP_Kid_01A","TFP_Kid_01","Kid");
            art.Duck=Animal("Ducks/TFP_Duck_01A","TFP_Duck_01","Duck");
            art.DuckBaby=Animal("Ducks/TFP_Duckling_01A","TFP_Duckling_01","Duckling");
            art.Horse=Animal("Horses/TFP_Horse_01A","TFP_Horse_01","Horse");
            art.Chicken=Animal("Assets/ithappy/Animals_FREE/Prefabs/Chicken_001.prefab",null,"Chicken");
            EditorUtility.SetDirty(art);
            PrepareWinter();
            var buildings=AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(U08BuildingFoundation.CatalogPath);
            var entries=buildings.Entries.Where(x=>x.DefinitionId!="ground_torch" && x.DefinitionId!="fishing_pier").ToList();
            var torch=new GameObject("Ground torch");torch.AddComponent<BuildingPresenter>();
            var torchModel=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/Prefabs/Tools/Serenity_Torch.prefab"),torch.transform);
            torchModel.transform.localPosition=Vector3.up*.8f;torchModel.transform.localScale=Vector3.one*2f;
            var hit=torch.AddComponent<BoxCollider>();hit.center=Vector3.up*.8f;hit.size=new Vector3(.35f,1.6f,.35f);
            entries.Add(new BuildingPresentationCatalog.Entry().Configure("ground_torch","Наземный факел",BuildingCategory.Infrastructure,1,1,Save(torch,"GroundTorch"))
                .ConfigureMetadata(BuildingEra.StoneAge,"Уличный факел для освещения поселения.","building_campfire",new[]{"controlled_fire"})
                .ConfigureConstruction(5,1).ConfigureEconomy(new[]{Cost("wood_log",3),Cost("plant_fiber",2)})
                .ConfigurePlacement(BuildingSpatialForm.Structure,BuildingPlacementConstraint.OwnedTerritory|BuildingPlacementConstraint.ReachableApproach));
            var pier=new GameObject("Buildable fishing pier");pier.AddComponent<BuildingPresenter>();
            var baseView=Object.Instantiate(WorldPropCatalog.Current.Find("pier"),pier.transform);
            baseView.name="Authored pier preview";
            baseView.transform.localPosition=Vector3.forward*4.5f;
            var pierHit=pier.AddComponent<BoxCollider>();pierHit.center=Vector3.up*.4f;pierHit.size=new Vector3(2,.8f,2);
            entries.Add(new BuildingPresentationCatalog.Entry().Configure("fishing_pier","Рыбацкий пирс",BuildingCategory.Food,4,4,Save(pier,"FishingPier"))
                .ConfigureMetadata(BuildingEra.StoneAge,"Пирс для медленной ручной рыбалки на озере или реке. Улов ждёт переноски на берегу.","building_fishing_camp",new[]{"gathering"})
                .ConfigureConstruction(30,2).ConfigureEconomy(new[]{Cost("wood_log",12),Cost("plant_fiber",4)})
                .ConfigurePlacement(BuildingSpatialForm.Structure,BuildingPlacementConstraint.Shoreline|BuildingPlacementConstraint.ReachableApproach,20000));
            buildings.Configure(entries,buildings.DefaultDefinitionId.ToString());EditorUtility.SetDirty(buildings);
            AssetDatabase.SaveAssets();
            Debug.Log("WM15_CURRENT_CONTENT authored livestock, ground torch and manual fishing pier");
        }
        private static BuildingPresentationCatalog.Entry.CostEntry Cost(string resource,long amount) =>
            new BuildingPresentationCatalog.Entry.CostEntry().Configure(resource,amount);
        private static GameObject Animal(string prefab,string model,string name)
        {
            var sourcePath=model==null?prefab:"Assets/Toon Farm Pack/Prefabs/Animals/"+prefab+".prefab";
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if(source==null)throw new InvalidDataException("Missing livestock prefab: "+sourcePath);
            var root=Object.Instantiate(source);
            root.name=name;
            foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(component);
            foreach(var collider in root.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {renderer.enabled=true;renderer.sharedMaterials=renderer.sharedMaterials.Select(x=>WM15PropAuthoring.OwnedMaterial(x)).ToArray();}
            var animator=root.GetComponentInChildren<Animator>() ?? root.AddComponent<Animator>();
            var controllerPath=Folder+"/"+name+".controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            foreach(var state in controller.layers[0].stateMachine.states.ToArray())controller.layers[0].stateMachine.RemoveState(state.state);
            foreach(var parameter in controller.parameters.ToArray())controller.RemoveParameter(parameter);
            controller.AddParameter("Moving",AnimatorControllerParameterType.Bool);
            var clips=model==null?Array.Empty<AnimationClip>():AssetDatabase.LoadAllAssetsAtPath("Assets/Toon Farm Pack/Models/"+model+".fbx").OfType<AnimationClip>().Where(x=>!x.name.StartsWith("__")).ToArray();
            var idleClip=model==null?AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ithappy/Animals_FREE/Animations/Other_Animations/Chicken_001_idle.anim"):
                clips.First(x=>x.name.IndexOf("_Idle_",StringComparison.OrdinalIgnoreCase)>=0);
            var walkClip=model==null?AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ithappy/Animals_FREE/Animations/Other_Animations/Chicken_003_walk.anim"):
                clips.First(x=>x.name.IndexOf("_Walk",StringComparison.OrdinalIgnoreCase)>=0);
            var machine=controller.layers[0].stateMachine;
            var idle=machine.AddState("Idle");idle.motion=Loop(idleClip,name+"Idle");machine.defaultState=idle;
            var walk=machine.AddState("Walk");walk.motion=Loop(walkClip,name+"Walk");
            var go=idle.AddTransition(walk);go.hasExitTime=false;go.duration=.12f;go.AddCondition(AnimatorConditionMode.If,0,"Moving");
            var stop=walk.AddTransition(idle);stop.hasExitTime=false;stop.duration=.15f;stop.AddCondition(AnimatorConditionMode.IfNot,0,"Moving");
            animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
            EditorUtility.SetDirty(controller);root.SetActive(true);return Save(root,name);
        }
        private static AnimationClip Loop(AnimationClip source,string name)
        {
            var path=Folder+"/"+name+".anim";
            var result=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(result==null){result=Object.Instantiate(source);AssetDatabase.CreateAsset(result,path);}
            var settings=AnimationUtility.GetAnimationClipSettings(result);settings.loopTime=true;settings.loopBlend=true;
            AnimationUtility.SetAnimationClipSettings(result,settings);EditorUtility.SetDirty(result);return result;
        }
        private static void PrepareWinter()
        {
            var art=Resources.Load<LateLandscapeCatalog>("World/LateLandscape");
            art.SnowAlbedo=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Winter/Snow_01_Albedo.tif");
            art.SnowNormal=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Winter/Snow_01_NM.tif");
            art.SnowHeight=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Winter/Snow_01_Height.tif");
            if(art.SnowAlbedo==null||art.SnowNormal==null||art.SnowHeight==null)
                throw new InvalidDataException("Run Tools/Prepare-WM15-Winter.py before authoring current content.");
            var snow=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Art/WM15Late/Prepared/SnowRock.mat");
            art.SnowRocks=new[]{"Rock_01","Rock_18"}.Select(name=>
            {
                var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Winter/"+name+".FBX"));
                root.name="Winter "+name;
                foreach(var renderer in root.GetComponentsInChildren<Renderer>())renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>snow).ToArray();
                var bounds=root.GetComponentsInChildren<Renderer>().Select(x=>x.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
                root.transform.localScale*=4f/Mathf.Max(.1f,Mathf.Max(bounds.size.x,bounds.size.z));
                return Save(root,"Winter"+name);
            }).ToArray();
            EditorUtility.SetDirty(art);
        }
        private static GameObject Save(GameObject root,string name)
        {
            var result=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab");Object.DestroyImmediate(root);
            if(result==null)throw new InvalidDataException(name);return result;
        }
    }
}
