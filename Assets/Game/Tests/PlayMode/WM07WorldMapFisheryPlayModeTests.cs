using System;
using System.Collections;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Time;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Simulation.Production;
using Game.Simulation.Resources;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("WM07")]
    public sealed class WM07WorldMapFisheryPlayModeTests
    {
        private const long Seed = 20261023L;

        [UnityTest]
        public IEnumerator AllLandingsRemainDryAndSaveReloadDoesNotDuplicateExtractionOrRecovery()
        {
            var world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var heights = new TiledWorldHeightSampler(world, Seed);
            var nodes = new TiledWorldPopulationPlanner(world, heights, Seed).Create().Resources
                .Where(value => value.ResourceTypeId == "fish").ToArray();
            Assert.That(nodes, Has.Length.EqualTo(5));
            Assert.That(nodes.All(value => !heights.IsWater(value.Position.X, value.Position.Z)), Is.True);
            Assert.That(nodes.All(value => heights.IsWater(value.VisualPosition.X, value.VisualPosition.Z)), Is.True);

            var original = CreateAuthority(nodes, world.GenerationId);
            var target = nodes[0];
            var targetId = target.CreateStableId(Seed, world.GenerationId);
            Assert.That(original.Service.TryGetSite(targetId, out var site), Is.True);
            for (var i = 0; i < 12; i++) site.CompleteWork(ProductionTaskKind.Gather);
            original.Ecology.AdvanceTo(0, new FreeSurface());
            original.Service.AdvanceTo(TimeSpan.TicksPerDay, Array.Empty<StableEntityId>());
            original.Ecology.AdvanceTo(TimeSpan.TicksPerDay, new FreeSurface());
            var savedAmount = site.RemainingAmount;

            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion,
                StableEntityId.FromDeterministicHash(7007, 2407),
                new GameTimeState(TimeSpan.TicksPerDay, TimeSpan.TicksPerDay, 1, false),
                (int)Seed, original.Service.CaptureState(), saveGenerationId: world.GenerationId);
            var codec = new SaveXmlCodec();
            var decoded = codec.Deserialize(codec.Serialize(snapshot));

            var restored = CreateAuthority(nodes, world.GenerationId);
            restored.Service.RestoreState(decoded.Production);
            Assert.That(restored.Service.Sites.Count(value => value.RecipeId == new ProductionRecipeId("fish_water")),
                Is.EqualTo(5));
            Assert.That(restored.Service.TryGetSite(targetId, out var loaded), Is.True);
            Assert.That(loaded.RemainingAmount, Is.EqualTo(savedAmount));
            restored.Service.AdvanceTo(TimeSpan.TicksPerDay, Array.Empty<StableEntityId>());
            restored.Ecology.AdvanceTo(TimeSpan.TicksPerDay, new FreeSurface());
            Assert.That(loaded.RemainingAmount, Is.EqualTo(savedAmount), "reload must not replay the saved day");

            restored.Service.AdvanceTo(TimeSpan.TicksPerDay * 2L, Array.Empty<StableEntityId>());
            restored.Ecology.AdvanceTo(TimeSpan.TicksPerDay * 2L, new FreeSurface());
            var nextDayAmount = loaded.RemainingAmount;
            Assert.That(nextDayAmount, Is.EqualTo(savedAmount));
            restored.Ecology.AdvanceTo(TimeSpan.TicksPerDay * 2L, new FreeSurface());
            Assert.That(loaded.RemainingAmount, Is.EqualTo(nextDayAmount), "same tick must not recover twice");
            yield return null;
        }

        private static Authority CreateAuthority(TiledWorldResourceNode[] nodes, string generationId)
        {
            var resources = new ResourceCatalog(new[]
            {
                new ResourceDefinition(new ResourceId("wood_log"), "Wood", ResourceCategory.Construction),
                new ResourceDefinition(new ResourceId("stone"), "Stone", ResourceCategory.Construction),
                new ResourceDefinition(new ResourceId("plant_fiber"), "Fiber", ResourceCategory.Construction),
                new ResourceDefinition(new ResourceId("berries"), "Berries", ResourceCategory.Food),
                new ResourceDefinition(new ResourceId("fish"), "Fish", ResourceCategory.Food)
            });
            var inventories = new ResourceInventoryRegistry();
            var service = new ProductionService(ProductionCatalog.CreateStoneAge(resources), resources,
                inventories, new BuildingRegistry(), new ResourceTransferService(inventories));
            var ecology = new ResourceEcologyService();
            var industry = new IndustrialExtractionService(resources, inventories,
                Array.Empty<IndustrialZoneDefinition>());
            service.AttachNaturalResources(ecology, industry);
            foreach (var node in nodes)
            {
                var site = ProductionSite.Restore(new ProductionSiteState(
                    node.CreateStableId(Seed, generationId), new ProductionRecipeId(node.RecipeId),
                    ProductionSiteKind.NaturalSource, node.Position, node.Capacity, node.Capacity,
                    CropPlotPhase.Unplanted, 0, 0));
                service.AddSite(site);
                ecology.Register(new NaturalNodeRegistration(site, node.Biome, new[] { node.Position }, false));
            }
            return new Authority(service, ecology);
        }

        private sealed class Authority
        {
            public Authority(ProductionService service, ResourceEcologyService ecology)
            {
                Service = service;
                Ecology = ecology;
            }
            public ProductionService Service { get; }
            public ResourceEcologyService Ecology { get; }
        }

        private sealed class FreeSurface : IResourceRespawnSurface
        {
            public bool IsFree(NaturalResourceBiome biome, WorldPosition position, float footprintRadius) => true;
        }
    }
}
