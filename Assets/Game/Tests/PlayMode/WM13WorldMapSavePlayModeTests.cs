using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Domain.Time;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Simulation;
using Game.Simulation.Time;
using Game.Simulation.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("WM13")]
    public sealed class WM13WorldMapSavePlayModeTests
    {
        [UnityTest]
        public IEnumerator ProposedGenerationFileSaveRestoresWildlifeAndDiscovery()
        {
            const long seed = 20261023L;
            var directory = Path.Combine(Application.temporaryCachePath, "Serenity", "WM13",
                Guid.NewGuid().ToString("N"));
            try
            {
                var world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
                var heights = new TiledWorldHeightSampler(world, seed);
                var wildlife = new WildlifePopulationService(
                    new WorldMapWildlifePlanner(world, heights, seed).Create(), seed);
                var start = world.PlayerStart;
                var connected = new ConnectedWorldService(new[]
                {
                    new WorldLocationState(ConnectedWorldContract.StarterSettlement, "Starter",
                        WorldLocationType.Settlement, (int)start.X, (int)start.Z)
                }, ConnectedWorldLayout.For(world), 0);
                var store = new FileSaveStore(directory);
                var coordinator = new SaveCoordinator(Id(1), new GameClock(new GameTimeState(),
                        new GameTimeSettings(400)), store, connectedWorldService: connected,
                    requiredSaveGenerationId: SaveSnapshot.WorldMapV2SaveGenerationId,
                    wildlifePopulationService: wildlife);
                var slot = Id(2);
                var expectedIds = wildlife.Animals.Select(value => value.Id).ToArray();
                var expectedCells = connected.RevealedCells.OrderBy(value => value).ToArray();
                coordinator.Save(slot);

                wildlife.Remove(expectedIds[0]);
                wildlife.AdvanceTo(TimeSpan.TicksPerDay);
                connected.RevealAll();
                coordinator.Load(slot);

                Assert.That(coordinator.Capture().FormatVersion, Is.EqualTo(SaveSnapshot.WorldMapV2Version));
                Assert.That(coordinator.Capture().SaveGenerationId,
                    Is.EqualTo(TiledWorldDefinition.ProposedWorldMapGenerationId));
                Assert.That(wildlife.Animals.Select(value => value.Id), Is.EqualTo(expectedIds));
                Assert.That(wildlife.LastDay, Is.Zero);
                Assert.That(connected.RevealedCells.OrderBy(value => value), Is.EqualTo(expectedCells));
                yield return null;
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));
    }
}
