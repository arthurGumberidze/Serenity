using System;
using System.Linq;
using NUnit.Framework;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Campaign;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Domestic;
using Game.Domain.Health;
using Game.Domain.Time;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Simulation.Domestic;
using Game.Simulation.Health;

namespace Game.Tests.Editor
{
    public sealed class P106DomesticAndSaveTests
    {
        [Test]
        public void Sleep_requires_completed_closed_habitable_access_and_assigned_indoor_bed()
        {
            var fixture = CreateFixture(completed: false);
            Assert.That(fixture.Domestic.EvaluateSleep(fixture.Man.Id).Reason, Is.EqualTo(SleepDenialReason.HomeIncomplete));

            fixture.HomeBuilding.MarkCompleted();
            Assert.That(fixture.Domestic.EvaluateSleep(fixture.Man.Id).Allowed, Is.True);
            Assert.That(fixture.Domestic.BeginSleep(fixture.Man.Id).Allowed, Is.True);
            Assert.That(fixture.Domestic.EvaluateSleepAtBed(fixture.Woman.Id, fixture.Bed.Id).Reason,
                Is.EqualTo(SleepDenialReason.BedAssignedToAnotherResident));
            fixture.Domestic.EndSleep(fixture.Man.Id);

            var outdoor = new BedState(StableEntityId.NewId(), fixture.HomeBuilding.Id,
                new GridCoordinate(50, 50), new WorldPosition(50, 0, 50), fixture.Woman.Id);
            fixture.Domestic.RegisterBed(outdoor);
            Assert.That(fixture.Domestic.EvaluateSleepAtBed(fixture.Woman.Id, outdoor.Id).Reason,
                Is.EqualTo(SleepDenialReason.BedOutsideHome));
        }

        [Test]
        public void Occupied_and_foreign_beds_report_explicit_denials()
        {
            var fixture = CreateFixture();
            Assert.That(fixture.Domestic.BeginSleep(fixture.Man.Id).Allowed, Is.True);
            Assert.That(fixture.Domestic.EvaluateSleepAtBed(fixture.Woman.Id, fixture.Bed.Id).Reason,
                Is.EqualTo(SleepDenialReason.BedAssignedToAnotherResident));

            fixture.Domestic.AddResident(fixture.HomeBuilding.Id, fixture.Stranger.Id);
            var shared = new BedState(StableEntityId.NewId(), fixture.HomeBuilding.Id,
                new GridCoordinate(1, 1), new WorldPosition(1, 0, 1), fixture.Stranger.Id, fixture.Man.Id);
            fixture.Domestic.RegisterBed(shared);
            Assert.That(fixture.Domestic.EvaluateSleepAtBed(fixture.Stranger.Id, shared.Id).Reason,
                Is.EqualTo(SleepDenialReason.BedOccupied));
        }

        [Test]
        public void Family_interaction_requires_adults_and_two_independent_consents_then_creates_pregnancy()
        {
            var fixture = CreateFixture();
            var biologicalTick = Character.BiologicalTicksPerYear * 25L;
            Assert.That(fixture.Domestic.EvaluateFamilyInteraction(fixture.Man.Id, fixture.Woman.Id, biologicalTick).Reason,
                Is.EqualTo(FamilyInteractionDenialReason.ConsentMissing));
            fixture.Domestic.SetConsent(fixture.Man.Id, fixture.Woman.Id, true);
            Assert.That(fixture.Domestic.EvaluateFamilyInteraction(fixture.Man.Id, fixture.Woman.Id, biologicalTick).Reason,
                Is.EqualTo(FamilyInteractionDenialReason.ConsentMissing));
            fixture.Domestic.SetConsent(fixture.Woman.Id, fixture.Man.Id, true);
            var interaction = fixture.Domestic.StartFamilyInteraction(fixture.Man.Id, fixture.Woman.Id,
                TimeSpan.FromDays(1).Ticks, biologicalTick);
            Assert.That(interaction.PregnancyId.IsValid, Is.True);
            Assert.That(fixture.Health.Pregnancies.Single().Status, Is.EqualTo(PregnancyStatus.Active));
        }

        [Test]
        public void Minor_is_rejected_even_when_both_consent()
        {
            var fixture = CreateFixture(womanBirthTick: Character.BiologicalTicksPerYear * 15L);
            var now = Character.BiologicalTicksPerYear * 25L;
            fixture.Domestic.SetConsent(fixture.Man.Id, fixture.Woman.Id, true);
            fixture.Domestic.SetConsent(fixture.Woman.Id, fixture.Man.Id, true);
            Assert.That(fixture.Domestic.EvaluateFamilyInteraction(fixture.Man.Id, fixture.Woman.Id, now).Reason,
                Is.EqualTo(FamilyInteractionDenialReason.NotAdult));
        }

        [Test]
        public void V23_codec_round_trips_domestic_runtime_and_rejects_missing_required_section()
        {
            var fixture = CreateFixture();
            fixture.Domestic.SetConsent(fixture.Man.Id, fixture.Woman.Id, true);
            var runtime = new CharacterRuntimeWorldSaveState(new[]
            {
                new CharacterRuntimeSaveEntry(CharacterSimulationTier.Tier1,
                    new CharacterRuntimeState(fixture.Man.Id, new WorldPosition(2, 0, 3), default, false,
                        hunger: 0.72, energy: 0.19))
            });
            var connected = new ConnectedWorldSaveState(ConnectedWorldContract.GenerationId,
                Array.Empty<WorldDiscoveryCell>(), Array.Empty<ScoutingAssignmentState>(), 0);
            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, StableEntityId.NewId(),
                new GameTimeState(10, 20, 1, false), 400, connectedWorld: connected,
                domestic: fixture.Domestic.CaptureState(), characterRuntime: runtime);
            var codec = new SaveXmlCodec();
            var bytes = codec.Serialize(snapshot);
            var restored = codec.Deserialize(bytes);
            Assert.That(restored.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentVersion));
            Assert.That(restored.ConnectedWorld.GenerationId, Is.EqualTo(ConnectedWorldContract.GenerationId));
            Assert.That(restored.Domestic.Homes.Count, Is.EqualTo(1));
            Assert.That(restored.Domestic.Beds.Single().AssignedResidentId, Is.EqualTo(fixture.Man.Id));
            Assert.That(restored.CharacterRuntime.Entries.Single().State.Hunger, Is.EqualTo(0.72));

            var xml = System.Text.Encoding.UTF8.GetString(bytes)
                .Replace("<domestic>", "<domesticMissing>").Replace("</domestic>", "</domesticMissing>");
            Assert.That(() => codec.Deserialize(System.Text.Encoding.UTF8.GetBytes(xml)),
                Throws.TypeOf<System.IO.InvalidDataException>());
        }

        private static Fixture CreateFixture(bool completed = true, long womanBirthTick = 0)
        {
            var characters = new CharacterRegistry();
            var man = CreateCharacter("Aren", CharacterSex.Male, 0);
            var woman = CreateCharacter("Mira", CharacterSex.Female, womanBirthTick);
            var stranger = CreateCharacter("Orin", CharacterSex.Male, 0);
            characters.Add(man); characters.Add(woman); characters.Add(stranger);
            characters.LinkSpouses(man.Id, woman.Id);
            var buildings = new BuildingRegistry();
            var definition = new BuildingDefinition(new BuildingDefinitionId("primitive_shelter"), "Shelter",
                BuildingCategory.Shelter, new BuildingFootprint(4, 3));
            var building = Building.CreateNew(definition, new GridCoordinate(0, 0), BuildingOrientation.North,
                new CombatSideId("player"), StableEntityId.NewId());
            if (completed) building.MarkCompleted();
            buildings.Add(building);
            var health = new HealthAndFamilyService(characters, HealthConditionCatalog.CreateStoneAge(), 106);
            var domestic = new DomesticService(characters, buildings, health);
            domestic.RegisterHome(new HomeState(building.Id, man.Id, new IndoorZoneState(0, 0, 3, 2, true, true),
                new[] { woman.Id }));
            var bed = new BedState(StableEntityId.NewId(), building.Id, new GridCoordinate(1, 1),
                new WorldPosition(1, 0, 1));
            domestic.RegisterBed(bed);
            domestic.AssignBed(bed.Id, man.Id);
            return new Fixture(man, woman, stranger, building, bed, health, domestic);
        }

        private static Character CreateCharacter(string name, CharacterSex sex, long birthTick) =>
            Character.CreateNew(new CharacterCreationData(new CharacterName(name), sex, birthTick,
                null, null, null, null, null,
                new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
                null, new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
                BodyHealth.Healthy, new Wealth(0), new Influence(0), null));

        private sealed class Fixture
        {
            public Fixture(Character man, Character woman, Character stranger, Building homeBuilding, BedState bed,
                HealthAndFamilyService health, DomesticService domestic)
            { Man = man; Woman = woman; Stranger = stranger; HomeBuilding = homeBuilding; Bed = bed; Health = health; Domestic = domestic; }
            public Character Man { get; }
            public Character Woman { get; }
            public Character Stranger { get; }
            public Building HomeBuilding { get; }
            public BedState Bed { get; }
            public HealthAndFamilyService Health { get; }
            public DomesticService Domestic { get; }
        }
    }
}
