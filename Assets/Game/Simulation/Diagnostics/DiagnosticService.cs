using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Campaign;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Diagnostics;
using Game.Domain.Domestic;
using Game.Domain.Military;
using Game.Domain.Resources;
using Game.Domain.World;
using Game.Simulation.Characters;
using Game.Simulation.Combat;
using Game.Simulation.Domestic;
using Game.Simulation.Health;
using Game.Simulation.Military;
using Game.Simulation.Research;
using Game.Simulation.Tiers;

namespace Game.Simulation.Diagnostics
{
    /// <summary>
    /// Engine-free, owner-thread diagnostic authority. Presentation submits deliberate commands; canonical
    /// services own every resulting technology, inventory, Character, roster, relationship and pregnancy.
    /// </summary>
    public sealed class DiagnosticService
    {
        public const long UnitsPerResource = 10000;
        private readonly long worldSeed;
        private readonly ResearchService research;
        private readonly ResourceCatalog resources;
        private readonly ResourceInventoryRegistry inventories;
        private readonly BuildingRegistry buildings;
        private readonly CharacterRegistry characters;
        private readonly CharacterAppearanceService appearances;
        private readonly HealthAndFamilyService health;
        private readonly DomesticService domestic;
        private readonly MilitaryUnitService units;
        private readonly MilitaryTrainingService training;
        private readonly TierManager tiers;
        private readonly CombatService combat;
        private readonly Func<long> calendarTick;
        private readonly Func<long> biologicalTick;
        private readonly Action<string> faultInjection;
        private bool cheatsUsed;
        private ulong sequence;

        public DiagnosticService(long seed, ResearchService researchService, ResourceCatalog resourceCatalog,
            ResourceInventoryRegistry inventoryRegistry, BuildingRegistry buildingRegistry,
            CharacterRegistry characterRegistry, CharacterAppearanceService appearanceService,
            HealthAndFamilyService healthService, DomesticService domesticService,
            MilitaryUnitService militaryUnits, MilitaryTrainingService militaryTraining,
            TierManager tierManager, CombatService combatService, Func<long> calendarTickProvider,
            Func<long> biologicalTickProvider, Action<string> transactionFaultInjection = null)
        {
            worldSeed = seed;
            research = researchService ?? throw new ArgumentNullException(nameof(researchService));
            resources = resourceCatalog ?? throw new ArgumentNullException(nameof(resourceCatalog));
            inventories = inventoryRegistry ?? throw new ArgumentNullException(nameof(inventoryRegistry));
            buildings = buildingRegistry ?? throw new ArgumentNullException(nameof(buildingRegistry));
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            appearances = appearanceService ?? throw new ArgumentNullException(nameof(appearanceService));
            health = healthService ?? throw new ArgumentNullException(nameof(healthService));
            domestic = domesticService ?? throw new ArgumentNullException(nameof(domesticService));
            units = militaryUnits ?? throw new ArgumentNullException(nameof(militaryUnits));
            training = militaryTraining ?? throw new ArgumentNullException(nameof(militaryTraining));
            tiers = tierManager ?? throw new ArgumentNullException(nameof(tierManager));
            combat = combatService ?? throw new ArgumentNullException(nameof(combatService));
            calendarTick = calendarTickProvider ?? throw new ArgumentNullException(nameof(calendarTickProvider));
            biologicalTick = biologicalTickProvider ?? throw new ArgumentNullException(nameof(biologicalTickProvider));
            faultInjection = transactionFaultInjection;
        }

        public bool CheatsUsed => cheatsUsed;
        public bool AchievementsEnabled => !cheatsUsed;
        public ulong Sequence => sequence;
        public string LastFailureDetail { get; private set; } = string.Empty;

        public DiagnosticExecutionResult Execute(string rawCode)
        {
            var parsed = DiagnosticCommandParser.Parse(rawCode);
            if (!parsed.IsValid) return new DiagnosticExecutionResult(default, parsed.Failure);
            return Execute(parsed.Command);
        }

        public DiagnosticExecutionResult Execute(DiagnosticCommandKind command)
        {
            LastFailureDetail = string.Empty;
            DiagnosticExecutionResult result;
            switch (command)
            {
                case DiagnosticCommandKind.UnlockAll: result = UnlockAll(); break;
                case DiagnosticCommandKind.GrantResources: result = GrantResources(); break;
                case DiagnosticCommandKind.CreateArmy: result = CreateArmy(); break;
                case DiagnosticCommandKind.CreateFamily: result = CreateFamily(); break;
                default: return new DiagnosticExecutionResult(command, DiagnosticFailure.UnknownCode);
            }
            if (result.Succeeded) cheatsUsed = true;
            return result;
        }

        public DiagnosticSaveState CaptureState() => new DiagnosticSaveState(cheatsUsed, sequence);

        public void RestoreState(DiagnosticSaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            // Loading can add evidence of cheats, never erase evidence already established in this party.
            cheatsUsed |= state.CheatsUsed;
            if (state.Sequence > sequence) sequence = state.Sequence;
        }

        private DiagnosticExecutionResult UnlockAll()
        {
            var before = research.CaptureState();
            try
            {
                var count = research.CompleteAllForDiagnostics();
                Fault("unlock_committed");
                sequence = checked(sequence + 1);
                return new DiagnosticExecutionResult(DiagnosticCommandKind.UnlockAll, DiagnosticFailure.None,
                    count);
            }
            catch (Exception exception)
            {
                LastFailureDetail = exception.GetType().Name + ": " + exception.Message;
                research.RestoreState(before);
                return Failed(DiagnosticCommandKind.UnlockAll);
            }
        }

        private DiagnosticExecutionResult GrantResources()
        {
            var definitions = resources.All.OrderBy(x => x.Id).ToArray();
            var storage = inventories.All.Where(x => x.Owner.Kind == InventoryOwnerKind.BuildingStorage)
                .Where(x => buildings.TryGet(x.Owner.Id, out var building) && building.IsOperational &&
                            building.OwnerSideId.Value == "player")
                .Where(x => definitions.All(definition => x.Accepts(definition.Id)))
                .OrderBy(x => x.Owner.Id).FirstOrDefault();
            if (storage == null)
                return new DiagnosticExecutionResult(DiagnosticCommandKind.GrantResources,
                    DiagnosticFailure.MissingStorage);
            var before = storage.CaptureState();
            try
            {
                var addedTotal = checked(UnitsPerResource * definitions.LongLength);
                storage.EnsureCapacity(checked(storage.TotalUnits + addedTotal));
                for (var i = 0; i < definitions.Length; i++)
                {
                    if (!storage.TryAdd(definitions[i].Id, new ResourceQuantity(UnitsPerResource)))
                        throw new InvalidOperationException("Canonical storage rejected a staged diagnostic grant.");
                    if (i == 0) Fault("resource_first_added");
                }
                sequence = checked(sequence + 1);
                return new DiagnosticExecutionResult(DiagnosticCommandKind.GrantResources,
                    DiagnosticFailure.None, definitions.Length, addedTotal);
            }
            catch (Exception exception)
            {
                LastFailureDetail = exception.GetType().Name + ": " + exception.Message;
                storage.RestoreState(before);
                return Failed(DiagnosticCommandKind.GrantResources);
            }
        }

        private DiagnosticExecutionResult CreateArmy()
        {
            var side = new CombatSideId("player");
            var barracks = buildings.All.Where(x => x.DefinitionId == MilitaryTrainingService.BarracksDefinitionId &&
                    x.IsOperational && x.OwnerSideId == side).OrderBy(x => x.Id).FirstOrDefault();
            if (barracks == null)
                return new DiagnosticExecutionResult(DiagnosticCommandKind.CreateArmy,
                    DiagnosticFailure.MissingBarracks);
            var trainingBefore = training.CaptureState();
            var unitsBefore = units.CaptureState();
            var created = new List<Character>();
            var nextSequence = checked(sequence + 1);
            try
            {
                foreach (var level in training.Catalog.All)
                {
                    var rank = level.Rank;
                    var id = DeterministicStableId.Create(worldSeed,
                        "diagnostic_army:" + nextSequence + ":" + (int)rank);
                    var character = CreateAdult(id, "Diag " + rank, rank == MilitaryRank.Soldier
                        ? CharacterSex.Female : CharacterSex.Male, "military");
                    characters.Add(character);
                    created.Add(character);
                    inventories.Add(new ResourceInventory(
                        new InventoryOwner(InventoryOwnerKind.Character, id), resources, 128));
                    appearances.GetOrCreate(character);
                    tiers.Materialize(character, CharacterSimulationTier.Tier3,
                        new CharacterRuntimeState(id, new WorldPosition(-10 + created.Count * 1.5f, 0, 7),
                            default, false, "starter_settlement", calendarTick(), biologicalTick()));
                    training.RegisterDiagnosticGraduate(id, barracks.Id, side, rank, calendarTick());
                    Fault("army_rank_" + (int)rank);
                }
                sequence = nextSequence;
                return new DiagnosticExecutionResult(DiagnosticCommandKind.CreateArmy,
                    DiagnosticFailure.None, created.Count, firstCharacterId: created[0].Id);
            }
            catch (Exception exception)
            {
                LastFailureDetail = exception.GetType().Name + ": " + exception.Message;
                training.RestoreState(trainingBefore);
                units.RestoreState(unitsBefore, combat);
                RollBackCharacters(created);
                return Failed(DiagnosticCommandKind.CreateArmy);
            }
        }

        private DiagnosticExecutionResult CreateFamily()
        {
            var home = domestic.Homes.Where(value =>
                    buildings.TryGet(value.BuildingId, out var building) && building.IsOperational &&
                    value.IndoorZone.IsClosed && value.IndoorZone.IsHabitable)
                .OrderBy(x => x.BuildingId).FirstOrDefault();
            if (home == null)
                return new DiagnosticExecutionResult(DiagnosticCommandKind.CreateFamily,
                    DiagnosticFailure.MissingHome);
            if (!TryFindBedCoordinate(home, out var coordinate))
                return new DiagnosticExecutionResult(DiagnosticCommandKind.CreateFamily,
                    DiagnosticFailure.NoIndoorBedPosition);
            var domesticBefore = domestic.CaptureState();
            var healthBefore = health.CaptureState();
            var created = new List<Character>();
            var nextSequence = checked(sequence + 1);
            try
            {
                var manId = DeterministicStableId.Create(worldSeed, "diagnostic_family:" + nextSequence + ":man");
                var womanId = DeterministicStableId.Create(worldSeed, "diagnostic_family:" + nextSequence + ":woman");
                var man = CreateAdult(manId, "Diag Aren " + nextSequence, CharacterSex.Male, "worker");
                var woman = CreateAdult(womanId, "Diag Mira " + nextSequence, CharacterSex.Female, "worker");
                characters.Add(man); created.Add(man);
                characters.Add(woman); created.Add(woman);
                characters.LinkSpouses(man.Id, woman.Id);
                man.SetRelationship(new RelationshipState(woman.Id, new RelationshipScore(80)));
                woman.SetRelationship(new RelationshipState(man.Id, new RelationshipScore(80)));
                foreach (var character in created)
                {
                    inventories.Add(new ResourceInventory(
                        new InventoryOwner(InventoryOwnerKind.Character, character.Id), resources, 128));
                    appearances.GetOrCreate(character);
                    tiers.Materialize(character, CharacterSimulationTier.Tier3,
                        new CharacterRuntimeState(character.Id,
                            new WorldPosition(coordinate.X, 0, coordinate.Z), default, false,
                            "starter_settlement", calendarTick(), biologicalTick()));
                }
                domestic.AddResident(home.BuildingId, man.Id);
                domestic.AddResident(home.BuildingId, woman.Id);
                var bedId = DeterministicStableId.Create(worldSeed, "diagnostic_family:" + nextSequence + ":bed");
                var bed = new BedState(bedId, home.BuildingId, coordinate,
                    new WorldPosition(coordinate.X, 0, coordinate.Z));
                domestic.RegisterBed(bed);
                domestic.AssignBed(bedId, woman.Id);
                domestic.SetConsent(man.Id, woman.Id, true);
                domestic.SetConsent(woman.Id, man.Id, true);
                Fault("family_before_interaction");
                var interaction = domestic.StartFamilyInteraction(man.Id, woman.Id,
                    calendarTick(), biologicalTick());
                sequence = nextSequence;
                return new DiagnosticExecutionResult(DiagnosticCommandKind.CreateFamily,
                    DiagnosticFailure.None, 2, firstCharacterId: man.Id, secondCharacterId: woman.Id,
                    bedId: bedId, pregnancyId: interaction.PregnancyId);
            }
            catch (Exception exception)
            {
                LastFailureDetail = exception.GetType().Name + ": " + exception.Message;
                domestic.RestoreState(domesticBefore);
                health.RestoreState(healthBefore);
                RollBackCharacters(created);
                return Failed(DiagnosticCommandKind.CreateFamily);
            }
        }

        private Character CreateAdult(StableEntityId id, string name, CharacterSex sex, string profession)
        {
            var now = biologicalTick();
            var birth = Math.Max(0L, now - Character.BiologicalTicksPerYear * 25L);
            return Character.CreateWithId(new CharacterCreationData(new CharacterName(name), sex, birth,
                null, null, null, null, new ProfessionId(profession),
                new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
                new[] { new SkillState(MilitaryTrainingService.CombatSkillId, new SkillLevel(10)) },
                new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
                BodyHealth.Healthy, new Wealth(0), new Influence(0), null), id);
        }

        private bool TryFindBedCoordinate(HomeState home, out GridCoordinate coordinate)
        {
            var occupied = new HashSet<GridCoordinate>(domestic.Beds.Where(x => x.HomeBuildingId == home.BuildingId)
                .Select(x => x.Coordinate));
            for (var z = home.IndoorZone.MinZ; z <= home.IndoorZone.MaxZ; z++)
                for (var x = home.IndoorZone.MinX; x <= home.IndoorZone.MaxX; x++)
                {
                    var candidate = new GridCoordinate(x, z);
                    if (!occupied.Contains(candidate)) { coordinate = candidate; return true; }
                }
            coordinate = default;
            return false;
        }

        private void RollBackCharacters(IEnumerable<Character> values)
        {
            foreach (var character in values.Reverse())
            {
                tiers.RemoveManaged(character.Id);
                appearances.Remove(character.Id);
                var owner = new InventoryOwner(InventoryOwnerKind.Character, character.Id);
                if (inventories.TryGet(owner, out var inventory)) inventories.Remove(owner, inventory);
                characters.Remove(character.Id);
            }
        }

        private void Fault(string checkpoint) => faultInjection?.Invoke(checkpoint);
        private static DiagnosticExecutionResult Failed(DiagnosticCommandKind command) =>
            new DiagnosticExecutionResult(command, DiagnosticFailure.TransactionFailed);
    }
}
