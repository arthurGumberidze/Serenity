using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Campaign;
using Game.Domain.Events;
using Game.Domain.Time;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Simulation.Events;
using Game.Simulation.World;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("WM11")]
    public sealed class WM11WorldMapPoiExplorationPlayModeTests
    {
        private const long Seed = 20261023;

        [UnityTest]
        public IEnumerator DiscoveryAndOneTimeEventSurviveXmlSaveLoadWithoutReplay()
        {
            var world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var heights = new TiledWorldHeightSampler(world, Seed);
            var roads = new WorldMapRoadNetworkPlanner(world, heights).Create();
            var plan = new WorldMapPoiPlanner(world, heights, roads, Seed).Create();
            var locations = Locations(world);
            var layout = ConnectedWorldLayout.For(world);
            var discovery = new ConnectedWorldService(locations, layout, 0);
            var targetSource = new WorldMapPoiEventTargetSource(plan.Points);
            var catalog = new EventCatalog(WorldMapPoiEventCatalog.CreateDefinitions(plan.Points));
            var events = new EventService(catalog, targetSource, new NarrativeEffects(), Seed);
            var adapter = new WorldMapPoiDiscoveryAdapter(plan, discovery, events);
            var cave = plan.Get("poi_cave");

            Assert.That(adapter.IsDiscovered(cave.Id), Is.False);
            Assert.That(adapter.ReportPhysicalPresence(cave.Position.X, cave.Position.Z, 0), Is.EqualTo(1));
            Assert.That(adapter.IsDiscovered(cave.Id), Is.True);
            var pending = events.PendingPlayerEvents.Single();
            Assert.That(events.ResolvePlayerChoice(pending.InstanceId, new EventChoiceId("acknowledge"), 0).Status,
                Is.EqualTo(EventChoiceCommandStatus.Applied));
            Assert.That(events.History.Count, Is.EqualTo(1));

            var snapshot = new SaveSnapshot(SaveSnapshot.ConnectedWorldVersion,
                DeterministicStableId.Create(Seed, "wm11_save_smoke"), new GameTimeState(0, 0, 1, false), 1,
                events: events.CaptureState(), connectedWorld: discovery.CaptureState());
            var restoredSnapshot = new SaveXmlCodec().Deserialize(new SaveXmlCodec().Serialize(snapshot));
            var restoredDiscovery = new ConnectedWorldService(locations, layout, 0);
            restoredDiscovery.RestoreState(restoredSnapshot.ConnectedWorld);
            var restoredEvents = new EventService(catalog, targetSource, new NarrativeEffects(), Seed);
            restoredEvents.RestoreState(restoredSnapshot.Events);
            var restoredAdapter = new WorldMapPoiDiscoveryAdapter(plan, restoredDiscovery, restoredEvents);

            Assert.That(restoredAdapter.IsDiscovered(cave.Id), Is.True);
            Assert.That(restoredAdapter.Advance(0), Is.Zero);
            Assert.That(restoredEvents.PendingPlayerEvents, Is.Empty);
            Assert.That(restoredEvents.History.Count, Is.EqualTo(1));
            Assert.That(restoredEvents.History[0].DefinitionId,
                Is.EqualTo(WorldMapPoiEventCatalog.DefinitionId(cave)));
            yield return null;
        }

        private static IReadOnlyList<WorldLocationState> Locations(TiledWorldDefinition world) => new[]
        {
            new WorldLocationState(ConnectedWorldContract.StarterSettlement, "Стартовая территория",
                WorldLocationType.Settlement, (int)world.PlayerStart.X, (int)world.PlayerStart.Z)
        };

        private sealed class NarrativeEffects : IEventEffectExecutor
        {
            public bool Supports(EventEffectKind kind) => kind == EventEffectKind.None;
            public bool CanApply(EventEffectDefinition effect, IReadOnlyList<EventTargetRef> targets) =>
                effect != null && effect.Kind == EventEffectKind.None && targets != null && targets.Count > 0;
            public bool TryApply(EventEffectDefinition effect, IReadOnlyList<EventTargetRef> targets,
                long calendarTick, out string outcomeCode)
            {
                outcomeCode = "poi_discovered";
                return CanApply(effect, targets);
            }
        }
    }
}
