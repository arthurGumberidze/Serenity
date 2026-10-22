using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Simulation.Health;

namespace Game.Simulation.Buildings
{
    public enum FacilityAssignmentFailure
    {
        None, MissingCharacter, MissingBuilding, InactiveBuilding, UnsupportedFacility, CapacityReached,
        CharacterAlreadyAssigned
    }

    public readonly struct FacilityAssignmentResult
    {
        public FacilityAssignmentResult(FacilityAssignmentFailure failure, StableEntityId buildingId = default)
        { Failure = failure; BuildingId = buildingId; }
        public FacilityAssignmentFailure Failure { get; }
        public StableEntityId BuildingId { get; }
        public bool Succeeded => Failure == FacilityAssignmentFailure.None;
    }

    /// <summary>
    /// Canonical assignment/effect boundary for Stone Age housing and non-production facilities.
    /// A character can own one home and one workplace; repeated commands are idempotent.
    /// </summary>
    public sealed class SettlementFacilityService
    {
        private static readonly BuildingDefinitionId HealerHut = new BuildingDefinitionId("healer_hut");
        private static readonly BuildingDefinitionId Shrine = new BuildingDefinitionId("shrine");
        private static readonly BuildingDefinitionId ChiefHut = new BuildingDefinitionId("chief_hut");
        private static readonly BuildingDefinitionId MeetingArea = new BuildingDefinitionId("meeting_area");
        private static readonly BuildingDefinitionId TrainingGround = new BuildingDefinitionId("training_ground");

        private readonly CharacterRegistry characters;
        private readonly BuildingRegistry buildings;
        private readonly Func<BuildingDefinitionId, BuildingDefinition> definitions;
        private readonly HealthAndFamilyService health;
        private readonly SortedDictionary<StableEntityId, StableEntityId> homes = new SortedDictionary<StableEntityId, StableEntityId>();
        private readonly SortedDictionary<StableEntityId, StableEntityId> workplaces = new SortedDictionary<StableEntityId, StableEntityId>();

        public SettlementFacilityService(CharacterRegistry characterRegistry, BuildingRegistry buildingRegistry,
            Func<BuildingDefinitionId, BuildingDefinition> definitionResolver,
            HealthAndFamilyService healthService)
        {
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            buildings = buildingRegistry ?? throw new ArgumentNullException(nameof(buildingRegistry));
            definitions = definitionResolver ?? throw new ArgumentNullException(nameof(definitionResolver));
            health = healthService ?? throw new ArgumentNullException(nameof(healthService));
        }

        public IReadOnlyDictionary<StableEntityId, StableEntityId> HomeAssignments => homes;
        public IReadOnlyDictionary<StableEntityId, StableEntityId> WorkplaceAssignments => workplaces;

        public FacilityAssignmentResult AssignHome(StableEntityId characterId, StableEntityId buildingId)
        {
            var check = Validate(characterId, buildingId, true, out var definition);
            if (check != FacilityAssignmentFailure.None) return new FacilityAssignmentResult(check);
            if (homes.TryGetValue(characterId, out var current))
                return current == buildingId
                    ? new FacilityAssignmentResult(FacilityAssignmentFailure.None, buildingId)
                    : new FacilityAssignmentResult(FacilityAssignmentFailure.CharacterAlreadyAssigned, current);
            if (homes.Count(x => x.Value == buildingId) >= definition.Housing.ResidentCapacity)
                return new FacilityAssignmentResult(FacilityAssignmentFailure.CapacityReached, buildingId);
            homes.Add(characterId, buildingId);
            return new FacilityAssignmentResult(FacilityAssignmentFailure.None, buildingId);
        }

        public FacilityAssignmentResult AssignWorkplace(StableEntityId characterId, StableEntityId buildingId)
        {
            var check = Validate(characterId, buildingId, false, out var definition);
            if (check != FacilityAssignmentFailure.None) return new FacilityAssignmentResult(check);
            if (workplaces.TryGetValue(characterId, out var current))
                return current == buildingId
                    ? new FacilityAssignmentResult(FacilityAssignmentFailure.None, buildingId)
                    : new FacilityAssignmentResult(FacilityAssignmentFailure.CharacterAlreadyAssigned, current);
            if (workplaces.Count(x => x.Value == buildingId) >= definition.Workplace.MaximumWorkers)
                return new FacilityAssignmentResult(FacilityAssignmentFailure.CapacityReached, buildingId);
            workplaces.Add(characterId, buildingId);
            return new FacilityAssignmentResult(FacilityAssignmentFailure.None, buildingId);
        }

        public bool UnassignHome(StableEntityId characterId) => homes.Remove(characterId);
        public bool UnassignWorkplace(StableEntityId characterId) => workplaces.Remove(characterId);

        public int Treat(StableEntityId healerId, StableEntityId patientId)
        {
            if (!workplaces.TryGetValue(healerId, out var buildingId) ||
                !IsOperationalDefinition(buildingId, HealerHut))
                throw new InvalidOperationException("The healer is not assigned to an operational Healer Hut.");
            characters.Get(patientId);
            CharacterSkillRules.GainWorkExperience(characters.Get(healerId), CharacterSkillRules.Medicine);
            return health.RecoverAll(patientId);
        }

        public bool Train(StableEntityId traineeId)
        {
            if (!workplaces.TryGetValue(traineeId, out var buildingId) ||
                !IsOperationalDefinition(buildingId, TrainingGround)) return false;
            return CharacterSkillRules.GainWorkExperience(characters.Get(traineeId), CharacterSkillRules.Combat);
        }

        public int MoralePerThousand => buildings.All.Any(x => x.IsOperational &&
            (x.DefinitionId == Shrine || x.DefinitionId == MeetingArea)) ? 1100 : 1000;
        public int GovernancePerThousand => buildings.All.Any(x => x.IsOperational && x.DefinitionId == ChiefHut)
            ? 1100 : 1000;

        public void RemoveBuildingAssignments(StableEntityId buildingId)
        {
            foreach (var id in homes.Where(x => x.Value == buildingId).Select(x => x.Key).ToArray()) homes.Remove(id);
            foreach (var id in workplaces.Where(x => x.Value == buildingId).Select(x => x.Key).ToArray()) workplaces.Remove(id);
        }

        private FacilityAssignmentFailure Validate(StableEntityId characterId, StableEntityId buildingId,
            bool housing, out BuildingDefinition definition)
        {
            definition = null;
            if (!characters.TryGet(characterId, out var character) || character.LifeState != CharacterLifeState.Alive)
                return FacilityAssignmentFailure.MissingCharacter;
            if (!buildings.TryGet(buildingId, out var building)) return FacilityAssignmentFailure.MissingBuilding;
            if (!building.IsOperational) return FacilityAssignmentFailure.InactiveBuilding;
            definition = definitions(building.DefinitionId);
            if (housing ? definition.Housing == null : definition.Workplace == null)
                return FacilityAssignmentFailure.UnsupportedFacility;
            return FacilityAssignmentFailure.None;
        }

        private bool IsOperationalDefinition(StableEntityId buildingId, BuildingDefinitionId definitionId) =>
            buildings.TryGet(buildingId, out var building) && building.IsOperational && building.DefinitionId == definitionId;
    }
}
