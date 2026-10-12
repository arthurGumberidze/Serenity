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
using Game.Domain.Time;
using Game.Domain.Weather;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Simulation.AI;
using Game.Simulation.Buildings;
using Game.Simulation.Combat;
using Game.Simulation.Dynasties;
using Game.Simulation.Health;
using Game.Simulation.Military;
using Game.Simulation.Production;
using Game.Simulation.Research;
using Game.Simulation.Resources;
using Game.Simulation.Tiers;
using Game.Simulation.Weather;
using Game.Simulation.World;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [TestFixture]
    [Category("U26")]
    public sealed class StrategicAiTests
    {
        private static readonly long AdultBiologicalTick = Character.BiologicalTicksPerYear * 20L;
        private static readonly long DecisionStep = TimeSpan.FromHours(6).Ticks;

        [Test, Category("U30")]
        public void ArrivalWithoutDefenderHoldsWithoutSelfRouteOrUnauthorizedCapture()
        {
            var fixture = Fixture.Create(3030);
            fixture.AdvanceTo(TimeSpan.FromDays(20).Ticks);
            var state = fixture.Ai.Strategic;
            Assert.That(state.RecentDecisions.Any(x => x.Kind == AiDecisionKind.AwaitBattle), Is.True);
            Assert.That(state.RecentDecisions.Any(x => x.Reason.Contains("NoDirectRoute")), Is.False);
            Assert.That(state.PendingOrders.Any(x => x.Kind == AiDecisionKind.Travel), Is.False);
            Assert.That(fixture.Map.Settlements.Single(x => x.Id == fixture.PlayerSettlement).OwnerSideId,
                Is.EqualTo(fixture.PlayerSide));
            Assert.That(fixture.Battles.Battles, Is.Empty);
            Assert.That(state.PendingOrders.Count(x => x.Kind == AiDecisionKind.AwaitBattle), Is.EqualTo(1));
        }

        [Test, Category("U30")]
        public void HoldingArmyCanResolveLaterBattleAndCaptureItsOrderedTarget()
        {
            var fixture = Fixture.Create(26003);
            fixture.AdvanceTo(TimeSpan.FromDays(20).Ticks);
            fixture.CreatePlayerDefender();
            var destination = new WorldLocationId("starter_settlement");
            var route = fixture.Map.GetRoutes(destination).First();
            var origin = route.From == destination ? route.To : route.From;
            fixture.Units.SetAbstractLocation(fixture.PlayerUnitId, origin.Value);
            Assert.That(fixture.Map.IssueTravelOrder(fixture.PlayerUnitId, destination, fixture.Tick).Issued, Is.True);
            fixture.AdvanceUntil(() => fixture.Map.Settlements.Single(x => x.Id == fixture.PlayerSettlement)
                .OwnerSideId == fixture.EnemySide, 500);
            Assert.That(fixture.Ai.Strategic.PendingOrders, Is.Empty);
            Assert.That(fixture.Battles.Battles.Any(x => x.Status == CombatEncounterStatus.Finished), Is.True);
        }

        [Test]
        public void DecisionSelectionUsesStableStrategicPriority()
        {
            var service = new AiDecisionService();
            Assert.That(service.Select(Facts(canCapture: true, hasActiveBattle: true)), Is.EqualTo(AiDecisionKind.Capture));
            Assert.That(service.Select(Facts(hasActiveBattle: true, isTravelling: true)), Is.EqualTo(AiDecisionKind.AwaitBattle));
            Assert.That(service.Select(Facts(isTravelling: true, canConstruct: true)), Is.EqualTo(AiDecisionKind.Travel));
            Assert.That(service.Select(Facts(canConstruct: true, hasResearch: true)), Is.EqualTo(AiDecisionKind.ConstructBuilding));
            Assert.That(service.Select(Facts(hasMilitaryCandidates: true, hasResearch: true)), Is.EqualTo(AiDecisionKind.FormMilitaryUnit));
            Assert.That(service.Select(Facts(hasResearch: true, economyShortage: true)), Is.EqualTo(AiDecisionKind.StartResearch));
            Assert.That(service.Select(Facts(economyShortage: true)), Is.EqualTo(AiDecisionKind.Produce));
        }

        [Test]
        [Category("U30A")]
        [Category("U31")]
        public void BootstrapIsSeedDeterministicCanonicalAndEconomicallyIsolated()
        {
            var first = Fixture.Create(26001);
            var second = Fixture.Create(26001);
            Assert.That(first.Ai.Residents, Is.EqualTo(second.Ai.Residents));
            Assert.That(first.Ai.Strategic.Id, Is.EqualTo(second.Ai.Strategic.Id));
            Assert.That(first.Ai.Dynasty.MemberIds, Is.EqualTo(first.Ai.Residents));
            Assert.That(first.Ai.Residents.All(id => first.Characters.TryGet(id, out _)), Is.True);
            Assert.That(first.Ai.Residents.All(id => first.Tiers.GetTier(id) == CharacterSimulationTier.Tier3), Is.True);
            Assert.That(first.Map.GetSettlementAt(new WorldLocationId("enemy_settlement")).ResidentCharacterIds,
                Is.EqualTo(first.Ai.Residents.OrderBy(x => x)));
            Assert.That(first.Ai.Inventories, Is.Not.SameAs(first.PlayerInventories));
            Assert.That(first.Ai.Research, Is.Not.SameAs(first.PlayerResearch));
            var playerWood = first.PlayerProduction.GetCanonicalTotal(new ResourceId("wood_log"));
            var aiOwner = first.Ai.Inventories.All.First(x =>
                x.GetAmount(new ResourceId("wood_log")).Units > 0);
            Assert.That(aiOwner.TryRemove(new ResourceId("wood_log"), new ResourceQuantity(1)), Is.True);
            Assert.That(first.PlayerProduction.GetCanonicalTotal(new ResourceId("wood_log")), Is.EqualTo(playerWood));
        }

        [Test]
        [Category("U30A")]
        public void DirectCatchUpMatchesSteppedResearchConstructionArmyAndTravel()
        {
            var direct = Fixture.Create(26002);
            var stepped = Fixture.Create(26002);
            var target = TimeSpan.FromDays(12).Ticks;
            direct.AdvanceTo(target);
            for (var tick = DecisionStep; tick <= target; tick += DecisionStep) stepped.AdvanceTo(tick);

            Assert.That(Digest(direct), Is.EqualTo(Digest(stepped)));
            Assert.That(direct.Ai.Research.IsCompleted(new TechnologyId("shelter_construction")), Is.True);
            var shelter = direct.Buildings.All.Single(x => x.OwnerSideId == direct.Ai.Strategic.SideId &&
                x.DefinitionId == new BuildingDefinitionId("primitive_shelter"));
            Assert.That(shelter.ConstructionState, Is.EqualTo(ConstructionState.Completed));
            Assert.That(direct.Ai.Strategic.KnownMilitaryUnits.Count, Is.EqualTo(1));
            var army = direct.Units.Get(direct.Ai.Strategic.KnownMilitaryUnits.Single());
            Assert.That(army.Representation, Is.EqualTo(MilitaryUnitRepresentationMode.Aggregate));
            Assert.That(army.CaptainId.HasValue, Is.True);
            TestContext.WriteLine("U26 deterministic digest " + Digest(direct));
        }

        [Test]
        public void BaselineAiResearchesBuildsFightsAndCapturesThroughCanonicalSystems()
        {
            var fixture = Fixture.Create(26003, defender: true);
            var playerBuilding = fixture.AddCompletedBuilding(fixture.Shelter, fixture.PlayerSide,
                new GridCoordinate(-30, -30), Id(260030));
            fixture.SyncSettlement(fixture.PlayerSettlement, fixture.PlayerSide,
                fixture.PlayerIds, new[] { playerBuilding.Id });

            for (var i = 1; i <= 500 && fixture.Map.Settlements.Single(x => x.Id == fixture.PlayerSettlement)
                     .OwnerSideId != fixture.EnemySide; i++)
                fixture.AdvanceTo(i * DecisionStep);

            var settlement = fixture.Map.Settlements.Single(x => x.Id == fixture.PlayerSettlement);
            Assert.That(fixture.Battles.Battles.Any(x => x.Status == CombatEncounterStatus.Finished), Is.True,
                "global travel never reached a canonical U23 resolution");
            Assert.That(settlement.OwnerSideId, Is.EqualTo(fixture.EnemySide));
            Assert.That(playerBuilding.OwnerSideId, Is.EqualTo(fixture.EnemySide));
            Assert.That(fixture.Ai.Strategic.ControlledSettlements, Does.Contain(fixture.PlayerSettlement));
            Assert.That(fixture.Ai.Strategic.RecentDecisions.Any(x => x.Kind == AiDecisionKind.Capture), Is.True);
        }

        [Test]
        [Category("U30A")]
        public void SaveV13RoundTripResumesWithoutRepeatingDecisionAndRejectsUnknownTarget()
        {
            var source = Fixture.Create(26004);
            source.AdvanceTo(DecisionStep);
            var before = source.Ai.Strategic.DecisionSequence;
            var snapshot = new SaveSnapshot(SaveSnapshot.StrategicAiVersion, Id(260041),
                new GameTimeState(source.Tick, source.BiologicalTick, 1, false), 400,
                strategicAi: source.Scheduler.CaptureState());
            var codec = new SaveXmlCodec();
            var decoded = codec.Deserialize(codec.Serialize(snapshot));
            Assert.That(decoded.FormatVersion, Is.EqualTo(SaveSnapshot.StrategicAiVersion));
            Assert.That(decoded.StrategicAi.Factions.Single().Strategic.DecisionSequence, Is.EqualTo(before));

            var restored = Fixture.Create(26004);
            restored.Scheduler.RestoreState(decoded.StrategicAi);
            var next = source.Tick + DecisionStep;
            source.AdvanceTo(next);
            restored.AdvanceTo(next);
            Assert.That(restored.Ai.Strategic.DecisionSequence, Is.EqualTo(before + 1));
            Assert.That(Digest(restored), Is.EqualTo(Digest(source)));

            var valid = decoded.StrategicAi.Factions.Single();
            var corruptStrategic = Copy(valid.Strategic, targetSettlement: new SettlementId(Id(999999)));
            var corrupt = new AiStrategicSaveState(decoded.StrategicAi.WorldSeed,
                decoded.StrategicAi.LastProcessedTick, new[] { new AiFactionSaveState(corruptStrategic,
                    valid.Dynasty, valid.Research, valid.Production, valid.WorldPiles) });
            Assert.Throws<ArgumentException>(() => Fixture.Create(26004).Scheduler.RestoreState(corrupt));
        }

        [Test]
        [Category("U30A")]
        [Category("U31")]
        public void FinishedPlayerVictoryCapturesWholeStateAndDefeatsAiCoherently()
        {
            var fixture = Fixture.Create(26005, defender: true);
            fixture.AdvanceUntil(() => fixture.Ai.Strategic.KnownMilitaryUnits.Count == 1, 80);
            var aiBuilding = fixture.Buildings.All.Single(x => x.OwnerSideId == fixture.EnemySide);
            var battle = fixture.ForcePlayerVictoryAtEnemy();
            var result = fixture.Scheduler.CaptureSettlement(fixture.EnemySettlement,
                fixture.PlayerSide, battle.BattleId, fixture.Tick + 1);
            Assert.That(result.Status, Is.EqualTo(SettlementCaptureStatus.Captured));
            Assert.That(fixture.Map.Settlements.Single(x => x.Id == fixture.EnemySettlement).OwnerSideId,
                Is.EqualTo(fixture.PlayerSide));
            Assert.That(aiBuilding.OwnerSideId, Is.EqualTo(fixture.PlayerSide));
            Assert.That(fixture.Ai.Strategic.IsDefeated, Is.True);
            Assert.That(fixture.Ai.Strategic.DefeatReason, Is.EqualTo(AiDefeatReason.NoControlledSettlement));
        }

        [Test]
        public void CapturePrevalidationPreventsPartialOwnershipMutation()
        {
            var fixture = Fixture.Create(26006, defender: true);
            fixture.AdvanceUntil(() => fixture.Ai.Strategic.KnownMilitaryUnits.Count == 1, 80);
            var capturable = fixture.Buildings.All.Single(x => x.OwnerSideId == fixture.EnemySide);
            var blocked = fixture.AddCompletedBuilding(fixture.Uncapturable, fixture.EnemySide,
                new GridCoordinate(35, 35), Id(260061));
            fixture.SyncSettlement(fixture.EnemySettlement, fixture.EnemySide, fixture.Ai.Residents,
                new[] { capturable.Id, blocked.Id });
            var battle = fixture.ForcePlayerVictoryAtEnemy();
            var result = fixture.Scheduler.CaptureSettlement(fixture.EnemySettlement,
                fixture.PlayerSide, battle.BattleId, fixture.Tick + 1);
            Assert.That(result.Status, Is.EqualTo(SettlementCaptureStatus.BuildingRejected));
            Assert.That(result.BuildingFailure, Is.EqualTo(BuildingCaptureFailure.NotCapturable));
            Assert.That(capturable.OwnerSideId, Is.EqualTo(fixture.EnemySide));
            Assert.That(blocked.OwnerSideId, Is.EqualTo(fixture.EnemySide));
            Assert.That(fixture.Map.Settlements.Single(x => x.Id == fixture.EnemySettlement).OwnerSideId,
                Is.EqualTo(fixture.EnemySide));
        }

        [Test]
        public void LostExpeditionReleasesSurvivorsAndFormsDeterministicReplacement()
        {
            var fixture = Fixture.Create(26007, defender: true);
            fixture.AdvanceUntil(() => fixture.Ai.Strategic.KnownMilitaryUnits.Count == 1, 80);
            for (var i = 0; i < 40 && !fixture.Battles.Battles.Any(x =>
                     x.Status == CombatEncounterStatus.Active); i++)
            {
                var nextArrival = fixture.Map.Travels.Select(x => x.ArrivalTick).DefaultIfEmpty(long.MaxValue).Min();
                fixture.AdvanceTo(nextArrival == long.MaxValue
                    ? fixture.Ai.Strategic.NextDecisionTick : nextArrival);
            }
            Assert.That(fixture.Battles.Battles.Any(x => x.Status == CombatEncounterStatus.Active), Is.True,
                "AI did not reach the defending unit.");
            var firstUnit = fixture.Ai.Strategic.KnownMilitaryUnits.Single();
            var active = fixture.Battles.Battles.Single(x => x.Status == CombatEncounterStatus.Active);
            foreach (var id in fixture.Units.Get(firstUnit).Roster.Where(x => x.IsActive).Select(x => x.CharacterId))
                fixture.Health.ApplyCombatInjury(id, new HealthConditionId("serious_wound"),
                    fixture.Tick, fixture.BiologicalTick, true);
            fixture.AdvanceTo(fixture.Tick + DecisionStep);
            fixture.AdvanceUntil(() => fixture.Ai.Strategic.KnownMilitaryUnits.Any(x => x != firstUnit), 80);
            Assert.That(fixture.Ai.Strategic.KnownMilitaryUnits, Has.None.EqualTo(firstUnit));
            Assert.That(fixture.Ai.Strategic.KnownMilitaryUnits.Count, Is.EqualTo(1));
            Assert.That(fixture.Units.Get(firstUnit).Status, Is.EqualTo(MilitaryUnitStatus.Disbanded));
        }

        [Test]
        public void SchedulerHostsTwoIndependentStatesWithoutSharedEconomy()
        {
            var fixture = Fixture.Create(26008);
            var playerAi = fixture.CreatePlayerAi();
            fixture.Scheduler.AddFaction(playerAi);
            fixture.AdvanceTo(TimeSpan.FromDays(1).Ticks);
            Assert.That(fixture.Scheduler.Factions.Count, Is.EqualTo(2));
            Assert.That(fixture.Scheduler.Factions.Select(x => x.Strategic.Id).Distinct().Count(), Is.EqualTo(2));
            Assert.That(playerAi.Inventories, Is.Not.SameAs(fixture.Ai.Inventories));
            Assert.That(playerAi.Strategic.DecisionSequence, Is.GreaterThan(0));
            Assert.That(fixture.Ai.Strategic.DecisionSequence, Is.GreaterThan(0));
        }

        [Test]
        public void HealthRoutesDeathsToMatchingDynastyAuthority()
        {
            var fixture = Fixture.Create(26009);
            var aiRuler = fixture.Ai.Strategic.RulerCharacterId;
            var playerRuler = fixture.PlayerIds[0];
            fixture.Health.ApplyCombatInjury(aiRuler, new HealthConditionId("serious_wound"), 0,
                AdultBiologicalTick, true);
            Assert.That(fixture.Ai.Dynasty.History, Has.Count.EqualTo(1));
            Assert.That(fixture.PlayerDynasty.History, Is.Empty);
            fixture.Health.ApplyCombatInjury(playerRuler, new HealthConditionId("serious_wound"), 0,
                AdultBiologicalTick, true);
            Assert.That(fixture.PlayerDynasty.History, Has.Count.EqualTo(1));
            Assert.That(fixture.Ai.Dynasty.History, Has.Count.EqualTo(1));
        }

        private static AiDecisionFacts Facts(bool economyShortage = false, bool hasResearch = false,
            bool canConstruct = false, bool hasArmy = false, bool hasMilitaryCandidates = false,
            bool hasTarget = false, bool isTravelling = false, bool hasActiveBattle = false,
            bool canCapture = false, bool needsCaptain = false) => new AiDecisionFacts(economyShortage,
                hasResearch, canConstruct, hasArmy, hasMilitaryCandidates, hasTarget, isTravelling,
                hasActiveBattle, canCapture, needsCaptain);

        private static string Digest(Fixture fixture)
        {
            var state = fixture.Ai.Strategic.CaptureState();
            return string.Join("|", state.DecisionSequence, state.Goal,
                string.Join(",", state.RecentDecisions.Select(x => x.Kind)),
                string.Join(",", fixture.Buildings.All.OrderBy(x => x.Id).Select(x => x.Id + ":" + x.OwnerSideId)),
                string.Join(",", state.KnownMilitaryUnits), fixture.Map.ComputeStateHash(),
                fixture.Ai.Research.ComputeStateHash(), fixture.Ai.Production.ComputeStateHash());
        }

        private static AiStrategicStateSnapshot Copy(AiStrategicStateSnapshot value,
            SettlementId? targetSettlement = null) => new AiStrategicStateSnapshot(value.Id, value.OwnerId,
                value.SideId, value.HomeSettlementId, value.RulerCharacterId, value.ControlledSettlements,
                value.KnownMilitaryUnits, value.Goal, targetSettlement ?? value.TargetSettlementId,
                value.TargetLocationId, value.LastDecisionTick, value.NextDecisionTick, value.DecisionSequence,
                value.PendingOrders, value.RecentDecisions, value.IsDefeated, value.DefeatReason);

        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));

        private sealed class Fixture
        {
            public const long SeedOffset = 910000;
            public long Seed;
            public long Tick;
            public long BiologicalTick = AdultBiologicalTick;
            public CharacterRegistry Characters;
            public HealthAndFamilyService Health;
            public TierManager Tiers;
            public CombatService Combat;
            public MilitaryUnitService Units;
            public BattleResolutionService Battles;
            public GlobalMapService Map;
            public ResourceCatalog Resources;
            public ProductionCatalog Recipes;
            public TechnologyCatalog Technologies;
            public BuildingRegistry Buildings;
            public BuildingOccupancyGrid Occupancy;
            public BuildingPlacementService Placement;
            public BuildingDamageService Damage;
            public AiFactionRuntime Ai;
            public AiStrategicScheduler Scheduler;
            public ResourceInventoryRegistry PlayerInventories;
            public ResearchService PlayerResearch;
            public ProductionService PlayerProduction;
            public WorldPileService PlayerPiles;
            public DynastyState PlayerDynasty;
            public SuccessionService PlayerSuccession;
            public StableEntityId[] PlayerIds;
            public MilitaryUnitId PlayerUnitId;
            public BuildingDefinition Shelter;
            public BuildingDefinition Storage;
            public BuildingDefinition Crop;
            public BuildingDefinition Uncapturable;
            public CombatSideId PlayerSide = new CombatSideId("player");
            public CombatSideId EnemySide = new CombatSideId("enemy");
            public SettlementId PlayerSettlement;
            public SettlementId EnemySettlement;

            public static Fixture Create(long seed, bool defender = false)
            {
                var value = new Fixture { Seed = seed };
                value.Characters = new CharacterRegistry();
                value.Health = new HealthAndFamilyService(value.Characters,
                    HealthConditionCatalog.CreateStoneAge(), seed);
                var tier1 = new MemoryTierAdapter(CharacterSimulationTier.Tier1);
                var tier2 = new MemoryTierAdapter(CharacterSimulationTier.Tier2);
                var tier3 = new MemoryTierAdapter(CharacterSimulationTier.Tier3);
                value.Tiers = new TierManager(value.Characters, tier1, tier2, tier3);
                var positions = new MemoryPositions();
                value.Combat = new CombatService(value.Characters, value.Health, positions, seed, value.Tiers);
                value.Tiers.BindTransitionGuard(value.Combat);
                value.Units = new MilitaryUnitService(value.Characters, value.Health);
                var representations = new MilitaryUnitRepresentationService(value.Units, value.Tiers,
                    value.Combat, value.Characters, value.Health);
                var unitCombat = new MilitaryUnitCombatService(value.Units, value.Combat);
                value.Battles = new BattleResolutionService(value.Characters, value.Combat, value.Units,
                    representations, unitCombat);
                value.Map = new GlobalMapService(StarterGlobalMap.Create(), value.Units, value.Battles);
                value.PlayerSettlement = value.Map.GetSettlementAt(new WorldLocationId("starter_settlement")).Id;
                value.EnemySettlement = value.Map.GetSettlementAt(new WorldLocationId("enemy_settlement")).Id;

                value.Resources = CreateResources();
                value.Recipes = ProductionCatalog.CreateStoneAge(value.Resources);
                value.Shelter = Definition("primitive_shelter", BuildingCategory.Shelter, 4, 3,
                    ("wood_log", 8), ("plant_fiber", 4));
                value.Storage = Definition("storage_basket", BuildingCategory.Storage, 2, 1,
                    ("wood_log", 6), ("plant_fiber", 4));
                value.Crop = Definition("primitive_crop_plot", BuildingCategory.Structure, 3, 3,
                    ("wood_log", 4), ("stone", 2));
                value.Uncapturable = new BuildingDefinition(new BuildingDefinitionId("ritual_stone"),
                    "Ritual Stone", BuildingCategory.Structure, new BuildingFootprint(1, 1), capturable: false);
                var definitions = new[] { value.Shelter, value.Storage, value.Crop, value.Uncapturable };
                value.Technologies = TechnologyCatalog.CreateStoneAge(value.Recipes,
                    new[] { value.Shelter.Id, value.Storage.Id, value.Crop.Id });
                value.Buildings = new BuildingRegistry();
                value.Occupancy = new BuildingOccupancyGrid();
                value.Placement = new BuildingPlacementService(value.Buildings, value.Occupancy,
                    new BuildingGridBounds(-50, -50, 50, 50));

                value.PlayerIds = Enumerable.Range(0, 4).Select(i => Id((int)(SeedOffset + seed % 10000 + i))).ToArray();
                var playerDynastyId = Id((int)(SeedOffset + seed % 10000 + 100));
                for (var i = 0; i < value.PlayerIds.Length; i++)
                {
                    var character = MakeCharacter(value.PlayerIds[i], "Player" + i, playerDynastyId,
                        15 + i, 25 + i);
                    value.Characters.Add(character);
                    value.Tiers.Materialize(character, CharacterSimulationTier.Tier3,
                        new CharacterRuntimeState(character.Id, default, default, false,
                            "starter_settlement", 0, AdultBiologicalTick));
                    positions.Set(character.Id, default);
                }
                value.PlayerDynasty = DynastyState.Create(playerDynastyId, "House Player", value.PlayerIds[0],
                    value.PlayerIds[0], value.PlayerIds);
                value.PlayerSuccession = new SuccessionService(value.Characters, value.PlayerDynasty,
                    new PlayerRealmState(value.PlayerIds[0]), new GameRunState());
                value.Health.BindSuccession(value.PlayerSuccession);
                value.PlayerInventories = new ResourceInventoryRegistry();
                foreach (var id in value.PlayerIds)
                    value.PlayerInventories.Add(new ResourceInventory(new InventoryOwner(
                        InventoryOwnerKind.Character, id), value.Resources, 128));
                SeedInventory(value.PlayerInventories.All.First(), 20);
                value.PlayerResearch = new ResearchService(value.Technologies,
                    new CharacterResearchRateProvider(value.Characters, value.Health));
                value.PlayerPiles = new WorldPileService(value.Resources, value.PlayerInventories);
                value.PlayerProduction = new ProductionService(value.Recipes, value.Resources,
                    value.PlayerInventories, value.Buildings, new ResourceTransferService(value.PlayerInventories),
                    researchUnlocks: value.PlayerResearch, capabilityProvider: value.Health);
                var weather = new WeatherService(seed, ClimateProfile.Temperate, value.Health);
                value.Damage = new BuildingDamageService(value.Buildings, definitions, value.PlayerProduction,
                    value.PlayerInventories, value.PlayerPiles, weather, value.Combat, value.Occupancy);
                value.Ai = AiRuntimeBootstrap.CreateStarterEnemy(seed, 0, AdultBiologicalTick,
                    value.Characters, value.Health, value.Tiers, value.Map, value.Buildings, value.Placement,
                    value.Resources, value.Recipes, value.Technologies, definitions, weather);
                value.Health.BindSuccession(value.Ai.Succession);
                value.Damage.RegisterEconomyScope(value.Ai.Production, value.Ai.Inventories, value.Ai.WorldPiles);
                value.SyncSettlement(value.PlayerSettlement, value.PlayerSide, value.PlayerIds, Array.Empty<StableEntityId>());
                value.Scheduler = new AiStrategicScheduler(seed, value.Characters, value.Health, value.Units,
                    value.Map, value.Battles, value.Buildings, value.Damage);
                value.Scheduler.AddFaction(value.Ai);
                if (defender) value.CreatePlayerDefender();
                return value;
            }

            public void AdvanceTo(long targetTick)
            {
                Tick = targetTick;
                BiologicalTick = checked(AdultBiologicalTick + targetTick * 400L);
                Scheduler.AdvanceTo(Tick, BiologicalTick);
            }

            public void AdvanceUntil(Func<bool> condition, int maximumSteps)
            {
                for (var i = 0; i < maximumSteps && !condition(); i++) AdvanceTo(Tick + DecisionStep);
                Assert.That(condition(), Is.True, "U26 fixture did not reach the expected canonical state.");
            }

            public void SyncSettlement(SettlementId id, CombatSideId side, IEnumerable<StableEntityId> residents,
                IEnumerable<StableEntityId> buildingIds)
            {
                var current = Map.Settlements.Single(x => x.Id == id);
                Map.SynchronizeSettlement(id, current.LocationId, side, buildingIds, residents);
            }

            public Building AddCompletedBuilding(BuildingDefinition definition, CombatSideId side,
                GridCoordinate coordinate, StableEntityId id)
            {
                var building = Building.Restore(new BuildingState(id, definition.Id, coordinate,
                    BuildingOrientation.North, ConstructionState.Completed, definition.MaxIntegrity,
                    definition.MaxIntegrity, BuildingStructuralStatus.Intact, side, BuildingFireStatus.None,
                    0, 0, 0, 0, false, false, null, null));
                Buildings.Add(building);
                Occupancy.Reserve(definition.Footprint.EnumerateCells(coordinate, BuildingOrientation.North),
                    id, definition.AllowsOverlap);
                return building;
            }

            public CombatEncounterState ForcePlayerVictoryAtEnemy()
            {
                if (!Units.TryGet(PlayerUnitId, out _)) CreatePlayerDefender();
                Units.SetAbstractLocation(PlayerUnitId, "enemy_settlement");
                var aiUnit = Ai.Strategic.KnownMilitaryUnits.Single();
                var battle = Battles.StartAbstractBattle(aiUnit, PlayerUnitId,
                    Id((int)(SeedOffset + Seed % 10000 + 500)), Tick);
                foreach (var id in Units.Get(aiUnit).Roster.Where(x => x.IsActive).Select(x => x.CharacterId))
                    Health.ApplyCombatInjury(id, new HealthConditionId("serious_wound"), Tick,
                        BiologicalTick, true);
                Battles.AdvanceTo(Tick + TimeSpan.FromMinutes(1).Ticks, BiologicalTick + 1);
                return Battles.Battles.Single(x => x.BattleId == battle.BattleId);
            }

            public AiFactionRuntime CreatePlayerAi()
            {
                var state = AiStrategicState.Create(new AiStateId(Id((int)(SeedOffset + Seed % 10000 + 700))),
                    "player_ai", PlayerSide, PlayerSettlement, PlayerIds[0], Tick);
                var construction = new ConstructionFundingService(Placement, PlayerInventories, Resources,
                    new StorageService(PlayerInventories, Resources), PlayerResearch);
                return new AiFactionRuntime(state, PlayerDynasty, PlayerSuccession, PlayerResearch,
                    PlayerProduction, PlayerInventories, PlayerPiles, construction,
                    new[] { Shelter, Storage, Crop, Uncapturable }, PlayerIds);
            }

            public void CreatePlayerDefender()
            {
                PlayerUnitId = new MilitaryUnitId(Id((int)(SeedOffset + Seed % 10000 + 400)));
                Units.Create(PlayerUnitId, new MilitarySideId(PlayerSide.Value), new[] { PlayerIds[0] },
                    PlayerIds[0], MilitaryUnitRepresentationMode.Aggregate, "starter_settlement");
            }
        }

        private static ResourceCatalog CreateResources() => new ResourceCatalog(new[]
        {
            new ResourceDefinition(new ResourceId("wood_log"), "Wood", ResourceCategory.Construction),
            new ResourceDefinition(new ResourceId("stone"), "Stone", ResourceCategory.Construction),
            new ResourceDefinition(new ResourceId("plant_fiber"), "Fiber", ResourceCategory.Construction),
            new ResourceDefinition(new ResourceId("hide"), "Hide", ResourceCategory.Other),
            new ResourceDefinition(new ResourceId("berries"), "Berries", ResourceCategory.Food)
        });

        private static BuildingDefinition Definition(string id, BuildingCategory category, int width, int height,
            params (string id, long amount)[] costs) => new BuildingDefinition(new BuildingDefinitionId(id), id,
                category, new BuildingFootprint(width, height), constructionCost: costs.Select(x =>
                    new ResourceAmount(new ResourceId(x.id), new ResourceQuantity(x.amount))),
                storageCapacity: category == BuildingCategory.Storage ? 100 : 0);

        private static Character MakeCharacter(StableEntityId id, string name, StableEntityId dynasty,
            int combat, int influence) => Character.Restore(new CharacterState(id, new CharacterName(name),
                CharacterSex.Male, 0, CharacterLifeState.Alive, null, null, null, dynasty, dynasty,
                new ProfessionId("worker"), new[] { new TraitId("calm"), new TraitId("brave"),
                    new TraitId("steady"), new TraitId("curious") },
                new[] { new SkillState(new SkillId("combat"), new SkillLevel(combat)),
                    new SkillState(new SkillId("research"), new SkillLevel(20)) },
                new CharacterAttributes(new CharacterAttributeValue(30), new CharacterAttributeValue(25)),
                BodyHealth.Healthy, new Wealth(0), new Influence(influence), null));

        private static void SeedInventory(ResourceInventory inventory, long amount)
        {
            foreach (var id in new[] { "berries", "wood_log", "plant_fiber", "stone" })
                Assert.That(inventory.TryAdd(new ResourceId(id), new ResourceQuantity(amount)), Is.True);
        }

        private sealed class MemoryPositions : ICombatPositionProvider
        {
            private readonly Dictionary<StableEntityId, WorldPosition> values =
                new Dictionary<StableEntityId, WorldPosition>();
            public void Set(StableEntityId id, WorldPosition position) => values[id] = position;
            public bool TryGetPosition(StableEntityId id, out WorldPosition position) => values.TryGetValue(id, out position);
            public void MoveTo(StableEntityId id, WorldPosition target, float stoppingDistance) => values[id] = target;
            public void Stop(StableEntityId id) { }
        }

        private sealed class MemoryTierAdapter : ICharacterTierAdapter
        {
            private readonly Dictionary<StableEntityId, CharacterRuntimeState> states =
                new Dictionary<StableEntityId, CharacterRuntimeState>();
            public MemoryTierAdapter(CharacterSimulationTier tier) { Tier = tier; }
            public CharacterSimulationTier Tier { get; }
            public bool IsActive(StableEntityId id) => states.ContainsKey(id);
            public CharacterRuntimeState Capture(StableEntityId id) => states[id];
            public void ValidateMaterialization(Character character, CharacterRuntimeState state)
            {
                if (character.Id != state.CharacterId || states.ContainsKey(character.Id))
                    throw new InvalidOperationException();
            }
            public void Materialize(Character character, CharacterRuntimeState state) => states.Add(character.Id, state);
            public void Dematerialize(StableEntityId id)
            {
                if (!states.Remove(id)) throw new InvalidOperationException();
            }
        }
    }
}
