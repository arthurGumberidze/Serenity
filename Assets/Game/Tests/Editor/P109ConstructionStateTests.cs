using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Time;
using Game.Domain.World;
using Game.Domain.Work;
using Game.Infrastructure;
using Game.Simulation.Buildings;
using Game.Simulation.AI;
using Game.Simulation.Resources;
using Game.Simulation.Work;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    public sealed class P109ConstructionStateTests
    {
        private static readonly ResourceId Wood = new ResourceId("wood");
        private static readonly ResourceId Stone = new ResourceId("stone");

        [Test]
        public void EmptyStocksPermitBlueprintWithoutCreatingMaterialsOrBuildWork()
        {
            var f = CreateLogisticsFixture();
            foreach (var source in f.Sources)
                foreach (var amount in source.CaptureState().Amounts)
                    source.TryRemove(amount.ResourceId, amount.Quantity);
            var result = f.Funding.TryConfirm(Definition(), new GridCoordinate(3,4), BuildingOrientation.NorthEast,
                f.Sources.Select(x => x.Owner), out var building);
            Assert.That(result.IsValid, Is.True);
            Assert.That(building.IsOperational, Is.False);
            Assert.That(building.ConstructionMaterials.Sum(x => x.RemainingUnits), Is.GreaterThan(0));
            Assert.That(f.Inventories.All.Sum(x => x.TotalUnits), Is.Zero);
            Assert.That(f.Work.Jobs, Is.Empty);
            var restored = Building.Restore(building.CaptureState());
            Assert.That(restored.Orientation.Degrees(), Is.EqualTo(45));
            var orientation = BuildingOrientation.North;
            var directions = new System.Collections.Generic.HashSet<int>();
            for (var i=0;i<8;i++) { directions.Add(orientation.Degrees()); orientation=orientation.RotateStep45(); }
            Assert.That(directions, Is.EquivalentTo(new[] {0,45,90,135,180,225,270,315}));
            Assert.That(orientation, Is.EqualTo(BuildingOrientation.North));
        }

        [Test]
        public void ConstructionBuffer_AcceptsEveryCategoryDeclaredByItsContract()
        {
            var hide = new ResourceId("cured_hide");
            var resources = new ResourceCatalog(new[]
            {
                new ResourceDefinition(Wood, "Wood", ResourceCategory.Construction),
                new ResourceDefinition(hide, "Cured hide", ResourceCategory.Other)
            });
            var inventories = new ResourceInventoryRegistry();
            var source = new ResourceInventory(new InventoryOwner(InventoryOwnerKind.BuildingStorage, Id(1)),
                resources, 10);
            source.TryAdd(Wood, new ResourceQuantity(2));
            source.TryAdd(hide, new ResourceQuantity(2));
            inventories.Add(source);
            var definition = new BuildingDefinition(new BuildingDefinitionId("mixed_contract"), "Mixed contract",
                BuildingCategory.Shelter, BuildingEra.StoneAge, "", "", null,
                new ConstructionSpec(new[] { new ResourceAmount(Wood, new ResourceQuantity(2)),
                    new ResourceAmount(hide, new ResourceQuantity(2)) }, TimeSpan.FromTicks(10)),
                new PlacementSpec(new BuildingFootprint(1, 1)));
            var building = Building.CreateNew(definition, default, BuildingOrientation.North,
                new CombatSideId("player"), Id(2));
            var sites = new ConstructionSiteService(resources, inventories);
            sites.Initialize(building, definition, new SettlementId(Id(3)));

            Assert.That(sites.Deliver(building, source.Owner, hide, new ResourceQuantity(2)),
                Is.EqualTo(TransferFailureReason.None));
            Assert.That(sites.Deliver(building, source.Owner, Wood, new ResourceQuantity(2)),
                Is.EqualTo(TransferFailureReason.None));
            Assert.That(building.ConstructionPhase, Is.EqualTo(ConstructionPhase.ReadyForWork));
        }

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
        public void Funding_QueuesPhysicalDeliveriesAndOnlyCarryingMovesStock()
        {
            var setup = CreateLogisticsFixture();
            var initial = setup.Sources.Sum(x => x.TotalUnits);

            var result = setup.Funding.TryConfirm(Definition(), new GridCoordinate(3, 4), BuildingOrientation.North,
                setup.Sources.Select(x => x.Owner), out var building);

            Assert.That(result.IsValid, Is.True);
            Assert.That(setup.Sources.Sum(x => x.TotalUnits), Is.EqualTo(initial),
                "Planning must not teleport resources.");
            Assert.That(setup.Inventories.Get(building.ConstructionBufferOwner).TotalUnits, Is.Zero);
            var deliveries = setup.Work.Jobs.Where(x => x.Type == WorkJobType.Haul).ToArray();
            Assert.That(deliveries.Sum(x => x.Quantity.Units), Is.EqualTo(7));
            Assert.That(deliveries.All(x => x.Destination.Value.Kind == WorkTargetKind.ConstructionBuffer), Is.True);

            var carryOwner = new InventoryOwner(InventoryOwnerKind.CarryLoad, Id(41));
            setup.Inventories.Add(new ResourceInventory(carryOwner, setup.Resources, 12));
            var claims = new HaulClaimRegistry();
            var query = new HaulWorldQuery(setup.Inventories, setup.Piles, setup.Buildings, claims);
            var firstDelivery = deliveries[0];
            Assert.That(query.TryClaimSpecified(new Tier1AiAgentState(Id(41), default),
                new InventoryOwner(InventoryOwnerKind.WorldPile, firstDelivery.Target.EntityId),
                building.ConstructionBufferOwner, firstDelivery.ResourceId.Value, firstDelivery.Quantity,
                out var candidate, out var claim), Is.True);
            Assert.That(candidate.Destination, Is.EqualTo(building.ConstructionBufferOwner));
            claims.Release(claim);
            var transfers = new ResourceTransferService(setup.Inventories);
            foreach (var delivery in deliveries)
            {
                var source = new InventoryOwner(InventoryOwnerKind.WorldPile, delivery.Target.EntityId);
                Assert.That(transfers.TransferExact(source, carryOwner, delivery.ResourceId.Value,
                    delivery.Quantity), Is.EqualTo(TransferFailureReason.None));
                Assert.That(transfers.TransferExact(carryOwner, building.ConstructionBufferOwner,
                    delivery.ResourceId.Value, delivery.Quantity), Is.EqualTo(TransferFailureReason.None));
                setup.Sites.NotifyDelivery(building);
            }

            Assert.That(building.ConstructionPhase, Is.EqualTo(ConstructionPhase.Building));
            Assert.That(setup.Sources.Sum(x => x.TotalUnits) + setup.Inventories.Get(carryOwner).TotalUnits +
                setup.Inventories.Get(building.ConstructionBufferOwner).TotalUnits +
                building.ConstructionMaterials.Sum(x => x.InstalledUnits), Is.EqualTo(initial));
        }

        [Test]
        public void Funding_PreemptsUnpickedAutonomousClaimsForConstructionDeliveries()
        {
            var setup = CreateLogisticsFixture();
            var wood = setup.Sources.Single(x => x.GetAmount(Wood).Units > 0);
            var destination = new InventoryOwner(InventoryOwnerKind.BuildingStorage, Id(99));
            Assert.That(setup.Claims.TryClaim(Id(40), wood.Owner, destination, Wood, new ResourceQuantity(4),
                6, 100, out _), Is.True);
            Assert.That(setup.Claims.TryClaim(Id(41), wood.Owner, destination, Wood, new ResourceQuantity(2),
                6, 100, out _), Is.True);
            Assert.That(setup.Claims.ReservedAtSource(wood.Owner, Wood), Is.EqualTo(6));

            var result = setup.Funding.TryConfirm(Definition(), new GridCoordinate(3, 4), BuildingOrientation.North,
                setup.Sources.Select(x => x.Owner), out _);

            Assert.That(result.IsValid, Is.True);
            Assert.That(setup.Claims.ReservedAtSource(wood.Owner, Wood), Is.Zero);
            Assert.That(setup.Work.Jobs.Where(x => x.Type == WorkJobType.Haul && x.ResourceId == Wood)
                .Sum(x => x.Quantity.Units), Is.EqualTo(5));
        }

        [Test]
        public void Cancellation_ReturnsDeliveredAndInstalledMaterialsToWorldPiles()
        {
            var setup = CreateLogisticsFixture();
            var initial = setup.Sources.Sum(x => x.TotalUnits);
            Assert.That(setup.Funding.TryConfirm(Definition(), new GridCoordinate(3, 4), BuildingOrientation.North,
                setup.Sources.Select(x => x.Owner), out var building).IsValid, Is.True);
            Assert.That(setup.Sites.Deliver(building, setup.Sources.Single(x => x.GetAmount(Wood).Units > 0).Owner,
                Wood, new ResourceQuantity(2)),
                Is.EqualTo(TransferFailureReason.None));
            Assert.That(setup.Sites.Deliver(building, setup.Sources.Single(x => x.GetAmount(Stone).Units > 0).Owner,
                Stone, new ResourceQuantity(1)),
                Is.EqualTo(TransferFailureReason.None));
            setup.Sites.NotifyDelivery(building);
            Assert.That(building.ConstructionStageIndex, Is.EqualTo(1));

            Assert.That(setup.Funding.Cancel(building, Definition()), Is.True);

            Assert.That(setup.Buildings.TryGet(building.Id, out _), Is.False);
            Assert.That(setup.Inventories.TryGet(building.ConstructionBufferOwner, out _), Is.False);
            Assert.That(setup.Work.Jobs.Where(x => x.Type == WorkJobType.Haul).All(x => x.IsTerminal), Is.True);
            var sourceOwners = setup.Sources.Select(x => x.Owner).ToHashSet();
            var spilled = setup.Piles.All.Where(pile => !sourceOwners.Contains(pile.InventoryOwner))
                .Sum(pile => setup.Inventories.Get(pile.InventoryOwner).TotalUnits);
            Assert.That(setup.Sources.Sum(x => x.TotalUnits) + spilled, Is.EqualTo(initial));
        }

        [Test]
        public void PendingConstructionDelivery_RoundTripsWithBufferDestination()
        {
            var setup = CreateLogisticsFixture();
            Assert.That(setup.Funding.TryConfirm(Definition(), new GridCoordinate(3, 4), BuildingOrientation.North,
                setup.Sources.Select(x => x.Owner), out var building).IsValid, Is.True);
            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, Id(902), new GameTimeState(), 400,
                production: new ProductionSaveState(0, 0, 0, 0, null, null, null, null,
                    setup.Inventories.CaptureStates()),
                buildingWorld: new BuildingWorldSaveState(new[] { building.CaptureState() },
                    setup.Piles.CaptureStates(), 0), work: setup.Work.CaptureState(),
                saveGenerationId: SaveSnapshot.StoneAgeBuildingSaveGenerationId);

            var restored = new SaveXmlCodec().Deserialize(new SaveXmlCodec().Serialize(snapshot));
            var work = new WorkManager(new CharacterRegistry());
            work.RestoreState(restored.Work);

            Assert.That(work.Jobs.Count(x => !x.IsTerminal && x.Type == WorkJobType.Haul), Is.EqualTo(2));
            Assert.That(work.Jobs.Where(x => x.Type == WorkJobType.Haul)
                .All(x => x.Destination.Value.Kind == WorkTargetKind.ConstructionBuffer &&
                    x.Destination.Value.EntityId == building.Id), Is.True);
            Assert.That(restored.Production.Inventories.Single(x => x.Owner == building.ConstructionBufferOwner)
                .Amounts, Is.Empty);
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
            var snapshot = new SaveSnapshot(SaveSnapshot.BuildingConstructionVersion, Id(900), new GameTimeState(), 400,
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

        private static LogisticsFixture CreateLogisticsFixture()
        {
            var resources = new ResourceCatalog(new[]
            {
                new ResourceDefinition(Wood, "Wood", ResourceCategory.Construction),
                new ResourceDefinition(Stone, "Stone", ResourceCategory.Construction)
            });
            var inventories = new ResourceInventoryRegistry();
            var piles = new WorldPileService(resources, inventories);
            var woodPile = piles.Create(Wood, new ResourceQuantity(6), new GridCoordinate(-2, 0), Id(10));
            var stonePile = piles.Create(Stone, new ResourceQuantity(4), new GridCoordinate(-3, 0), Id(11));
            var sources = new[] { inventories.Get(woodPile.InventoryOwner), inventories.Get(stonePile.InventoryOwner) };
            var buildings = new BuildingRegistry();
            var occupancy = new BuildingOccupancyGrid();
            var placement = new BuildingPlacementService(buildings, occupancy,
                new BuildingGridBounds(-10, -10, 10, 10));
            var sites = new ConstructionSiteService(resources, inventories);
            var work = new WorkManager(new CharacterRegistry());
            var claims = new HaulClaimRegistry();
            var funding = new ConstructionFundingService(placement, inventories, resources,
                new StorageService(inventories, resources)) { RequiresWorker = true };
            funding.ConfigureLogistics(sites, work, piles, new SettlementId(Id(30)), claims);
            return new LogisticsFixture(resources, inventories, piles, sources, buildings, sites, work, funding, claims);
        }

        [Test]
        public void RetryDeliveryRequestsOnlyMaterialsMissingFromTheExistingBuffer()
        {
            var f=CreateLogisticsFixture();
            Assert.That(f.Funding.TryConfirm(Definition(),new GridCoordinate(3,4),BuildingOrientation.North,
                f.Sources.Select(x=>x.Owner),out var building).IsValid,Is.True);
            Assert.That(f.Sites.Deliver(building,f.Sources[0].Owner,Wood,new ResourceQuantity(3)),Is.EqualTo(TransferFailureReason.None));
            foreach(var job in f.Work.Jobs.Where(x=>!x.IsTerminal).ToArray())f.Work.Cancel(job.Id);
            Assert.That(f.Funding.TryScheduleDeliveries(building,Definition(),f.Inventories.All.Select(x=>x.Owner),WorkPriority.Normal),Is.True);
            var pending=f.Work.Jobs.Where(x=>!x.IsTerminal).ToArray();
            Assert.That(pending.Where(x=>x.ResourceId==Wood).Sum(x=>x.Quantity.Units),Is.EqualTo(2));
            Assert.That(pending.Where(x=>x.ResourceId==Stone).Sum(x=>x.Quantity.Units),Is.EqualTo(2));
            foreach(var job in pending)f.Work.Cancel(job.Id);
            f.Sites.Deliver(building,f.Sources[0].Owner,Wood,new ResourceQuantity(2));
            f.Sites.Deliver(building,f.Sources[1].Owner,Stone,new ResourceQuantity(2));
            Assert.That(f.Funding.TryScheduleDeliveries(building,Definition(),f.Inventories.All.Select(x=>x.Owner),WorkPriority.Normal),Is.True);
            Assert.That(f.Work.Jobs.Any(x=>!x.IsTerminal),Is.False);
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

        private sealed class LogisticsFixture
        {
            public LogisticsFixture(ResourceCatalog resources, ResourceInventoryRegistry inventories,
                WorldPileService piles, IReadOnlyList<ResourceInventory> sources, BuildingRegistry buildings,
                ConstructionSiteService sites, WorkManager work, ConstructionFundingService funding,
                HaulClaimRegistry claims)
            { Resources = resources; Inventories = inventories; Piles = piles; Sources = sources; Buildings = buildings;
                Sites = sites; Work = work; Funding = funding; Claims = claims; }
            public ResourceCatalog Resources { get; }
            public ResourceInventoryRegistry Inventories { get; }
            public WorldPileService Piles { get; }
            public IReadOnlyList<ResourceInventory> Sources { get; }
            public BuildingRegistry Buildings { get; }
            public ConstructionSiteService Sites { get; }
            public WorkManager Work { get; }
            public ConstructionFundingService Funding { get; }
            public HaulClaimRegistry Claims { get; }
        }
    }
}
