using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Diagnostics;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Presentation.Environment;
using Game.Simulation.Buildings;
using Game.Simulation.Time;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Infrastructure.Integration
{
    // Explicit opt-in, disposable session; no performance workload and no user save slot is overwritten.
    public sealed class WM15MorningPlayerSmoke : MonoBehaviour
    {
        private string output;
        private readonly List<string> checks=new();
        private readonly List<string> errors=new();
        private float deadline;
        private bool finished;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args=Environment.GetCommandLineArgs();var index=Array.IndexOf(args,"-wm15MorningSmoke");
            if(index<0 || index+1>=args.Length)return;
            var host=new GameObject("Morning feedback functional route");DontDestroyOnLoad(host);
            var runner=host.AddComponent<WM15MorningPlayerSmoke>();
            runner.output=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(runner.output);
            runner.deadline=Time.realtimeSinceStartup+1800;
            Application.runInBackground=true;Application.logMessageReceived+=runner.OnLog;
            runner.StartCoroutine(runner.Run());
        }
        private void OnLog(string message,string stack,LogType type)
        { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert){errors.Add(message+"\n"+stack);File.WriteAllLines(Path.Combine(output,"errors.txt"),errors);} }
        private void Check(bool ok,string label)
        { checks.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllLines(Path.Combine(output,"checks.txt"),checks);Debug.Log("MORNING_SMOKE "+checks.Last()); }
        private void Update(){if(!finished && Time.realtimeSinceStartup>deadline){Check(false,"Route timeout");Finish();}}
        private IEnumerator Capture(LocalSceneCompositionRoot root,string name)
        {
            root.WeatherPresentation.ShowPreview(root.Weather.GetWeatherForDay(0),12*TimeSpan.TicksPerHour);
            yield return null;yield return new WaitForEndOfFrame();
            var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);
            root.WeatherPresentation.ClearPreview(root.Clock.State.CalendarTicks);
        }
        private IEnumerator Run()
        {
            MainMenuController menu;
            while((menu=FindAnyObjectByType<MainMenuController>())==null)yield return null;
            yield return new WaitForSecondsRealtime(2);menu.StartNewGame(1380062136);
            LocalSceneCompositionRoot root;
            while((root=FindAnyObjectByType<LocalSceneCompositionRoot>())==null || !root.IsInitialized)yield return null;
            root.Clock.Pause();root.GameplayUi.SetOnboardingVisible(false);
            var args=Environment.GetCommandLineArgs();var resume=Array.IndexOf(args,"-wm15MorningResume");
            if(resume>=0 && resume+1<args.Length)
            {
                yield return Guard(ResumeConstruction(root,StableEntityId.Parse(args[resume+1])));
                Check(errors.Count==0,"No runtime errors during saved construction route");Finish();yield break;
            }
            Check(errors.Count==0,"New game initialized without runtime errors");
            Check(!FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(x=>
                x.name.StartsWith("World Map V2 Crossing ") || x.name.StartsWith("World Map V2 NavLink crossing:")),"No generated bridge models or crossing links");
            Check(FindAnyObjectByType<LateLandscapePresentation>().SnowSceneryCount==0,"Painted snow rock props removed");
            yield return Capture(root,"01-start");

            root.Diagnostics.Execute(DiagnosticCommandKind.UnlockAll);
            root.Diagnostics.Execute(DiagnosticCommandKind.GrantResources);
            yield return Guard(Gate(root));
            yield return Guard(Hunt(root));
            yield return Guard(Camp(root));
            yield return Guard(Neighbour(root));
            foreach(var lake in root.WorldDefinition.MapData.Hydrology.Lakes)
            {
                var point=root.WorldDefinition.MapData.CoordinateTransform.ToUnity(lake.Center);
                root.CameraController.FocusPosition(new Vector3(point.X,point.Y,point.Z),110);
                yield return new WaitForSecondsRealtime(1);yield return Capture(root,"lake-"+lake.Id);
            }
            Check(errors.Count==0,"No runtime errors during functional route");Finish();
        }
        private IEnumerator Guard(IEnumerator routine)
        {
            var stack=new Stack<IEnumerator>();stack.Push(routine);
            while(stack.Count>0)
            {
                object next=null;bool moved;
                try { moved=stack.Peek().MoveNext();if(moved)next=stack.Peek().Current; }
                catch(Exception e){Check(false,"Scenario exception: "+e);yield break;}
                if(!moved){stack.Pop();continue;}
                if(next is IEnumerator nested)stack.Push(nested);else yield return next;
            }
        }
        private void CompleteFixture(LocalSceneCompositionRoot root,Building building)
        {
            foreach(var amount in building.ConstructionMaterials.ToArray())
            {
                var source=root.Inventories.All.First(x=>x.Owner!=building.ConstructionBufferOwner && x.GetAmount(amount.ResourceId).Units>=amount.RemainingUnits);
                var result=root.ConstructionSites.Deliver(building,source.Owner,amount.ResourceId,new ResourceQuantity(amount.RemainingUnits));
                if(result!=Game.Simulation.Resources.TransferFailureReason.None)throw new InvalidOperationException("Fixture delivery: "+result);
            }
            root.ConstructionSites.NotifyDelivery(building);building.MarkCompleted();
            foreach(var job in root.Work.Jobs.Where(x=>!x.IsTerminal && (x.Target.EntityId==building.Id || x.Destination?.EntityId==building.Id)).ToArray())root.Work.Cancel(job.Id);
        }
        private IEnumerator Gate(LocalSceneCompositionRoot root)
        {
            var wallDef=root.BuildingCatalog.ResolveDefinition(new BuildingDefinitionId("palisade"));
            Building wall=null;
            for(var z=-28;z<=30 && wall==null;z+=4)
            for(var x=-30;x<=30 && wall==null;x+=4)
            {
                var cells=Enumerable.Range(0,9).Select(i=>new GridCoordinate(x+i,z)).ToArray();
                var request=new BuildingPlacementRequest(wallDef,cells[0],BuildingOrientation.North,root.StarterSettlementId,cells);
                if(!root.BuildingPlacementService.Evaluate(request).IsValid)continue;
                wall=root.BuildingPlacementService.Confirm(request);wall.MarkCompleted();root.BuildingSpawner.Spawn(wall);
            }
            Check(wall!=null,"Palisade fixture uses valid dry placement");if(wall==null)yield break;
            var middle=wall.PlacementCells[4];
            var target=root.BuildingSpawner.GridToWorld(middle);
            root.BuildingPlacementController.TryStartPlacement(new BuildingDefinitionId("gate"));
            var valid=root.BuildingPlacementController.TryMovePreviewToWorldPoint(target);
            Check(valid,"Gate preview accepts existing player palisade");
            var gate=valid?root.BuildingPlacementController.ConfirmPlacement():null;root.BuildingPlacementController.CancelPlacement();
            Check(gate!=null && wall.PlacementCells.Count==6,"Gate replaces exactly three wall cells");
            if(gate!=null)
            {
                CompleteFixture(root,gate);root.BuildingPathing.SetGateOpen(gate.Id,true);
                root.CameraController.FocusPosition(target,17);yield return new WaitForSecondsRealtime(1);
                Check(root.BuildingPresentations.TryGet(gate.Id,out var view) && !view.GetComponent<BoxCollider>().enabled,
                    "Open gate has no blocking root collider");
                var gatePosition=view.transform.position;
                var rayClear=NavMesh.SamplePosition(gatePosition+Vector3.forward*2,out var front,2,NavMesh.AllAreas) &&
                    NavMesh.SamplePosition(gatePosition-Vector3.forward*2,out var rear,2,NavMesh.AllAreas) &&
                    !NavMesh.Raycast(front.position,rear.position,out _,NavMesh.AllAreas);
                Check(rayClear,"Open gate permits direct navigation through the wall");
                yield return Capture(root,"02-gate-open");
                root.BuildingPathing.SetGateOpen(gate.Id,false);yield return Capture(root,"03-gate-closed");
            }
        }
        private IEnumerator Hunt(LocalSceneCompositionRoot root)
        {
            root.CharacterPresentations.TryGet(root.MaleDemoCharacter.Id,out var hunterView);
            var prey=FindObjectsByType<WorldMapWildlifeView>(FindObjectsSortMode.None).OrderBy(x=>Vector3.Distance(x.transform.position,hunterView.transform.position)).FirstOrDefault(x=>
                root.Production.Animals.TryGet(x.StableId,out var health) && health.Alive && health.Maximum>30);
            Check(prey!=null,"Living wildlife available for flee route");if(prey==null)yield break;
            var id=prey.StableId;var start=prey.transform.position;
            var worker=root.MaleDemoCharacter.Id;
            root.SelectionProbe.SelectCharacterIds(new[]{worker});
            root.GameplayUi.OpenContextMenu(new ContextCommandTarget(ContextTargetKind.ResourceSource,
                new WorldPosition(start.x,start.y,start.z),id,new ResourceId("meat"),ContextResourceClass.Gatherable),new Vector2(700,400));
            var command=root.GameplayUi.ExecuteContextAction(ContextActionKind.Gather,false);root.GameplayUi.CloseContextMenu();
            Check(command.Orders.Count==1,"Hunt command accepted");
            var impact=false;void Hit(Game.Domain.World.HuntingImpact hit){if(hit.Animal==id && hit.Damage>0)impact=true;}
            root.Production.Animals.Impact+=Hit;
            root.Clock.SetSpeed(GameSpeed.Fast);root.Clock.Resume();var until=Time.realtimeSinceStartup+85;
            while(!impact && Time.realtimeSinceStartup<until)yield return null;
            Check(impact,"Real arrow hit reaches animal health authority");
            if(impact)
            {
                var before=prey.transform.position;yield return new WaitForSecondsRealtime(2);
                var population=FindAnyObjectByType<WorldMapWildlifePresenter>().Population;
                Check(population.TryGet(id,out var state) && state.FleeSequence>0,"Damage starts canonical flee state");
                Check(Vector3.Distance(before,prey.transform.position)>1,"Wounded wildlife moves away");
                root.CameraController.FocusPosition(prey.transform.position,20);yield return Capture(root,"04-flee");
            }
            root.Production.Animals.Impact-=Hit;root.Clock.Pause();root.AiScheduler.ReplaceWithPlayerCommand(worker);
        }
        private IEnumerator Camp(LocalSceneCompositionRoot root)
        {
            Building camp=null;
            root.BuildingPlacementController.TryStartPlacement(new BuildingDefinitionId("fishing_camp"));
            foreach(var lake in root.WorldDefinition.MapData.Hydrology.Lakes)
            {
                foreach(var point in lake.Shoreline.Points.Where((_,i)=>i%4==0))
                {
                    var position=root.WorldDefinition.MapData.CoordinateTransform.ToUnity(point);
                    root.CameraController.FocusPosition(new Vector3(position.X,0,position.Z),45);
                    yield return null;
                    if(!root.BuildingPlacementController.TryMovePreviewToWorldPoint(new Vector3(position.X,0,position.Z)))continue;
                    if(root.BuildingPlacementController.PreviewClearanceSourceIds.Count>0)continue;
                    camp=root.BuildingPlacementController.ConfirmPlacement();if(camp!=null)break;
                }
                if(camp!=null)break;
            }
            Check(camp!=null,"Fishing camp snaps to a valid real shore");root.BuildingPlacementController.CancelPlacement();
            if(camp==null)yield break;
            // The fixture pays construction materials; this route tests camp work, not elapsed construction time.
            CompleteFixture(root,camp);
            var site=root.Production.Sites.Single(x=>x.OwnerBuildingId==camp.Id);
            var worker=root.Characters.GetAll().First(x=>root.CanCommandResident(x.Id) && x.Id!=root.MaleDemoCharacter.Id &&
                root.CharacterPresentations.TryGet(x.Id,out _)).Id;
            root.SetPerformanceTierPolicySuppressed(true);
            root.TiledWorld.RefreshNow(site.Position,new[]{site.Position},Array.Empty<Tuple<WorldPosition,WorldPosition>>());
            yield return new WaitForSecondsRealtime(1);
            root.AiRuntime.TryGetMovement(worker,out var movement);
            var entrance=new Vector3(site.Position.X,site.Position.Y,site.Position.Z);
            if(NavMesh.SamplePosition(entrance,out var hit,4,NavMesh.AllAreas))movement.Presenter.GetComponent<NavMeshAgent>().Warp(hit.position);
            if(root.AiAgents.TryGet(worker,out var workerState))workerState.UpdatePosition(movement.CurrentPosition);
            root.SelectionProbe.SelectCharacterIds(new[]{worker});
            root.GameplayUi.OpenContextMenu(new ContextCommandTarget(ContextTargetKind.Building,site.Position,camp.Id),new Vector2(700,400));
            Check(root.GameplayUi.ContextActionStates.ContainsKey(ContextActionKind.Craft),"Camp offers its own fishing work");
            if(root.GameplayUi.ContextActionStates.TryGetValue(ContextActionKind.Craft,out var availability))
                Check(availability.Enabled,"Camp command availability: "+availability.Reason);
            var command=root.GameplayUi.ExecuteContextAction(ContextActionKind.Craft,false);root.GameplayUi.CloseContextMenu();
            Check(command.Orders.Count==1,"Camp worker assignment accepted");
            root.Clock.SetSpeed(GameSpeed.VeryFast);root.Clock.Resume();
            var until=Time.realtimeSinceStartup+70;var entered=false;var outputOwner=root.Production.OutputOwner(camp.Id);
            while(Time.realtimeSinceStartup<until)
            {
                entered|=root.AiScheduler.ShouldHideInsideBuilding(worker);
                if(root.Inventories.Get(outputOwner).TotalUnits>=30)break;
                yield return null;
            }
            root.Clock.Pause();Check(entered,"Fisher reaches camp and is hidden while working");
            Check(root.Inventories.Get(outputOwner).TotalUnits==30,"Camp reaches its internal limit of 30 fish");
            root.CameraController.FocusPosition(entrance+Vector3.forward*3,25);yield return Capture(root,"05-fishing-camp");
            // An empty nearby warehouse and one free carrier exercise the normal automatic haul path.
            var fish=new ResourceId("fish");
            root.Production.SetWorkplacePaused(camp.Id,site.RecipeId,true);
            var storageDef=root.BuildingCatalog.ResolveDefinition(new BuildingDefinitionId("storage_basket"));
            Building warehouse=null;
            for(var z=-18;z<=18 && warehouse==null;z+=3)
            for(var x=-18;x<=18 && warehouse==null;x+=3)
            {
                var request=new BuildingPlacementRequest(storageDef,new GridCoordinate(camp.Coordinate.X+x,camp.Coordinate.Z+z),BuildingOrientation.North,root.StarterSettlementId);
                if(!root.BuildingPlacementService.Evaluate(request).IsValid)continue;
                warehouse=root.BuildingPlacementService.Confirm(request);warehouse.MarkCompleted();
                root.Storage.Attach(warehouse,storageDef);root.BuildingSpawner.Spawn(warehouse);
            }
            Check(warehouse!=null,"Empty warehouse available near camp");
            if(warehouse!=null)
            {
                var store=root.Inventories.Get(new InventoryOwner(InventoryOwnerKind.BuildingStorage,warehouse.Id));
                var carrier=root.Characters.GetAll().First(x=>root.CanCommandResident(x.Id) && x.Id!=worker && root.AiRuntime.TryGetMovement(x.Id,out _)).Id;
                root.AiScheduler.ReplaceWithPlayerCommand(carrier);
                root.AiRuntime.TryGetMovement(carrier,out var carrierMove);
                if(NavMesh.SamplePosition(entrance+Vector3.right*3,out var carrierStart,6,NavMesh.AllAreas))carrierMove.Presenter.GetComponent<NavMeshAgent>().Warp(carrierStart.position);
                if(root.AiAgents.TryGet(carrier,out var carrierState))carrierState.UpdatePosition(carrierMove.CurrentPosition);
                root.Clock.Resume();until=Time.realtimeSinceStartup+60;
                while(Time.realtimeSinceStartup<until && store.GetAmount(fish).Units==0)yield return null;
                root.Clock.Pause();Check(store.GetAmount(fish).Units>0,"Automatic carrier deposits real camp fish in warehouse");
                var producedBefore=root.Production.GetProducedTotal(fish);
                root.Production.SetWorkplacePaused(camp.Id,site.RecipeId,false);
                root.Clock.Resume();yield return new WaitForSecondsRealtime(12);root.Clock.Pause();
                Check(root.Production.GetProducedTotal(fish)>producedBefore,"Fishing resumes after hauling frees capacity");
            }
            root.SetPerformanceTierPolicySuppressed(false);
        }
        private IEnumerator Neighbour(LocalSceneCompositionRoot root)
        {
            var faction=root.StarterEnemyAi;
            var before=faction.Residents.ToDictionary(x=>x,x=>root.Tiers.GetRuntimeState(x).Position);
            var initialProduced=faction.Production.GetProducedTotal(new ResourceId("wood_log"))+faction.Production.GetProducedTotal(new ResourceId("berries"));
            var initialBuildings=root.Buildings.All.Count(x=>x.OwnerSideId==faction.Strategic.SideId);
            var home=root.GlobalMap.GetLocation(new Game.Domain.World.WorldLocationId("enemy_settlement"));
            root.CameraController.FocusPosition(new Vector3(home.MapX,0,home.MapY),65);
            root.Clock.SetSpeed(GameSpeed.VeryFast);root.Clock.Resume();
            var moved=false;var until=Time.realtimeSinceStartup+240;
            while(Time.realtimeSinceStartup<until)
            {
                moved|=faction.Residents.Any(x=>root.Tiers.GetRuntimeState(x).Position.DistanceSquared(before[x])>4);
                if(root.Buildings.All.Count(x=>x.OwnerSideId==faction.Strategic.SideId && x.IsOperational)>initialBuildings)break;
                yield return null;
            }
            root.Clock.Pause();
            Check(moved,"Neighbour residents move for economic tasks");
            var produced=faction.Production.GetProducedTotal(new ResourceId("wood_log"))+faction.Production.GetProducedTotal(new ResourceId("berries"));
            Check(produced>initialProduced || produced>0,"Neighbour performs canonical production jobs (output="+produced+")");
            Check(!ReferenceEquals(faction.Inventories,root.Inventories),"Neighbour has separate inventory authority");
            Check(faction.Production.Sites.Where(x=>x.Kind==Game.Domain.Production.ProductionSiteKind.NaturalSource).All(x=>
                root.Production.TryGetSite(x.Id,out var canonical) && ReferenceEquals(x,canonical)),"Neighbour gathers actual shared world sources");
            Check(root.Buildings.All.Count(x=>x.OwnerSideId==faction.Strategic.SideId)>initialBuildings ||
                root.Work.Jobs.Any(x=>x.Type==WorkJobType.Build && root.Buildings.TryGet(x.Target.EntityId,out var b) && b.OwnerSideId==faction.Strategic.SideId),
                "Neighbour schedules real construction");
            yield return Capture(root,"06-neighbour");
            var checkpoint=root.Saves.Capture();
            File.WriteAllBytes(Path.Combine(output,"checkpoint.xml"),new SaveXmlCodec().Serialize(checkpoint));
            var slot=StableEntityId.Parse(Guid.NewGuid().ToString("N"));
            root.Saves.Save(slot);root.Saves.Load(slot);root.RebindAfterLoad(root.Saves.Clock);
            Check(root.StarterEnemyAi.Production.GetProducedTotal(new ResourceId("wood_log"))+root.StarterEnemyAi.Production.GetProducedTotal(new ResourceId("berries"))==produced,
                "Neighbour production survives real save/load");
            Check(root.Production.Sites.Any(x=>x.Continuous),"Continuous camp state survives real save/load");
            root.Clock.Resume();yield return new WaitForSecondsRealtime(12);root.Clock.Pause();
            Check(!FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(x=>x.name.StartsWith("World Map V2 Crossing ")),"No bridges reappear after loading");
        }
        private void Finish()
        {
            finished=true;Application.logMessageReceived-=OnLog;File.WriteAllLines(Path.Combine(output,"errors.txt"),errors);
            Application.Quit(errors.Count==0 && checks.All(x=>!x.StartsWith("FAIL"))?0:1);
        }

        private IEnumerator ResumeConstruction(LocalSceneCompositionRoot root,StableEntityId slot)
        {
            root.Saves.Load(slot);root.RebindAfterLoad(root.Saves.Clock);root.Clock.Pause();
            var faction=root.StarterEnemyAi;
            var construction=root.Buildings.All.First(x=>x.OwnerSideId==faction.Strategic.SideId && !x.IsOperational);
            Check(construction.ConstructionMaterials.All(x=>x.RemainingUnits==0),"Loaded neighbour construction retains physically delivered materials");
            var until=Time.realtimeSinceStartup+180;
            root.CameraController.FocusPosition(Vector3.zero,45);root.Clock.SetSpeed(GameSpeed.VeryFast);root.Clock.Resume();
            while(Time.realtimeSinceStartup<until && !construction.IsOperational)yield return null;
            root.Clock.Pause();Check(construction.IsOperational,"Neighbour completes its real building off camera after save/load");
            var home=root.GlobalMap.GetLocation(new Game.Domain.World.WorldLocationId("enemy_settlement"));
            root.CameraController.FocusPosition(new Vector3(home.MapX,0,home.MapY),60);
            yield return new WaitForSecondsRealtime(2);yield return Capture(root,"07-neighbour-completed");
            var camp=root.Buildings.All.First(x=>x.DefinitionId.ToString()=="fishing_camp");
            var definition=root.BuildingCatalog.ResolveDefinition(camp.DefinitionId);
            Check(!Game.Domain.World.ShoreBuildingPlacement.IsValid(root.WorldHeight,new GridCoordinate(0,0),definition.Footprint,
                BuildingOrientation.North,1,0,0),"Camp rejects an inland footprint");
            var lake=root.WorldDefinition.MapData.Hydrology.Lakes.First();
            var center=root.WorldDefinition.MapData.CoordinateTransform.ToUnity(lake.Center);
            Check(!Game.Domain.World.ShoreBuildingPlacement.IsValid(root.WorldHeight,new GridCoordinate(Mathf.RoundToInt(center.X),Mathf.RoundToInt(center.Z)),
                definition.Footprint,BuildingOrientation.North,1,0,0),"Camp rejects a footprint inside a lake");
            Check(Game.Presentation.Localization.RussianTextCatalog.Content("recipe","fish_camp")=="Ловля рыбы","Fishing production caption is translated");
            File.WriteAllBytes(Path.Combine(output,"checkpoint.xml"),new SaveXmlCodec().Serialize(root.Saves.Capture()));
        }
    }
}

