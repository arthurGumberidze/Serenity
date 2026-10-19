using System;
using System.Linq;
using Game.Domain;
using Game.Domain.Campaign;
using Game.Domain.Time;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Presentation.Environment;
using Game.Simulation.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("P102")]
    public sealed class P102ConnectedWorldTests
    {
        [Test]
        public void CanonicalGraphUsesPhysicalCoordinatesAndPreservesSixIds()
        {
            var state = StarterGlobalMap.Create();
            Assert.That(state.GraphId, Is.EqualTo(ConnectedWorldContract.GenerationId));
            Assert.That(state.Locations.Count, Is.GreaterThan(6));
            Assert.That(ConnectedWorldContract.PreservedLocationIds.All(id =>
                state.Locations.Any(x => x.Id.Value == id)), Is.True);
            Assert.That(state.Locations.Max(x => x.MapX) - state.Locations.Min(x => x.MapX), Is.GreaterThan(300));
            Assert.That(state.Locations.Max(x => x.MapY) - state.Locations.Min(x => x.MapY), Is.GreaterThan(350));
        }

        [Test]
        public void PresenceAndAssignedScoutingRevealPersistentRealCells()
        {
            var state = StarterGlobalMap.Create();
            var service = new ConnectedWorldService(state.Locations);
            Assert.That(service.IsLocationRevealed(ConnectedWorldContract.StarterSettlement), Is.True);
            var enemy = new WorldLocationId("enemy_settlement");
            Assert.That(service.IsLocationRevealed(enemy), Is.False);
            var scout = DeterministicStableId.Create(102, "scout");
            service.AssignScout(scout, enemy, 0, 100);
            service.AdvanceTo(99);
            Assert.That(service.IsLocationRevealed(enemy), Is.False);
            service.AdvanceTo(100);
            Assert.That(service.IsLocationRevealed(enemy), Is.True);
            var restored = new ConnectedWorldService(state.Locations);
            restored.RestoreState(service.CaptureState());
            Assert.That(restored.IsLocationRevealed(enemy), Is.True);
            restored.ReportPhysicalPresence(-132, 62);
            Assert.That(restored.IsLocationRevealed(new WorldLocationId("west_marsh")), Is.True);
        }

        [Test]
        public void RevealAllIsIdempotentAndSurvivesSaveRestore()
        {
            var graph = StarterGlobalMap.Create();
            var service = new ConnectedWorldService(graph.Locations);
            var total = service.Layout.Columns * service.Layout.Rows;
            var previouslyRevealed = service.RevealedCellCount;
            Assert.That(service.RevealAll(), Is.EqualTo(total - previouslyRevealed));
            Assert.That(service.RevealedCellCount, Is.EqualTo(total));
            Assert.That(service.RevealAll(), Is.Zero);
            var restored = new ConnectedWorldService(graph.Locations);
            restored.RestoreState(service.CaptureState());
            Assert.That(restored.RevealedCellCount, Is.EqualTo(total));
        }

        [Test]
        public void Version20XmlRoundTripPreservesFogAndGeneration()
        {
            var graph = StarterGlobalMap.Create();
            var service = new ConnectedWorldService(graph.Locations);
            service.ReportPhysicalPresence(142, 158);
            var snapshot = new SaveSnapshot(SaveSnapshot.ConnectedWorldVersion, DeterministicStableId.Create(102, "session"),
                new GameTimeState(0, 0, 1, false), 1, connectedWorld: service.CaptureState());
            var roundTrip = new SaveXmlCodec().Deserialize(new SaveXmlCodec().Serialize(snapshot));
            Assert.That(roundTrip.FormatVersion, Is.EqualTo(SaveSnapshot.ConnectedWorldVersion));
            Assert.That(roundTrip.ConnectedWorld.GenerationId, Is.EqualTo(ConnectedWorldContract.GenerationId));
            Assert.That(roundTrip.ConnectedWorld.RevealedCells, Is.EquivalentTo(snapshot.ConnectedWorld.RevealedCells));
        }

        [Test]
        public void AuthoredScenePassesAreaIdentityResourceAndGeographyAudit()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/LocalGameplay.unity");
            var world = UnityEngine.Object.FindAnyObjectByType<P102ConnectedWorldPresentation>();
            Assert.That(world, Is.Not.Null);
            Assert.DoesNotThrow(world.ValidateConfiguration);
            Assert.That(world.Terrain.terrainData.size.x, Is.GreaterThanOrEqualTo(512));
            Assert.That(world.AccessibleAreaSquareMeters, Is.EqualTo(233344));
            Assert.That(world.CanonicalResourceAnchorCount, Is.EqualTo(24));
            Assert.That(world.Districts.Count, Is.EqualTo(10));
        }
    }
}
