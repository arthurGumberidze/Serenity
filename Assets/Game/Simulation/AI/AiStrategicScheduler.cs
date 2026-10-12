using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Dynasties;
using Game.Domain.Health;
using Game.Domain.Military;
using Game.Domain.Production;
using Game.Domain.Research;
using Game.Domain.Resources;
using Game.Domain.World;
using Game.Simulation.Buildings;
using Game.Simulation.Combat;
using Game.Simulation.Dynasties;
using Game.Simulation.Military;
using Game.Simulation.Production;
using Game.Simulation.Research;
using Game.Simulation.Resources;
using Game.Simulation.World;

namespace Game.Simulation.AI
{
    public sealed class AiFactionRuntime
    {
        public AiFactionRuntime(AiStrategicState strategicState, DynastyState dynastyState,
            SuccessionService successionService, ResearchService researchService,
            ProductionService productionService, ResourceInventoryRegistry inventoryRegistry,
            WorldPileService worldPileService, ConstructionFundingService constructionService,
            IEnumerable<BuildingDefinition> buildingDefinitions,
            IEnumerable<StableEntityId> residentIds)
        {
            Strategic = strategicState ?? throw new ArgumentNullException(nameof(strategicState));
            Dynasty = dynastyState ?? throw new ArgumentNullException(nameof(dynastyState));
            Succession = successionService ?? throw new ArgumentNullException(nameof(successionService));
            Research = researchService ?? throw new ArgumentNullException(nameof(researchService));
            Production = productionService ?? throw new ArgumentNullException(nameof(productionService));
            Inventories = inventoryRegistry ?? throw new ArgumentNullException(nameof(inventoryRegistry));
            WorldPiles = worldPileService ?? throw new ArgumentNullException(nameof(worldPileService));
            Construction = constructionService ?? throw new ArgumentNullException(nameof(constructionService));
            Buildings = (buildingDefinitions ?? throw new ArgumentNullException(nameof(buildingDefinitions)))
                .ToDictionary(x => x.Id);
            Residents = Array.AsReadOnly((residentIds ?? throw new ArgumentNullException(nameof(residentIds)))
                .Distinct().OrderBy(x => x).ToArray());
        }
        public AiStrategicState Strategic { get; }
        public DynastyState Dynasty { get; }
        public SuccessionService Succession { get; }
        public ResearchService Research { get; }
        public ProductionService Production { get; }
        public ResourceInventoryRegistry Inventories { get; }
        public WorldPileService WorldPiles { get; }
        public ConstructionFundingService Construction { get; }
        public IReadOnlyDictionary<BuildingDefinitionId, BuildingDefinition> Buildings { get; }
        public IReadOnlyList<StableEntityId> Residents { get; }
        public AiFactionSaveState CaptureState() => new AiFactionSaveState(Strategic.CaptureState(),
            Dynasty.CaptureState(), Research.CaptureState(), Production.CaptureState(), WorldPiles.CaptureStates());
    }

    public readonly struct AiDecisionFacts
    {
        public AiDecisionFacts(bool economyShortage, bool hasResearch, bool canConstruct, bool hasArmy,
            bool hasMilitaryCandidates, bool hasTarget, bool isTravelling, bool hasActiveBattle,
            bool canCapture, bool needsCaptain)
        {
            EconomyShortage = economyShortage; HasResearch = hasResearch; CanConstruct = canConstruct;
            HasArmy = hasArmy; HasMilitaryCandidates = hasMilitaryCandidates; HasTarget = hasTarget;
            IsTravelling = isTravelling; HasActiveBattle = hasActiveBattle; CanCapture = canCapture;
            NeedsCaptain = needsCaptain;
        }
        public bool EconomyShortage { get; }
        public bool HasResearch { get; }
        public bool CanConstruct { get; }
        public bool HasArmy { get; }
        public bool HasMilitaryCandidates { get; }
        public bool HasTarget { get; }
        public bool IsTravelling { get; }
        public bool HasActiveBattle { get; }
        public bool CanCapture { get; }
        public bool NeedsCaptain { get; }
    }

    public sealed class AiDecisionService
    {
        public AiDecisionKind Select(AiDecisionFacts facts)
        {
            if (facts.CanCapture) return AiDecisionKind.Capture;
            if (facts.HasActiveBattle) return AiDecisionKind.AwaitBattle;
            if (facts.IsTravelling) return AiDecisionKind.Travel;
            if (facts.CanConstruct) return AiDecisionKind.ConstructBuilding;
            if (!facts.HasArmy && facts.HasMilitaryCandidates) return AiDecisionKind.FormMilitaryUnit;
            if (facts.NeedsCaptain) return AiDecisionKind.AssignCaptain;
            if (facts.HasArmy && facts.HasTarget) return AiDecisionKind.Travel;
            if (facts.HasResearch) return AiDecisionKind.StartResearch;
            if (facts.EconomyShortage) return AiDecisionKind.Produce;
            return AiDecisionKind.Recover;
        }
    }

    public static class StateDefeatRule
    {
        public static bool IsDefeated(GlobalMapService map, CombatSideId sideId) =>
            (map ?? throw new ArgumentNullException(nameof(map))).Settlements.All(x => x.OwnerSideId != sideId);
    }

    public enum SettlementCaptureStatus
    {
        Captured = 1, AlreadyOwned = 2, MissingSettlement = 3, BattleNotFinished = 4,
        NotVictoriousSide = 5, BuildingRejected = 6
    }

    public readonly struct SettlementCaptureResult
    {
        public SettlementCaptureResult(SettlementCaptureStatus status, BuildingCaptureFailure buildingFailure = BuildingCaptureFailure.None)
        { Status = status; BuildingFailure = buildingFailure; }
        public SettlementCaptureStatus Status { get; }
        public BuildingCaptureFailure BuildingFailure { get; }
        public bool Captured => Status == SettlementCaptureStatus.Captured;
    }

    public sealed class SettlementCaptureService
    {
        private readonly GlobalMapService map;
        private readonly BattleResolutionService battles;
        private readonly BuildingDamageService buildings;

        public SettlementCaptureService(GlobalMapService globalMap, BattleResolutionService battleService,
            BuildingDamageService buildingService)
        { map = globalMap ?? throw new ArgumentNullException(nameof(globalMap)); battles = battleService ?? throw new ArgumentNullException(nameof(battleService)); buildings = buildingService; }

        public SettlementCaptureResult Capture(SettlementId settlementId, CombatSideId newOwner,
            StableEntityId battleId, long tick)
        {
            var settlement = map.Settlements.SingleOrDefault(x => x.Id == settlementId);
            if (settlement == null) return new SettlementCaptureResult(SettlementCaptureStatus.MissingSettlement);
            if (settlement.OwnerSideId == newOwner) return new SettlementCaptureResult(SettlementCaptureStatus.AlreadyOwned);
            var battle = battles.Battles.SingleOrDefault(x => x.BattleId == battleId);
            if (battle == null || battle.Status != CombatEncounterStatus.Finished)
                return new SettlementCaptureResult(SettlementCaptureStatus.BattleNotFinished);
            var victor = battle.Outcome == CombatOutcome.SideAVictory ? battle.SideA :
                battle.Outcome == CombatOutcome.SideBVictory ? battle.SideB : default;
            if (!victor.IsValid || victor != newOwner || victor == settlement.OwnerSideId)
                return new SettlementCaptureResult(SettlementCaptureStatus.NotVictoriousSide);

            if (buildings != null && settlement.BuildingIds.Count > 0)
            {
                var result = buildings.CaptureBuildings(settlement.BuildingIds, newOwner, battle, tick);
                if (!result.Succeeded)
                    return new SettlementCaptureResult(SettlementCaptureStatus.BuildingRejected, result.Failure);
            }
            map.SynchronizeSettlement(settlement.Id, settlement.LocationId, newOwner,
                settlement.BuildingIds, settlement.ResidentCharacterIds);
            return new SettlementCaptureResult(SettlementCaptureStatus.Captured);
        }
    }

    public sealed class AiStrategicScheduler
    {
        private readonly SortedDictionary<AiStateId, AiFactionRuntime> factions =
            new SortedDictionary<AiStateId, AiFactionRuntime>();
        private readonly CharacterRegistry characters;
        private readonly ICharacterCapabilityProvider capabilities;
        private readonly MilitaryUnitService units;
        private readonly GlobalMapService map;
        private readonly BattleResolutionService battles;
        private readonly SettlementCaptureService capture;
        private readonly AiDecisionService decisions;
        private readonly AiStrategicSettings settings;
        private readonly BuildingRegistry buildingRegistry;
        private readonly long worldSeed;

        public AiStrategicScheduler(long simulationSeed, CharacterRegistry characterRegistry,
            ICharacterCapabilityProvider capabilityProvider, MilitaryUnitService militaryUnits,
            GlobalMapService globalMap, BattleResolutionService battleService, BuildingRegistry buildings,
            BuildingDamageService buildingDamage = null, AiStrategicSettings strategicSettings = null,
            AiDecisionService decisionService = null)
        {
            worldSeed = simulationSeed; characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            capabilities = capabilityProvider ?? throw new ArgumentNullException(nameof(capabilityProvider));
            units = militaryUnits ?? throw new ArgumentNullException(nameof(militaryUnits));
            map = globalMap ?? throw new ArgumentNullException(nameof(globalMap));
            battles = battleService ?? throw new ArgumentNullException(nameof(battleService));
            buildingRegistry = buildings ?? throw new ArgumentNullException(nameof(buildings));
            capture = new SettlementCaptureService(map, battles, buildingDamage);
            settings = strategicSettings ?? AiStrategicSettings.StoneAge;
            decisions = decisionService ?? new AiDecisionService();
        }

        public long LastProcessedTick { get; private set; }
        public IReadOnlyList<AiFactionRuntime> Factions => factions.Values.ToArray();
        public MilitaryUnitRosterSummary GetUnitSummary(MilitaryUnitId id) => units.GetRosterSummary(id);
        public MilitaryUnitState GetUnit(MilitaryUnitId id) => units.Get(id);
        public void AddFaction(AiFactionRuntime faction)
        {
            if (faction == null) throw new ArgumentNullException(nameof(faction));
            if (factions.Values.Any(x => x.Strategic.OwnerId == faction.Strategic.OwnerId ||
                                         x.Strategic.SideId == faction.Strategic.SideId))
                throw new InvalidOperationException("AI owner and side must be unique.");
            var home = map.Settlements.SingleOrDefault(x => x.Id == faction.Strategic.HomeSettlementId);
            if (home == null || home.OwnerSideId != faction.Strategic.SideId)
                throw new InvalidOperationException("AI home settlement ownership is invalid.");
            if (!characters.TryGet(faction.Strategic.RulerCharacterId, out _))
                throw new InvalidOperationException("AI ruler is missing from the canonical Character registry.");
            if (!factions.TryAdd(faction.Strategic.Id, faction)) throw new InvalidOperationException("AI state already exists.");
            faction.Production.SetTarget(new ResourceId("berries"), settings.FoodTarget);
            faction.Production.SetTarget(new ResourceId("wood_log"), settings.MaterialTarget);
            faction.Production.SetTarget(new ResourceId("plant_fiber"), settings.MaterialTarget);
            faction.Production.SetTarget(new ResourceId("stone"), settings.MaterialTarget);
        }

        public void AdvanceTo(long calendarTick, long biologicalTick)
        {
            if (calendarTick < LastProcessedTick) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            while (true)
            {
                var dueTick = factions.Values.Where(x => !x.Strategic.IsDefeated &&
                        x.Strategic.NextDecisionTick <= calendarTick)
                    .Select(x => Math.Max(LastProcessedTick, x.Strategic.NextDecisionTick))
                    .DefaultIfEmpty(long.MaxValue).Min();
                if (dueTick == long.MaxValue) break;
                AdvanceSharedServices(dueTick, biologicalTick);
                foreach (var faction in factions.Values) AdvanceFactionServices(faction, dueTick);
                foreach (var faction in factions.Values) Reconcile(faction, dueTick);
                foreach (var faction in factions.Values.Where(x => !x.Strategic.IsDefeated &&
                             x.Strategic.NextDecisionTick <= dueTick).ToArray())
                    Decide(faction, dueTick);
            }
            AdvanceSharedServices(calendarTick, biologicalTick);
            foreach (var faction in factions.Values) AdvanceFactionServices(faction, calendarTick);
            foreach (var faction in factions.Values) Reconcile(faction, calendarTick);
            LastProcessedTick = calendarTick;
        }

        public SettlementCaptureResult CaptureSettlement(SettlementId settlementId, CombatSideId newOwner,
            StableEntityId battleId, long tick)
        {
            if (tick < LastProcessedTick) throw new ArgumentOutOfRangeException(nameof(tick));
            var result = capture.Capture(settlementId, newOwner, battleId, tick);
            if (result.Captured)
                foreach (var faction in factions.Values) Reconcile(faction, tick);
            return result;
        }

        public AiStrategicSaveState CaptureState() => new AiStrategicSaveState(worldSeed, LastProcessedTick,
            factions.Values.Select(x => x.CaptureState()));

        public void RestoreState(AiStrategicSaveState state)
        {
            if (state == null || state.WorldSeed != worldSeed) throw new ArgumentException("AI save seed does not match.", nameof(state));
            if (state.Factions.Count != factions.Count) throw new ArgumentException("AI faction set does not match.", nameof(state));
            if (state.LastProcessedTick < 0) throw new ArgumentException("AI checkpoint is invalid.", nameof(state));
            foreach (var saved in state.Factions) ValidateSavedFaction(saved, state.LastProcessedTick);
            foreach (var saved in state.Factions)
            {
                if (!factions.TryGetValue(saved.Strategic.Id, out var faction))
                    throw new ArgumentException("Saved AI state is unavailable.", nameof(state));
                faction.Dynasty.RestoreFrom(saved.Dynasty);
                faction.Research.RestoreState(saved.Research);
                faction.Production.RestoreState(saved.Production);
                faction.WorldPiles.RestoreStates(saved.WorldPiles);
                faction.Strategic.Restore(saved.Strategic);
            }
            LastProcessedTick = state.LastProcessedTick;
        }

        private void AdvanceSharedServices(long tick, long biologicalTick)
        {
            map.AdvanceTo(tick);
            battles.AdvanceTo(tick, biologicalTick);
        }

        private void AdvanceFactionServices(AiFactionRuntime faction, long tick)
        {
            var homeLocation = map.Settlements.Single(x => x.Id == faction.Strategic.HomeSettlementId).LocationId.Value;
            var workers = Eligible(faction, x => x.CanPerformOrdinaryWork).Where(id =>
            {
                if (!units.TryGetForMember(id, out var unit)) return true;
                return !unit.ActiveEncounterId.HasValue && !map.Travels.Any(x => x.UnitId == unit.Id) &&
                    string.Equals(unit.AbstractLocationId, homeLocation, StringComparison.Ordinal);
            }).ToArray();
            faction.Production.Population = faction.Residents.Count(id => characters.TryGet(id, out var c) && c.LifeState == CharacterLifeState.Alive);
            faction.Production.AdvanceTo(tick, workers);
            var workerSet = new HashSet<StableEntityId>(workers);
            var researchers = Eligible(faction, x => x.CanResearch).Where(workerSet.Contains).Take(2).ToArray();
            faction.Research.AssignWorkers(researchers);
            faction.Research.AdvanceWorkersTo(tick, researchers);
        }

        private void ValidateSavedFaction(AiFactionSaveState saved, long lastProcessedTick)
        {
            if (!factions.TryGetValue(saved.Strategic.Id, out var runtime))
                throw new ArgumentException("Saved AI state is unavailable.", nameof(saved));
            if (saved.Strategic.OwnerId != runtime.Strategic.OwnerId ||
                saved.Strategic.SideId != runtime.Strategic.SideId ||
                saved.Strategic.HomeSettlementId != runtime.Strategic.HomeSettlementId ||
                saved.Strategic.LastDecisionTick > lastProcessedTick)
                throw new ArgumentException("Saved AI identity or checkpoint is invalid.", nameof(saved));
            if (!characters.TryGet(saved.Strategic.RulerCharacterId, out _) ||
                !saved.Dynasty.MemberIds.Contains(saved.Strategic.RulerCharacterId))
                throw new ArgumentException("Saved AI ruler is invalid.", nameof(saved));
            foreach (var settlementId in saved.Strategic.ControlledSettlements)
            {
                var settlement = map.Settlements.SingleOrDefault(x => x.Id == settlementId);
                if (settlement == null || settlement.OwnerSideId != saved.Strategic.SideId)
                    throw new ArgumentException("Saved AI settlement ownership is invalid.", nameof(saved));
            }
            foreach (var unitId in saved.Strategic.KnownMilitaryUnits)
                if (!units.TryGet(unitId, out var unit) ||
                    unit.OwnerSideId.Value != saved.Strategic.SideId.Value)
                    throw new ArgumentException("Saved AI military reference is invalid.", nameof(saved));
            if (saved.Strategic.TargetSettlementId.HasValue &&
                map.Settlements.All(x => x.Id != saved.Strategic.TargetSettlementId.Value))
                throw new ArgumentException("Saved AI target settlement is invalid.", nameof(saved));
            if (saved.Strategic.TargetLocationId.HasValue &&
                map.Locations.All(x => x.Id != saved.Strategic.TargetLocationId.Value))
                throw new ArgumentException("Saved AI target location is invalid.", nameof(saved));
        }

        private void Reconcile(AiFactionRuntime faction, long tick)
        {
            var state = faction.Strategic;
            if (characters.TryGet(state.RulerCharacterId, out var ruler) && ruler.LifeState == CharacterLifeState.Dead)
            {
                faction.Succession.ProcessDeath(ruler.Id, ruler.BiologicalDeathTick ?? tick, tick);
                if (faction.Dynasty.CurrentMonarchId.HasValue) state.SetRuler(faction.Dynasty.CurrentMonarchId.Value);
                else state.MarkDefeated(AiDefeatReason.DynastyExtinct);
            }
            foreach (var id in state.KnownMilitaryUnits.ToArray())
                if (!units.TryGet(id, out _)) state.RemoveUnit(id);

            foreach (var id in state.KnownMilitaryUnits.ToArray())
                if (units.TryGet(id, out var spent) && units.GetRosterSummary(id).CombatCapable == 0)
                {
                    if (!spent.ActiveEncounterId.HasValue)
                        foreach (var member in spent.Roster.Where(x => x.IsActive).Select(x => x.CharacterId).ToArray())
                            units.RemoveMember(id, member);
                    state.RemoveUnit(id);
                }

            var owned = map.Settlements.Where(x => x.OwnerSideId == state.SideId).Select(x => x.Id).ToArray();
            foreach (var old in state.ControlledSettlements.Where(x => !owned.Contains(x)).ToArray()) state.RemoveSettlement(old);
            foreach (var current in owned) state.AddSettlement(current);
            if (StateDefeatRule.IsDefeated(map, state.SideId))
            { state.MarkDefeated(AiDefeatReason.NoControlledSettlement); return; }

            foreach (var unitId in state.KnownMilitaryUnits.ToArray())
            {
                if (!units.TryGet(unitId, out var unit)) continue;
                var order = state.PendingOrders.LastOrDefault(x => x.Kind == AiDecisionKind.Travel &&
                    x.UnitId == unitId && x.TargetSettlementId.HasValue);
                if (!order.TargetSettlementId.HasValue) continue;
                var battle = battles.Battles.Where(x => x.Status == CombatEncounterStatus.Finished &&
                        (x.SideAUnitId == unitId || x.SideBUnitId == unitId) &&
                        x.StartCalendarTick >= order.IssuedTick)
                    .OrderByDescending(x => x.StartCalendarTick).ThenByDescending(x => x.BattleId).FirstOrDefault();
                if (battle == null) continue;
                var target = state.TargetSettlementId.HasValue
                    ? map.Settlements.SingleOrDefault(x => x.Id == state.TargetSettlementId.Value) : null;
                if (target == null) continue;
                var result = capture.Capture(target.Id, state.SideId, battle.BattleId, tick);
                state.CompleteOrders(AiDecisionKind.Travel);
                if (result.Captured)
                {
                    state.AddSettlement(target.Id);
                    state.RecordDecision(tick, settings.DecisionIntervalTicks, AiDecisionKind.Capture,
                        AiStrategicGoal.CaptureSettlement, 1000, "Finished hostile victory captured the settlement.",
                        target.Id, target.LocationId, unitId);
                }
                else if (result.Status == SettlementCaptureStatus.NotVictoriousSide)
                {
                    // Release surviving members from a defeated expedition. The next strategic tick may
                    // form a fresh canonical U21 unit instead of leaving the state stuck on a spent army.
                    foreach (var member in unit.Roster.Where(x => x.IsActive).Select(x => x.CharacterId).ToArray())
                        units.RemoveMember(unitId, member);
                    state.RemoveUnit(unitId);
                }
            }
        }

        private void Decide(AiFactionRuntime faction, long tick)
        {
            var state = faction.Strategic;
            if (state.IsDefeated) return;
            var knownUnits = state.KnownMilitaryUnits.Where(id => units.TryGet(id, out _)).Select(units.Get).ToArray();
            var unit = knownUnits.FirstOrDefault(x => x.Status != MilitaryUnitStatus.Disbanded);
            var target = SelectTarget(state, unit);
            var definition = SelectBuilding(faction);
            var candidates = Eligible(faction, x => x.CanFight).Where(id => !units.TryGetForMember(id, out _)).ToArray();
            var hasSettlementBuilding = buildingRegistry.All.Any(x => x.OwnerSideId == state.SideId &&
                x.StructuralStatus != BuildingStructuralStatus.Destroyed);
            var facts = new AiDecisionFacts(
                faction.Production.GetCanonicalTotal(new ResourceId("berries")) < settings.FoodTarget ||
                faction.Production.GetCanonicalTotal(new ResourceId("wood_log")) < settings.MaterialTarget,
                faction.Research.ActiveProject != null || faction.Research.AvailableTechnologies.Count > 0,
                definition != null, unit != null,
                hasSettlementBuilding && candidates.Length >= settings.MinimumArmySize, target != null,
                unit != null && map.Travels.Any(x => x.UnitId == unit.Id),
                unit != null && unit.ActiveEncounterId.HasValue, false,
                unit != null && !unit.CaptainId.HasValue);
            var selected = decisions.Select(facts);
            switch (selected)
            {
                case AiDecisionKind.Produce:
                    Record(state, tick, selected, AiStrategicGoal.StabilizeEconomy, 900, "Canonical target stock is below policy.");
                    break;
                case AiDecisionKind.StartResearch:
                    StartResearch(faction, tick); break;
                case AiDecisionKind.ConstructBuilding:
                    Construct(faction, definition, tick); break;
                case AiDecisionKind.FormMilitaryUnit:
                    FormUnit(faction, candidates, tick); break;
                case AiDecisionKind.AssignCaptain:
                    units.RefreshCaptain(unit.Id);
                    Record(state, tick, selected, AiStrategicGoal.FormArmy, 700, "Refreshed captain through U21.", unitId: unit.Id);
                    break;
                case AiDecisionKind.Travel:
                    Travel(faction, unit, target, tick); break;
                case AiDecisionKind.AwaitBattle:
                    Record(state, tick, selected, AiStrategicGoal.AttackSettlement, 950, "Canonical battle is active.",
                        state.TargetSettlementId, state.TargetLocationId, unit.Id); break;
                default:
                    Record(state, tick, AiDecisionKind.Recover, AiStrategicGoal.Recover, 100,
                        "No valid strategic command is currently available."); break;
            }
        }

        private void StartResearch(AiFactionRuntime faction, long tick)
        {
            if (faction.Research.ActiveProject == null)
            {
                var technology = faction.Research.AvailableTechnologies
                    .OrderByDescending(x => TechnologyScore(x)).ThenBy(x => x.Id).FirstOrDefault();
                if (technology != null) faction.Research.Start(technology.Id);
            }
            var active = faction.Research.ActiveProject;
            Record(faction.Strategic, tick, AiDecisionKind.StartResearch, AiStrategicGoal.AdvanceResearch,
                active == null ? 0 : TechnologyScore(active.TechnologyId),
                active == null ? "No research is available." : "Selected available technology by stable score: " + active.TechnologyId);
        }

        private void Construct(AiFactionRuntime faction, BuildingDefinition definition, long tick)
        {
            Building placed;
            ConstructionEvaluation result = default;
            for (var offset = 0; offset < 64; offset++)
            {
                var coordinate = ConstructionCoordinate(offset);
                result = faction.Construction.TryConfirm(definition, coordinate, BuildingOrientation.North,
                    faction.Inventories.All.Select(x => x.Owner).OrderBy(x => x.Id), out placed,
                    ownerSideId: faction.Strategic.SideId,
                    stableId: DerivedId(worldSeed, faction.Strategic.Id.ToString(),
                        "building:" + definition.Id + ":" + coordinate, faction.Strategic.DecisionSequence + 1));
                if (!result.IsValid) continue;
                var settlement = map.Settlements.Single(x => x.Id == faction.Strategic.HomeSettlementId);
                map.SynchronizeSettlement(settlement.Id, settlement.LocationId, settlement.OwnerSideId,
                    settlement.BuildingIds.Concat(new[] { placed.Id }), settlement.ResidentCharacterIds);
                if (definition.Id.ToString() == "primitive_crop_plot")
                    faction.Production.AddSite(ProductionSite.CreateCropPlot(placed.Id,
                        new ProductionRecipeId("cultivate_berries"), new WorldPosition(coordinate.X, 0, coordinate.Z),
                        faction.Production.Settings.CropGrowthDuration));
                Record(faction.Strategic, tick, AiDecisionKind.ConstructBuilding,
                    AiStrategicGoal.DevelopSettlement, 800, "Spent canonical resources through construction funding.");
                return;
            }
            Record(faction.Strategic, tick, AiDecisionKind.Recover, AiStrategicGoal.Recover, 50,
                "Construction had no valid funded coordinate: " + result.Failure);
        }

        private void FormUnit(AiFactionRuntime faction, StableEntityId[] candidates, long tick)
        {
            var roster = candidates.Take(settings.MinimumArmySize).ToArray();
            var id = new MilitaryUnitId(DerivedId(worldSeed, faction.Strategic.Id.ToString(), "unit", faction.Strategic.DecisionSequence + 1));
            var captain = roster.OrderByDescending(characterId => characters.Get(characterId).Influence.Value)
                .ThenBy(characterId => characterId).First();
            var unit = units.Create(id, new MilitarySideId(faction.Strategic.SideId.Value), roster, captain,
                MilitaryUnitRepresentationMode.Aggregate,
                map.Settlements.Single(x => x.Id == faction.Strategic.HomeSettlementId).LocationId.Value);
            units.LoadInitialSupply(unit.Id, faction.Inventories,
                faction.Inventories.All.Select(x => x.Owner));
            faction.Strategic.AddUnit(unit.Id);
            Record(faction.Strategic, tick, AiDecisionKind.FormMilitaryUnit, AiStrategicGoal.FormArmy, 750,
                "Created a U21 unit from eligible canonical Characters.", unitId: unit.Id);
        }

        private void Travel(AiFactionRuntime faction, MilitaryUnitState unit, SettlementState target, long tick)
        {
            if (unit == null || target == null)
            { Record(faction.Strategic, tick, AiDecisionKind.Recover, AiStrategicGoal.Recover, 0, "No valid unit or target."); return; }
            if (map.Travels.Any(x => x.UnitId == unit.Id))
            { Record(faction.Strategic, tick, AiDecisionKind.Travel, AiStrategicGoal.AttackSettlement, 650,
                "Existing U25 travel order remains in progress.", target.Id, target.LocationId, unit.Id); return; }
            var current = new WorldLocationId(unit.AbstractLocationId);
            var next = NextHop(current, target.LocationId);
            if (!next.HasValue)
            { Record(faction.Strategic, tick, AiDecisionKind.Recover, AiStrategicGoal.Recover, 0, "No route to hostile target."); return; }
            var order = map.IssueTravelOrder(unit.Id, next.Value, tick);
            if (order.Issued)
            {
                faction.Strategic.ClearOrders();
                faction.Strategic.AddOrder(new AiPendingOrder(faction.Strategic.DecisionSequence + 1,
                    AiDecisionKind.Travel, tick, unit.Id, target.Id, target.LocationId));
                Record(faction.Strategic, tick, AiDecisionKind.Travel, AiStrategicGoal.AttackSettlement, 700,
                    "Issued canonical U25 route leg.", target.Id, target.LocationId, unit.Id);
            }
            else Record(faction.Strategic, tick, AiDecisionKind.Recover, AiStrategicGoal.Recover, 0,
                "Travel rejected: " + order.Status);
        }

        private SettlementState SelectTarget(AiStrategicState state, MilitaryUnitState unit)
        {
            var origin = unit == null || string.IsNullOrEmpty(unit.AbstractLocationId)
                ? map.Settlements.Single(x => x.Id == state.HomeSettlementId).LocationId
                : new WorldLocationId(unit.AbstractLocationId);
            return map.Settlements.Where(x => x.OwnerSideId != state.SideId)
                .Select(x => new { Settlement = x, Distance = RouteDistance(origin, x.LocationId) })
                .Where(x => x.Distance < long.MaxValue)
                .OrderBy(x => x.Distance).ThenBy(x => x.Settlement.Id).Select(x => x.Settlement).FirstOrDefault();
        }

        private BuildingDefinition SelectBuilding(AiFactionRuntime faction)
        {
            foreach (var id in settings.BuildingPriorities)
            {
                var key = new BuildingDefinitionId(id);
                if (!faction.Buildings.TryGetValue(key, out var definition) ||
                    !faction.Construction.IsBuildingUnlocked(key)) continue;
                if (buildingRegistry.All.Any(x => x.OwnerSideId == faction.Strategic.SideId && x.DefinitionId == key)) continue;
                for (var offset = 0; offset < 64; offset++)
                    if (faction.Construction.Evaluate(definition, ConstructionCoordinate(offset),
                            BuildingOrientation.North, faction.Inventories.All.Select(x => x.Owner)).IsValid)
                        return definition;
            }
            return null;
        }

        private static GridCoordinate ConstructionCoordinate(int offset) =>
            new GridCoordinate(18 + (offset % 8) * 4, 18 + (offset / 8) * 4);

        private IReadOnlyList<StableEntityId> Eligible(AiFactionRuntime faction, Func<CharacterCapabilities, bool> predicate) =>
            faction.Residents.Where(id => characters.TryGet(id, out var c) && c.LifeState == CharacterLifeState.Alive &&
                predicate(capabilities.GetCapabilities(id))).OrderBy(id => id).ToArray();

        private WorldLocationId? NextHop(WorldLocationId origin, WorldLocationId target)
        {
            if (origin == target) return origin;
            var frontier = new SortedSet<Tuple<long, string, WorldLocationId>>(Comparer<Tuple<long,string,WorldLocationId>>.Create((a,b) =>
            { var c=a.Item1.CompareTo(b.Item1); if(c!=0)return c; c=string.CompareOrdinal(a.Item2,b.Item2); return c!=0?c:a.Item3.CompareTo(b.Item3); }));
            var distance = new Dictionary<WorldLocationId, long> { [origin] = 0 };
            var first = new Dictionary<WorldLocationId, WorldLocationId>();
            frontier.Add(Tuple.Create(0L, origin.Value, origin));
            while (frontier.Count > 0)
            {
                var current = frontier.Min; frontier.Remove(current);
                if (current.Item3 == target) return first[current.Item3];
                foreach (var route in map.GetRoutes(current.Item3).OrderBy(x => Other(x, current.Item3)))
                {
                    var next = Other(route, current.Item3); var value = checked(current.Item1 + route.TravelDurationTicks);
                    var firstHop = current.Item3 == origin ? next : first[current.Item3];
                    if (distance.TryGetValue(next, out var old) && (old < value || old == value && first[next].CompareTo(firstHop) <= 0)) continue;
                    distance[next] = value; first[next] = firstHop; frontier.Add(Tuple.Create(value, next.Value, next));
                }
            }
            return null;
        }

        private long RouteDistance(WorldLocationId origin, WorldLocationId target)
        {
            if (origin == target) return 0;
            var distances = new Dictionary<WorldLocationId,long> { [origin]=0 };
            var open = new List<WorldLocationId> { origin };
            while(open.Count>0) { var current=open.OrderBy(x=>distances[x]).ThenBy(x=>x).First(); open.Remove(current);
                if(current==target)return distances[current]; foreach(var route in map.GetRoutes(current)) { var next=Other(route,current); var d=checked(distances[current]+route.TravelDurationTicks); if(!distances.TryGetValue(next,out var old)||d<old){distances[next]=d;if(!open.Contains(next))open.Add(next);} } }
            return long.MaxValue;
        }

        private static WorldLocationId Other(WorldRouteState route, WorldLocationId value) => route.From == value ? route.To : route.From;
        private static int TechnologyScore(TechnologyDefinition value) => TechnologyScore(value.Id);
        private static int TechnologyScore(TechnologyId value)
        { var text=value.ToString(); if(text=="shelter_construction")return 1000; if(text=="stone_tools")return 900; if(text=="primitive_agriculture")return 850; return 500-text.Length; }
        private void Record(AiStrategicState state, long tick, AiDecisionKind kind, AiStrategicGoal goal,
            int score, string reason, SettlementId? settlement = null, WorldLocationId? location = null,
            MilitaryUnitId? unitId = null) => state.RecordDecision(tick, settings.DecisionIntervalTicks,
                kind, goal, score, reason, settlement, location, unitId);
        private static StableEntityId DerivedId(long seed, string owner, string purpose, long sequence)
        { ulong a=14695981039346656037UL,b=1099511628211UL; foreach(var c in seed+"|"+owner+"|"+purpose){a=(a^c)*1099511628211UL;} foreach(var c in sequence.ToString()){b=(b^c)*14695981039346656037UL;} return StableEntityId.FromDeterministicHash(a,b); }
    }
}
