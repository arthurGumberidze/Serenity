using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.AI;
using Game.Domain.Resources;
using Game.Domain.Diagnostics;
using Game.Domain.Time;
using Game.Domain.Work;
using Game.Infrastructure;
using Game.Simulation.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Game.Tests.Editor
{
    public sealed class WM15RetestRepairRuntimeTests
    {
        private const string Evidence="Artifacts/WM15RetestRepair/gameplay";
        [UnityTest,Timeout(900000)]
        public IEnumerator AutomaticBuilderUpgradeAndDistantPierDelivery()
        {
            Directory.CreateDirectory(Evidence);
            yield return new EnterPlayMode();
            Application.runInBackground=true;
            yield return SceneManager.LoadSceneAsync("MainMenu");yield return null;
            var menu=UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
            menu.OpenNewGame();yield return new WaitForSecondsRealtime(5);
            menu.StartNewGame(1380062136);
            LocalSceneCompositionRoot root;
            while((root=UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>())==null||!root.IsInitialized)yield return null;
            root.Clock.Pause();root.GameplayUi.SetOnboardingVisible(false);
            root.Diagnostics.Execute(DiagnosticCommandKind.UnlockAll);
            root.Diagnostics.Execute(DiagnosticCommandKind.GrantResources);
            var hut=root.Buildings.All.First(x=>x.OwnerSideId.Value=="player"&&x.IsOperational&&x.DefinitionId.FamilyId.ToString()=="stone_age_hut");
            var expected=root.BuildingLifecycle.UpgradeOption(hut).TargetDefinitionId;
            Assert.That(root.BuildingLifecycle.RequestUpgrade(hut.Id).Succeeded,Is.True);
            root.BuildingPresentations.TryGet(hut.Id,out var view);
            Assert.That(view.transform.Find("Construction frame")?.gameObject.activeSelf,Is.True,"Upgrade scaffolding appears immediately while paused");
            root.CameraController.FocusPosition(view.transform.position,22);
            root.Clock.SetSpeed(GameSpeed.VeryFast);root.Clock.Resume();
            var until=Time.realtimeSinceStartup+120;var sawWork=false;
            while(Time.realtimeSinceStartup<until && hut.DefinitionId!=expected)
            {
                sawWork|=(root.BuildingLifecycle.UpgradeJob(hut.Id)?.ProgressTicks??0)>0;
                yield return null;
            }
            Dump(root,"upgrade");
            Assert.That(sawWork,Is.True,"An automatically selected resident reaches the real building and performs work");
            Assert.That(hut.DefinitionId,Is.EqualTo(expected),"Actual scheduler completes the upgrade");
            yield return new WaitForSecondsRealtime(1);root.Clock.Pause();
            foreach(var home in root.Facilities.HomeAssignments)
                Assert.That(root.Domestic.EvaluateSleep(home.Key).Allowed,Is.True,"Assigned resident has a usable bed: "+home.Key);
            yield return SnowRoute(root);

            Building pier=null;
            var placementDiagnostics=new List<string>();
            root.BuildingPlacementController.enabled=false;
            Assert.That(root.BuildingPlacementController.TryStartPlacement(new BuildingDefinitionId("fishing_pier")),Is.True);
            foreach(var lake in root.WorldDefinition.MapData.Hydrology.Lakes.OrderBy(lake=>
                root.WorldDefinition.MapData.CoordinateTransform.ToUnity(lake.Center).DistanceSquared(root.WorldDefinition.PlayerStart)))
            {
                foreach(var point in lake.Shoreline.Points.Where((_,i)=>i%3==0))
                {
                    var position=root.WorldDefinition.MapData.CoordinateTransform.ToUnity(point);
                    var world=new Vector3(position.X,0,position.Z);
                    root.CameraController.FocusPosition(world,40);yield return null;
                    var retry=Time.realtimeSinceStartup+1.5f;
                    while(!root.BuildingPlacementController.TryMovePreviewToWorldPoint(world)&&Time.realtimeSinceStartup<retry)yield return null;
                    placementDiagnostics.Add(world+" "+root.BuildingPlacementController.PreviewCoordinate+" "+root.BuildingPlacementController.LastPlacementFailure+" "+root.BuildingPlacementController.LastConstructionFailure+" "+root.BuildingPlacementController.SurfaceBlockedReason);
                    if(!root.BuildingPlacementController.IsPreviewValid||root.BuildingPlacementController.PreviewClearanceSourceIds.Count>0)continue;
                    pier=root.BuildingPlacementController.ConfirmPlacement();if(pier!=null)break;
                }
                if(pier!=null)break;
            }
            root.BuildingPlacementController.CancelPlacement();
            File.WriteAllLines(Evidence+"/pier-placement.txt",placementDiagnostics);
            root.BuildingPlacementController.enabled=true;
            Assert.That(pier,Is.Not.Null,"A pier is placed on a real shore");
            Assert.That(pier.IsOperational,Is.False);
            var pierApproach=root.ConstructionWorkTarget(pier);
            File.WriteAllText(Evidence+"/pier-route.txt","Approach="+pierApproach+"\n"+string.Join("\n",root.CharacterPresentations.Active.Where(x=>x!=null&&x.IsBound&&root.CanCommandResident(x.CharacterId)).Select(x=>x.CharacterId+" "+x.transform.position+" reachable="+Game.Presentation.AI.ReachableNavMeshPoint.TryFind(x.transform.position,new Vector3(pierApproach.X,pierApproach.Y,pierApproach.Z),out _))));
            var installed=false;root.Clock.Resume();until=Time.realtimeSinceStartup+180;
            while(Time.realtimeSinceStartup<until&&!pier.IsOperational)
            {
                installed|=pier.ConstructionStageIndex>0;yield return null;
            }
            Dump(root,"pier");
            Assert.That(installed,Is.True,"Real carriers deliver and install construction materials");
            Assert.That(pier.IsOperational,Is.True,"Distant construction finishes without moving workers manually");
            root.Clock.Pause();
            var worker=root.Facilities.HomeAssignments.Keys.First(id=>root.CanCommandResident(id)&&root.AiAgents.TryGet(id,out _)&&root.CharacterPresentations.TryGet(id,out _));
            foreach(var job in root.Work.GetQueue(worker).ToArray())root.Work.Cancel(job.Id);
            root.AiScheduler.ReplaceWithPlayerCommand(worker);
            root.SelectionProbe.SelectCharacterIds(new[]{worker});
            root.BuildingPresentations.TryGet(pier.Id,out var pierView);
            var pierPosition=pierView.transform.position;
            root.SelectionProbe.SelectBuilding(pierView);
            Assert.That(root.SelectionProbe.SelectedCharacterIds.Count,Is.EqualTo(0));
            var inspectorOrder=root.GameplayUi.IssuePierFishing(pier,new WorldPosition(pierPosition.x,pierPosition.y,pierPosition.z));
            Assert.That(inspectorOrder.AssigneeId.HasValue,Is.True,"Pier card automatically chooses a resident after building selection clears character selection");
            Assert.That(root.Production.RequestedFishingPiers[inspectorOrder.AssigneeId.Value],Is.EqualTo(pier.Id));
            root.Work.Cancel(inspectorOrder.Id);
            root.SelectionProbe.SelectCharacterIds(new[]{worker});
            root.GameplayUi.OpenContextMenu(new ContextCommandTarget(ContextTargetKind.Building,
                new WorldPosition(pierPosition.x,pierPosition.y,pierPosition.z),pier.Id),new Vector2(700,400));
            Assert.That(root.GameplayUi.ContextActionStates[ContextActionKind.Gather].Enabled,Is.True,"Built pier offers reachable fishing");
            var before=root.Production.GetProducedTotal(new ResourceId("fish"));
            var command=root.GameplayUi.ExecuteContextAction(ContextActionKind.Gather,false);
            Assert.That(command.Orders.Count,Is.EqualTo(1));
            Assert.That(root.Production.RequestedFishingPiers[worker],Is.EqualTo(pier.Id));
            root.Clock.Resume();until=Time.realtimeSinceStartup+90;
            while(Time.realtimeSinceStartup<until&&root.Production.GetProducedTotal(new ResourceId("fish"))==before)yield return null;
            Dump(root,"fishing");
            Assert.That(root.Production.GetProducedTotal(new ResourceId("fish")),Is.GreaterThan(before),"The selected resident reaches this pier and catches real fish");
            foreach(var job in root.Work.GetQueue(worker).ToArray())root.Work.Cancel(job.Id);
            root.AiScheduler.ReplaceWithPlayerCommand(worker);
            root.AiAgents.TryGet(worker,out var tired);tired.Needs.Advance(0,Math.Max(0,tired.Needs.Energy-.10));
            until=Time.realtimeSinceStartup+90;
            while(Time.realtimeSinceStartup<until&&!root.Domestic.IsSleepingInBed(worker))yield return null;
            Dump(root,"sleep");
            Assert.That(root.Domestic.IsSleepingInBed(worker),Is.True,"An exhausted resident reaches their assigned house and sleeps");
            root.Clock.Pause();
        }
        private static IEnumerator SnowRoute(LocalSceneCompositionRoot root)
        {
            var snow=root.GetComponent<DeformableSnowPresentation>();var map=root.TiledWorld.ArtWorldToDesign;
            var mountain=new WorldPosition((855-map.x)/map.z,0,(792-map.y)/map.w);
            root.TiledWorld.RefreshNow(mountain,new[]{mountain},Array.Empty<Tuple<WorldPosition,WorldPosition>>());
            yield return new WaitForSecondsRealtime(3);
            var start=Vector3.zero;var end=Vector3.zero;var found=false;var path=new NavMeshPath();
            for(var z=700;z<=850&&!found;z+=5)for(var x=770;x<=950&&!found;x+=5)
            {
                var point=new Vector3((x-map.x)/map.z,0,(z-map.y)/map.w);
                point.y=root.WorldHeight.SampleHeight(point.x,point.z);
                if(!snow.IsTrackableSnow(point)||!snow.IsTrackableSnow(point+Vector3.forward*18))continue;
                if(!NavMesh.SamplePosition(point,out var a,2,NavMesh.AllAreas)||!NavMesh.SamplePosition(point+Vector3.forward*18,out var b,3,NavMesh.AllAreas))continue;
                if(!snow.IsTrackableSnow(a.position)||!snow.IsTrackableSnow(b.position))continue;
                if(!NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                start=a.position;end=b.position;found=true;
            }
            Assert.That(found,Is.True,"There is a walkable snowy route");
            snow.QueueAround(start,2);snow.QueueAround(end,2);
            root.CameraController.FocusPosition(start,16);yield return new WaitForSecondsRealtime(5);
            var preparationDeadline=Time.realtimeSinceStartup+30;
            while(Time.realtimeSinceStartup<preparationDeadline&&(!snow.IsPreparedAt(start)||!snow.IsPreparedAt(end)))yield return null;
            Assert.That(snow.IsPreparedAt(start)&&snow.IsPreparedAt(end),Is.True,"Snow is prepared ahead of the walk");
            root.CameraController.enabled=false;
            var camera=Camera.main;var brain=camera.GetComponents<Behaviour>().FirstOrDefault(x=>x.GetType().FullName=="Unity.Cinemachine.CinemachineBrain");
            if(brain!=null)brain.enabled=false;
            camera.transform.SetPositionAndRotation(start+new Vector3(7,6,-6),Quaternion.LookRotation(start+Vector3.forward*5-(start+new Vector3(7,6,-6))));
            yield return new WaitForSecondsRealtime(2);
            Capture("snow-before");
            var frames=new List<float>();for(var i=0;i<120;i++){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}
            frames.Sort();File.WriteAllText(Evidence+"/snow-frame-times.txt","close camera, weather on: mean="+frames.Average()+" p95="+frames[114]+" max="+frames.Last()+" ms\n");
            var weather=UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include)
                .Where(x=>(x.gameObject.name=="Localized alpine snowfall"||x.gameObject.name=="Mountain lake ground mist")).ToArray();
            var landscapes=UnityEngine.Object.FindObjectsByType<LateLandscapePresentation>();
            foreach(var landscape in landscapes)landscape.enabled=false;
            foreach(var effect in weather)effect.gameObject.SetActive(false);
            frames.Clear();for(var i=0;i<120;i++){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}
            frames.Sort();File.AppendAllText(Evidence+"/snow-frame-times.txt","close camera, weather off: mean="+frames.Average()+" p95="+frames[114]+" max="+frames.Last()+" ms\n");
            foreach(var landscape in landscapes)landscape.enabled=true;
            var resident=root.CharacterPresentations.Active.First(x=>x!=null&&x.IsBound&&root.CanCommandResident(x.CharacterId)&&root.AiRuntime.TryGetMovement(x.CharacterId,out _));
            var worker=resident.CharacterId;
            root.SetPerformanceTierPolicySuppressed(true);
            foreach(var job in root.Work.GetQueue(worker).ToArray())root.Work.Cancel(job.Id);
            root.AiScheduler.ReplaceWithPlayerCommand(worker);
            var navigation=resident.GetComponent<NavMeshAgent>();navigation.enabled=true;Assert.That(navigation.Warp(start),Is.True);
            root.AiAgents.TryGet(worker,out var state);state.UpdatePosition(new WorldPosition(start.x,start.y,start.z));
            state.Needs.RecoverEnergy(1);state.Needs.SatisfyHunger(1);state.Needs.SatisfyThirst(1);
            yield return new WaitForSecondsRealtime(.3f);var before=snow.VisibleTrackCount;
            var walk=root.Work.CreateMoveJob(worker,new WorldPosition(end.x,end.y,end.z),WorkPriority.Immediate);
            root.Clock.SetSpeed(GameSpeed.Maximum);root.Clock.Resume();var until=Time.realtimeSinceStartup+30;
            while(Time.realtimeSinceStartup<until&&!walk.IsTerminal)yield return null;
            root.Clock.Pause();
            var trailDeadline=Time.realtimeSinceStartup+10;
            while(Time.realtimeSinceStartup<trailDeadline&&snow.VisibleTrackCount-before<=8)yield return null;
            Capture("snow-after-real-walk-x10");
            File.AppendAllText(Evidence+"/snow-frame-times.txt","walk="+walk.Status+" new footprints="+(snow.VisibleTrackCount-before)+" depression="+snow.MaximumDepression+"\n");
            Assert.That(walk.Status,Is.EqualTo(WorkJobStatus.Completed));
            Assert.That(snow.VisibleTrackCount-before,Is.GreaterThan(8),"Normal navigation at x10 leaves continuous footprints");
            Assert.That(snow.MaximumDepression,Is.GreaterThan(.06f));
            root.CameraController.enabled=true;if(brain!=null)brain.enabled=true;
            root.SetPerformanceTierPolicySuppressed(false);
        }
        private static void Capture(string name)
        {
            var camera=Camera.main;var target=RenderTexture.GetTemporary(1280,720,24);
            var previous=camera.targetTexture;var active=RenderTexture.active;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(Evidence+"/"+name+".png",texture.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;UnityEngine.Object.Destroy(texture);RenderTexture.ReleaseTemporary(target);}
        }
        private static void Dump(LocalSceneCompositionRoot root,string phase)
        {
            File.WriteAllLines(Evidence+"/"+phase+"-jobs.txt",root.Work.Jobs.Select(x=>x.Id+" "+x.Type+" "+x.Status+" "+x.AssigneeId+" "+x.ProgressTicks+" "+x.Target.EntityId));
            File.WriteAllLines(Evidence+"/"+phase+"-residents.txt",root.Characters.GetAll().Where(x=>root.CanCommandResident(x.Id)).Select(x=>
                root.AiAgents.TryGet(x.Id,out var agent)?x.Id+" "+agent.CurrentAction+" "+agent.Phase+" "+agent.Position+" energy="+agent.Needs.Energy:x.Id.ToString()));
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();}
    }
}
