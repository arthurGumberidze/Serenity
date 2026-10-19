using System;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Campaign;
using Game.Domain.Time;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Simulation;
using Game.Simulation.Time;
using Game.Simulation.World;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("P109")]
    public sealed class P109TiledWorldRuntimeTests
    {
        private TiledWorldDefinition world;
        private TiledWorldHeightSampler heights;

        [SetUp]
        public void SetUp()
        {
            world = TiledWorldDefinitionLoader.LoadReference();
            heights = new TiledWorldHeightSampler(world, 1092026);
        }

        [Test]
        public void HeightGenerationIsDeterministicAndNeighbourBordersAreIdentical()
        {
            var first = heights.CreateNormalizedHeights(new WorldTileId(5, 4), 65);
            var neighbour = heights.CreateNormalizedHeights(new WorldTileId(6, 4), 65);
            var replay = new TiledWorldHeightSampler(world, 1092026)
                .CreateNormalizedHeights(new WorldTileId(5, 4), 65);
            for (var z = 0; z < 65; z++)
            {
                Assert.That(first[z, 64], Is.EqualTo(neighbour[z, 0]));
                for (var x = 0; x < 65; x++) Assert.That(replay[z, x], Is.EqualTo(first[z, x]));
            }
            var otherSeed = new TiledWorldHeightSampler(world, 1092027)
                .CreateNormalizedHeights(new WorldTileId(5, 4), 65);
            Assert.That(otherSeed.Cast<float>().SequenceEqual(first.Cast<float>()), Is.False);
        }

        [Test]
        public void RiverLakesAndStarterSettlementShareOneGeometryContract()
        {
            foreach (var point in world.River)
            {
                Assert.That(heights.IsWater(point.X, point.Z), Is.True);
                Assert.That(heights.SampleHeight(point.X, point.Z), Is.LessThan(TiledWorldHeightSampler.WaterLevelMeters));
                Assert.That(heights.IsBuildable(point.X, point.Z), Is.False);
            }
            foreach (var lake in world.Zones.Where(value => value.Uses.HasFlag(WorldZoneUse.Fishing)))
            {
                Assert.That(heights.IsWater(lake.Center.X, lake.Center.Z), Is.True, lake.Id);
                Assert.That(heights.SampleHeight(lake.Center.X, lake.Center.Z),
                    Is.LessThan(TiledWorldHeightSampler.WaterLevelMeters), lake.Id);
            }
            Assert.That(heights.IsWater(world.PlayerStart.X, world.PlayerStart.Z), Is.False);
            Assert.That(heights.IsBuildable(world.PlayerStart.X, world.PlayerStart.Z), Is.True);
        }

        [Test]
        public void StreamingKeepsVisualsBoundedWhileRetainingActorsAndCrossMapRoutes()
        {
            var planner = new TiledWorldStreamingPlanner(world);
            var camera = Center(6, 1);
            var actor = Center(0, 11);
            var destination = Center(11, 11);
            var plan = planner.CreatePlan(camera, new[] { actor },
                new[] { Tuple.Create(Center(0, 0), destination) });
            Assert.That(plan.VisualTiles.Count, Is.EqualTo(9));
            Assert.That(plan.CollisionTiles, Does.Contain(new WorldTileId(0, 11)));
            Assert.That(plan.CollisionTiles, Does.Contain(new WorldTileId(11, 11)));
            Assert.That(plan.CollisionTiles.Count, Is.GreaterThan(plan.VisualTiles.Count));
            var corridor = planner.NavigationCorridor(Center(0, 0), destination);
            Assert.That(corridor.Count, Is.EqualTo(12));
            Assert.That(corridor.All(tile => tile.X == tile.Z), Is.True);
        }

        [Test]
        public void DiscoveryAndGlobalMapUseFullTwelveKilometreLayout()
        {
            var map = StarterGlobalMap.CreateTiled(world, worldSeed: 1092026);
            var layout = ConnectedWorldLayout.For(world);
            var connected = new ConnectedWorldService(map.Locations, layout);
            Assert.That(layout.Columns, Is.EqualTo(48));
            Assert.That(layout.Rows, Is.EqualTo(48));
            Assert.That(map.GraphId, Is.EqualTo(world.GenerationId));
            Assert.That(map.Settlements.Count, Is.EqualTo(3));
            Assert.That(map.Locations.Max(value => value.MapX) - map.Locations.Min(value => value.MapX),
                Is.GreaterThan(9000));
            connected.ReportPhysicalPresence(world.Origin.X + world.WidthMeters - 1,
                world.Origin.Z + world.DepthMeters - 1);
            Assert.That(connected.IsRevealed(world.Origin.X + world.WidthMeters - 1,
                world.Origin.Z + world.DepthMeters - 1), Is.True);
        }

        [Test]
        public void LegacyGenerationIsRejectedBeforeClockOrWorldStateCanRebind()
        {
            var map = StarterGlobalMap.CreateTiled(world, worldSeed: 1092026);
            var connected = new ConnectedWorldService(map.Locations, ConnectedWorldLayout.For(world));
            var clock = new GameClock(new GameTimeState(17, 23, 1, false));
            var slot = DeterministicStableId.Create(1092026, "legacy_slot");
            var legacyWorld = new ConnectedWorldSaveState(ConnectedWorldContract.GenerationId,
                new[] { new WorldDiscoveryCell(0, 0) }, Array.Empty<ScoutingAssignmentState>(), 999);
            var legacy = new SaveSnapshot(SaveSnapshot.CurrentVersion,
                DeterministicStableId.Create(1092026, "legacy_session"),
                new GameTimeState(999, 999, 1, false), 400, connectedWorld: legacyWorld);
            var coordinator = new SaveCoordinator(DeterministicStableId.Create(1092026, "current_session"),
                clock, new MemoryStore(legacy), connectedWorldService: connected);
            var revealedBefore = connected.RevealedCells.ToArray();
            Assert.Throws<NotSupportedException>(() => coordinator.Load(slot));
            Assert.That(coordinator.Clock, Is.SameAs(clock));
            Assert.That(coordinator.Clock.State.CalendarTicks, Is.EqualTo(17));
            Assert.That(connected.RevealedCells, Is.EquivalentTo(revealedBefore));
        }

        private WorldPosition Center(int x, int z)
        {
            var origin = world.TileOrigin(new WorldTileId(x, z));
            return new WorldPosition(origin.X + 500f, 0f, origin.Z + 500f);
        }

        private sealed class MemoryStore : ISaveStore
        {
            private readonly SaveSnapshot snapshot;
            public MemoryStore(SaveSnapshot value) { snapshot = value; }
            public void Save(StableEntityId slotId, SaveSnapshot value) => throw new NotSupportedException();
            public SaveSnapshot Load(StableEntityId slotId) => snapshot;
        }
    }
}
