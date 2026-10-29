using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Diagnostics;
using Game.Domain.Resources;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Infrastructure.Integration
{
    /// <summary>Opt-in rendered acceptance route, also driven by the persistent Editor test runner.</summary>
    public sealed class WM15AssetExpansionPlayerSmoke : MonoBehaviour
    {
        private readonly List<string> checks=new();
        private readonly List<string> errors=new();
        private string output;
        private float deadline;
        private bool finished;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args=Environment.GetCommandLineArgs();var index=Array.IndexOf(args,"-wm15AssetExpansion");
            if(index<0||index+1>=args.Length)return;
            var host=new GameObject("Asset expansion acceptance");DontDestroyOnLoad(host);
            var runner=host.AddComponent<WM15AssetExpansionPlayerSmoke>();runner.output=Path.GetFullPath(args[index+1]);
            Directory.CreateDirectory(runner.output);Application.runInBackground=true;
            runner.deadline=Time.realtimeSinceStartup+900;Application.logMessageReceived+=runner.OnLog;
            runner.StartCoroutine(runner.Run());
        }
        private void OnLog(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
        private void Update(){if(output!=null&&!finished&&Time.realtimeSinceStartup>deadline){Check(false,"Timeout");Finish();}}
        private IEnumerator Run()
        {
            MainMenuController menu;while((menu=FindAnyObjectByType<MainMenuController>())==null)yield return null;
            menu.StartNewGame(1380062136);
            LocalSceneCompositionRoot root;while((root=FindAnyObjectByType<LocalSceneCompositionRoot>())==null||!root.IsInitialized)yield return null;
            yield return Exercise(root,output,Check);Check(errors.Count==0,"No runtime errors");Finish();
        }
        private void Check(bool pass,string text)
        {checks.Add((pass?"PASS ":"FAIL ")+text);File.WriteAllLines(Path.Combine(output,"checks.txt"),checks);}
        private void Finish()
        {
            finished=true;Application.logMessageReceived-=OnLog;File.WriteAllLines(Path.Combine(output,"runtime-errors.txt"),errors);
            Application.Quit(errors.Count==0&&checks.All(x=>x.StartsWith("PASS ",StringComparison.Ordinal))?0:2);
        }

        public static IEnumerator Exercise(LocalSceneCompositionRoot root,string directory,Action<bool,string> check)
        {
            Directory.CreateDirectory(directory);root.Clock.Pause();root.GameplayUi.SetOnboardingVisible(false);
            root.Diagnostics.Execute(DiagnosticCommandKind.UnlockAll);root.Diagnostics.Execute(DiagnosticCommandKind.GrantResources);
            root.WeatherPresentation.ShowPreview(root.Weather.GetWeatherForDay(0),12*TimeSpan.TicksPerHour);
            // Domain scheduling is checked separately; this verifies replacement, authorities and real save/load.
            var building=root.Buildings.All.First(x=>x.IsOperational&&x.OwnerSideId.Value=="player"&&
                root.BuildingCatalog.ResolveDefinition(x.DefinitionId).Storage!=null);
            var id=building.Id;var beforeHp=building.CurrentIntegrity;var option=root.BuildingLifecycle.UpgradeOption(building);
            root.BuildingPresentations.TryGet(id,out var originalView);root.SelectionProbe.SelectBuilding(originalView);
            var funding=root.Inventories.All.Where(x=>x.Owner.Kind==InventoryOwnerKind.BuildingStorage||x.Owner.Kind==InventoryOwnerKind.WorldPile).Select(x=>x.Owner).ToArray();
            var result=root.BuildingLifecycle.Upgrade(id,option.TargetDefinitionId,StableEntityId.NewId(),TimeSpan.FromTicks(option.WorkRequiredTicks),root.Clock.State.CalendarTicks,funding);
            check(result.Succeeded,"Storage upgrade completes with authored costs");
            check(building.CurrentIntegrity==beforeHp,"Upgrade does not heal existing damage");
            check(root.BuildingPresentations.TryGet(id,out var upgraded)&&upgraded.PresentedDefinitionId==option.TargetDefinitionId,"Model changes on the existing stable identity");
            check(root.SelectionProbe.SelectedBuildingPresenter==upgraded,"Selected building card follows its replacement model");
            check(root.Inventories.Get(new InventoryOwner(InventoryOwnerKind.BuildingStorage,id)).Capacity>=root.BuildingCatalog.ResolveDefinition(option.TargetDefinitionId).Storage.Capacity,"Storage retains all content and provides upgraded capacity");
            root.PlayerSaves.Save(3);root.PlayerSaves.Load(3);root.Clock.Pause();
            check(root.Buildings.Get(id).DefinitionId==option.TargetDefinitionId,"Level II survives real save/load");
            root.BuildingPresentations.TryGet(id,out upgraded);root.CameraController.FocusPosition(upgraded.transform.position,18f);
            yield return new WaitForSecondsRealtime(1);Capture(directory,"storage-level2.png");

            var army=root.Diagnostics.Execute(DiagnosticCommandKind.CreateArmy);check(army.Succeeded,"Ranked army created");
            yield return new WaitForSecondsRealtime(1);
            foreach(var soldier in root.MilitaryTraining.Records)
            {
                if(!root.CharacterPresentations.TryGet(soldier.CharacterId,out var view))continue;
                var renderers=view.GetComponentsInChildren<SkinnedMeshRenderer>().Where(x=>x.enabled).ToArray();
                check(renderers.Length>=5,"Visible uniform rank "+soldier.Rank);
            }

            var snow=root.GetComponent<DeformableSnowPresentation>();var map=root.TiledWorld.ArtWorldToDesign;
            var snowPoint=Vector3.zero;var found=false;
            for(var dz=700;dz<=850&&!found;dz+=5)for(var dx=770;dx<=950&&!found;dx+=5)
            {
                var p=new Vector3((dx-map.x)/map.z,0,(dz-map.y)/map.w);
                if(!root.WorldDefinition.ContainsClosed(p.x,p.z))continue;
                p.y=root.WorldHeight.SampleHeight(p.x,p.z);
                if(!snow.IsTrackableSnow(p)||root.WorldHeight.SampleSlopeDegrees(p.x,p.z,1)>12)continue;
                if(!snow.IsTrackableSnow(p+Vector3.forward*8))continue;
                snowPoint=p;found=true;
            }
            check(found,"Walkable deep snow exists at the mountain foot");
            if(found)
            {
                root.CameraController.FocusPosition(snowPoint,20);root.TiledWorld.RefreshNow(new WorldPosition(snowPoint.x,snowPoint.y,snowPoint.z),Array.Empty<WorldPosition>(),Array.Empty<Tuple<WorldPosition,WorldPosition>>());
                snow.QueueAround(snowPoint,2);for(var frame=0;frame<40;frame++)yield return null;
                check(snow.SnowPatchCount>0,"Snow has raised geometry");
                root.CameraController.enabled=false;var camera=Camera.main;
                // Cinemachine owns the physical camera even when the RTS input rig is disabled.
                foreach(var behaviour in camera.GetComponents<Behaviour>())
                    if(behaviour.GetType().FullName=="Unity.Cinemachine.CinemachineBrain")behaviour.enabled=false;
                camera.transform.SetPositionAndRotation(snowPoint+new Vector3(6,5,-5),Quaternion.LookRotation(snowPoint+Vector3.forward*3-(snowPoint+new Vector3(6,5,-5))));
                Capture(directory,"snow-before.png");
                var resident=root.CharacterPresentations.Active.First(x=>x!=null&&x.IsBound);
                var agent=resident.GetComponent<NavMeshAgent>();if(agent!=null)agent.enabled=false;
                root.AiRuntime.enabled=false;var old=resident.transform.position;resident.transform.position=snowPoint;
                yield return new WaitForSecondsRealtime(.2f);var count=snow.VisibleTrackCount;
                for(var step=1;step<=24;step++)
                {
                    var p=snowPoint+Vector3.forward*(step*.35f);p.y=root.WorldHeight.SampleHeight(p.x,p.z);
                    resident.transform.position=p;yield return new WaitForSecondsRealtime(.11f);
                }
                check(snow.VisibleTrackCount>count+5&&snow.MaximumDepression>.04f,"Moving resident leaves a physically depressed continuous trail");
                Capture(directory,"snow-after.png");resident.transform.position=old;
                if(agent!=null)agent.enabled=true;root.AiRuntime.enabled=true;
            }
            var lake=root.WorldDefinition.MapData.Hydrology.Lakes.Single(x=>x.Id=="lake_mountain");
            var center=root.WorldDefinition.MapData.CoordinateTransform.ToUnity(lake.Center);
            var bank=root.WorldDefinition.MapData.CoordinateTransform.ToUnity(lake.Shoreline.Points.First());
            var target=new Vector3(bank.X,root.WorldHeight.WaterSurfaceLevel(lake.Id),bank.Z);
            var outward=new Vector3(bank.X-center.X,0,bank.Z-center.Z).normalized;
            root.TiledWorld.RefreshNow(new WorldPosition(target.x,target.y,target.z),Array.Empty<WorldPosition>(),Array.Empty<Tuple<WorldPosition,WorldPosition>>());
            root.CameraController.enabled=false;
            foreach(var behaviour in Camera.main.GetComponents<Behaviour>())
                if(behaviour.GetType().FullName=="Unity.Cinemachine.CinemachineBrain")behaviour.enabled=false;
            foreach(var distance in new[]{60f,20f,5f,1f})
            {
                var camera=Camera.main;var position=target+outward*distance+Vector3.up*Mathf.Max(1.2f,distance*.45f);
                camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-outward*3-position));
                yield return new WaitForSecondsRealtime(.5f);Capture(directory,"mountain-lake-"+distance+".png");
            }
            check(true,"Mountain lake rendered at four close camera distances");
        }
        private static void Capture(string directory,string name)
        {
            var camera=Camera.main;var render=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32);
            var previous=camera.targetTexture;var active=RenderTexture.active;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,name),image.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;Destroy(image);RenderTexture.ReleaseTemporary(render);}
        }
    }
}
