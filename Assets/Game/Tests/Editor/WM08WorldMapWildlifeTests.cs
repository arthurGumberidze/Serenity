using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Presentation.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("WM08")]
    public sealed class WM08WorldMapWildlifeTests
    {
        private const long Seed = 20261023L;
        private TiledWorldDefinition world;
        private WildlifePopulationPlan plan;

        [OneTimeSetUp]
        public void SetUpPlan()
        {
            world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var heights = new TiledWorldHeightSampler(world, Seed);
            plan = new WorldMapWildlifePlanner(world, heights, Seed).Create();
        }

        [Test]
        public void TypedContractAndSeededPopulationMatchAuthoredSpeciesTargets()
        {
            Assert.That(plan.Definition.Species.Select(value => value.Id), Is.EquivalentTo(new[]
                { "deer", "boar", "hare", "fox", "wolf", "bear", "waterfowl" }));
            Assert.That(plan.Definition.CanonicalPopulationTarget.Contains(plan.Individuals.Count), Is.True);
            foreach (var species in plan.Definition.Species)
            {
                var count = plan.Individuals.Count(value => value.SpeciesId == species.Id);
                Assert.That(species.TargetPopulation.Contains(count), Is.True, species.Id);
                Assert.That(count, Is.GreaterThanOrEqualTo(plan.Capacities.Count(value =>
                    value.SpeciesId == species.Id)), species.Id);
                Assert.That(plan.Individuals.Where(value => value.SpeciesId == species.Id)
                    .Select(value => value.Id).Distinct().Count(), Is.EqualTo(count), species.Id);
            }

            var repeat = new WorldMapWildlifePlanner(world, new TiledWorldHeightSampler(world, Seed), Seed).Create();
            Assert.That(repeat.Individuals.Select(value => value.Id),
                Is.EqualTo(plan.Individuals.Select(value => value.Id)));
            Assert.That(repeat.Individuals.Select(value => value.HabitatNodeId),
                Is.EqualTo(plan.Individuals.Select(value => value.HabitatNodeId)));
        }

        [Test]
        public void SuitableHabitatsAreMapWideAndConcentrationZonesOnlyMultiplyDensity()
        {
            Assert.That(plan.Capacities.Any(value => value.DensityMultiplier > 1f), Is.True);
            foreach (var species in plan.Definition.Species)
            {
                var capacities = plan.Capacities.Where(value => value.SpeciesId == species.Id).ToArray();
                Assert.That(capacities, Is.Not.Empty, species.Id);
                Assert.That(capacities.Any(value => value.DensityMultiplier == 1f), Is.True,
                    species.Id + " must exist outside concentration zones");
                Assert.That(capacities.Select(value => value.RegionId).Distinct().Count(),
                    Is.GreaterThanOrEqualTo(2), species.Id);
                foreach (var capacity in capacities)
                {
                    var region = world.MapData.Biomes.Regions.Single(value => value.Id == capacity.RegionId);
                    Assert.That(WorldMapWildlifePlanner.IsSuitable(species, region.Biomes), Is.True,
                        species.Id + ":" + region.Id);
                    Assert.That(plan.Individuals.Any(value => value.SpeciesId == species.Id &&
                        plan.HabitatGraph.Node(value.HabitatNodeId).RegionId == region.Id), Is.True,
                        species.Id + " absent from suitable region " + region.Id);
                }
            }
        }

        [Test]
        public void HabitatGraphConnectsFoodRestWaterAndShelterAcrossAllRegions()
        {
            foreach (var region in world.MapData.Biomes.Regions)
                Assert.That(plan.HabitatGraph.Nodes.Where(value => value.RegionId == region.Id)
                    .Select(value => value.Function), Is.EquivalentTo(new[]
                    {
                        WildlifeHabitatFunction.Food, WildlifeHabitatFunction.Rest,
                        WildlifeHabitatFunction.Water, WildlifeHabitatFunction.Shelter
                    }), region.Id);

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Queue<string>();
            pending.Enqueue(plan.HabitatGraph.Nodes[0].Id);
            while (pending.Count > 0)
            {
                var id = pending.Dequeue();
                if (!visited.Add(id)) continue;
                foreach (var edge in plan.HabitatGraph.Edges)
                {
                    if (edge.FromNodeId == id) pending.Enqueue(edge.ToNodeId);
                    if (edge.ToNodeId == id) pending.Enqueue(edge.FromNodeId);
                }
            }
            Assert.That(visited.Count, Is.EqualTo(plan.HabitatGraph.Nodes.Count));
        }

        [Test]
        public void MigrationAndReproductionUseOnePopulationAndRespectCarryingCapacity()
        {
            var service = new WildlifePopulationService(plan, Seed);
            var migrationSource = plan.Capacities.First(capacity =>
                plan.HabitatGraph.RegionNeighbors(capacity.RegionId).Any(region =>
                {
                    var destination = plan.Capacities.FirstOrDefault(other =>
                        other.SpeciesId == capacity.SpeciesId && other.RegionId == region);
                    return destination != null && service.Animals.Count(animal =>
                        animal.SpeciesId == destination.SpeciesId &&
                        plan.HabitatGraph.Node(animal.HabitatNodeId).RegionId == destination.RegionId) <
                        destination.CarryingCapacity;
                }));
            var beforeIds = service.Animals.Select(value => value.Id).ToArray();
            var beforeRegions = service.Animals.ToDictionary(value => value.Id,
                value => plan.HabitatGraph.Node(value.HabitatNodeId).RegionId);
            service.ApplyHuntingPressure(migrationSource.SpeciesId, migrationSource.RegionId, 1000);
            service.AdvanceTo(TimeSpan.TicksPerDay);
            Assert.That(beforeIds.All(id => service.Animals.Any(value => value.Id == id)), Is.True);
            Assert.That(service.Animals.Any(value => beforeRegions.TryGetValue(value.Id, out var oldRegion) &&
                oldRegion != plan.HabitatGraph.Node(value.HabitatNodeId).RegionId), Is.True);

            var reproductionHabitat = plan.Capacities.First(value => service.Animals.Count(animal =>
                animal.SpeciesId == value.SpeciesId &&
                plan.HabitatGraph.Node(animal.HabitatNodeId).RegionId == value.RegionId) >= 3);
            var removed = service.Animals.First(value => value.SpeciesId == reproductionHabitat.SpeciesId &&
                plan.HabitatGraph.Node(value.HabitatNodeId).RegionId == reproductionHabitat.RegionId);
            Assert.That(service.Remove(removed.Id), Is.True);
            var afterRemoval = service.Count;
            service.AdvanceTo(TimeSpan.TicksPerDay * 40L);
            Assert.That(service.Count, Is.GreaterThan(afterRemoval));
            foreach (var capacity in plan.Capacities)
                Assert.That(service.Animals.Count(value => value.SpeciesId == capacity.SpeciesId &&
                    plan.HabitatGraph.Node(value.HabitatNodeId).RegionId == capacity.RegionId),
                    Is.LessThanOrEqualTo(capacity.CarryingCapacity), capacity.SpeciesId + ":" + capacity.RegionId);
        }

        [Test]
        public void TierAndStateRoundTripPreserveEveryStableIdentityWithoutDuplication()
        {
            var service = new WildlifePopulationService(plan, Seed);
            service.RebalanceTiers(world.PlayerStart);
            Assert.That(service.Animals.Count(value => value.Tier == CharacterSimulationTier.Tier1), Is.EqualTo(35));
            Assert.That(service.Animals.Count(value => value.Tier == CharacterSimulationTier.Tier2), Is.EqualTo(80));
            Assert.That(service.Animals.Count(value => value.Tier == CharacterSimulationTier.Tier3),
                Is.EqualTo(service.Count - 115));
            var snapshot = service.CaptureState();
            var restored = new WildlifePopulationService(plan, Seed);
            restored.RestoreState(snapshot);
            Assert.That(restored.Animals.Select(value => value.Id), Is.EqualTo(service.Animals.Select(value => value.Id)));
            Assert.That(restored.Animals.Select(value => value.Tier), Is.EqualTo(service.Animals.Select(value => value.Tier)));
        }

        [Test]
        public void CatalogReusesImportedAssetsAndAvailableAnimationWithoutGeneratedArt()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WorldMapWildlifeArtCatalog>(
                "Assets/Game/Resources/World/WorldMapWildlifeArt.asset");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Entries, Has.Count.EqualTo(7));
            foreach (var entry in catalog.Entries)
            {
                Assert.That(entry.Prefab, Is.Not.Null, entry.SpeciesId);
                Assert.That(entry.Prefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty, entry.SpeciesId);
                var animator = entry.Prefab.GetComponentInChildren<Animator>(true);
                if (entry.SpeciesId != "waterfowl")
                    Assert.That(animator?.runtimeAnimatorController, Is.Not.Null, entry.SpeciesId);
                Assert.That(AssetDatabase.GetAssetPath(entry.Prefab), Does.Not.StartWith("Assets/Game/Art/WorldMapV2/Wildlife"));
            }
        }
    }
}
