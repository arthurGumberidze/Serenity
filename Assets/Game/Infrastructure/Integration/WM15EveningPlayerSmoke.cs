using System;
using System.Collections;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Diagnostics;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Presentation.Environment;
using Game.Presentation.Production;
using UnityEngine;

namespace Game.Infrastructure.Integration
{
    public sealed partial class WM15OctoberPlayerSmoke
    {
        private IEnumerator EveningReview(LocalSceneCompositionRoot root)
        {
            root.Diagnostics.Execute(DiagnosticCommandKind.UnlockAll);
            root.Diagnostics.Execute(DiagnosticCommandKind.GrantResources);
            var placement=root.BuildingPlacementController;placement.enabled=false;
            root.CharacterPresentations.TryGet(root.MaleDemoCharacter.Id,out var ruler);
            var start=ruler.transform.position;
            var followup=Environment.GetCommandLineArgs().Contains("-wm15EveningFollowup");
            foreach(var id in followup?Array.Empty<string>():new[]{"primitive_garden","animal_pasture"})
            {
                Building building=null;placement.TryStartPlacement(new BuildingDefinitionId(id));
                for(var radius=10;radius<=48 && building==null;radius+=8)
                for(var i=0;i<16 && building==null;i++)
                {
                    var angle=i*Mathf.PI/8;var p=start+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*radius;
                    placement.TryMovePreviewToWorldPoint(p);yield return null;
                    if(placement.TryMovePreviewToWorldPoint(p) && placement.PreviewClearanceSourceIds.Count==0)building=placement.ConfirmPlacement();
                }
                Check(building!=null,id+" has a placeable construction footprint");
                if(building==null){placement.CancelPlacement();continue;}
                CompleteReviewBuilding(root,building);yield return null;
                root.BuildingPresentations.TryGet(building.Id,out var view);view.RefreshStructuralPresentation();
                root.CameraController.FocusPosition(view.transform.position,26);
                if(id=="primitive_garden")
                {
                    var site=root.Production.Sites.Single(x=>x.Id==building.Id);
                    foreach(var recipe in new[]{"cultivate_carrot","cultivate_cabbage","cultivate_plant_fiber","cultivate_berries"})
                    {
                        site.SelectCrop(root.Production.Recipes.Get(new ProductionRecipeId(recipe)));
                        site.CompleteWork(ProductionTaskKind.Plant);view.RefreshStructuralPresentation();yield return null;
                        var plants=view.GetComponentsInChildren<Transform>().Where(x=>x.name=="Growing plant").ToArray();
                        Check(plants.Length>4 && plants.SelectMany(x=>x.GetComponentsInChildren<MeshRenderer>()).Any(x=>x.enabled && x.bounds.size.magnitude>.03f),recipe+" sprouts render after sowing");
                        yield return CaptureReadable(root,"evening-"+recipe+"-sprouts");
                        site.AdvanceGrowth(site.GrowthRequiredTicks);view.RefreshStructuralPresentation();yield return null;
                        Check(plants.All(x=>x.localScale.x>.95f),recipe+" reaches full visible size");
                        yield return CaptureReadable(root,"evening-"+recipe+"-ripe");
                        site.CompleteWork(ProductionTaskKind.Harvest);
                    }
                }
                else
                {
                    root.Production.EstablishSheepPasture(building.Id,building.OwnerSideId);yield return new WaitForSecondsRealtime(.4f);
                    Check(root.Production.Animals.AtHome(building.Id).Count>=2,"Dedicated pen receives founding breeding pair");
                    Check(view.GetComponentsInChildren<Transform>().Any(x=>x.name=="Pasture shelter"),"Pen uses imported separate shelter");
                    yield return CaptureReadable(root,"evening-pasture");
                }
            }
            placement.CancelPlacement();
            foreach(var lake in root.WorldDefinition.MapData.Hydrology.Lakes)
            {
                var p=root.WorldDefinition.MapData.CoordinateTransform.ToUnity(lake.Center);
                root.CameraController.FocusPosition(new Vector3(p.X,root.WorldHeight.WaterSurfaceLevel(lake.Id),p.Z),115);
                yield return new WaitForSecondsRealtime(2);
                var stock=root.Production.FishStockAt(new Game.Domain.AI.WorldPosition(p.X,0,p.Z));
                Check(stock!=null && stock.SourceCapacity>=2000,lake.Id+" has shared finite fish stock");
                var schools=root.GetComponent<WorksitePresentation>();Check(schools.FishSchoolCount>10,"Free schools cover water bodies");
                yield return CaptureReadable(root,"evening-"+lake.Id);
                placement.TryStartPlacement(new BuildingDefinitionId("fishing_camp"));var validCount=0;var placed=false;double worstPreview=0;
                foreach(var shore in lake.Shoreline.Points.Where((_,i)=>i%8==0))
                {
                    if(!placement.IsActive)placement.TryStartPlacement(new BuildingDefinitionId("fishing_camp"));
                    var bank=root.WorldDefinition.MapData.CoordinateTransform.ToUnity(shore);
                    var cursor=new Vector3(bank.X,bank.Y,bank.Z);root.CameraController.FocusPosition(cursor,45);
                    var until=Time.realtimeSinceStartup+2;
                    while(Time.realtimeSinceStartup<until)
                    {
                        var timer=System.Diagnostics.Stopwatch.StartNew();var valid=placement.TryMovePreviewToWorldPoint(cursor);
                        worstPreview=Math.Max(worstPreview,timer.Elapsed.TotalMilliseconds);
                        if(valid){validCount++;break;}yield return null;
                    }
                    if(!placement.IsPreviewValid)Debug.Log("WM15_SHORE_REJECT "+lake.Id+" "+placement.LastPlacementFailure+" "+placement.SurfaceBlockedReason);
                    else if(!placed)
                    {
                        yield return CaptureReadable(root,"evening-shore-preview-"+lake.Id);
                        var camp=placement.ConfirmPlacement();Check(camp!=null,lake.Id+" accepts fishing camp construction");placed=camp!=null;
                    }
                }
                Check(validCount>=4,lake.Id+" offers >=4 easily found shoreline previews ("+validCount+")");
                Check(worstPreview<150,lake.Id+" preview worst call "+worstPreview.ToString("F1")+" ms");
                placement.CancelPlacement();
            }
            var quarry=root.WorldPopulation.Plan.Industry.FirstOrDefault(x=>x.DefinitionId=="stone_quarry");
            if(quarry!=null)
            {
                root.CameraController.FocusPosition(new Vector3(quarry.Position.X,quarry.Position.Y,quarry.Position.Z),28);
                yield return new WaitForSecondsRealtime(2);
                yield return CaptureReadable(root,"evening-quarry");
            }
            var wildlife=FindAnyObjectByType<WorldMapWildlifePresenter>();
            foreach(var species in new[]{"boar","cow"})
            {
                var animal=wildlife.Population.Animals.FirstOrDefault(x=>x.SpeciesId==species);
                Check(animal!=null,species+" exists in canonical population");if(animal==null)continue;
                root.CameraController.FocusPosition(new Vector3(animal.Position.X,animal.Position.Y,animal.Position.Z),22);
                root.Clock.Resume();yield return new WaitForSecondsRealtime(3);
                yield return CaptureReadable(root,"evening-"+species);root.Clock.Pause();
            }
            root.CameraController.FocusPosition(start,35);yield return new WaitForSecondsRealtime(2);
            placement.enabled=true;
            if(!followup){yield return WorkRoute(root,"hunt_animal");yield return WorkRoute(root,"chop_wood");}
            yield return WorkRoute(root,"fish_water");
            Check(errors.Count==0,"No runtime errors during evening review");
        }
    }
}
