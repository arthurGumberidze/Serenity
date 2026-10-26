using System;
using System.Collections;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Diagnostics;
using Game.Domain.Work;
using Game.Domain.AI;
using Game.Domain.Resources;
using Game.Presentation.Buildings;
using Game.Presentation.Environment;
using UnityEngine;

namespace Game.Infrastructure.Integration
{
    public sealed partial class WM15OctoberPlayerSmoke
    {
        private IEnumerator AfternoonReview(LocalSceneCompositionRoot root)
        {
            root.Diagnostics.Execute(DiagnosticCommandKind.UnlockAll);
            root.Diagnostics.Execute(DiagnosticCommandKind.GrantResources);
            var placement = root.BuildingPlacementController;
            placement.enabled = false; // Diagnostic supplies exact cursor positions instead of the physical mouse.
            root.CharacterPresentations.TryGet(root.MaleDemoCharacter.Id,out var ruler);
            var start = ruler.transform.position;
            Building garden = null;
            placement.TryStartPlacement(new BuildingDefinitionId("primitive_garden"));
            for (var x = -18; x <= 18 && garden == null; x += 3)
            for (var z = -18; z <= 18 && garden == null; z += 3)
                if (placement.TryMovePreviewToWorldPoint(start + new Vector3(x,0,z)) && placement.PreviewClearanceSourceIds.Count == 0)
                    garden = placement.ConfirmPlacement();
            Check(garden != null,"Garden can be placed on clear starting ground");
            if (garden != null)
            {
                CompleteReviewBuilding(root,garden); yield return null; yield return null;
                root.BuildingPresentations.TryGet(garden.Id,out var view); view.RefreshStructuralPresentation();
                var worker = root.MaleDemoCharacter.Id;
                root.SelectionProbe.SelectCharacterIds(new[] { worker });
                var target = new ContextCommandTarget(ContextTargetKind.Building,
                    new WorldPosition(view.transform.position.x,view.transform.position.y,view.transform.position.z),garden.Id);
                root.GameplayUi.OpenContextMenu(target,new Vector2(700,450));
                Check(root.GameplayUi.ContextActionStates.ContainsKey(ContextActionKind.Gather),"Garden menu exposes cultivation");
                var issued = root.GameplayUi.ExecuteContextAction(ContextActionKind.Gather,false);
                Check(issued.Orders.Count == 1,"Garden cultivation issues a canonical production order");
                root.CameraController.FocusPosition(view.transform.position,24);
                root.Clock.SetSpeed(Game.Simulation.Time.GameSpeed.VeryFast); root.Clock.Resume();
                var until = Time.realtimeSinceStartup + 80;
                while (Time.realtimeSinceStartup < until && root.Production.TryGetSite(garden.Id,out var site) &&
                    site.CropPhase == Game.Domain.Production.CropPlotPhase.Unplanted) yield return null;
                root.Clock.Pause(); root.Clock.SetSpeed(Game.Simulation.Time.GameSpeed.Normal);
                Check(root.Production.TryGetSite(garden.Id,out var plot) && plot.CropPhase != Game.Domain.Production.CropPlotPhase.Unplanted,
                    "Worker physically plants the garden");
                yield return CaptureReadable(root,"afternoon-garden");
                root.AiScheduler.ReplaceWithPlayerCommand(worker);
            }
            foreach (var id in new[] { "stone_age_hut", "healer_hut", "butcher_station", "military_barracks", "palisade", "gate" })
            {
                placement.TryStartPlacement(new BuildingDefinitionId(id)); placement.TryMovePreviewToWorldPoint(start);
                var before = placement.Orientation; placement.RotatePreview();
                Check(placement.Orientation == before.RotateClockwise(),id + " rotates 90 degrees");
                placement.CancelPlacement();
            }
            var campfire = root.BuildingPresentations.All.FirstOrDefault(x => x.IsBound && x.Building.DefinitionId.ToString() == "campfire");
            Check(campfire != null,"Starting camp contains a campfire to inspect at night");
            if (campfire != null)
            {
                campfire.RefreshStructuralPresentation(); var light = campfire.GetComponentInChildren<Light>();
                Check(light != null && light.enabled && light.range >= 8 && light.intensity > 1,"Operational campfire has warm local lighting");
                root.CameraController.FocusPosition(campfire.transform.position,22);
                root.WeatherPresentation.ShowPreview(root.Weather.GetWeatherForDay(0),TimeSpan.TicksPerHour*23);
                yield return new WaitForSecondsRealtime(1); yield return Capture("afternoon-campfire-night");
                root.WeatherPresentation.ClearPreview(root.Clock.State.CalendarTicks);
            }
            var landscape = FindAnyObjectByType<LateLandscapePresentation>();
            Check(landscape != null && landscape.FloatingPetalCount > 100,"Western lake has bounded floating petals");
            var lake = root.WorldDefinition.MapData.Hydrology.Lakes.First(x=>x.Id=="lake_west_grove");
            var center = root.WorldDefinition.MapData.CoordinateTransform.ToUnity(lake.Center);
            root.CameraController.FocusPosition(new Vector3(center.X,root.WorldHeight.WaterSurfaceLevel(lake.Id),center.Z),130);
            yield return new WaitForSecondsRealtime(2); yield return CaptureReadable(root,"afternoon-sakura-lake");
            var petals = landscape.GetComponentsInChildren<ParticleSystem>().FirstOrDefault(x=>x.name=="Sakura petals on western lake");
            if (petals != null && petals.particleCount > 0)
            {
                var particles = new ParticleSystem.Particle[petals.particleCount]; petals.GetParticles(particles);
                root.CameraController.FocusPosition(particles[0].position,14);
                yield return new WaitForSecondsRealtime(1); yield return CaptureReadable(root,"afternoon-sakura-petals");
            }

            Building camp = null; double worst = 0;
            placement.TryStartPlacement(new BuildingDefinitionId("fishing_camp"));
            var mountain = root.WorldDefinition.MapData.Hydrology.Lakes.First(x=>x.Id.IndexOf("ridge",StringComparison.OrdinalIgnoreCase)>=0 || x.Id.IndexOf("mount",StringComparison.OrdinalIgnoreCase)>=0);
            foreach (var point in mountain.Shoreline.Points.Where((_,i)=>i%5==0))
            {
                var p = root.WorldDefinition.MapData.CoordinateTransform.ToUnity(point);
                var cursor = new Vector3(p.X,root.WorldHeight.SampleHeight(p.X,p.Z),p.Z);
                root.CameraController.FocusPosition(cursor,40);
                var until = Time.realtimeSinceStartup + 4f;
                while (Time.realtimeSinceStartup < until && camp == null)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    var valid = placement.TryMovePreviewToWorldPoint(cursor);
                    worst = Math.Max(worst,timer.Elapsed.TotalMilliseconds);
                    if (valid && placement.PreviewClearanceSourceIds.Count == 0) camp = placement.ConfirmPlacement();
                    yield return null;
                }
                if (camp != null) break;
            }
            Check(camp != null,"Fishing camp places at mountain lake through normal preview/confirmation");
            Check(worst < 80,"Shore preview calls bounded: worst " + worst.ToString("F1") + " ms");
            if (camp != null) { CompleteReviewBuilding(root,camp); yield return null; yield return CaptureReadable(root,"afternoon-fishing-camp"); }
            placement.CancelPlacement(); placement.enabled = true;
            yield return WorkRoute(root,"fish_water");
            root.Clock.Resume(); yield return new WaitForSecondsRealtime(1);
            var animal = FindObjectsByType<WorldMapWildlifeView>(FindObjectsSortMode.None).FirstOrDefault(x=>x.SpeciesId=="deer" &&
                x.GetComponentInChildren<Animator>()?.GetBool("Moving")==true);
            Check(animal != null,"At least one deer walks in the loaded world");
            if (animal != null)
            {
                root.CameraController.FocusPosition(animal.transform.position,20);
                yield return new WaitForSecondsRealtime(.3f);
                var animator = animal.GetComponentInChildren<Animator>();
                Check(animator != null && animator.GetBool("Moving"),"Moving deer uses imported walk animation");
                var bones = animal.GetComponentsInChildren<Transform>(); var pose = bones.Select(x=>x.localRotation).ToArray();
                yield return new WaitForSecondsRealtime(.2f);
                Check(bones.Where((bone,i)=>Quaternion.Angle(bone.localRotation,pose[i])>1f).Any(),"Deer skeleton changes pose while walking");
                yield return CaptureReadable(root,"afternoon-wildlife-walk"); root.Clock.Pause();
            }
            var wildlife = FindAnyObjectByType<WorldMapWildlifePresenter>();
            var boarState = wildlife.Population.Animals.First(x=>x.SpeciesId=="boar");
            root.CameraController.FocusPosition(new Vector3(boarState.Position.X,boarState.Position.Y,boarState.Position.Z),12);
            yield return new WaitForSecondsRealtime(2);
            var boars = FindObjectsByType<WorldMapWildlifeView>(FindObjectsSortMode.None).Where(x=>x.SpeciesId=="boar").ToArray();
            Check(boars.Length>0,"Boars materialize near their habitat");
            foreach(var boar in boars)
            {
                var feet = boar.GetComponentsInChildren<Transform>().Where(x=>x.name.StartsWith("foot.") || x.name.StartsWith("front_foot."));
                Check(Mathf.Abs(feet.Min(x=>x.position.y)-boar.transform.position.y-.025f)<.06f,"Boar hooves stay at terrain height");
            }
            yield return CaptureReadable(root,"afternoon-boar-ground");
            Check(errors.Count == 0,"No runtime errors during afternoon reproduction");
        }

        private void CompleteReviewBuilding(LocalSceneCompositionRoot root, Building building)
        {
            // Real materials and construction buffer; only the labour duration is shortened for this UI reproduction.
            foreach (var material in building.ConstructionMaterials.Where(x=>x.RemainingUnits>0).ToArray())
            {
                var supply = root.Inventories.All.First(x=>x.Owner!=building.ConstructionBufferOwner &&
                    x.GetAmount(material.ResourceId).Units>=material.RemainingUnits);
                var delivered = root.ConstructionSites.Deliver(building,supply.Owner,material.ResourceId,new ResourceQuantity(material.RemainingUnits));
                Check(delivered==Game.Simulation.Resources.TransferFailureReason.None,"Review construction receives "+material.ResourceId);
            }
            root.ConstructionSites.NotifyDelivery(building); building.MarkCompleted();
            foreach (var job in root.Work.Jobs.Where(x=>!x.IsTerminal && x.Destination.HasValue && x.Destination.Value.EntityId==building.Id).ToArray())
                root.Work.Cancel(job.Id);
        }
    }
}
