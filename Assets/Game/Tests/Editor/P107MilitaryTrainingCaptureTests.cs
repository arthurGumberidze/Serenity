using System;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Campaign;
using Game.Domain.Combat;
using Game.Domain.Health;
using Game.Domain.Military;
using Game.Domain.Resources;
using Game.Domain.Time;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Simulation.Buildings;
using Game.Simulation.Characters;
using Game.Simulation.Military;
using Game.Simulation.World;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("P10.7")]
    public sealed class P107MilitaryTrainingCaptureTests
    {
        [Test]
        public void BarracksTrainsExistingAdultRepeatedlyWithIncreasingCurveAndMilitaryClothing()
        {
            var fixture = Fixture.Create();
            var identity = fixture.Soldier;
            var first = fixture.Training.TryStart(identity.Id, fixture.Barracks.Id, new CombatSideId("player"),
                0, fixture.Inventories.All.Select(x => x.Owner));
            Assert.That(first.Started, Is.True);
            Assert.That(first.Record.Rank, Is.EqualTo(MilitaryRank.Recruit));
            Assert.That(first.Record.CompletionTick, Is.EqualTo(TimeSpan.FromHours(4).Ticks));
            fixture.Training.AdvanceTo(first.Record.CompletionTick);

            Assert.That(fixture.Characters.Get(identity.Id), Is.SameAs(identity));
            Assert.That(fixture.Characters.Count, Is.EqualTo(3));
            Assert.That(fixture.Training.GetRank(identity.Id), Is.EqualTo(MilitaryRank.Recruit));
            Assert.That(identity.TryGetSkill(MilitaryTrainingService.CombatSkillId, out var skill), Is.True);
            Assert.That(skill.Level.Value, Is.EqualTo(20));
            Assert.That(fixture.Appearances.Get(identity.Id).ClothingId,
                Is.EqualTo(StoneAgeAppearanceProfiles.MilitaryClothing));
            Assert.That(fixture.Units.TryGetForMember(identity.Id, out var unit), Is.True);
            Assert.That(unit.ActiveMemberIds, Does.Contain(identity.Id));

            var second = fixture.Training.TryStart(identity.Id, fixture.Barracks.Id, new CombatSideId("player"),
                first.Record.CompletionTick, fixture.Inventories.All.Select(x => x.Owner));
            Assert.That(second.Started, Is.True);
            Assert.That(second.Record.Rank, Is.EqualTo(MilitaryRank.Soldier));
            Assert.That(second.Record.CompletionTick - second.Record.StartTick,
                Is.GreaterThan(first.Record.CompletionTick - first.Record.StartTick));
            Assert.That(second.Record.CompletionTick - second.Record.StartTick, Is.EqualTo(TimeSpan.FromHours(8).Ticks));
            fixture.Training.AdvanceTo(second.Record.CompletionTick);
            Assert.That(fixture.Training.GetRank(identity.Id), Is.EqualTo(MilitaryRank.Soldier));
            Assert.That(identity.TryGetSkill(MilitaryTrainingService.CombatSkillId, out skill), Is.True);
            Assert.That(skill.Level.Value, Is.EqualTo(40));
            Assert.That(fixture.Units.Get(unit.Id).ActiveMemberIds.Count(x => x == identity.Id), Is.EqualTo(1));
            Assert.That(fixture.Funds.GetAmount(new ResourceId("berries")).Units, Is.EqualTo(24));
            Assert.That(fixture.Funds.GetAmount(new ResourceId("wood_log")).Units, Is.EqualTo(17));
        }

        [Test]
        public void TrainingRejectsChildInactiveBarracksRepeatWhileBusyAndMissingResources()
        {
            var fixture = Fixture.Create();
            Assert.That(fixture.Training.TryStart(fixture.Child.Id, fixture.Barracks.Id,
                new CombatSideId("player"), 0, fixture.Inventories.All.Select(x => x.Owner)).Failure,
                Is.EqualTo(MilitaryTrainingStartFailure.NotAdult));
            var first = fixture.Training.TryStart(fixture.Soldier.Id, fixture.Barracks.Id,
                new CombatSideId("player"), 0, fixture.Inventories.All.Select(x => x.Owner));
            Assert.That(fixture.Training.TryStart(fixture.Soldier.Id, fixture.Barracks.Id,
                new CombatSideId("player"), 0, fixture.Inventories.All.Select(x => x.Owner)).Failure,
                Is.EqualTo(MilitaryTrainingStartFailure.AlreadyTraining));
            fixture.Training.AdvanceTo(first.Record.CompletionTick);
            fixture.Funds.TryRemove(new ResourceId("berries"),
                fixture.Funds.GetAmount(new ResourceId("berries")));
            Assert.That(fixture.Training.TryStart(fixture.Soldier.Id, fixture.Barracks.Id,
                new CombatSideId("player"), first.Record.CompletionTick,
                fixture.Inventories.All.Select(x => x.Owner)).Failure,
                Is.EqualTo(MilitaryTrainingStartFailure.InsufficientResources));
        }

        [Test]
        public void DirectTargetMatrixAllowsOnlyMilitaryEnemyMilitaryBuildingOrArea()
        {
            Assert.That(MilitaryDirectTargetRules.Evaluate(true, true, true,
                MilitaryDirectTargetKind.EnemyMilitary, true, true, true, true).Enabled, Is.True);
            Assert.That(MilitaryDirectTargetRules.Evaluate(true, true, true,
                MilitaryDirectTargetKind.EnemyMilitaryBuilding, true, true, true, true).Enabled, Is.True);
            Assert.That(MilitaryDirectTargetRules.Evaluate(true, true, true,
                MilitaryDirectTargetKind.Area, true, true, true, true).Enabled, Is.True);
            Assert.That(MilitaryDirectTargetRules.Evaluate(true, false, true,
                MilitaryDirectTargetKind.Area, true, true, true, true).Reason,
                Is.EqualTo(MilitaryAttackDisabledReason.AttackerIsCivilian));
            Assert.That(MilitaryDirectTargetRules.Evaluate(true, true, true,
                MilitaryDirectTargetKind.Civilian, true, true, true, true).Reason,
                Is.EqualTo(MilitaryAttackDisabledReason.CivilianDirectTargetForbidden));
            Assert.That(MilitaryDirectTargetRules.Evaluate(true, true, true,
                MilitaryDirectTargetKind.EnemyMilitaryBuilding, true, true, false, true).Reason,
                Is.EqualTo(MilitaryAttackDisabledReason.NonMilitaryBuilding));
            Assert.That(MilitaryDirectTargetRules.Evaluate(true, true, true,
                MilitaryDirectTargetKind.EnemyMilitary, true, false, true, true).Reason,
                Is.EqualTo(MilitaryAttackDisabledReason.FriendlyTarget));
        }

        [Test]
        public void CaptureRequiresBothFlagsFreezesOnContestResetsOnRecaptureAndFallsBackAfterBarracksDestroyed()
        {
            var fixture = Fixture.Create();
            var capture = fixture.CreateCapture(TimeSpan.FromHours(5).Ticks);
            var enemyUnit = fixture.Units.Create(new MilitarySideId("enemy"),
                new[] { fixture.Enemy.Id }, fixture.Enemy.Id);
            Assert.That(capture.TryClaimFlag(fixture.SettlementId, fixture.Main.Id, enemyUnit.Id, 0), Is.True);
            capture.AdvanceTo(TimeSpan.FromHours(1).Ticks, Character.BiologicalTicksPerYear * 20L);
            Assert.That(capture.Get(fixture.SettlementId).ProgressTicks, Is.Zero);

            Assert.That(capture.TryClaimFlag(fixture.SettlementId, fixture.Barracks.Id, enemyUnit.Id,
                TimeSpan.FromHours(1).Ticks), Is.True);
            capture.AdvanceTo(TimeSpan.FromHours(2).Ticks, Character.BiologicalTicksPerYear * 20L);
            Assert.That(capture.Get(fixture.SettlementId).ProgressTicks, Is.EqualTo(TimeSpan.FromHours(1).Ticks));
            capture.AdvanceTo(TimeSpan.FromHours(3).Ticks, Character.BiologicalTicksPerYear * 20L,
                (id, side) => true);
            Assert.That(capture.Get(fixture.SettlementId).Status, Is.EqualTo(SettlementCaptureClockStatus.Frozen));
            Assert.That(capture.Get(fixture.SettlementId).ProgressTicks, Is.EqualTo(TimeSpan.FromHours(1).Ticks));

            fixture.Main.Capture(new CombatSideId("player"), TimeSpan.FromHours(3).Ticks);
            capture.AdvanceTo(TimeSpan.FromHours(4).Ticks, Character.BiologicalTicksPerYear * 20L);
            Assert.That(capture.Get(fixture.SettlementId).ProgressTicks, Is.Zero);
            fixture.Main.Capture(new CombatSideId("enemy"), TimeSpan.FromHours(4).Ticks);
            fixture.Barracks.ApplyDamage(StableEntityId.NewId(), fixture.Barracks.CurrentIntegrity,
                BuildingDamageType.Combat, TimeSpan.FromHours(4).Ticks);
            capture.AdvanceTo(TimeSpan.FromHours(9).Ticks, Character.BiologicalTicksPerYear * 20L);
            Assert.That(capture.Get(fixture.SettlementId).ActiveBarracksId, Is.Null);
            Assert.That(capture.Get(fixture.SettlementId).Status, Is.EqualTo(SettlementCaptureClockStatus.Completed));
            Assert.That(fixture.Map.GetSettlementAt(new WorldLocationId("starter_settlement")).OwnerSideId,
                Is.EqualTo(new CombatSideId("enemy")));
        }

        [Test]
        public void PhysicalMilitaryPresenceClaimsOnlyNearbyCaptureFlags()
        {
            var fixture = Fixture.Create();
            var capture = fixture.CreateCapture(TimeSpan.FromHours(5).Ticks);
            var enemyUnit = fixture.Units.Create(new MilitarySideId("enemy"),
                new[] { fixture.Enemy.Id }, fixture.Enemy.Id);
            Assert.That(capture.TryClaimFlagsNear(enemyUnit.Id, new WorldPosition(0, 0, 0), 2f, 0), Is.EqualTo(1));
            Assert.That(fixture.Main.OwnerSideId, Is.EqualTo(new CombatSideId("enemy")));
            Assert.That(fixture.Barracks.OwnerSideId, Is.EqualTo(new CombatSideId("player")));
            Assert.That(capture.TryClaimFlagsNear(enemyUnit.Id, new WorldPosition(5, 0, 0), 2f, 0), Is.EqualTo(1));
            Assert.That(fixture.Barracks.OwnerSideId, Is.EqualTo(new CombatSideId("enemy")));
        }

        [Test]
        public void CaptureOutcomeAndV24CodecAreDeterministicAndIdempotent()
        {
            var first = Fixture.Create();
            var firstCapture = CompleteCapture(first);
            var outcome = firstCapture.Get(first.SettlementId).Outcome;
            Assert.That(outcome, Is.Not.Null);
            var resolution = outcome.ResolutionId;
            firstCapture.AdvanceTo(TimeSpan.FromHours(12).Ticks, Character.BiologicalTicksPerYear * 20L);
            Assert.That(firstCapture.Get(first.SettlementId).Outcome.ResolutionId, Is.EqualTo(resolution));

            var second = Fixture.Create();
            var secondOutcome = CompleteCapture(second).Get(second.SettlementId).Outcome;
            Assert.That(secondOutcome.JoinedSoldierIds, Is.EqualTo(outcome.JoinedSoldierIds));
            Assert.That(secondOutcome.ExecutedSoldierIds, Is.EqualTo(outcome.ExecutedSoldierIds));

            var campaign = new MilitaryCampaignSaveState(first.Training.CaptureState(), firstCapture.States,
                Array.Empty<MilitaryAttackOrderState>(), 0);
            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, StableEntityId.NewId(),
                new GameTimeState(TimeSpan.FromHours(12).Ticks, Character.BiologicalTicksPerYear * 20L, 1, false), 400,
                militaryCampaign: campaign);
            var codec = new SaveXmlCodec();
            var restored = codec.Deserialize(codec.Serialize(snapshot));
            Assert.That(restored.FormatVersion, Is.EqualTo(SaveSnapshot.MilitaryCampaignVersion));
            Assert.That(restored.MilitaryCampaign.Captures.Single().Outcome.ResolutionId, Is.EqualTo(resolution));
            Assert.That(restored.MilitaryCampaign.Captures.Single().Seed,
                Is.EqualTo(firstCapture.Get(first.SettlementId).Seed));
        }

        private static SettlementFlagCaptureService CompleteCapture(Fixture fixture)
        {
            var capture = fixture.CreateCapture(TimeSpan.FromHours(5).Ticks);
            fixture.Units.Create(new MilitarySideId("player"),
                new[] { fixture.Soldier.Id }, fixture.Soldier.Id);
            var enemy = fixture.Units.Create(new MilitarySideId("enemy"),
                new[] { fixture.Enemy.Id }, fixture.Enemy.Id);
            capture.TryClaimFlag(fixture.SettlementId, fixture.Main.Id, enemy.Id, 0);
            capture.TryClaimFlag(fixture.SettlementId, fixture.Barracks.Id, enemy.Id, 0);
            capture.AdvanceTo(TimeSpan.FromHours(5).Ticks, Character.BiologicalTicksPerYear * 20L);
            return capture;
        }

        private sealed class Fixture
        {
            public CharacterRegistry Characters;
            public Character Soldier;
            public Character Child;
            public Character Enemy;
            public BuildingRegistry Buildings;
            public Building Main;
            public Building Barracks;
            public ResourceInventoryRegistry Inventories;
            public ResourceInventory Funds;
            public CharacterAppearanceService Appearances;
            public MilitaryUnitService Units;
            public MilitaryTrainingService Training;
            public GlobalMapService Map;
            public SettlementId SettlementId;
            public BuildingDefinition MainDefinition;
            public BuildingDefinition BarracksDefinition;

            public static Fixture Create()
            {
                var value = new Fixture();
                value.Characters = new CharacterRegistry();
                value.Soldier = Person("Aren", CharacterSex.Male);
                value.Child = Person("Ira", CharacterSex.Female);
                value.Enemy = Person("Varek", CharacterSex.Male);
                value.Characters.Add(value.Soldier); value.Characters.Add(value.Child); value.Characters.Add(value.Enemy);
                var capabilities = new Capabilities(value.Child.Id);
                value.Buildings = new BuildingRegistry();
                value.MainDefinition = new BuildingDefinition(new BuildingDefinitionId("primitive_shelter"),
                    "Main", BuildingCategory.Shelter, new BuildingFootprint(4, 3));
                value.BarracksDefinition = new BuildingDefinition(MilitaryTrainingService.BarracksDefinitionId,
                    "Barracks", BuildingCategory.Structure, new BuildingFootprint(4, 3));
                value.Main = Building.CreateNew(value.MainDefinition, new GridCoordinate(0, 0), BuildingOrientation.North,
                    new CombatSideId("player"), Id("main"));
                value.Barracks = Building.CreateNew(value.BarracksDefinition, new GridCoordinate(5, 0), BuildingOrientation.North,
                    new CombatSideId("player"), Id("barracks"));
                value.Main.MarkCompleted(); value.Barracks.MarkCompleted();
                value.Buildings.Add(value.Main); value.Buildings.Add(value.Barracks);
                var resources = new ResourceCatalog(new[]
                {
                    new ResourceDefinition(new ResourceId("berries"), "Berries", ResourceCategory.Food),
                    new ResourceDefinition(new ResourceId("wood_log"), "Wood", ResourceCategory.Construction)
                });
                value.Inventories = new ResourceInventoryRegistry();
                value.Funds = new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character, value.Soldier.Id),
                    resources, 100);
                value.Funds.TryAdd(new ResourceId("berries"), new ResourceQuantity(30));
                value.Funds.TryAdd(new ResourceId("wood_log"), new ResourceQuantity(20));
                value.Inventories.Add(value.Funds);
                value.Appearances = new CharacterAppearanceService(value.Characters,
                    StoneAgeAppearanceProfiles.CreateCatalog(), 107);
                value.Units = new MilitaryUnitService(value.Characters, capabilities);
                value.Training = new MilitaryTrainingService(value.Characters, capabilities, value.Buildings,
                    value.Inventories, value.Units, value.Appearances, 107);
                value.Map = new GlobalMapService(StarterGlobalMap.Create(0, 107), value.Units);
                var settlement = value.Map.GetSettlementAt(new WorldLocationId("starter_settlement"));
                value.SettlementId = settlement.Id;
                value.Map.SynchronizeSettlement(settlement.Id, settlement.LocationId, settlement.OwnerSideId,
                    new[] { value.Main.Id, value.Barracks.Id },
                    new[] { value.Soldier.Id, value.Child.Id });
                return value;
            }

            public SettlementFlagCaptureService CreateCapture(long duration)
            {
                var damage = new BuildingDamageService(Buildings, new[] { MainDefinition, BarracksDefinition });
                var capture = new SettlementFlagCaptureService(107, Map, Buildings, damage, Characters, Units);
                capture.Register(SettlementId, Main.Id, 0, duration);
                return capture;
            }

            private static Character Person(string name, CharacterSex sex) => Character.CreateWithId(
                new CharacterCreationData(new CharacterName(name), sex, 0, null, null, null, null, null,
                    new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
                    null, new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
                    BodyHealth.Healthy, new Wealth(0), new Influence(10), null), Id("person:" + name));

            private static StableEntityId Id(string value)
                => DeterministicStableId.Create(107, value);
        }

        private sealed class Capabilities : ICharacterCapabilityProvider
        {
            private readonly StableEntityId child;
            public Capabilities(StableEntityId childId) { child = childId; }
            public CharacterCapabilities GetCapabilities(StableEntityId id) => id == child
                ? new CharacterCapabilities(false, false, 1000, 1000, 1000, 1000, false)
                : new CharacterCapabilities(true, false, 1000, 1000, 1000, 1000, true);
        }
    }
}
