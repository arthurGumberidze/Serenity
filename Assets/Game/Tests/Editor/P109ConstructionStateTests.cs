using System;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Combat;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Time;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Simulation.Buildings;
using Game.Simulation.Resources;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    public sealed class P109ConstructionStateTests
    {
        private static readonly ResourceId Wood = new ResourceId("wood");
        private static readonly ResourceId Stone = new ResourceId("stone");

        [Test]
        public void ConstructionBuffer_TransfersAndConsumesEachStageWithoutDuplicatingStock()
        {
            var fixture = CreateFixture();
            Assert.That(fixture.Building.ConstructionPhase, Is.EqualTo(ConstructionPhase.AwaitingMaterials));
            Assert.That(fixture.Sites.Deliver(fixture.Building, fixture.Source.Owner, Wood,
                new ResourceQuantity(6)), Is.EqualTo(TransferFailureReason.DestinationRejected));
            Assert.That(fixture.Source.TotalUnits, Is.EqualTo(10));

            Assert.That(fixture.Sites.Deliver(fixture.Building, fixture.Source.Owner, Wood,
                new ResourceQuantity(2)), Is.EqualTo(TransferFailureReason.None));
            Assert.That(fixture.Sites.Deliver(fixture.Building, fixture.Source.Owner, Stone,
                new ResourceQuantity(1)), Is.EqualTo(TransferFailureReason.None));
            Assert.That(fixture.Building.ConstructionPhase, Is.EqualTo(ConstructionPhase.ReadyForWork));
            Assert.That(fixture.Sites.CommitReadyStage(fixture.Building), Is.True);
            Assert.That(fixture.Building.ConstructionStageIndex, Is.EqualTo(1));
            Assert.That(fixture.Buffer.TotalUnits, Is.Zero);
            Assert.That(fixture.Source.TotalUnits, Is.EqualTo(7));

            Assert.That(fixture.Sites.Deliver(fixture.Building, fixture.Source.Owner, Wood,
                new ResourceQuantity(3)), Is.EqualTo(TransferFailureReason.None));
            Assert.That(fixture.Sites.Deliver(fixture.Building, fixture.Source.Owner, Stone,
                new ResourceQuantity(1)), Is.EqualTo(TransferFailureReason.None));
            Assert.That(fixture.Sites.CommitReadyStage(fixture.Building), Is.True);
            Assert.That(fixture.Building.ConstructionPhase, Is.EqualTo(ConstructionPhase.Building));
            Assert.That(fixture.Source.TotalUnits + fixture.Buffer.TotalUnits +
                fixture.Building.ConstructionMaterials.Sum(x => x.InstalledUnits), Is.EqualTo(10));
        }

        [Test]
        public void ConstructionWork_WaitsForAllMaterialStages()
        {
            var fixture = CreateFixture();
            fixture.Building.AdvanceConstruction(TimeSpan.FromTicks(20));
            Assert.That(fixture.Building.ConstructionWorkTicks, Is.Zero);
            DeliverAll(fixture);
            fixture.Building.AdvanceConstruction(TimeSpan.FromTicks(20));
            Assert.That(fixture.Building.ConstructionState, Is.EqualTo(ConstructionState.Completed));
            Assert.That(fixture.Building.ConstructionPhase, Is.EqualTo(ConstructionPhase.Completed));
        }

        [Test]
        public void Version26_RoundTripsSettlementStagesAndDeliveredBuffer()
        {
            var fixture = CreateFixture();
            fixture.Sites.Deliver(fixture.Building, fixture.Source.Owner, Wood, new ResourceQuantity(2));
            fixture.Sites.Deliver(fixture.Building, fixture.Source.Owner, Stone, new ResourceQuantity(1));
            fixture.Sites.CommitReadyStage(fixture.Building);
            fixture.Sites.Deliver(fixture.Building, fixture.Source.Owner, Wood, new ResourceQuantity(1));

            var production = new ProductionSaveState(0, 0, 0, 0, null, null, null, null,
                fixture.Inventories.CaptureStates());
            var buildings = new BuildingWorldSaveState(new[] { fixture.Building.CaptureState() }, null, 0);
            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, Id(900), new GameTimeState(), 400,
                production: production, buildingWorld: buildings);
            var restored = new SaveXmlCodec().Deserialize(new SaveXmlCodec().Serialize(snapshot));

            Assert.That(restored.SaveGenerationId, Is.EqualTo(SaveSnapshot.StoneAgeBuildingSaveGenerationId));
            var building = restored.BuildingWorld.Buildings.Single();
            Assert.That(building.SettlementId, Is.EqualTo(fixture.Settlement));
            Assert.That(building.ConstructionPhase, Is.EqualTo(ConstructionPhase.AwaitingMaterials));
            Assert.That(building.ConstructionStageIndex, Is.EqualTo(1));
            Assert.That(building.ConstructionMaterials.Single(x => x.ResourceId == Wood).InstalledUnits, Is.EqualTo(2));
            var buffer = restored.Production.Inventories.Single(x => x.Owner.Kind == InventoryOwnerKind.ConstructionBuffer);
            Assert.That(buffer.Amounts.Single().Quantity.Units, Is.EqualTo(1));
        }

        [Test]
        public void Version25_RemainsReadableAsLegacyConstructionWithoutGenerationRewrite()
        {
            var building = Building.CreateNew(Definition(), new GridCoordinate(1, 2), BuildingOrientation.North,
                new CombatSideId("player"));
            var snapshot = new SaveSnapshot(SaveSnapshot.DiagnosticVersion, Id(901), new GameTimeState(), 400,
                buildingWorld: new BuildingWorldSaveState(new[] { building.CaptureState() }, null, 0));
            var restored = new SaveXmlCodec().Deserialize(new SaveXmlCodec().Serialize(snapshot));
            Assert.That(restored.FormatVersion, Is.EqualTo(25));
            Assert.That(restored.SaveGenerationId, Is.Null);
            Assert.That(restored.BuildingWorld.Buildings.Single().ConstructionPhase,
                Is.EqualTo(ConstructionPhase.LegacyPlanned));
        }

        private static Fixture CreateFixture()
        {
            var catalog = new ResourceCatalog(new[]
            {
                new ResourceDefinition(Wood, "Wood", ResourceCategory.Construction),
                new ResourceDefinition(Stone, "Stone", ResourceCategory.Construction)
            });
            var inventories = new ResourceInventoryRegistry();
            var source = new ResourceInventory(new InventoryOwner(InventoryOwnerKind.BuildingStorage, Id(10)),
                catalog, 20, new[] { ResourceCategory.Construction });
            source.TryAdd(Wood, new ResourceQuantity(6));
            source.TryAdd(Stone, new ResourceQuantity(4));
            inventories.Add(source);
            var building = Building.CreateNew(Definition(), new GridCoordinate(3, 4), BuildingOrientation.East,
                new CombatSideId("player"), Id(20));
            var settlement = new SettlementId(Id(30));
            var sites = new ConstructionSiteService(catalog, inventories);
            var buffer = sites.Initialize(building, Definition(), settlement);
            return new Fixture(inventories, source, building, settlement, sites, buffer);
        }

        private static void DeliverAll(Fixture fixture)
        {
            while (fixture.Building.ConstructionPhase != ConstructionPhase.Building)
            {
                foreach (var cost in fixture.Building.NextStageMaterialCost())
                    fixture.Sites.Deliver(fixture.Building, fixture.Source.Owner, cost.ResourceId, cost.Quantity);
                Assert.That(fixture.Sites.CommitReadyStage(fixture.Building), Is.True);
            }
        }

        private static BuildingDefinition Definition() => new BuildingDefinition(new BuildingDefinitionId("test_hut"),
            "Test hut", BuildingCategory.Shelter, BuildingEra.StoneAge, "", "", null,
            new ConstructionSpec(new[]
            {
                new ResourceAmount(Wood, new ResourceQuantity(5)),
                new ResourceAmount(Stone, new ResourceQuantity(2))
            }, TimeSpan.FromTicks(20), 2),
            new PlacementSpec(new BuildingFootprint(2, 2)));

        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));

        private sealed class Fixture
        {
            public Fixture(ResourceInventoryRegistry inventories, ResourceInventory source, Building building,
                SettlementId settlement, ConstructionSiteService sites, ResourceInventory buffer)
            { Inventories = inventories; Source = source; Building = building; Settlement = settlement; Sites = sites; Buffer = buffer; }
            public ResourceInventoryRegistry Inventories { get; }
            public ResourceInventory Source { get; }
            public Building Building { get; }
            public SettlementId Settlement { get; }
            public ConstructionSiteService Sites { get; }
            public ResourceInventory Buffer { get; }
        }
    }
}
