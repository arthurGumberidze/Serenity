using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Resources;

namespace Game.Simulation.AI
{
    public readonly struct RulerStrategyResourceFact
    {
        public RulerStrategyResourceFact(string resourceId, long amount)
        {
            if (string.IsNullOrWhiteSpace(resourceId) || amount < 0) throw new ArgumentOutOfRangeException();
            ResourceId = resourceId;
            Amount = amount;
        }
        public string ResourceId { get; }
        public long Amount { get; }
    }

    public readonly struct RulerStrategyRelationFact
    {
        public RulerStrategyRelationFact(string sideId, int score)
        {
            if (string.IsNullOrWhiteSpace(sideId) || score < -100 || score > 100)
                throw new ArgumentOutOfRangeException();
            SideId = sideId;
            Score = score;
        }
        public string SideId { get; }
        public int Score { get; }
    }

    public sealed class RulerStrategyRequestContext
    {
        public RulerStrategyRequestContext(int population, int settlementCount, int militaryUnitCount,
            AiStrategicGoal currentGoal, IEnumerable<RulerStrategyResourceFact> resources,
            IEnumerable<string> completedTechnologies, IEnumerable<string> availableTechnologies,
            IEnumerable<string> availableBuildingPriorities, IEnumerable<RulerStrategyRelationFact> relations)
        {
            if (population < 0 || settlementCount < 0 || militaryUnitCount < 0)
                throw new ArgumentOutOfRangeException();
            Population = population;
            SettlementCount = settlementCount;
            MilitaryUnitCount = militaryUnitCount;
            CurrentGoal = currentGoal;
            Resources = Array.AsReadOnly((resources ?? Array.Empty<RulerStrategyResourceFact>())
                .OrderBy(value => value.ResourceId, StringComparer.Ordinal).ToArray());
            CompletedTechnologies = Clean(completedTechnologies);
            AvailableTechnologies = Clean(availableTechnologies);
            AvailableBuildingPriorities = Clean(availableBuildingPriorities);
            Relations = Array.AsReadOnly((relations ?? Array.Empty<RulerStrategyRelationFact>())
                .OrderBy(value => value.SideId, StringComparer.Ordinal).ToArray());
        }

        public int Population { get; }
        public int SettlementCount { get; }
        public int MilitaryUnitCount { get; }
        public AiStrategicGoal CurrentGoal { get; }
        public IReadOnlyList<RulerStrategyResourceFact> Resources { get; }
        public IReadOnlyList<string> CompletedTechnologies { get; }
        public IReadOnlyList<string> AvailableTechnologies { get; }
        public IReadOnlyList<string> AvailableBuildingPriorities { get; }
        public IReadOnlyList<RulerStrategyRelationFact> Relations { get; }

        private static IReadOnlyList<string> Clean(IEnumerable<string> values)
        {
            var result = (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            return new ReadOnlyCollection<string>(result);
        }
    }

    public sealed class RulerStrategyRequest
    {
        public RulerStrategyRequest(AiStateId factionId, RulerStrategyPendingRequest pending,
            RulerStrategyRequestContext context, long planHorizonTicks = 0)
        {
            if (planHorizonTicks == 0) planHorizonTicks = RulerStrategyState.AnnualIntervalTicks;
            if (!factionId.IsValid || planHorizonTicks < 0) throw new ArgumentException(nameof(factionId));
            FactionId = factionId;
            Pending = pending;
            Context = context ?? throw new ArgumentNullException(nameof(context));
            PlanHorizonTicks = planHorizonTicks;
        }
        public AiStateId FactionId { get; }
        public RulerStrategyPendingRequest Pending { get; }
        public RulerStrategyRequestContext Context { get; }
        public long PlanHorizonTicks { get; }
    }

    /// <summary>
    /// Central, tick-driven request journal. It never calls a network provider and never issues gameplay commands.
    /// </summary>
    public sealed class RulerStrategyCoordinator
    {
        private static readonly long CrisisCooldownTicks = TimeSpan.FromDays(30).Ticks;
        private static readonly long WarCooldownTicks = TimeSpan.FromDays(7).Ticks;
        private readonly AiStrategicScheduler scheduler;
        public bool PeriodicRequestsOnly { get; set; }

        public RulerStrategyCoordinator(AiStrategicScheduler strategicScheduler)
        {
            scheduler = strategicScheduler ?? throw new ArgumentNullException(nameof(strategicScheduler));
        }

        public void AdvanceTo(long calendarTick)
        {
            if (calendarTick < 0) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            foreach (var faction in scheduler.Factions.OrderBy(value => value.Strategic.Id))
            {
                if (faction.Strategic.IsDefeated || faction.RulerStrategy.PendingRequest.HasValue) continue;
                if (calendarTick >= faction.RulerStrategy.NextAnnualTick)
                {
                    faction.RulerStrategy.TryBegin(calendarTick, RulerStrategyTrigger.Annual,
                        "Составить стратегию поселения до следующего планового запроса.", out _);
                    continue;
                }
                if (PeriodicRequestsOnly) continue;
                if (faction.Production.GetCanonicalTotal(new ResourceId("berries")) == 0 &&
                    TriggerAllowed(faction, RulerStrategyTrigger.Crisis, calendarTick, CrisisCooldownTicks))
                {
                    faction.RulerStrategy.TryBegin(calendarTick, RulerStrategyTrigger.Crisis,
                        "Запасы пищи исчерпаны; нужен безопасный антикризисный план.", out _);
                    continue;
                }
                if ((faction.Strategic.Goal == AiStrategicGoal.AttackSettlement ||
                     faction.Strategic.PendingOrders.Any(value => value.Kind == AiDecisionKind.PrepareAttack)) &&
                    TriggerAllowed(faction, RulerStrategyTrigger.War, calendarTick, WarCooldownTicks))
                    faction.RulerStrategy.TryBegin(calendarTick, RulerStrategyTrigger.War,
                        "Поселение готовится к войне или уже ведёт её.", out _);
            }
        }

        public bool Request(AiStateId factionId, RulerStrategyTrigger trigger, long calendarTick,
            string subject = null)
        {
            var faction = Find(factionId);
            return faction != null && !faction.Strategic.IsDefeated &&
                faction.RulerStrategy.TryBegin(calendarTick, trigger, subject ?? string.Empty, out _);
        }

        public IReadOnlyList<RulerStrategyRequest> PendingRequests() => scheduler.Factions
            .Where(faction => faction.RulerStrategy.PendingRequest.HasValue)
            .OrderBy(faction => faction.Strategic.Id)
            .Select(faction => BuildRequest(faction, faction.RulerStrategy.PendingRequest.Value)).ToArray();

        public bool TryAccept(AiStateId factionId, RulerStrategyPlan plan, long completedTick,
            out string rejection)
        {
            var faction = Find(factionId);
            if (faction == null || plan == null || !faction.RulerStrategy.PendingRequest.HasValue ||
                faction.RulerStrategy.PendingRequest.Value.Sequence != plan.RequestSequence)
            {
                rejection = "Strategy response does not match a pending faction request.";
                return false;
            }
            var allowed = new HashSet<string>(faction.Buildings.Values
                .Where(value => faction.Construction.IsBuildingUnlocked(value.Id))
                .Select(value => value.Id.FamilyId.ToString()), StringComparer.Ordinal);
            if (plan.BuildingPriorities.Any(value => !allowed.Contains(value)))
            {
                rejection = "Strategy contains an unavailable building priority.";
                return false;
            }
            var livingPopulation = faction.Residents.Count;
            if (plan.MinimumArmySize > Math.Max(1, livingPopulation))
            {
                rejection = "Strategy army size exceeds the settlement population.";
                return false;
            }
            faction.RulerStrategy.Complete(completedTick, plan.Provider, RulerStrategyRequestStatus.Accepted,
                plan.Summary, plan);
            rejection = string.Empty;
            return true;
        }

        public void CompleteFailure(AiStateId factionId, long completedTick, RulerStrategyProviderKind provider,
            RulerStrategyRequestStatus status, string note)
        {
            if (status == RulerStrategyRequestStatus.Accepted) throw new ArgumentOutOfRangeException(nameof(status));
            var faction = Find(factionId) ?? throw new ArgumentException("Unknown AI faction.", nameof(factionId));
            faction.RulerStrategy.Complete(completedTick, provider, status, Truncate(note));
        }

        private AiFactionRuntime Find(AiStateId id) => scheduler.Factions.FirstOrDefault(value => value.Strategic.Id == id);

        private static bool TriggerAllowed(AiFactionRuntime faction, RulerStrategyTrigger trigger,
            long tick, long cooldown) => !faction.RulerStrategy.Journal.Any(entry => entry.Trigger == trigger &&
                tick - entry.CompletedTick < cooldown);

        private static RulerStrategyRequest BuildRequest(AiFactionRuntime faction,
            RulerStrategyPendingRequest pending)
        {
            var resources = new[] { "berries", "wood_log", "plant_fiber", "stone" }
                .Select(id => new RulerStrategyResourceFact(id,
                    faction.Production.GetCanonicalTotal(new ResourceId(id))));
            var completed = faction.Research.CompletedTechnologies.Select(value => value.ToString());
            var available = faction.Research.AvailableTechnologies.Select(value => value.Id.ToString());
            var buildings = faction.Buildings.Values.Where(value => faction.Construction.IsBuildingUnlocked(value.Id))
                .Select(value => value.Id.FamilyId.ToString());
            var relations = faction.Relations.Select(value => new RulerStrategyRelationFact(value.Key, value.Value));
            return new RulerStrategyRequest(faction.Strategic.Id, pending,
                new RulerStrategyRequestContext(faction.Residents.Count,
                    faction.Strategic.ControlledSettlements.Count, faction.Strategic.KnownMilitaryUnits.Count,
                    faction.Strategic.Goal, resources, completed, available, buildings, relations),
                faction.RulerStrategy.RequestIntervalTicks);
        }

        private static string Truncate(string value)
        {
            value = value ?? string.Empty;
            return value.Length <= 512 ? value : value.Substring(0, 512);
        }
    }
}
