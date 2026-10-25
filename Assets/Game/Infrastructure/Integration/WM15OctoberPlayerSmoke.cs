using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Combat;
using Game.Domain.Diagnostics;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Presentation.Characters;
using Game.Presentation.Environment;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    // Opt-in functional reproduction in a disposable new game. No performance workload or save writes.
    public sealed class WM15OctoberPlayerSmoke : MonoBehaviour
    {
        private string output;
        private readonly List<string> results = new();
        private readonly List<string> errors = new();
        private float deadline;
        private bool finished;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args,"-wm15OctoberSmoke");
            if (i < 0 || i+1 >= args.Length) return;
            var host = new GameObject("October functional smoke");
            DontDestroyOnLoad(host);
            var runner = host.AddComponent<WM15OctoberPlayerSmoke>();
            runner.output = Path.GetFullPath(args[i+1]); Directory.CreateDirectory(runner.output);
            runner.deadline = Time.realtimeSinceStartup+900f;
            Application.runInBackground = true;
            Application.logMessageReceived += runner.OnLog;
            runner.StartCoroutine(runner.Run());
        }
        private void Check(bool ok,string label)
        {
            results.Add((ok ? "PASS " : "FAIL ")+label);
            File.WriteAllLines(Path.Combine(output,"checks.txt"),results);
            Debug.Log("OCTOBER_SMOKE "+results.Last());
        }
        private void OnLog(string text,string stack,LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                errors.Add(text+"\n"+stack);
        }
        private void Update()
        {
            if (!finished && Time.realtimeSinceStartup > deadline) { Check(false,"Timed out"); Finish(); }
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0); texture.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG()); Destroy(texture);
        }
        private IEnumerator Run()
        {
            MainMenuController menu;
            while ((menu=FindAnyObjectByType<MainMenuController>()) == null) yield return null;
            yield return new WaitForSecondsRealtime(2f); // Let MainMenu.Start and panel layout finish first.
            menu.OpenNewGame(); yield return null;
            var until=Time.realtimeSinceStartup+45f;
            while (Time.realtimeSinceStartup < until && FindObjectsByType<UmaAppearanceProjection>(FindObjectsSortMode.None).Any(x=>x.IsGenerating)) yield return null;
            Check(FindObjectsByType<UmaAppearanceProjection>(FindObjectsSortMode.None).Any(x=>x.UsesUma),"Ruler editor UMA generated");
            yield return new WaitForSecondsRealtime(2f);
            yield return Capture("01-editor");
            menu.StartNewGame(1380062136);
            LocalSceneCompositionRoot root;
            while ((root=FindAnyObjectByType<LocalSceneCompositionRoot>()) == null || !root.IsInitialized) yield return null;
            root.Clock.Pause();
            root.GameplayUi.SetOnboardingVisible(false);
            until=Time.realtimeSinceStartup+45f;
            while (Time.realtimeSinceStartup < until && FindObjectsByType<UmaAppearanceProjection>(FindObjectsSortMode.None).Any(x=>x.IsGenerating)) yield return null;
            Check(root.CharacterPresentations.TryGet(root.MaleDemoCharacter.Id,out var ruler) &&
                ruler.GetComponent<UmaAppearanceProjection>()?.UsesUma == true,"World ruler visible UMA");
            yield return Capture("02-start");

            var animal=FindObjectsByType<WorldMapWildlifeView>(FindObjectsSortMode.None).FirstOrDefault();
            Check(animal != null,"Wildlife materialized");
            if (animal != null)
            {
                root.CameraController.FocusPosition(animal.transform.position,24f);
                yield return null; yield return null; Physics.SyncTransforms();
                var screen=Camera.main.WorldToScreenPoint(animal.GetComponent<Collider>().bounds.center);
                Check(root.SelectionProbe.TrySelectAt(new Vector2(screen.x,screen.y),false) && root.SelectionProbe.SelectedWildlife==animal,"Wildlife left click");
                Check(root.PointerRaycaster.TryGetContextHit(new Vector2(screen.x,screen.y),out var hit) && hit.collider.GetComponentInParent<WorldMapWildlifeView>()==animal,"Wildlife right click hit");
                root.GameplayUi.RefreshNow(); yield return null; yield return null;
                Check(root.GameplayUi.RootVisual.Q("selection-inspector").resolvedStyle.display != DisplayStyle.None,"Animal inspector stays visible after layout");
                yield return Capture("03-wildlife");
                if (root.Production.TryGetSite(animal.StableId,out var prey))
                {
                    root.SelectionProbe.SelectCharacterIds(new[]{root.MaleDemoCharacter.Id});
                    root.GameplayUi.OpenContextMenu(new ContextCommandTarget(ContextTargetKind.ResourceSource,prey.Position,prey.Id,new ResourceId("meat"),ContextResourceClass.Gatherable),new Vector2(Screen.width*.5f,Screen.height*.5f));
                    Check(root.GameplayUi.ContextActionStates.ContainsKey(ContextActionKind.Gather) && !root.GameplayUi.ContextActionStates.ContainsKey(ContextActionKind.Attack),"Wildlife menu has hunt, no military attack");
                    root.GameplayUi.CloseContextMenu();
                }
            }

            root.Diagnostics.Execute(DiagnosticCommandKind.GrantResources);
            root.Diagnostics.Execute(DiagnosticCommandKind.UnlockAll);
            var origin=root.WorldDefinition.PlayerStart;
            root.CameraController.FocusPosition(new Vector3(origin.X,origin.Y,origin.Z),40f);
            var placement=root.BuildingPlacementController;
            placement.TryStartPlacement(new BuildingDefinitionId("military_barracks"));
            Building barracks=null;
            for(var z=-40;z<=40 && barracks==null;z+=6)
            for(var x=-40;x<=40 && barracks==null;x+=6)
            {
                if (!placement.TryMovePreviewToWorldPoint(new Vector3(origin.X+x,origin.Y,origin.Z+z)) || placement.PreviewClearanceSourceIds.Count>0) continue;
                barracks=placement.ConfirmPlacement();
            }
            Check(barracks!=null,"Dry-land barracks placement confirms");
            if (barracks==null) results.Add("Placement: "+placement.LastPlacementFailure+" / "+placement.SurfaceBlockedReason+" / "+placement.LastConstructionFailure);
            placement.CancelPlacement();
            if(barracks!=null)
            {
                // Deliver real stock through the construction buffer; skip labour duration only.
                foreach (var material in barracks.ConstructionMaterials.ToArray())
                {
                    var supply=root.Inventories.All.First(v=>v.Owner!=barracks.ConstructionBufferOwner &&
                        v.GetAmount(material.ResourceId).Units>=material.RemainingUnits);
                    var delivered=root.ConstructionSites.Deliver(barracks,supply.Owner,material.ResourceId,new ResourceQuantity(material.RemainingUnits));
                    Check(delivered==Game.Simulation.Resources.TransferFailureReason.None,"Barracks material delivered: "+material.ResourceId);
                }
                root.ConstructionSites.NotifyDelivery(barracks);
                barracks.MarkCompleted();
                foreach(var order in root.Work.Jobs.Where(j=>!j.IsTerminal && j.Destination.HasValue && j.Destination.Value.EntityId==barracks.Id).ToArray()) root.Work.Cancel(order.Id);
                yield return null;
                var before=root.Characters.GetAll().Select(c=>c.Id).ToHashSet();
                var army=root.Diagnostics.Execute(DiagnosticCommandKind.CreateArmy);
                yield return null; yield return null;
                var added=root.Characters.GetAll().Where(c=>!before.Contains(c.Id)).ToArray();
                Check(army.AffectedCount==4 && added.Length==4 && added.All(c=>root.CanCommandResident(c.Id) && root.CharacterPresentations.TryGet(c.Id,out _)),"Army cheat adds four commandable physical residents");
                var trainee=root.Characters.GetAll().First(c=>before.Contains(c.Id) && root.CanCommandResident(c.Id) &&
                    c.Id!=root.MaleDemoCharacter.Id && c.Id!=root.FemaleDemoCharacter.Id);
                root.CharacterPresentations.TryGet(trainee.Id,out var civilianView);
                var civilianMeshes=civilianView.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r=>r.sharedMesh).ToArray();
                var start=root.MilitaryTraining.TryStart(trainee.Id,barracks.Id,new CombatSideId("player"),root.Clock.State.CalendarTicks,root.Inventories.All.Select(v=>v.Owner));
                Check(start.Started,"Training starts");
                if(start.Started)
                {
                    root.Clock.Resume(); until=Time.realtimeSinceStartup+80f;
                    while(Time.realtimeSinceStartup<until && !root.MilitaryTraining.IsAtBarracks(start.Record)) yield return null;
                    root.Clock.Pause();
                    Check(root.MilitaryTraining.IsAtBarracks(start.Record),"Trainee reaches barracks");
                    if(root.MilitaryTraining.IsAtBarracks(start.Record))
                    {
                        Check(root.CharacterPresentations.TryGet(trainee.Id,out var view) && view.GetComponentsInChildren<Renderer>().All(r=>r.forceRenderingOff),"Trainee hidden indoors");
                        var active=root.MilitaryTraining.Active.Single(r=>r.CharacterId==trainee.Id);
                        var rankBefore=root.MilitaryTraining.GetRank(trainee.Id);
                        // Advance the shared clock as well: advancing the training service alone would
                        // make the next normal simulation tick illegally move its time backwards.
                        root.AiRuntime.SetSpeed(Game.Simulation.Time.GameSpeed.Maximum);
                        root.AiRuntime.SetPaused(false);
                        root.AiRuntime.AdvanceForDebug(TimeSpan.FromTicks((active.CompletionTick-root.Clock.State.CalendarTicks)/600+TimeSpan.TicksPerSecond));
                        root.AiRuntime.SetPaused(true); root.AiRuntime.SetSpeed(Game.Simulation.Time.GameSpeed.Normal);
                        var rankAfter=root.MilitaryTraining.GetRank(trainee.Id);
                        Check(rankAfter.HasValue && (!rankBefore.HasValue || rankAfter.Value>rankBefore.Value),"Training completes after arrival");
                        yield return null; yield return null;
                        Check(root.CharacterPresentations.TryGet(trainee.Id,out view) && view.GetComponentsInChildren<Renderer>().Any(r=>r.enabled && !r.forceRenderingOff),"Graduate reappears");
                        Check(view.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>!civilianMeshes.Contains(r.sharedMesh)),"Graduate wears training tunic");
                        root.CameraController.FocusPosition(view.transform.position,18f);
                        yield return Capture("04-graduate");
                    }
                }
            }
            foreach(var lake in root.WorldDefinition.MapData.Hydrology.Lakes)
            {
                Check(root.GetComponent<WorldWorkApproaches>().transform.Cast<Transform>().Any(t=>t.name=="Wooden fishing pier "+lake.Id),"Pier for "+lake.Id);
                var center=root.WorldDefinition.MapData.CoordinateTransform.ToUnity(lake.Center);
                root.CameraController.FocusPosition(new Vector3(center.X,root.WorldHeight.SampleHeight(center.X,center.Z),center.Z),100f);
                yield return null; yield return null; yield return Capture("lake-"+lake.Id);
            }
            var fish=root.Production.Sites.First(s=>s.RecipeId.ToString()=="fish_water");
            var stance=root.Production.WorkPosition(root.MaleDemoCharacter.Id,fish);
            Check(NavMesh.SamplePosition(new Vector3(stance.X,stance.Y,stance.Z),out var dockHit,.8f,NavMesh.AllAreas),"Fishing stance lies on dock navigation");
            root.SelectionProbe.SelectCharacterIds(new[]{root.MaleDemoCharacter.Id});
            root.GameplayUi.OpenContextMenu(new ContextCommandTarget(ContextTargetKind.ResourceSource,fish.Position,fish.Id,new ResourceId("fish"),ContextResourceClass.Gatherable),new Vector2(700,450));
            Check(root.GameplayUi.ContextActionStates.ContainsKey(ContextActionKind.Gather) && !root.GameplayUi.ContextActionStates.ContainsKey(ContextActionKind.Attack),"Fish menu offers gathering, never attack");
            root.GameplayUi.CloseContextMenu();
            foreach (var recipe in new[]{"chop_wood","gather_stone","hunt_animal","fish_water"})
                yield return WorkRoute(root,recipe);
            Check(errors.Count==0,"No runtime errors"); Finish();
        }
        private IEnumerator WorkRoute(LocalSceneCompositionRoot root,string recipe)
        {
            var worker=root.MaleDemoCharacter.Id;
            if (!root.CharacterPresentations.TryGet(worker,out var person)) { Check(false,recipe+" worker visible"); yield break; }
            var position=person.transform.position;
            var site=root.Production.Sites.Where(s=>s.RecipeId.ToString()==recipe && s.IsWorkAvailable)
                .OrderBy(s=>(new Vector3(s.Position.X,s.Position.Y,s.Position.Z)-position).sqrMagnitude).FirstOrDefault();
            if(site==null) { Check(false,recipe+" source available"); yield break; }
            var kind=recipe=="chop_wood"?ContextActionKind.Chop:recipe=="gather_stone"?ContextActionKind.Mine:ContextActionKind.Gather;
            var resource=recipe=="chop_wood"?"wood_log":recipe=="gather_stone"?"stone":recipe=="fish_water"?"fish":"meat";
            var resourceClass=recipe=="chop_wood"?ContextResourceClass.Choppable:recipe=="gather_stone"?ContextResourceClass.Mineable:ContextResourceClass.Gatherable;
            root.SelectionProbe.SelectCharacterIds(new[]{worker});
            root.GameplayUi.OpenContextMenu(new ContextCommandTarget(ContextTargetKind.ResourceSource,site.Position,site.Id,new ResourceId(resource),resourceClass),new Vector2(700,450));
            var command=root.GameplayUi.ExecuteContextAction(kind,false);
            Check(command.Orders.Count==1,recipe+" direct order accepted: "+string.Join(",",command.Entries.Select(e=>e.Availability.Reason)));
            if(command.Orders.Count==0) yield break;
            var until=Time.realtimeSinceStartup+100f;
            root.Clock.SetSpeed(Game.Simulation.Time.GameSpeed.VeryFast); root.Clock.Resume();
            var worked=false; var completed=false; var impacts=0;
            void OnImpact(Game.Domain.World.HuntingImpact impact) { if(impact.Animal==site.Id && impact.Damage>0) impacts++; }
            root.Production.Animals.Impact+=OnImpact;
            while(Time.realtimeSinceStartup<until)
            {
                var task=root.Production.ActiveTasks.FirstOrDefault(t=>t.WorkerId==worker && t.SiteId==site.Id);
                if(task!=null && root.AiAgents.TryGet(worker,out var agent) && agent.Phase==AiActionPhase.Working && !worked)
                {
                    worked=true; root.CameraController.FocusPosition(person.transform.position,18f);
                    yield return new WaitForSecondsRealtime(.3f);
                    if(recipe=="chop_wood" || recipe=="gather_stone")
                    {
                        // Inspect under the canopy, from the worker's side, rather than looking down through leaves.
                        root.Clock.SetSpeed(Game.Simulation.Time.GameSpeed.Normal);
                        root.CameraController.enabled=false;
                        var camera=Camera.main;
                        camera.transform.position=person.transform.position-person.transform.forward*2f+person.transform.right*3.2f+Vector3.up*2f;
                        camera.transform.rotation=Quaternion.LookRotation(person.transform.position+Vector3.up*1.1f-camera.transform.position);
                        for(var frame=0;frame<4;frame++)
                        { yield return new WaitForSecondsRealtime(.23f); yield return Capture("work-"+recipe+"-"+frame); }
                        root.CameraController.enabled=true;
                        root.Clock.SetSpeed(Game.Simulation.Time.GameSpeed.VeryFast);
                    }
                    else yield return Capture("work-"+recipe);
                    if(recipe=="fish_water")
                    {
                        var p=root.Production.WorkPosition(worker,site);
                        Check((person.transform.position-new Vector3(p.X,p.Y,p.Z)).magnitude<1.5f,"Fisher physically stands on pier");
                    }
                }
                if(recipe=="hunt_animal")
                {
                    if(root.Production.Animals.TryGet(site.Id,out var animal) && animal.Harvested) { completed=true; break; }
                }
                else if(worked && task==null) { completed=true; break; }
                yield return null;
            }
            root.Production.Animals.Impact-=OnImpact;
            root.Clock.Pause(); root.Clock.SetSpeed(Game.Simulation.Time.GameSpeed.Normal);
            Check(worked,recipe+" worker reaches source and works");
            Check(completed,recipe+" production completes");
            if(recipe=="hunt_animal") Check(impacts>0,"Bow impacts reduce animal health before skinning");
            foreach(var task in root.Production.ActiveTasks.Where(t=>t.WorkerId==worker).ToArray()) root.Production.Cancel(task.Id);
        }
        private void Finish()
        {
            finished=true; Application.logMessageReceived-=OnLog;
            File.WriteAllLines(Path.Combine(output,"checks.txt"),results);
            File.WriteAllLines(Path.Combine(output,"errors.txt"),errors);
            Application.Quit(results.Any(x=>x.StartsWith("FAIL")) || errors.Count>0 ? 1 : 0);
        }
    }
}
