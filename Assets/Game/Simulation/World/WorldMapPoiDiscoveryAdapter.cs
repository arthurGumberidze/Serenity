using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.World;
using Game.Simulation.Events;

namespace Game.Simulation.World
{
    /// <summary>Event target projection for immutable POI plan entries; it owns no discovery state.</summary>
    public sealed class WorldMapPoiEventTargetSource : IEventTargetSource
    {
        private readonly Dictionary<StableEntityId, WorldMapPoiPlanEntry> points;

        public WorldMapPoiEventTargetSource(IEnumerable<WorldMapPoiPlanEntry> values)
        {
            points = (values ?? throw new ArgumentNullException(nameof(values))).ToDictionary(value => value.StableId);
            if (points.Count != 9) throw new ArgumentException("Nine World Map POIs are required.", nameof(values));
        }

        public IEnumerable<EventTargetSet> GetTargets(EventDefinition definition) => Array.Empty<EventTargetSet>();

        public bool Evaluate(EventConditionDefinition condition, IReadOnlyList<EventTargetRef> targets)
        {
            if (condition == null || targets == null || condition.TargetIndex < 0 ||
                condition.TargetIndex >= targets.Count) return false;
            if (condition.Kind != EventConditionKind.Always && condition.Kind != EventConditionKind.TargetExists)
                return false;
            return IsValid(targets[condition.TargetIndex]);
        }

        public bool IsValid(EventTargetRef target) => target.Kind == EventTargetKind.PointOfInterest &&
            target.TryGetStableEntityId(out var id) && points.ContainsKey(id);

        public string GetDisplayName(EventTargetRef target) => IsValid(target) &&
            target.TryGetStableEntityId(out var id) ? points[id].DisplayName : target.Kind.ToString();
    }

    /// <summary>Routes event target queries to the existing canonical adapters.</summary>
    public sealed class CompositeEventTargetSource : IEventTargetSource
    {
        private readonly IEventTargetSource[] sources;

        public CompositeEventTargetSource(params IEventTargetSource[] values)
        {
            sources = (values ?? throw new ArgumentNullException(nameof(values))).Where(value => value != null).ToArray();
            if (sources.Length == 0) throw new ArgumentException("At least one event target source is required.", nameof(values));
        }

        public IEnumerable<EventTargetSet> GetTargets(EventDefinition definition) =>
            sources.SelectMany(source => source.GetTargets(definition) ?? Array.Empty<EventTargetSet>());

        public bool Evaluate(EventConditionDefinition condition, IReadOnlyList<EventTargetRef> targets)
        {
            var source = sources.FirstOrDefault(candidate => targets != null && targets.All(candidate.IsValid));
            return source != null && source.Evaluate(condition, targets);
        }

        public bool IsValid(EventTargetRef target) => sources.Any(source => source.IsValid(target));

        public string GetDisplayName(EventTargetRef target)
        {
            var source = sources.FirstOrDefault(candidate => candidate.IsValid(target));
            return source != null ? source.GetDisplayName(target) : target.Kind.ToString();
        }
    }

    /// <summary>
    /// Bridges saved ConnectedWorld revealed cells to saved EventService instances/history. It deliberately keeps
    /// no discovered/processed collection of its own, so Save/Load cannot replay an already offered POI event.
    /// </summary>
    public sealed class WorldMapPoiDiscoveryAdapter
    {
        private readonly WorldMapPoiPlan plan;
        private readonly ConnectedWorldService discovery;
        private readonly EventService events;

        public WorldMapPoiDiscoveryAdapter(WorldMapPoiPlan poiPlan, ConnectedWorldService discoveryService,
            EventService eventService)
        {
            plan = poiPlan ?? throw new ArgumentNullException(nameof(poiPlan));
            discovery = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
            events = eventService ?? throw new ArgumentNullException(nameof(eventService));
            if (!string.Equals(discovery.Layout.GenerationId, TiledWorldDefinition.ProposedWorldMapGenerationId,
                    StringComparison.Ordinal))
                throw new ArgumentException("WM11 discovery requires the proposed V2 layout.", nameof(discoveryService));
        }

        public bool IsDiscovered(string poiId)
        {
            var point = plan.Get(poiId);
            return discovery.IsRevealed(point.Position.X, point.Position.Z);
        }

        public int ReportPhysicalPresence(float worldX, float worldZ, long calendarTick)
        {
            discovery.ReportPhysicalPresence(worldX, worldZ);
            return Advance(calendarTick);
        }

        public int Advance(long calendarTick)
        {
            if (calendarTick < events.LastProcessedTick || calendarTick < discovery.LastProcessedTick)
                throw new ArgumentOutOfRangeException(nameof(calendarTick));
            var offered = 0;
            foreach (var point in plan.Points)
            {
                if (!discovery.IsRevealed(point.Position.X, point.Position.Z) || WasOffered(point)) continue;
                events.Enqueue(WorldMapPoiEventCatalog.DefinitionId(point),
                    new EventTargetSet(new[] { EventTargetRef.PointOfInterest(point.StableId) }), calendarTick);
                offered++;
            }
            return offered;
        }

        private bool WasOffered(WorldMapPoiPlanEntry point)
        {
            var definitionId = WorldMapPoiEventCatalog.DefinitionId(point);
            return events.ActiveInstances.Any(value => value.DefinitionId == definitionId && Targets(value.Targets, point)) ||
                   events.History.Any(value => value.DefinitionId == definitionId && Targets(value.Targets, point));
        }

        private static bool Targets(IReadOnlyList<EventTargetRef> targets, WorldMapPoiPlanEntry point) =>
            targets.Any(value => value.Kind == EventTargetKind.PointOfInterest &&
                                 value.TryGetStableEntityId(out var id) && id == point.StableId);
    }

    public static class WorldMapPoiEventCatalog
    {
        public static EventDefinitionId DefinitionId(WorldMapPoiPlanEntry point) =>
            new EventDefinitionId("poi_discovery_" + (point ?? throw new ArgumentNullException(nameof(point))).Id
                .Replace("poi_", string.Empty));

        public static IReadOnlyList<EventDefinition> CreateDefinitions(IEnumerable<WorldMapPoiPlanEntry> points)
        {
            return (points ?? throw new ArgumentNullException(nameof(points))).OrderBy(value => value.Id, StringComparer.Ordinal)
                .Select(Create).ToArray();
        }

        private static EventDefinition Create(WorldMapPoiPlanEntry point)
        {
            var purpose = point.Purposes.Count == 0 ? "exploration" : string.Join(", ", point.Purposes);
            return new EventDefinition(DefinitionId(point), EventCategory.Exploration, EventAudience.Player,
                new EventOwnerId("player"), EventTargetSelector.Explicit,
                new EventTriggerDefinition(EventTriggerKind.Explicit), 100, TimeSpan.FromDays(36500).Ticks,
                new EventNodeId("discovered"), new[]
                {
                    new EventNodeDefinition(new EventNodeId("discovered"), point.DisplayName,
                        "Открыта новая точка интереса. Назначение: " + purpose + ".", new[]
                        {
                            new EventChoiceDefinition(new EventChoiceId("acknowledge"), "Отметить открытие", 100,
                                new[] { new EventEffectDefinition(EventEffectKind.None, "poi_discovered") })
                        })
                }, new[] { new EventConditionDefinition(EventConditionKind.TargetExists) },
                InvalidEventTargetPolicy.Expire);
        }
    }
}
