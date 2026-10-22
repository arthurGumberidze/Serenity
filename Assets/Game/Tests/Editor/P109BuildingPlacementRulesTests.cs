using System;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Combat;
using Game.Domain.Time;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Simulation.Buildings;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("P10.9")]
    public sealed class P109BuildingPlacementRulesTests
    {
        [Test]
        public void GardenDragBuildsBoundedRectanglesWithAnchorFirst()
        {
            var anchor = new GridCoordinate(10, 20);
            var cells = Game.Presentation.Buildings.BuildingPlacementController.CreateRectangularZoneCells(
                anchor, new GridCoordinate(30, 10));

            Assert.That(cells[0], Is.EqualTo(anchor));
            Assert.That(cells.Count, Is.EqualTo(64));
            Assert.That(cells.Select(x => x.X).Distinct().Count(), Is.EqualTo(8));
            Assert.That(cells.Select(x => x.Z).Distinct().Count(), Is.EqualTo(8));
            var minimum = Game.Presentation.Buildings.BuildingPlacementController.CreateRectangularZoneCells(
                anchor, anchor);
            Assert.That(minimum.Count, Is.EqualTo(4));
        }
        [Test]
        public void OwnedTerritoryRequiresSettlementAndKeepsWholeFootprintInside()
        {
            var service = Service(out _);
            var settlement = new SettlementId(Id(1));
            var territories = new SettlementTerritoryRegistry();
            territories.Replace(settlement, new[] { new GridTerritoryRegion(0, 0, 3, 3) });
            service.ConfigureTerritories(territories);
            var definition = Definition("owned", BuildingSpatialForm.Structure,
                BuildingPlacementConstraint.OwnedTerritory, new BuildingFootprint(2, 2));

            Assert.That(service.Evaluate(new BuildingPlacementRequest(definition,
                new GridCoordinate(0, 0), BuildingOrientation.North)).FailureReason,
                Is.EqualTo(PlacementFailureReason.MissingSettlement));
            Assert.That(service.Evaluate(new BuildingPlacementRequest(definition,
                new GridCoordinate(2, 2), BuildingOrientation.North, settlement)).IsValid, Is.True);
            Assert.That(service.Evaluate(new BuildingPlacementRequest(definition,
                new GridCoordinate(3, 3), BuildingOrientation.North, settlement)).FailureReason,
                Is.EqualTo(PlacementFailureReason.OutsideOwnedTerritory));
        }

        [Test]
        public void ReachableApproachUsesDeterministicFreePerimeterAndReportsBlockedAccess()
        {
            var service = Service(out var occupancy);
            var definition = Definition("approach", BuildingSpatialForm.Structure,
                BuildingPlacementConstraint.ReachableApproach);
            var anchor = new GridCoordinate(0, 0);
            foreach (var cell in new[] { new GridCoordinate(-1, 0), new GridCoordinate(0, -1),
                         new GridCoordinate(0, 1), new GridCoordinate(1, 0) })
                occupancy.Reserve(new[] { cell }, Id(100 + cell.X * 3 + cell.Z), false);

            var result = service.Evaluate(definition, anchor, BuildingOrientation.North);
            Assert.That(result.FailureReason, Is.EqualTo(PlacementFailureReason.NoReachableApproach));
            Assert.That(result.ApproachCells, Is.Empty);
        }

        [Test]
        public void ZonesRequireConnectedShapeAndSegmentsPreserveOrderedTurns()
        {
            var service = Service(out _);
            var zone = Definition("zone", BuildingSpatialForm.Zone, BuildingPlacementConstraint.None);
            var connected = new[] { new GridCoordinate(2, 2), new GridCoordinate(3, 2),
                new GridCoordinate(3, 3), new GridCoordinate(2, 3) };
            var zoneResult = service.Evaluate(new BuildingPlacementRequest(zone, connected[0],
                BuildingOrientation.North, authoredCells: connected));
            Assert.That(zoneResult.IsValid, Is.True);
            Assert.That(zoneResult.Cells, Is.Ordered.By("X").Then.By("Z"));
            Assert.That(service.Evaluate(new BuildingPlacementRequest(zone, connected[0],
                BuildingOrientation.North, authoredCells: new[] { connected[0], new GridCoordinate(5, 5) }))
                .FailureReason, Is.EqualTo(PlacementFailureReason.InvalidShape));

            var segment = Definition("segment", BuildingSpatialForm.Segment, BuildingPlacementConstraint.None);
            var turn = new[] { new GridCoordinate(-2, -2), new GridCoordinate(-1, -2),
                new GridCoordinate(-1, -1), new GridCoordinate(0, -1) };
            var segmentResult = service.Evaluate(new BuildingPlacementRequest(segment, turn[0],
                BuildingOrientation.East, authoredCells: turn));
            Assert.That(segmentResult.IsValid, Is.True);
            Assert.That(segmentResult.Cells, Is.EqualTo(turn));
            Assert.That(service.Evaluate(new BuildingPlacementRequest(segment, turn[0],
                BuildingOrientation.East, authoredCells: new[] { turn[0], new GridCoordinate(0, -2) }))
                .FailureReason, Is.EqualTo(PlacementFailureReason.InvalidShape));
        }

        [Test]
        public void SpecializedConstraintsFailClosedAndCanBeSatisfiedByWorldAuthority()
        {
            var service = Service(out _);
            var shore = Definition("shore", BuildingSpatialForm.Structure, BuildingPlacementConstraint.Shoreline);
            Assert.That(service.Evaluate(shore, new GridCoordinate(0, 0), BuildingOrientation.North).FailureReason,
                Is.EqualTo(PlacementFailureReason.ShorelineRequired));
            service.ConfigureRuleEvaluator(context => context.Request.Definition.Id == shore.Id
                ? PlacementFailureReason.None : PlacementFailureReason.GroundwaterRequired);
            Assert.That(service.Evaluate(shore, new GridCoordinate(0, 0), BuildingOrientation.North).IsValid, Is.True);
        }

        [Test]
        public void ConfirmAndVersion27RoundTripPreserveSettlementAndExactAuthoredCells()
        {
            var service = Service(out _);
            var definition = Definition("road", BuildingSpatialForm.Segment, BuildingPlacementConstraint.None);
            var settlement = new SettlementId(Id(20));
            var cells = new[] { new GridCoordinate(1, 1), new GridCoordinate(2, 1), new GridCoordinate(2, 2) };
            var building = service.Confirm(new BuildingPlacementRequest(definition, cells[0],
                BuildingOrientation.North, settlement, cells), new CombatSideId("player"));
            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, Id(21), new GameTimeState(), 400,
                buildingWorld: new BuildingWorldSaveState(new[] { building.CaptureState() }, null, 0));
            var restored = new SaveXmlCodec().Deserialize(new SaveXmlCodec().Serialize(snapshot));
            var state = restored.BuildingWorld.Buildings.Single();
            Assert.That(state.SettlementId, Is.EqualTo(settlement));
            Assert.That(state.PlacementCells, Is.EqualTo(cells));
        }

        private static BuildingPlacementService Service(out BuildingOccupancyGrid occupancy)
        {
            occupancy = new BuildingOccupancyGrid();
            return new BuildingPlacementService(new BuildingRegistry(), occupancy,
                new BuildingGridBounds(-10, -10, 10, 10));
        }

        private static BuildingDefinition Definition(string id, BuildingSpatialForm form,
            BuildingPlacementConstraint constraints, BuildingFootprint? footprint = null) =>
            new BuildingDefinition(new BuildingDefinitionId(id), id, BuildingCategory.Structure,
                BuildingEra.StoneAge, string.Empty, string.Empty, null,
                new ConstructionSpec(null, TimeSpan.FromTicks(1)),
                new PlacementSpec(footprint ?? new BuildingFootprint(1, 1), spatialForm: form,
                    constraints: constraints));

        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));
    }
}
