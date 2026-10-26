using System;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Simulation.Production;
using Game.Simulation.Resources;
using Game.Presentation.Environment;
using UnityEngine;
using NUnit.Framework;
using Object=UnityEngine.Object;

namespace Game.Tests.Editor
{
    public sealed class WM15EveningFeedbackTests
    {
        [Test] public void NewContentIsAvailableThroughResearchWithoutCheats()
        {
            var catalog=UnityEditor.AssetDatabase.LoadAssetAtPath<Game.Presentation.Buildings.BuildingPresentationCatalog>("Assets/Game/Art/Config/BuildingPresentationCatalog.asset");
            var technology=Game.Domain.Research.TechnologyCatalog.CreateStoneAge(ProductionCatalog.CreateStoneAge(catalog.ResourceCatalog.CreateCatalog()),
                catalog.Entries.Select(x=>new BuildingDefinitionId(x.DefinitionId)));
            Assert.That(technology.All.Single(x=>x.Id.ToString()=="gathering").Unlocks.Any(x=>x.ContentId=="fish_breed"),Is.True);
            Assert.That(technology.All.Single(x=>x.Id.ToString()=="primitive_agriculture").Unlocks.Any(x=>x.ContentId=="animal_pasture"),Is.True);
        }
        [Test] public void BoarWalkMovesBonesAfterSeveralCycles()
        {
            var prefab=Resources.Load<WorldMapWildlifeArtCatalog>("World/WorldMapWildlifeArt").For("boar").Prefab;
            var view=Object.Instantiate(prefab);
            try
            {
                var animator=view.GetComponentInChildren<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();animator.SetBool("Moving",true);animator.Play("Walk",0,3.1f);animator.Update(0);
                var bones=view.GetComponentsInChildren<Transform>().Where(x=>x.name.Contains("foot") || x.name.Contains("leg")).ToArray();
                Assert.That(bones.Length,Is.GreaterThan(3));
                var first=bones.Select(x=>x.localRotation).ToArray();
                animator.Play("Walk",0,3.6f);animator.Update(0);
                Assert.That(bones.Where((x,i)=>Quaternion.Angle(x.localRotation,first[i])>2).Count(),Is.GreaterThan(1),"Looped clip must actually animate this rig");
            }
            finally { Object.DestroyImmediate(view); }
        }
        private static ResourceCatalog Catalog()=>new ResourceCatalog(new[]{"wood_log","stone","plant_fiber","berries","fish"}.Select(x=>new ResourceDefinition(new ResourceId(x),x,ResourceCategory.Food)));
        [Test] public void FishingUsesFiniteSharedStockAndOnlyBreedingRestoresIt()
        {
            var resources=Catalog();var inventories=new ResourceInventoryRegistry();var buildings=new BuildingRegistry();
            var definition=new BuildingDefinition(new BuildingDefinitionId("fishing_camp"),"Fishing",BuildingCategory.Food,
                BuildingEra.StoneAge,"","",null,new ConstructionSpec(null,TimeSpan.FromMinutes(1)),new PlacementSpec(new BuildingFootprint(12,9)),
                workplace:new WorkplaceSpec(1),production:new ProductionSpec(new[]{new ProductionRecipeId("fish_camp"),new ProductionRecipeId("fish_breed")},1,30));
            var building=Building.Restore(new BuildingState(StableEntityId.NewId(),definition.Id,new GridCoordinate(0,0),BuildingOrientation.North,ConstructionState.Completed));buildings.Add(building);
            var worker=StableEntityId.NewId();inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character,worker),resources,10));
            var service=new ProductionService(ProductionCatalog.CreateStoneAge(resources),resources,inventories,buildings,new ResourceTransferService(inventories));
            var stock=ProductionSite.CreateNatural(new ProductionRecipeId("fish_water"),default,40);service.AddSite(stock);
            service.WaterBodyAt=_=>"lake";service.RegisterFishWater("lake",stock.Id);
            service.RegisterBuildingWorkplace(building.Id,definition,default);
            Assert.That(service.TryReserveAtBuilding(worker,building.Id,out var task),Is.True);
            service.AdvanceTask(task.Task.Id,TimeSpan.FromDays(1));
            Assert.That(stock.RemainingAmount,Is.EqualTo(10));
            var output=inventories.Get(service.OutputOwner(building.Id));Assert.That(output.TotalUnits,Is.EqualTo(30));
            output.TryRemove(new ResourceId("fish"),new ResourceQuantity(30));
            service.AdvanceTask(task.Task.Id,TimeSpan.FromDays(1));Assert.That(stock.RemainingAmount,Is.Zero);Assert.That(output.TotalUnits,Is.EqualTo(10));
            service.AdvanceTo(TimeSpan.FromDays(10).Ticks,Array.Empty<StableEntityId>());Assert.That(stock.RemainingAmount,Is.Zero);
            service.SetFisheryBreeding(building.Id,true);
            Assert.That(service.TryReserveAtBuilding(worker,building.Id,out var breeding),Is.True);
            service.AdvanceTask(breeding.Task.Id,TimeSpan.FromMinutes(24));
            Assert.That(stock.RemainingAmount,Is.EqualTo(6));Assert.That(output.TotalUnits,Is.EqualTo(10));
            var saved=service.CaptureState();service.RestoreState(saved);
            Assert.That(service.FishStockAt(default).RemainingAmount,Is.EqualTo(6));
            Assert.That(service.Sites.Single(x=>x.RecipeId.ToString()=="fish_breed").Paused,Is.False);
            var manual=service.Recipes.Get(new ProductionRecipeId("fish_water"));var camp=service.Recipes.Get(new ProductionRecipeId("fish_camp"));
            Assert.That(manual.WorkRequiredTicks,Is.EqualTo(camp.WorkRequiredTicks*3));
        }
        [Test] public void CropGrowthAdvancesWithinDayAndSurvivesRestore()
        {
            var resources=Catalog();var inv=new ResourceInventoryRegistry();
            var service=new ProductionService(ProductionCatalog.CreateStoneAge(resources),resources,inv,new BuildingRegistry(),new ResourceTransferService(inv));
            var site=ProductionSite.CreateCropPlot(StableEntityId.NewId(),new ProductionRecipeId("cultivate_berries"),default,TimeSpan.FromDays(2));service.AddSite(site);
            site.CompleteWork(ProductionTaskKind.Plant);
            service.AdvanceTo(TimeSpan.FromHours(6).Ticks,Array.Empty<StableEntityId>());
            Assert.That(site.GrowthProgressTicks,Is.EqualTo(TimeSpan.FromHours(6).Ticks));
            service.RestoreState(service.CaptureState());service.AdvanceTo(TimeSpan.FromHours(12).Ticks,Array.Empty<StableEntityId>());
            Assert.That(service.Sites.Single().GrowthProgressTicks,Is.EqualTo(TimeSpan.FromHours(12).Ticks));
        }
    }
}
