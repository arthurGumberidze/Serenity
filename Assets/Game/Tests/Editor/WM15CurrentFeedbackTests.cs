using System;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Combat;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.World;
using Game.Simulation.Production;
using Game.Simulation.Resources;
using Game.Infrastructure;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Editor
{
    public sealed class WM15CurrentFeedbackTests
    {
        private static ProductionService Service(out BuildingRegistry buildings, out StableEntityId worker)
        {
            var resources = new ResourceCatalog(new[] { "wood_log", "stone", "plant_fiber", "berries", "fish", "meat", "hide" }
                .Select(x => new ResourceDefinition(new ResourceId(x), x, ResourceCategory.Food)));
            var inventories = new ResourceInventoryRegistry();
            worker = StableEntityId.NewId();
            inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character, worker), resources, 100));
            buildings = new BuildingRegistry();
            return new ProductionService(ProductionCatalog.CreateStoneAge(resources), resources, inventories,
                buildings, new ResourceTransferService(inventories));
        }

        [Test]
        public void ManualFishingRequiresPierAndStopsWhenLastPierIsRemoved()
        {
            var service = Service(out _, out var worker);
            var stock = ProductionSite.CreateNatural(new ProductionRecipeId("fish_water"), default, 500);
            service.AddSite(stock);
            var available = false;
            service.ManualFishingAccess = _ => available;
            Assert.That(service.TryReserveAtSite(worker, stock.Id, out _), Is.False);
            available = true;
            Assert.That(service.TryReserveAtSite(worker, stock.Id, out var job), Is.True);
            available = false;
            service.AdvanceTask(job.Task.Id, TimeSpan.FromDays(1));
            Assert.That(service.IsTaskActive(job.Task.Id), Is.False);
            Assert.That(stock.RemainingAmount, Is.EqualTo(500));
        }

        [Test]
        public void PastureStartsEmptyAndPlayerChoosesOneYoungPair()
        {
            var service = Service(out var buildings, out _);
            var home = StableEntityId.NewId();
            buildings.Add(Building.Restore(new BuildingState(home, new BuildingDefinitionId("animal_pasture"),
                default, BuildingOrientation.North, ConstructionState.Completed)));
            service.AddSite(ProductionSite.CreateCropPlot(home, new ProductionRecipeId("cultivate_berries"),
                default, TimeSpan.FromDays(2)));
            Assert.That(service.Animals.AtHome(home), Is.Empty);
            Assert.That(service.EstablishPasture(home, new CombatSideId("player"), "horse"), Is.True);
            var initial = service.Animals.AtHome(home);
            Assert.That(initial.Count, Is.EqualTo(2));
            Assert.That(initial.All(x => x.Species == "horse"), Is.True);
            foreach (var species in initial.GroupBy(x => x.Species))
            {
                Assert.That(species.Count(), Is.EqualTo(2));
                Assert.That(species.Count(x => x.Male), Is.EqualTo(1));
                Assert.That(species.All(x => x.GrowthFraction(0, 3) == 0), Is.True);
            }
            service.AdvanceTo(TimeSpan.FromDays(3).Ticks, Array.Empty<StableEntityId>());
            Assert.That(service.Animals.AtHome(home).Count, Is.EqualTo(initial.Count));
            service.AdvanceTo(TimeSpan.FromDays(7).Ticks, Array.Empty<StableEntityId>());
            Assert.That(service.Animals.AtHome(home).Count, Is.GreaterThan(initial.Count));
            Assert.That(service.Animals.AtHome(home).Any(x => x.BornTick >= TimeSpan.FromDays(5).Ticks), Is.True);
            var before = service.Animals.AtHome(home).Select(x => (x.Id, x.Species, x.BornTick)).ToArray();
            service.RestoreState(service.CaptureState());
            Assert.That(service.Animals.AtHome(home).Select(x => (x.Id, x.Species, x.BornTick)), Is.EqualTo(before));
            Assert.That(service.EstablishPasture(home, new CombatSideId("player"), "cow"), Is.False);
        }

        [Test]
        public void LivestockCannotBreedWithoutLivingAdultPair()
        {
            var service = Service(out var buildings, out _);
            var home = StableEntityId.NewId();
            buildings.Add(Building.Restore(new BuildingState(home, new BuildingDefinitionId("animal_pasture"),
                default, BuildingOrientation.North, ConstructionState.Completed)));
            service.AddSite(ProductionSite.CreateCropPlot(home, new ProductionRecipeId("cultivate_berries"),
                default, TimeSpan.FromDays(2)));
            service.EstablishPasture(home, new CombatSideId("player"), "cow");
            foreach (var animal in service.Animals.AtHome(home).Where(x => x.Male))
                service.Animals.Damage(animal.Id, animal.Maximum);
            var initial = service.Animals.AtHome(home).Count;
            service.AdvanceTo(TimeSpan.FromDays(12).Ticks, Array.Empty<StableEntityId>());
            Assert.That(service.Animals.AtHome(home).Count, Is.EqualTo(initial));
        }

        [Test]
        public void SavedAgeGrowsGraduallyAndClampsAtAdultSize()
        {
            var born = TimeSpan.FromDays(10).Ticks;
            var animal = new AnimalVitalState(StableEntityId.NewId(), "sheep", 90, 90, true, bornTick: born);
            Assert.That(animal.GrowthFraction(born - 1, 3), Is.Zero);
            Assert.That(animal.GrowthFraction(born + TimeSpan.FromHours(12).Ticks, 3), Is.EqualTo(1d / 6d).Within(.00001));
            Assert.That(animal.GrowthFraction(born + TimeSpan.FromDays(1.5).Ticks, 3), Is.EqualTo(.5));
            Assert.That(animal.GrowthFraction(born + TimeSpan.FromDays(30).Ticks, 3), Is.EqualTo(1));
        }

        [Test]
        public void FishRotationCannotSnapNinetyDegreesInOneFrame()
        {
            var current=Quaternion.LookRotation(Vector3.forward);
            var target=Vector3.right;
            var next=WorksitePresentation.SmoothSwimmingRotation(current,target,1f/30f,38f);
            Assert.That(Quaternion.Angle(current,next),Is.LessThanOrEqualTo(38f/30f+.001f));
            Assert.That(Quaternion.Angle(next,Quaternion.LookRotation(target)),Is.GreaterThan(80f));
        }
    }
}
