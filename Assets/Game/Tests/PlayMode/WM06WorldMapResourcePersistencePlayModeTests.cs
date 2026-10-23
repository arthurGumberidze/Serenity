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
    [Category("WM06")]
    public sealed class WM06WorldMapResourcePersistencePlayModeTests
    {
        [UnityTest]
        public IEnumerator DepletedV2NodeRoundTripsThroughTheCanonicalProductionSavePayload()
        {
            const long seed = 20261023L;
            var world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var plan = new TiledWorldPopulationPlanner(world, new TiledWorldHeightSampler(world, seed), seed).Create();
            var node = plan.VisibleResources.First(value => value.ResourceTypeId == "wood");
            var id = node.CreateStableId(seed, world.GenerationId);

            var original = CreateAuthority(node, id);
            Assert.That(original.Service.TryGetSite(id, out var site), Is.True);
            site.CompleteWork(ProductionTaskKind.Gather, true);
            original.Ecology.AdvanceTo(123, new FreeSurface());
            Assert.That(site.IsDepleted, Is.True);

            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion,
                StableEntityId.FromDeterministicHash(1001, 2002), new GameTimeState(123, 123, 1, false),
                400, original.Service.CaptureState(), saveGenerationId: world.GenerationId);
            var codec = new SaveXmlCodec();
            var decoded = codec.Deserialize(codec.Serialize(snapshot));

            var restored = CreateAuthority(node, id);
            restored.Service.RestoreState(decoded.Production);
            Assert.That(restored.Service.TryGetSite(id, out var loaded), Is.True);
            Assert.That(loaded.RemainingAmount, Is.Zero);
            Assert.That(loaded.SourceCapacity, Is.EqualTo(node.Capacity));
            Assert.That(restored.Ecology.CaptureState().Single().DepletedAtTick, Is.EqualTo(123));
            Assert.That(node.CreateStableId(seed, world.GenerationId), Is.EqualTo(loaded.Id));
            yield return null;
        }

        private static Authority CreateAuthority(TiledWorldResourceNode node, StableEntityId id)
        {
            var resources = new ResourceCatalog(new[]
            {
                new ResourceDefinition(new ResourceId("wood_log"), "Wood", ResourceCategory.Construction),
                new ResourceDefinition(new ResourceId("stone"), "Stone", ResourceCategory.Construction),
                new ResourceDefinition(new ResourceId("plant_fiber"), "Fiber", ResourceCategory.Construction),
                new ResourceDefinition(new ResourceId("berries"), "Berries", ResourceCategory.Food)
            });
            var inventories = new ResourceInventoryRegistry();
            var service = new ProductionService(ProductionCatalog.CreateStoneAge(resources), resources,
                inventories, new BuildingRegistry(), new ResourceTransferService(inventories));
            var ecology = new ResourceEcologyService();
            var industry = new IndustrialExtractionService(resources, inventories,
                Array.Empty<IndustrialZoneDefinition>());
            service.AttachNaturalResources(ecology, industry);
            var site = ProductionSite.Restore(new ProductionSiteState(id,
                new ProductionRecipeId(node.RecipeId), ProductionSiteKind.NaturalSource,
                node.Position, node.Capacity, node.Capacity, CropPlotPhase.Unplanted, 0, 0));
            service.AddSite(site);
            ecology.Register(new NaturalNodeRegistration(site, node.Biome, new[] { node.Position }, true));
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
