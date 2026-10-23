using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Events;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Time;
using Game.Domain.Work;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Simulation;
using Game.Simulation.Time;
using Game.Simulation.World;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [TestFixture]
    [Category("WM13")]
    public sealed class WM13WorldMapSaveVersionTests
    {
        private const long Seed = 20261023L;
        private string temporaryRoot;

        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(Path.GetTempPath(), "serenity-wm13-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
        }

        [Test]
        public void V30CodecRoundTripPreservesCanonicalWorldMapState()
        {
            var world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var heights = new TiledWorldHeightSampler(world, Seed);
            var population = new TiledWorldPopulationPlanner(world, heights, Seed).Create();
            var land = population.Resources.First(value => !value.Hidden && value.RecipeId != "fish_water");
            var fish = population.Resources.First(value => value.RecipeId == "fish_water");
            var hiddenIds = population.Resources.Where(value => value.Hidden)
                .Select(value => value.CreateStableId(Seed, world.GenerationId)).ToArray();
            var landId = land.CreateStableId(Seed, world.GenerationId);
            var fishId = fish.CreateStableId(Seed, world.GenerationId);
            var production = new ProductionSaveState(TimeSpan.TicksPerDay * 3L, 0, 0, 0,
                Array.Empty<ResourceStockTarget>(), new[]
                {
                    Site(land, landId, 0),
                    Site(fish, fishId, fish.Capacity - 7)
                }, Array.Empty<ProductionTaskState>(), Array.Empty<Game.Domain.Resources.ResourceAmount>(),
                Array.Empty<Game.Domain.Resources.ResourceInventoryState>(),
                new[] { new NaturalNodeLifecycleState(landId, land.Biome, TimeSpan.TicksPerDay, 2) });

            var wildlifePlan = new WorldMapWildlifePlanner(world, heights, Seed).Create();
            var wildlife = new WildlifePopulationService(wildlifePlan, Seed);
            var pressureHabitat = wildlifePlan.Capacities.First();
            wildlife.ApplyHuntingPressure(pressureHabitat.SpeciesId, pressureHabitat.RegionId, 800);
            wildlife.Remove(wildlife.Animals.First().Id);
            wildlife.AdvanceTo(TimeSpan.TicksPerDay);

            var poiId = wildlifePlan.Individuals.First().Id;
            var eventId = new EventInstanceId(Id(13));
            var history = new EventHistoryEntry(eventId, eventId, new EventDefinitionId("wm11_poi_discovery"),
                EventCategory.Exploration, EventAudience.Player, new EventOwnerId("player"),
                new[] { EventTargetRef.PointOfInterest(poiId) }, 10, 12, null, "discovered", EventStatus.Completed);
            var snapshot = new SaveSnapshot(SaveSnapshot.WorldMapV2Version, Id(1),
                new GameTimeState(TimeSpan.TicksPerDay * 3L, 0, 1, false), 400,
                production: production,
                buildingWorld: new BuildingWorldSaveState(Array.Empty<BuildingState>(),
                    Array.Empty<WorldResourcePileState>(), 0),
                events: new EventSaveState(Seed, 12, 1, Array.Empty<EventInstanceState>(),
                    Array.Empty<EventCooldownState>(), new[] { history }, 0, 0),
                work: new WorkSaveState(1, Array.Empty<WorkGroupState>(), Array.Empty<WorkOrderState>()),
                connectedWorld: new ConnectedWorldSaveState(world.GenerationId,
                    new[] { new WorldDiscoveryCell(2, 3) }, Array.Empty<ScoutingAssignmentState>(), 12),
                saveGenerationId: SaveSnapshot.WorldMapV2SaveGenerationId,
                wildlife: wildlife.CaptureState());

            var codec = new SaveXmlCodec();
            var restored = codec.Deserialize(codec.Serialize(snapshot));

            Assert.That(restored.FormatVersion, Is.EqualTo(SaveSnapshot.WorldMapV2Version));
            Assert.That(restored.SaveGenerationId, Is.EqualTo(TiledWorldDefinition.ProposedWorldMapGenerationId));
            Assert.That(restored.Production.Sites.Single(value => value.Id == landId).RemainingAmount, Is.Zero);
            Assert.That(restored.Production.Sites.Single(value => value.Id == fishId).RemainingAmount,
                Is.EqualTo(fish.Capacity - 7));
            Assert.That(hiddenIds, Is.Not.Empty);
            Assert.That(restored.Production.Sites.Any(value => hiddenIds.Contains(value.Id)), Is.False);
            Assert.That(restored.ConnectedWorld.GenerationId, Is.EqualTo(world.GenerationId));
            Assert.That(restored.ConnectedWorld.RevealedCells, Has.Count.EqualTo(1));
            Assert.That(restored.Events.History.Single().DefinitionId.ToString(), Is.EqualTo("wm11_poi_discovery"));
            Assert.That(restored.BuildingWorld, Is.Not.Null);
            Assert.That(restored.Work, Is.Not.Null);
            Assert.That(restored.Wildlife.Animals.Select(value => value.Id),
                Is.EqualTo(wildlife.Animals.Select(value => value.Id)));
            Assert.That(restored.Wildlife.HuntingPressures.Single().Permille, Is.EqualTo(600));
        }

        [Test]
        public void CoordinatorRejectsWrongSaveAndWorldGenerationBeforeRestore()
        {
            var world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var heights = new TiledWorldHeightSampler(world, Seed);
            var wildlife = new WildlifePopulationService(new WorldMapWildlifePlanner(world, heights, Seed).Create(), Seed);
            var pressureHabitat = wildlife.Plan.Capacities.First();
            wildlife.ApplyHuntingPressure(pressureHabitat.SpeciesId, pressureHabitat.RegionId, 700);
            var connected = Connected(world);
            var store = new MemoryStore();
            var coordinator = new SaveCoordinator(Id(2), new GameClock(new GameTimeState(),
                    new GameTimeSettings(400)), store, connectedWorldService: connected,
                requiredSaveGenerationId: SaveSnapshot.WorldMapV2SaveGenerationId,
                wildlifePopulationService: wildlife);
            var oldClock = coordinator.Clock;
            var before = wildlife.CaptureState();

            store.Snapshot = Snapshot(world.GenerationId, SaveSnapshot.StoneAgeBuildingSaveGenerationId,
                wildlife.CaptureState());
            var saveGenerationError = Assert.Throws<NotSupportedException>(() => coordinator.Load(Id(3)));
            Assert.That(saveGenerationError.Message, Does.Contain(SaveSnapshot.StoneAgeBuildingSaveGenerationId));
            AssertUnchanged(before, wildlife.CaptureState(), oldClock, coordinator.Clock);

            store.Snapshot = Snapshot(TiledWorldDefinition.ReferenceGenerationId,
                SaveSnapshot.WorldMapV2SaveGenerationId, wildlife.CaptureState());
            var worldGenerationError = Assert.Throws<NotSupportedException>(() => coordinator.Load(Id(3)));
            Assert.That(worldGenerationError.Message, Does.Contain(TiledWorldDefinition.ReferenceGenerationId));
            AssertUnchanged(before, wildlife.CaptureState(), oldClock, coordinator.Clock);
        }

        [Test]
        public void StartupCatalogReportsWrongGenerationAndV2DirectoryCannotOverwriteCurrentFiles()
        {
            var current = StartupSaveProfile.Current;
            var proposed = StartupSaveProfile.WorldMapV2Proposed;
            Assert.That(current.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentVersion));
            Assert.That(proposed.FormatVersion, Is.EqualTo(SaveSnapshot.WorldMapV2Version));
            Assert.That(proposed.SaveGenerationId, Is.EqualTo(TiledWorldDefinition.ProposedWorldMapGenerationId));
            var currentDirectory = current.ResolveDirectory(temporaryRoot);
            var proposedDirectory = proposed.ResolveDirectory(temporaryRoot);
            Assert.That(proposedDirectory, Is.Not.EqualTo(currentDirectory));

            Directory.CreateDirectory(currentDirectory);
            var protectedPath = Path.Combine(currentDirectory, LocalGameplaySaveFacade.SlotId(1) + ".xml");
            File.WriteAllText(protectedPath, "protected-old-save");
            var beforeHash = Sha256(protectedPath);
            new FileSaveStore(proposedDirectory).Save(LocalGameplaySaveFacade.SlotId(1),
                Snapshot(TiledWorldDefinition.ProposedWorldMapGenerationId,
                    SaveSnapshot.WorldMapV2SaveGenerationId,
                    new WildlifeWorldState(0, 0, Array.Empty<WildlifeIndividualState>())));

            Assert.That(Sha256(protectedPath), Is.EqualTo(beforeHash));
            Assert.That(Directory.GetFiles(currentDirectory), Has.Length.EqualTo(1));
            Assert.That(Directory.GetFiles(proposedDirectory), Has.Length.EqualTo(1));

            var catalog = new StartupSaveCatalog(new FakeCatalog(new[]
            {
                new SaveSlotMetadata(LocalGameplaySaveFacade.SlotId(1), Id(8), 5, null, DateTime.UtcNow,
                    false, string.Empty, Seed, SaveSnapshot.CurrentVersion,
                    SaveSnapshot.StoneAgeBuildingSaveGenerationId)
            }), expectedGenerationId: proposed.SaveGenerationId);
            var slot = catalog.ListSlots().Single(value => value.Index == 1);
            Assert.That(slot.Detail, Does.Contain("другое поколение мира"));
            Assert.That(slot.WorldSeed, Is.Null);
            Assert.That(catalog.LatestUsableSlot(), Is.Null);
        }

        private static ProductionSiteState Site(TiledWorldResourceNode node, StableEntityId id, long remaining) =>
            new ProductionSiteState(id, new ProductionRecipeId(node.RecipeId), ProductionSiteKind.NaturalSource,
                node.Position, remaining, node.Capacity, CropPlotPhase.Unplanted, 0, 0);

        private static ConnectedWorldService Connected(TiledWorldDefinition world)
        {
            var start = world.PlayerStart;
            return new ConnectedWorldService(new[]
            {
                new WorldLocationState(ConnectedWorldContract.StarterSettlement, "Starter",
                    WorldLocationType.Settlement, (int)start.X, (int)start.Z)
            }, ConnectedWorldLayout.For(world), 0);
        }

        private static SaveSnapshot Snapshot(string worldGeneration, string saveGeneration,
            WildlifeWorldState wildlife) => new SaveSnapshot(SaveSnapshot.WorldMapV2Version, Id(4),
            new GameTimeState(), 400,
            connectedWorld: new ConnectedWorldSaveState(worldGeneration, Array.Empty<WorldDiscoveryCell>(),
                Array.Empty<ScoutingAssignmentState>(), 0),
            saveGenerationId: saveGeneration, wildlife: wildlife);

        private static void AssertUnchanged(WildlifeWorldState expected, WildlifeWorldState actual,
            GameClock expectedClock, GameClock actualClock)
        {
            Assert.That(actual.Animals.Select(value => value.Id), Is.EqualTo(expected.Animals.Select(value => value.Id)));
            Assert.That(actual.HuntingPressures.Select(value => value.Permille),
                Is.EqualTo(expected.HuntingPressures.Select(value => value.Permille)));
            Assert.That(actualClock, Is.SameAs(expectedClock));
        }

        private static string Sha256(string path)
        {
            using (var hash = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));

        private sealed class MemoryStore : ISaveStore
        {
            public SaveSnapshot Snapshot { get; set; }
            public void Save(StableEntityId slotId, SaveSnapshot snapshot) => Snapshot = snapshot;
            public SaveSnapshot Load(StableEntityId slotId) => Snapshot;
        }

        private sealed class FakeCatalog : ISaveSlotCatalog
        {
            private readonly IReadOnlyList<SaveSlotMetadata> slots;
            public FakeCatalog(IReadOnlyList<SaveSlotMetadata> values) => slots = values;
            public IReadOnlyList<SaveSlotMetadata> ListSlots() => slots;
        }
    }
}
