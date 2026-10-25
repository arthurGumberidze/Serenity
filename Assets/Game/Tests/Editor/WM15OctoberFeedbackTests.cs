using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Health;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Domain.World;
using Game.Domain.Buildings;
using Game.Domain.Production;
using Game.Simulation.Production;
using Game.Simulation.Resources;
using Game.Infrastructure;
using NUnit.Framework;
namespace Game.Tests.Editor
{
    public sealed class WM15OctoberFeedbackTests
    {
        [Test]
        public void BothMouseButtonsPickAnimalThroughDecorativeCanopy()
        {
            var cameraObject=new UnityEngine.GameObject("pointer test camera");
            var canopy=new UnityEngine.GameObject("canopy");
            var animal=new UnityEngine.GameObject("animal");
            try
            {
                var camera=cameraObject.AddComponent<UnityEngine.Camera>();
                camera.transform.position=new UnityEngine.Vector3(10000,10000,10000);
                canopy.layer=animal.layer=31;
                canopy.transform.position=camera.transform.position+UnityEngine.Vector3.forward*3;
                animal.transform.position=camera.transform.position+UnityEngine.Vector3.forward*5;
                canopy.AddComponent<UnityEngine.BoxCollider>().size=UnityEngine.Vector3.one*2;
                canopy.AddComponent<Game.Presentation.Production.ProductionSourcePresenter>();
                var body=animal.AddComponent<UnityEngine.BoxCollider>(); body.isTrigger=true;
                animal.AddComponent<Game.Presentation.Environment.WorldMapWildlifeView>();
                var pointer=cameraObject.AddComponent<Game.Presentation.Interaction.WorldPointerRaycaster>();
                pointer.Configure(camera,1<<31,20f);
                UnityEngine.Physics.SyncTransforms();
                var screen=camera.WorldToScreenPoint(animal.transform.position);
                Assert.That(pointer.TryGetSelectableHit(screen,out var left),Is.True);
                Assert.That(left.collider,Is.EqualTo(body));
                Assert.That(pointer.TryGetContextHit(screen,out var right),Is.True);
                Assert.That(right.collider,Is.EqualTo(body));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(animal); UnityEngine.Object.DestroyImmediate(canopy);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
        [Test]
        public void DirectHuntingAndFishingReserveSourcesWithoutAnAutomaticCamp()
        {
            var resources=new ResourceCatalog(new[]{"wood_log","stone","plant_fiber","berries","fish","carcass","meat","hide"}
                .Select(id=>new ResourceDefinition(new ResourceId(id),id,ResourceCategory.Construction)));
            var inventories=new ResourceInventoryRegistry();
            var worker=StableEntityId.NewId();
            inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character,worker),resources,128));
            var production=new ProductionService(ProductionCatalog.CreatePlayablePatch(resources),resources,inventories,
                new BuildingRegistry(),new ResourceTransferService(inventories));
            foreach(var recipe in new[]{"hunt_animal","fish_water"})
            {
                var id=new ProductionRecipeId(recipe);
                Assert.That(production.IsRecipeAvailable(id),Is.False,"Automatic work still needs its camp.");
                Assert.That(production.IsRecipeAvailable(id,true),Is.True);
                var site=ProductionSite.CreateNatural(id,new WorldPosition(1,0,1),48);
                production.AddSite(site);
                Assert.That(production.TryReserveAtSite(worker,site.Id,out var job),Is.True,recipe);
                production.Cancel(job.Task.Id);
            }
        }
        [Test]
        public void FishAndWildlifeAreHarvestTargetsNeverMilitaryAreaAttacks()
        {
            foreach (var resource in new[] { "fish", "meat" })
            {
                var target = new ContextCommandTarget(ContextTargetKind.ResourceSource,new WorldPosition(0,0,0),
                    StableEntityId.NewId(),new ResourceId(resource),ContextResourceClass.Gatherable);
                Assert.That(ContextActionRules.SupportsTarget(ContextActionKind.Gather,target),Is.True);
                Assert.That(ContextActionRules.SupportsTarget(ContextActionKind.Attack,target),Is.False);
                Assert.That(ContextActionRules.SupportsTarget(ContextActionKind.Build,target),Is.False);
            }
        }
        [Test]
        public void NeckDepthIsSafeButHeadSubmersionReducesPersistentVitalHealth()
        {
            var healthy = BodyHealth.Healthy;
            Assert.That(SubmersionRules.Damage(healthy,1.65f,1.7f,4).Head.Value,Is.EqualTo(100));
            var damaged = SubmersionRules.Damage(healthy,1.8f,1.7f,4);
            Assert.That(damaged.Head.Value,Is.EqualTo(96));
            Assert.That(damaged.Torso.Value,Is.EqualTo(96));
            Assert.That(SubmersionRules.Damage(damaged,2f,1.7f,100).Head.Value,Is.Zero);
        }
        [Test]
        public void RoundedLakesKeepWaterAndDryStarterLandDistinct()
        {
            var world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var heights = new TiledWorldHeightSampler(world,1380062136);
            foreach (var lake in world.MapData.Hydrology.Lakes)
            {
                Assert.That(lake.Shoreline.Points.Count,Is.GreaterThanOrEqualTo(24));
                var p = world.MapData.CoordinateTransform.ToUnity(lake.Center);
                Assert.That(heights.IsWater(p.X,p.Z),Is.True,lake.Id);
                Assert.That(heights.IsBuildable(p.X,p.Z),Is.False,lake.Id);
            }
            var start = world.PlayerStart;
            Assert.That(heights.IsWater(start.X,start.Z),Is.False);
            Assert.That(heights.IsBuildable(start.X,start.Z),Is.True);
        }
    }
}
