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
using Game.Domain.Work;
using Game.Presentation.Environment;
using Game.Simulation.Time;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Infrastructure.Integration
{
    public sealed partial class SR02SampleReview
    {
        private readonly List<string> routeChecks=new();
        private StableEntityId harvestedSource;
        private void CheckRoute(bool pass,string message)
        {
            routeChecks.Add((pass?"PASS ":"FAIL ")+message);
            if(!pass) {errors.Add(message);File.WriteAllLines(output+"/errors.txt",errors);}
            File.WriteAllLines(output+"/physical-routes.txt",routeChecks);
        }
        private bool DryPoint(Vector2 design,out Vector3 point)
        {
            point=Point(design.x,design.y);
            if(!root.WorldHeight.IsWater(point.x,point.z))return true;
            for(var r=.5f;r<=20;r+=.5f)for(var a=0;a<32;a++)
            {
                var candidate=Point(design.x+Mathf.Cos(a*Mathf.PI/16)*r,design.y+Mathf.Sin(a*Mathf.PI/16)*r);
                if(root.WorldHeight.IsWater(candidate.x,candidate.z)||!NavMesh.SamplePosition(candidate,out _,1,NavMesh.AllAreas))continue;
                routeChecks.Add($"WATER_ENDPOINT original D1={design}; nearest tested dry D1=({candidate.x+570},{candidate.z+120}); offset={r}m. Original endpoint is not PASS.");
                point=candidate;return true;
            }
            return false;
        }
        private IEnumerator Routes()
        {
            deadline=Time.realtimeSinceStartup+3000;
            routeChecks.Add("Fixture: fresh seed 1380062136; starting poses placed explicitly for each independent route; traversal uses actual resident movement/colliders. No resources granted. This does not prove travel from the starting settlement.");
            var ids=root.Characters.GetAll().Where(c=>root.CanCommandResident(c.Id))
                .OrderBy(c=>root.PlayerControl.Evaluate(c.Id).IsEligible?0:1).ThenBy(c=>c.Id).Take(3).Select(c=>c.Id).ToArray();
            CheckRoute(root.PlayerControl.Evaluate(ids[0]).IsEligible,"route leader has ordinary direct-control eligibility");
            root.SelectionProbe.SelectCharacterIds(ids);
            root.CameraController.FocusPosition(Point(450,610),35);
            yield return new WaitForSecondsRealtime(3);
            var candidates=new[]{new Vector2(365,555),new Vector2(430,600),new Vector2(500,620),new Vector2(520,650)};
            var routes=new List<(string name,Vector3 start,Vector3 end)>();
            for(var i=0;i<3;i++)
                if(DryPoint(candidates[i],out var a)&&DryPoint(candidates[i+1],out var b))routes.Add(("composition-"+i,a,b));
            foreach(var z in new[]{600,630,660})routes.Add(("seam-"+z,Point(495,z),Point(505,z)));
            var bridge=root.TiledWorld.RoadNetworkPlan.Crossings.OrderBy(c=>
                (c.Link.Start.X+220)*(c.Link.Start.X+220)+(c.Link.Start.Z-422)*(c.Link.Start.Z-422)).First();
            Vector3 V(WorldPosition p)=>new(p.X,p.Y,p.Z);
            var bridgeStart=V(bridge.Link.Start);var bridgeEnd=V(bridge.Link.End);
            CheckRoute(!FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Any(t=>t.name.StartsWith("World Map V2 Crossing ")),"D-204: no free generated bridge restored");
            routeChecks.Add("INTENTIONAL_WATER "+bridge.Definition.Id+" has no built bridge; authored endpoints are not a traversable crossing. "+bridgeStart+" -> "+bridgeEnd);
            var args=Environment.GetCommandLineArgs();var filterIndex=Array.IndexOf(args,"-sr02RouteFilter");
            var filter=filterIndex>=0&&filterIndex+1<args.Length?args[filterIndex+1]:null;
            if(args.Contains("-sr02WorkOnly"))routes.Clear();
            foreach(var route in routes)
            {
                if(filter!=null&&!route.name.Contains(filter))continue;
                yield return Traverse(ids.Take(1).ToArray(),route.start,route.end,route.name+" RTS forward",false);
                yield return Traverse(ids.Take(1).ToArray(),route.end,route.start,route.name+" RTS reverse",false);
                yield return Traverse(ids.Take(1).ToArray(),route.start,route.end,route.name+" direct forward",true);
                yield return Traverse(ids.Take(1).ToArray(),route.end,route.start,route.name+" direct reverse",true);
                yield return Traverse(ids,route.start,route.end,route.name+" group forward",false);
                yield return Traverse(ids,route.end,route.start,route.name+" group reverse",false);
                yield return Traverse(ids,route.start,route.end,route.name+" group direct forward",true);
                yield return Traverse(ids,route.end,route.start,route.name+" group direct reverse",true);
            }
            yield return FarCameraCollision(ids[0]);
            if(filter==null)yield return ConstructionAndResource(ids);
            root.Clock.Pause();
            var snapshot=root.Saves.Capture();
            var slot=StableEntityId.Parse("00000000000000000000000053523032");
            root.Saves.Save(slot);root.Saves.Load(slot);root.RebindAfterLoad(root.Saves.Clock);
            CheckRoute(root.Saves.Capture().SaveGenerationId==snapshot.SaveGenerationId,"sample Save/Load generation round-trip");
            if(harvestedSource.IsValid) CheckRoute(root.Production.TryGetSite(harvestedSource,out var restored)&&restored.IsDepleted,
                "same finite tree remains depleted after Save/Load");
            File.WriteAllBytes(output+"/roundtrip.xml",new SaveXmlCodec().Serialize(root.Saves.Capture()));
            var saveDirectory=Environment.GetEnvironmentVariable("SERENITY_SAVE_DIRECTORY");
            if(!string.IsNullOrWhiteSpace(saveDirectory))
            {
                var wrongSlot=StableEntityId.Parse("00000000000000000000000053523033");
                var codec=new SaveXmlCodec();var before=codec.Serialize(root.Saves.Capture());
                var text=System.Text.Encoding.UTF8.GetString(before);
                var replacement=snapshot.SaveGenerationId==SaveSnapshot.SR02SampleSaveGenerationId
                    ? SaveSnapshot.WorldMapV2SaveGenerationId:SaveSnapshot.SR02SampleSaveGenerationId;
                var wrong=codec.Deserialize(System.Text.Encoding.UTF8.GetBytes(text.Replace(
                    "<saveGenerationId>"+snapshot.SaveGenerationId+"</saveGenerationId>",
                    "<saveGenerationId>"+replacement+"</saveGenerationId>")));
                new FileSaveStore(saveDirectory).Save(wrongSlot,wrong);
                var rejected=false;try{root.Saves.Load(wrongSlot);}catch(NotSupportedException){rejected=true;}
                CheckRoute(rejected&&before.SequenceEqual(codec.Serialize(root.Saves.Capture())),
                    "different sample generation rejected before canonical state mutation");
            }
            yield return Capture("routes-save-load");
        }
        private IEnumerator Traverse(StableEntityId[] ids,Vector3 start,Vector3 end,string label,bool direct)
        {
            root.Clock.Pause();root.ThirdPersonControl.Exit();
            root.SelectionProbe.SelectCharacterIds(ids);root.CameraController.FocusPosition((start+end)*.5f,30);
            yield return null;
            var ready=true;
            for(var i=0;i<ids.Length;i++)
            {
                var id=ids[i];root.AiScheduler.ReplaceWithPlayerCommand(id);
                foreach(var job in root.Work.GetQueue(id).ToArray())root.Work.Cancel(job.Id);
                if(!root.AiRuntime.TryGetMovement(id,out var move)||!NavMesh.SamplePosition(start+Vector3.right*i*1.5f,out var hit,4,NavMesh.AllAreas)) {ready=false;continue;}
                // Camera LOD disables distant NavMeshAgents; make this explicit fixture pose physical first.
                move.SetCrowdDetail(true);
                var warped=move.Agent.Warp(hit.position);
                ready &= warped&&Vector3.Distance(move.Presenter.transform.position,hit.position)<.1f;
                if(root.AiAgents.TryGet(id,out var agent))agent.UpdatePosition(new WorldPosition(hit.position.x,hit.position.y,hit.position.z));
            }
            if(!ready){CheckRoute(false,label+" cannot establish physical starting pose");yield break;}
            // Let crowd relocation and collider carving settle before issuing the independent order.
            yield return new WaitForSecondsRealtime(1);
            root.Clock.SetSpeed(GameSpeed.Normal);root.Clock.Resume();
            var entered=!direct||root.ThirdPersonControl.TryEnter(ids[0]).IsEligible;
            if(!entered){CheckRoute(false,label+" direct entry rejected: "+root.ThirdPersonControl.LastEntryMessage);yield break;}
            if(!direct)
            {
                var issued=root.ManualMoveCommands.Issue(ids,new WorldPosition(end.x,end.y,end.z),WorkPriority.Immediate);
                CheckRoute(issued.CreatedCount==ids.Length,label+" command "+string.Join(",",issued.Entries.Select(e=>e.CharacterId+":"+e.Status)));
                if(issued.CreatedCount!=ids.Length)yield break;
            }
            else if(ids.Length>1)root.ManualMoveCommands.Issue(ids.Skip(1).ToArray(),new WorldPosition(end.x,end.y,end.z),WorkPriority.Immediate);
            var path=new NavMeshPath();var corner=1;
            if(direct&&root.AiRuntime.TryGetMovement(ids[0],out var directMover))
                NavMesh.CalculatePath(directMover.Agent.transform.position,end,NavMesh.AllAreas,path);
            File.AppendAllText(output+"/movement-trace.txt", "\n"+label+" directPath="+path.status+" corners="+
                string.Join(";",path.corners.Select(p=>p.ToString("F3")))+"\n");
            var until=Time.realtimeSinceStartup+Mathf.Max(25,Vector3.Distance(start,end)*1.3f);
            var nextTrace=0f;
            var arrived=false;var maxError=0f;
            while(Time.realtimeSinceStartup<until)
            {
                if(direct&&root.AiRuntime.TryGetMovement(ids[0],out var move))
                {
                    var corners=path.corners;
                    while(corner<corners.Length-1&&Vector3.ProjectOnPlane(corners[corner]-move.Agent.transform.position,Vector3.up).magnitude<1.2f)corner++;
                    var target=corner<corners.Length?corners[corner]:end;
                    var delta=Vector3.ProjectOnPlane(target-move.Agent.transform.position,Vector3.up);
                    // A slow frame must not overshoot a waypoint and oscillate around it.
                    var remaining=Vector3.ProjectOnPlane(end-move.Agent.transform.position,Vector3.up).magnitude;
                    move.MoveDirect(remaining<1.1f?Vector3.zero:delta,
                        Mathf.Min(Time.unscaledDeltaTime,delta.magnitude/Mathf.Max(.1f,move.Agent.speed)),false,false,1,1000);
                }
                if(Time.realtimeSinceStartup>=nextTrace)
                {
                    nextTrace=Time.realtimeSinceStartup+1;
                    foreach(var id in ids)if(root.AiRuntime.TryGetMovement(id,out var m))
                    {
                        var a=m.Agent;var on=a.isActiveAndEnabled&&a.isOnNavMesh;
                        var job=root.Work.TryGetAssigned(id,out var j)?j.Type.ToString():"none";
                        var phase=root.AiAgents.TryGet(id,out var s)?s.Phase.ToString():"none";
                        var solids=Physics.OverlapCapsule(a.transform.position+Vector3.up*.45f,
                            a.transform.position+Vector3.up*1.45f,.5f,~0,QueryTriggerInteraction.Ignore);
                        File.AppendAllText(output+"/movement-trace.txt",$"{Time.realtimeSinceStartup:F2} {id} pos={a.transform.position:F3} state={m.State} authority={m.Authority} on={on} job={job} phase={phase}"+
                            (on?$" path={a.pathStatus} pending={a.pathPending} stopped={a.isStopped} velocity={a.velocity:F3} next={a.nextPosition:F3} dest={a.destination:F3}":"")+
                            " nearby="+string.Join(";",solids.Select(c=>c.name))+"\n");
                    }
                }
                maxError=ids.Max(id=>root.AiRuntime.TryGetMovement(id,out var m)?
                    Vector3.ProjectOnPlane(m.Agent.transform.position-end,Vector3.up).magnitude:1000);
                if(maxError<(ids.Length==1?1.2f:3f)){arrived=true;break;}
                yield return null;
            }
            CheckRoute(arrived,$"{label}; max remaining={maxError:F2}m; start={start}; end={end}");
            root.ThirdPersonControl.Exit();root.Clock.Pause();
            yield return Capture(label.Replace(' ','-'));
        }
        private IEnumerator ConstructionAndResource(StableEntityId[] ids)
        {
            // Explicit progression fixture only. Stock, site buffers and work are never granted/completed here.
            var stockBefore=root.Inventories.All.Sum(i=>i.TotalUnits);
            var unlock=root.Diagnostics.Execute(DiagnosticCommandKind.UnlockAll);
            CheckRoute(unlock.Succeeded&&stockBefore==root.Inventories.All.Sum(i=>i.TotalUnits),
                "TEST FIXTURE: technologies opened, inventory units unchanged; construction still consumes real materials");
            root.Clock.Pause();root.CameraController.FocusPosition(Point(450,610),28);
            var placement=root.BuildingPlacementController;
            var valid=placement.TryStartPlacement(new BuildingDefinitionId("storage_basket"))&&
                placement.TryMovePreviewToWorldPoint(Point(450,610))&&placement.IsPreviewValid;
            CheckRoute(valid,"ordinary storage_basket preview on sample pad");
            if(!valid)yield break;
            var building=placement.ConfirmPlacement();
            CheckRoute(building!=null,"canonical funded construction blueprint");
            if(building==null)yield break;
            var builders=root.DirectWorkCommands.ProfessionGroups.Primary(WorkGroupRole.Builders);
            foreach(var id in ids)
            {
                root.DirectWorkCommands.ProfessionGroups.Reassign(id,builders.Id);
                root.Work.SetMemberShift(builders.Id,id,WorkShiftKind.Day);
            }
            root.Clock.SetSpeed(GameSpeed.VeryFast);root.Clock.Resume();
            var until=Time.realtimeSinceStartup+420;
            while(!building.IsOperational&&Time.realtimeSinceStartup<until)yield return null;
            CheckRoute(building.IsOperational,"basket completed by physical material delivery/building; no resource grants");
            yield return Capture("sample-construction");
            root.Clock.Pause();
            var source=root.Production.Sites.Where(s=>s.RecipeId.ToString()=="chop_wood"&&!s.IsDepleted&&
                root.WorldHeight.Landscape.Contains(s.Position.X,s.Position.Z)).OrderBy(s=>
                s.Position.DistanceSquared(new WorldPosition(-120,19,490))).FirstOrDefault();
            CheckRoute(source!=null,"finite wood source near sample clearing");
            if(source==null)yield break;
            foreach(var id in ids)root.DirectWorkCommands.ProfessionGroups.Reassign(id,
                root.DirectWorkCommands.ProfessionGroups.Primary(WorkGroupRole.General).Id);
            var before=source.RemainingAmount;
            var wood=new ResourceId("wood_log");
            var store=root.Inventories.Get(new InventoryOwner(InventoryOwnerKind.BuildingStorage,building.Id));
            var storedBefore=store.GetAmount(wood).Units;
            root.SelectionProbe.SelectCharacterIds(ids);
            root.DirectWorkCommands.Issue(WorkJobType.Chop,
                WorkTarget.ForEntity(WorkTargetKind.ProductionSite,source.Id,source.Position),WorkPriority.Immediate);
            root.Clock.Resume();until=Time.realtimeSinceStartup+180;
            while(!source.IsDepleted&&Time.realtimeSinceStartup<until)yield return null;
            CheckRoute(source.IsDepleted,$"physical tree work depleted source {source.Id}: {before}->{source.RemainingAmount}");
            if(source.IsDepleted)harvestedSource=source.Id;
            until=Time.realtimeSinceStartup+180;
            while(store.GetAmount(wood).Units<=storedBefore&&Time.realtimeSinceStartup<until)yield return null;
            CheckRoute(store.GetAmount(wood).Units>storedBefore,
                $"physical wood delivery into sample basket: {storedBefore}->{store.GetAmount(wood).Units}");
            yield return Capture("sample-harvest");
            root.Clock.Pause();
            yield return HouseEntry(ids);
        }
        private IEnumerator FarCameraCollision(StableEntityId id)
        {
            root.Clock.Pause();root.ThirdPersonControl.Exit();root.SelectionProbe.SelectCharacterIds(new[]{id});
            var tree=root.TiledWorld.VegetationPlan.Instances.Where(v=>WorldMapVegetationRenderer.IsTree(v)&&
                root.WorldHeight.Landscape.Contains(v.Position.X,v.Position.Z)).OrderBy(v=>v.Position.DistanceSquared(
                    new WorldPosition(-120,19,490))).First();
            var trunk=new Vector3(tree.Position.X,tree.Position.Y,tree.Position.Z);
            if(!root.AiRuntime.TryGetMovement(id,out var move)||!NavMesh.SamplePosition(trunk+Vector3.right*3,out var hit,2,NavMesh.AllAreas))
            {CheckRoute(false,"far-camera collision fixture unavailable");yield break;}
            root.AiScheduler.ReplaceWithPlayerCommand(id);move.SetCrowdDetail(true);
            move.Agent.Warp(hit.position);if(root.AiAgents.TryGet(id,out var agent))agent.UpdatePosition(
                new WorldPosition(hit.position.x,hit.position.y,hit.position.z));
            root.CameraController.FocusPosition(Point(185,840),35);
            yield return new WaitForSecondsRealtime(3);
            var proxy=FindObjectsByType<WorldMapVegetationColliderProxy>(FindObjectsSortMode.None).FirstOrDefault(p=>p.StableId==tree.StableId);
            CheckRoute(proxy!=null&&proxy.GetComponent<CapsuleCollider>().enabled,
                "tree collision retained near selected resident with camera at start settlement");
            if(proxy!=null)CheckRoute(Physics.Linecast(hit.position+Vector3.up,trunk+Vector3.up,out var obstruction,~0,QueryTriggerInteraction.Ignore)&&
                obstruction.collider.GetComponentInParent<WorldMapVegetationColliderProxy>()!=null,
                "far-camera physical trunk intercepts approach ray");
            var population=FindAnyObjectByType<P109TiledWorldPopulationPresenter>().Plan;
            var rocks=population.Resources.Count(n=>n.RecipeId=="gather_stone"&&root.WorldHeight.Landscape.Contains(n.Position.X,n.Position.Z));
            var trees=FindObjectsByType<WorldMapVegetationColliderProxy>(FindObjectsSortMode.None).Count(p=>
                root.WorldHeight.Landscape.Contains(p.transform.position.x,p.transform.position.z));
            CheckRoute(rocks<=24&&trees+rocks<=180&&root.TiledWorld.ActiveVegetationColliderCount<=650,
                $"sample collision budget: active tree proxies={trees}, all stone sources={rocks}, global tree proxies={root.TiledWorld.ActiveVegetationColliderCount}");
            yield return Capture("far-camera-collision");
        }
        private IEnumerator ResumeWork()
        {
            deadline=Time.realtimeSinceStartup+1200;
            var slot=StableEntityId.Parse("00000000000000000000000053523032");
            root.Saves.Load(slot);root.RebindAfterLoad(root.Saves.Clock);
            CheckRoute(true,"loaded actual routes11 save after profession reassignment");
            CheckRoute(root.Buildings.All.All(b=>root.BuildingPresentations.TryGet(b.Id,out var v)&&ReferenceEquals(v.Building,b)),
                "all loaded buildings have presenters bound to restored canonical objects");
            foreach(var mover in root.AiRuntime.Movements)
                mover.FailureObserved=reason=>File.AppendAllText(output+"/movement-failures.txt",
                    $"{Time.realtimeSinceStartup:F2} {mover.CharacterId} {mover.CurrentPosition} crowd={mover.CrowdDetailEnabled} {reason}\n");
            var ids=root.Characters.GetAll().Where(c=>root.CanCommandResident(c.Id))
                .OrderBy(c=>root.PlayerControl.Evaluate(c.Id).IsEligible?0:1).ThenBy(c=>c.Id).Take(3).Select(c=>c.Id).ToArray();
            var house=root.Buildings.All.Where(b=>b.DefinitionId.FamilyId==new BuildingDefinitionId("stone_age_hut")&&b.OwnerSideId.ToString()=="player")
                .OrderBy(b=>root.ConstructionWorkTarget(b).DistanceSquared(new WorldPosition(-107,19,490))).First();
            root.CameraController.FocusPosition(Point(462,610),28);root.Clock.SetSpeed(GameSpeed.VeryFast);
            yield return HouseEntry(ids,house);
            root.Saves.Save(slot);root.Saves.Load(slot);root.RebindAfterLoad(root.Saves.Clock);
            var source=StableEntityId.Parse("3e41f23919dd1474b3608050086e5311");
            CheckRoute(root.Production.TryGetSite(source,out var restored)&&restored.IsDepleted,"harvested tree stays depleted after resumed Save/Load");
            File.WriteAllBytes(output+"/roundtrip.xml",new SaveXmlCodec().Serialize(root.Saves.Capture()));
        }
        private IEnumerator HouseEntry(StableEntityId[] ids,Building house=null)
        {
            if(house==null)
            {
            var placement=root.BuildingPlacementController;
            var valid=placement.TryStartPlacement(new BuildingDefinitionId("stone_age_hut"))&&
                placement.TryMovePreviewToWorldPoint(Point(462,610))&&placement.IsPreviewValid;
            CheckRoute(valid,"funded hut preview on sample pad; failure="+placement.LastConstructionFailure);
            if(!valid)yield break;
            house=placement.ConfirmPlacement();CheckRoute(house!=null,"hut canonical blueprint");
            if(house==null)yield break;
            }
            var builders=root.DirectWorkCommands.ProfessionGroups.Primary(WorkGroupRole.Builders);
            foreach(var id in ids)root.DirectWorkCommands.ProfessionGroups.Reassign(id,builders.Id);
            root.Clock.Resume();var until=Time.realtimeSinceStartup+420;
            var nextTrace=0f;
            while(!house.IsOperational&&Time.realtimeSinceStartup<until)
            {
                if(Time.realtimeSinceStartup>=nextTrace)
                {
                    nextTrace=Time.realtimeSinceStartup+5;
                    File.AppendAllText(output+"/construction-trace.txt",Time.realtimeSinceStartup+" failures="+
                        string.Join(",",root.AiScheduler.HaulExecutionFailures.Select(p=>p.Key+":"+p.Value))+"\n");
                    foreach(var id in root.Characters.GetAll().Where(c=>root.CanCommandResident(c.Id)).Select(c=>c.Id))
                        if(root.AiRuntime.TryGetMovement(id,out var m)&&root.AiAgents.TryGet(id,out var a))
                            File.AppendAllText(output+"/construction-trace.txt",$"{id} {m.CurrentPosition} {m.State} {a.CurrentAction}/{a.Phase}"+
                                (root.Work.TryGetAssigned(id,out var j)?$" job={j.Id} {j.Type} target={j.Target.Position} destination={j.Destination?.Position}":"")+"\n");
                }
                yield return null;
            }
            CheckRoute(house.IsOperational,"hut physically built with real materials");
            if(!house.IsOperational)yield break;
            CheckRoute(root.BuildingPresentations.TryGet(house.Id,out var houseView)&&
                houseView.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&r.gameObject.activeInHierarchy),
                "completed hut has a visible physical model");
            yield return Capture("sample-house-completed");
            var occupants=root.Facilities.HomeAssignments.Where(p=>p.Value==house.Id&&root.CanCommandResident(p.Key))
                .Select(p=>p.Key).ToArray();
            var candidates=occupants.Where(id=>ids.Contains(id)).ToArray();
            if(candidates.Length==0)candidates=occupants.Length>0?occupants:ids;
            var sleeper=candidates.OrderBy(id=>root.AiAgents.TryGet(id,out var a)?a.Needs.Energy:1).First();
            if(!root.Facilities.HomeAssignments.TryGetValue(sleeper,out var currentHome)||currentHome!=house.Id)
                root.Facilities.UnassignHome(sleeper);
            var assignment=root.Facilities.AssignHome(sleeper,house.Id);
            CheckRoute(assignment.Succeeded,"entry resident assigned to sample hut; resident="+sleeper+" result="+assignment.Failure);
            if(!assignment.Succeeded)yield break;
            // A rested resident can enter and finish voluntary sleep within one simulation tick.
            // Let the ordinary clock reach the scheduled night, without changing needs or time.
            until=Time.realtimeSinceStartup+240;
            while(Time.realtimeSinceStartup<until)
            {
                var hour=(root.Clock.State.CalendarTicks/TimeSpan.TicksPerHour)%24;
                if((hour>=22||hour<6)&&!root.Work.IsOnShift(sleeper,root.Clock.State.CalendarTicks))break;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(1);
            root.AiScheduler.TryStartSleepNow(sleeper,out var reason);
            until=Time.realtimeSinceStartup+90;
            nextTrace=0;
            while(!root.AiScheduler.ShouldHideInsideBuilding(sleeper)&&Time.realtimeSinceStartup<until)
            {
                if(Time.realtimeSinceStartup>=nextTrace&&root.AiRuntime.TryGetMovement(sleeper,out var m)&&root.AiAgents.TryGet(sleeper,out var a))
                {
                    nextTrace=Time.realtimeSinceStartup+5;
                    File.AppendAllText(output+"/sleep-trace.txt",$"{Time.realtimeSinceStartup} {sleeper} {m.CurrentPosition} {m.State} {a.CurrentAction}/{a.Phase} energy={a.Needs.Energy} denial={reason}\n");
                }
                yield return null;
            }
            var entered=root.Domestic.IsSleepingInBed(sleeper)&&root.Domestic.TryGetAssignedBed(sleeper,out var bed)&&bed.HomeBuildingId==house.Id;
            CheckRoute(entered,"resident physically enters assigned new hut; denial="+reason);
            yield return Capture("sample-house-entry");
            root.AiScheduler.ReplaceWithPlayerCommand(sleeper);
            root.ManualMoveCommands.Issue(new[]{sleeper},new WorldPosition(-120,root.WorldHeight.SampleHeight(-120,480),480),WorkPriority.Immediate);
            until=Time.realtimeSinceStartup+30;
            while(root.AiScheduler.ShouldHideInsideBuilding(sleeper)&&Time.realtimeSinceStartup<until)yield return null;
            CheckRoute(entered&&!root.AiScheduler.ShouldHideInsideBuilding(sleeper),"resident exits new hut for move command");
            yield return Capture("sample-house-exit");root.Clock.Pause();
        }
    }
}
