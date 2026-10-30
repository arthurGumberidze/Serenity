using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Health;
using Game.Domain.Resources;
using Game.Simulation.Health;

namespace Game.Simulation.Buildings
{
    public enum FacilityAssignmentFailure
    {
        None, MissingCharacter, MissingBuilding, InactiveBuilding, UnsupportedFacility, CapacityReached,
        CharacterAlreadyAssigned, RestrictedHousehold
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
        private readonly ResourceInventoryRegistry inventories;
        private readonly SortedDictionary<StableEntityId, StableEntityId> homes = new SortedDictionary<StableEntityId, StableEntityId>();
        private readonly SortedDictionary<StableEntityId, StableEntityId> workplaces = new SortedDictionary<StableEntityId, StableEntityId>();
        private readonly SortedSet<StableEntityId> waitingFamilies = new SortedSet<StableEntityId>();
        private readonly SortedDictionary<StableEntityId, LivingBuildingPolicyState> policies =
            new SortedDictionary<StableEntityId, LivingBuildingPolicyState>();
        private StableEntityId? rulingDynastyId;
        private readonly List<ClinicPatientState> clinicPatients = new List<ClinicPatientState>();

        public SettlementFacilityService(CharacterRegistry characterRegistry, BuildingRegistry buildingRegistry,
            Func<BuildingDefinitionId, BuildingDefinition> definitionResolver,
            HealthAndFamilyService healthService, ResourceInventoryRegistry inventoryRegistry = null)
        {
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            buildings = buildingRegistry ?? throw new ArgumentNullException(nameof(buildingRegistry));
            definitions = definitionResolver ?? throw new ArgumentNullException(nameof(definitionResolver));
            health = healthService ?? throw new ArgumentNullException(nameof(healthService));
            inventories = inventoryRegistry;
        }

        public IReadOnlyDictionary<StableEntityId, StableEntityId> HomeAssignments => homes;
        public Func<Character,bool> HousingResidentEligible { get; set; }
        public Func<Building,bool> HousingBuildingEligible { get; set; }
        public IReadOnlyDictionary<StableEntityId, StableEntityId> WorkplaceAssignments => workplaces;
        public IReadOnlyCollection<StableEntityId> WaitingFamilyIds => waitingFamilies;
        public StableEntityId? RulingDynastyId => rulingDynastyId;
        public IReadOnlyList<ClinicPatientState> ClinicPatients => clinicPatients
            .OrderByDescending(x => x.Severity).ThenBy(x => x.AdmittedTick).ThenBy(x => x.PatientId).ToArray();

        /// <summary>
        /// Doctors assigned to one clinic alternate deterministic twelve-hour shifts.  The first stable
        /// identity covers the day, the second the night, and further doctors repeat that pattern.
        /// </summary>
        public WorkShiftKind GetDoctorShift(StableEntityId doctorId, StableEntityId clinicId)
        {
            var doctors = workplaces.Where(x => x.Value == clinicId).Select(x => x.Key).OrderBy(x => x).ToArray();
            var index = Array.IndexOf(doctors, doctorId);
            if (index < 0 || !IsOperationalDefinition(clinicId, HealerHut))
                throw new InvalidOperationException("The doctor is not assigned to this operational clinic.");
            return index % 2 == 0 ? WorkShiftKind.Day : WorkShiftKind.Night;
        }

        public FacilityAssignmentResult AssignHome(StableEntityId characterId, StableEntityId buildingId)
        {
            var check = Validate(characterId, buildingId, true, out var definition);
            if (check != FacilityAssignmentFailure.None) return new FacilityAssignmentResult(check);
            if (homes.TryGetValue(characterId, out var current))
                return current == buildingId
                    ? new FacilityAssignmentResult(FacilityAssignmentFailure.None, buildingId)
                    : new FacilityAssignmentResult(FacilityAssignmentFailure.CharacterAlreadyAssigned, current);
            if (IsChiefHouse(buildingId))
            {
                var resident = characters.Get(characterId);
                if (!rulingDynastyId.HasValue || resident.DynastyId != rulingDynastyId)
                    return new FacilityAssignmentResult(FacilityAssignmentFailure.RestrictedHousehold, buildingId);
            }
            else if (homes.Count(x => x.Value == buildingId) >= definition.Housing.ResidentCapacity)
                return new FacilityAssignmentResult(FacilityAssignmentFailure.CapacityReached, buildingId);
            homes.Add(characterId, buildingId);
            UpdateHouseholdStatus(characters.Get(characterId).FamilyId);
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
            characters.Get(characterId).AssignProfession(ProfessionFor(buildings.Get(buildingId), definition));
            return new FacilityAssignmentResult(FacilityAssignmentFailure.None, buildingId);
        }

        public FacilityAssignmentResult ChangeWorkplace(StableEntityId characterId, StableEntityId buildingId)
        {
            var check = Validate(characterId, buildingId, false, out var definition);
            if (check != FacilityAssignmentFailure.None) return new FacilityAssignmentResult(check);
            if (workplaces.TryGetValue(characterId, out var current) && current == buildingId)
                return new FacilityAssignmentResult(FacilityAssignmentFailure.None, buildingId);
            if (workplaces.Count(x => x.Value == buildingId) >= definition.Workplace.MaximumWorkers)
                return new FacilityAssignmentResult(FacilityAssignmentFailure.CapacityReached, buildingId);
            workplaces[characterId] = buildingId;
            characters.Get(characterId).AssignProfession(ProfessionFor(buildings.Get(buildingId), definition));
            return new FacilityAssignmentResult(FacilityAssignmentFailure.None, buildingId);
        }

        public bool UnassignHome(StableEntityId characterId)
        {
            var familyId = characters.TryGet(characterId, out var character) ? character.FamilyId : null;
            var removed = homes.Remove(characterId);
            if (removed) UpdateHouseholdStatus(familyId);
            return removed;
        }
        public bool UnassignWorkplace(StableEntityId characterId) => workplaces.Remove(characterId);

        public int Treat(StableEntityId healerId, StableEntityId patientId)
        {
            if (!workplaces.TryGetValue(healerId, out var buildingId) ||
                !IsOperationalDefinition(buildingId, HealerHut))
                throw new InvalidOperationException("The healer is not assigned to an operational Healer Hut.");
            characters.Get(patientId);
            return RequestTreatment(patientId, buildingId, health.LastCalendarTick) ? 1 : 0;
        }

        public bool RequestTreatment(StableEntityId patientId, StableEntityId clinicId, long calendarTick)
        {
            if (calendarTick < 0 || !characters.TryGet(patientId, out var patient) ||
                patient.LifeState != CharacterLifeState.Alive || !IsOperationalDefinition(clinicId, HealerHut) ||
                clinicPatients.Any(x => x.PatientId == patientId)) return false;
            var active = health.Conditions.Where(x => x.TargetId == patientId &&
                x.State != HealthConditionState.Recovered && x.State != HealthConditionState.Fatal).ToArray();
            if (active.Length == 0) return false;
            // Six physical places include both doctors, leaving four patient places.
            if (clinicPatients.Count(x => x.ClinicId == clinicId) >= 4) return false;
            clinicPatients.Add(new ClinicPatientState(patientId, clinicId,
                active.Max(x => (int)x.Severity), calendarTick));
            return true;
        }

        public int AdvanceClinics(long calendarTick)
        {
            if (calendarTick < health.LastCalendarTick) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            AutoAdmitPatients(calendarTick);
            var completed = 0;
            foreach (var clinic in buildings.All.Where(x => x.IsOperational && x.DefinitionId.FamilyId == HealerHut)
                         .OrderBy(x => x.Id))
            {
                var hour = (int)((calendarTick % TimeSpan.TicksPerDay) / TimeSpan.TicksPerHour);
                var currentShift = hour >= 6 && hour < 18 ? WorkShiftKind.Day : WorkShiftKind.Night;
                var doctors = workplaces.Where(x => x.Value == clinic.Id).Select(x => x.Key)
                    .Where(x => characters.TryGet(x, out var doctor) && doctor.LifeState == CharacterLifeState.Alive)
                    .Where(x => GetDoctorShift(x, clinic.Id) == currentShift)
                    .OrderBy(x => x).ToArray();
                if (doctors.Length == 0) continue;
                var active = clinicPatients.FirstOrDefault(x => x.ClinicId == clinic.Id && x.InTreatment);
                if (active == null)
                {
                    var next = clinicPatients.Where(x => x.ClinicId == clinic.Id && !x.InTreatment)
                        .OrderByDescending(x => x.Severity).ThenBy(x => x.AdmittedTick).ThenBy(x => x.PatientId)
                        .FirstOrDefault();
                    if (next == null || !ConsumeMedicine(clinic.Id, next.Severity)) continue;
                    clinicPatients.Remove(next);
                    clinicPatients.Add(new ClinicPatientState(next.PatientId, next.ClinicId, next.Severity,
                        next.AdmittedTick, checked(calendarTick + TimeSpan.FromHours(next.Severity).Ticks)));
                    continue;
                }
                if (active.CompletionTick > calendarTick) continue;
                health.RecoverAll(active.PatientId);
                CharacterSkillRules.GainWorkExperience(characters.Get(doctors[0]), CharacterSkillRules.Medicine);
                clinicPatients.Remove(active);
                completed++;
            }
            return completed;
        }

        private void AutoAdmitPatients(long calendarTick)
        {
            foreach (var patient in health.Conditions.Where(x => x.State != HealthConditionState.Recovered &&
                         x.State != HealthConditionState.Fatal).OrderByDescending(x => x.Severity)
                         .ThenBy(x => x.TargetId).Select(x => x.TargetId).Distinct())
            {
                if (clinicPatients.Any(x => x.PatientId == patient)) continue;
                var clinic = buildings.All.Where(x => x.IsOperational && x.DefinitionId.FamilyId == HealerHut &&
                        clinicPatients.Count(p => p.ClinicId == x.Id) < 4).OrderBy(x => x.Id).FirstOrDefault();
                if (clinic != null) RequestTreatment(patient, clinic.Id, calendarTick);
            }
        }

        private bool ConsumeMedicine(StableEntityId clinicId, int severity)
        {
            if (inventories == null) return true;
            var medicine = new ResourceId("medicine");
            foreach (var owner in new[]
                     {
                         new InventoryOwner(InventoryOwnerKind.ProductionOutput, clinicId),
                         new InventoryOwner(InventoryOwnerKind.ProductionInput, clinicId),
                         new InventoryOwner(InventoryOwnerKind.BuildingStorage, clinicId)
                     })
                if (inventories.TryGet(owner, out var inventory) &&
                    inventory.TryRemove(medicine, new ResourceQuantity(severity))) return true;
            return false;
        }

        public bool Train(StableEntityId traineeId)
        {
            if (!workplaces.TryGetValue(traineeId, out var buildingId) ||
                !IsOperationalDefinition(buildingId, TrainingGround)) return false;
            return CharacterSkillRules.GainWorkExperience(characters.Get(traineeId), CharacterSkillRules.Combat);
        }

        public int MoralePerThousand => buildings.All.Any(x => x.IsOperational &&
            (x.DefinitionId.FamilyId == Shrine || x.DefinitionId.FamilyId == MeetingArea)) ? 1100 : 1000;
        public int GovernancePerThousand => buildings.All.Any(x => x.IsOperational && (HousingBuildingEligible?.Invoke(x)??true) && x.DefinitionId.FamilyId == ChiefHut)
            ? 1100 : 1000;

        public HouseholdHousingStatus GetHouseholdStatus(StableEntityId familyId) =>
            waitingFamilies.Contains(familyId)
                ? HouseholdHousingStatus.AwaitingFamilyHousing
                : HouseholdHousingStatus.HousedTogether;

        public void SetRulingDynasty(StableEntityId? dynastyId)
        {
            if (dynastyId.HasValue && !dynastyId.Value.IsValid)
                throw new ArgumentException("Ruling dynasty ID must be valid.", nameof(dynastyId));
            rulingDynastyId = dynastyId;
            ReconcileHousing();
        }

        public LivingBuildingPolicyState GetPolicy(StableEntityId buildingId)
        {
            if (!buildingId.IsValid) throw new ArgumentException("Building ID is required.", nameof(buildingId));
            return policies.TryGetValue(buildingId, out var policy)
                ? policy : new LivingBuildingPolicyState(buildingId);
        }

        public void SetPolicy(StableEntityId buildingId, BuildingWorkPriority priority,
            WorkShiftKind shift, bool workPaused, GateAccessMode gateAccess)
        {
            if (!buildings.TryGet(buildingId, out _)) throw new KeyNotFoundException("Building was not found.");
            policies[buildingId] = new LivingBuildingPolicyState(buildingId, priority, shift, workPaused, gateAccess);
        }

        /// <summary>
        /// Deterministically fills free beds, then atomically reunites waiting families as soon as one
        /// operational home can fit every living member. The chief's residence is reserved for the
        /// complete living ruling dynasty and has no ordinary resident limit.
        /// </summary>
        public void ReconcileHousing()
        {
            PruneInvalidAssignments();
            MoveRulingDynastyToChiefHouse();

            var living = characters.GetAll().Where(x => x.LifeState == CharacterLifeState.Alive &&
                (HousingResidentEligible?.Invoke(x)??true)).ToArray();
            foreach (var family in living.Where(x => x.FamilyId.HasValue)
                         .GroupBy(x => x.FamilyId.Value).OrderBy(x => x.Key))
            {
                var members = family.OrderBy(x => x.Id).ToArray();
                if (rulingDynastyId.HasValue && members.All(x => x.DynastyId == rulingDynastyId) &&
                    members.All(x => homes.TryGetValue(x.Id, out var home) && IsChiefHouse(home)))
                {
                    waitingFamilies.Remove(family.Key);
                    continue;
                }
                var assignedHomes = members.Where(x => homes.ContainsKey(x.Id))
                    .Select(x => homes[x.Id]).Distinct().ToArray();
                if (assignedHomes.Length == 1 && members.All(x => homes.ContainsKey(x.Id)))
                {
                    waitingFamilies.Remove(family.Key);
                    continue;
                }

                var destination = FindFamilyHome(members);
                if (destination.HasValue)
                {
                    foreach (var member in members) homes.Remove(member.Id);
                    foreach (var member in members) homes[member.Id] = destination.Value;
                    waitingFamilies.Remove(family.Key);
                }
                else
                {
                    waitingFamilies.Add(family.Key);
                    foreach (var member in members.Where(x => !homes.ContainsKey(x.Id)))
                    {
                        var bed = FindFreeOrdinaryHome();
                        if (bed.HasValue) homes[member.Id] = bed.Value;
                    }
                }
            }

            foreach (var individual in living.Where(x => !x.FamilyId.HasValue && !homes.ContainsKey(x.Id))
                         .OrderBy(x => x.Id))
            {
                var bed = FindFreeOrdinaryHome();
                if (!bed.HasValue) break;
                homes[individual.Id] = bed.Value;
            }
        }

        public void RemoveBuildingAssignments(StableEntityId buildingId)
        {
            foreach (var id in homes.Where(x => x.Value == buildingId).Select(x => x.Key).ToArray()) homes.Remove(id);
            foreach (var id in workplaces.Where(x => x.Value == buildingId).Select(x => x.Key).ToArray()) workplaces.Remove(id);
            policies.Remove(buildingId);
            clinicPatients.RemoveAll(x => x.ClinicId == buildingId);
            ReconcileHousing();
        }

        public SettlementFacilitySaveState CaptureState() => new SettlementFacilitySaveState(
            homes.Select(x => new FacilityAssignmentState(x.Key, x.Value)),
            workplaces.Select(x => new FacilityAssignmentState(x.Key, x.Value)), waitingFamilies,
            rulingDynastyId, policies.Values, clinicPatients);

        public void RestoreState(SettlementFacilitySaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            foreach (var assignment in state.Homes)
                ValidateRestoredAssignment(assignment, true);
            foreach (var assignment in state.Workplaces)
                ValidateRestoredAssignment(assignment, false);
            foreach (var policy in state.Policies)
                if (!buildings.TryGet(policy.BuildingId, out _))
                    throw new ArgumentException("Facility save references a missing policy building.", nameof(state));
            homes.Clear();
            foreach (var assignment in state.Homes) homes.Add(assignment.CharacterId, assignment.BuildingId);
            workplaces.Clear();
            foreach (var assignment in state.Workplaces) workplaces.Add(assignment.CharacterId, assignment.BuildingId);
            waitingFamilies.Clear();
            foreach (var familyId in state.WaitingFamilyIds) waitingFamilies.Add(familyId);
            rulingDynastyId = state.RulingDynastyId;
            policies.Clear();
            foreach (var policy in state.Policies) policies.Add(policy.BuildingId, policy);
            clinicPatients.Clear();
            foreach (var patient in state.ClinicPatients)
                if (characters.TryGet(patient.PatientId, out _) && IsOperationalDefinition(patient.ClinicId, HealerHut))
                    clinicPatients.Add(patient);
            ReconcileHousing();
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
            buildings.TryGet(buildingId, out var building) && building.IsOperational && building.DefinitionId.FamilyId == definitionId;

        private bool IsChiefHouse(StableEntityId buildingId) =>
            buildings.TryGet(buildingId, out var building) && building.DefinitionId.FamilyId == ChiefHut;

        private static ProfessionId ProfessionFor(Building building, BuildingDefinition definition)
        {
            if (definition.Workplace.RequiredProfession.HasValue) return definition.Workplace.RequiredProfession.Value;
            var id = building.DefinitionId.FamilyId.ToString();
            if (id == "healer_hut") return new ProfessionId("healer");
            if (id.Contains("garden") || id.Contains("crop") || id == "animal_pasture")
                return new ProfessionId("farmer");
            if (id.Contains("fishing")) return new ProfessionId("fisher");
            if (id.Contains("hunting")) return new ProfessionId("hunter");
            if (id.Contains("kitchen") || id == "campfire") return new ProfessionId("cook");
            if (id.Contains("quarry") || id.Contains("mine")) return new ProfessionId("miner");
            if (definition.Category == BuildingCategory.Military) return new ProfessionId("military");
            if (definition.Category == BuildingCategory.Production) return new ProfessionId("crafter");
            return new ProfessionId("worker");
        }

        private void UpdateHouseholdStatus(StableEntityId? familyId)
        {
            if (!familyId.HasValue) return;
            var members = characters.GetAll().Where(x => x.LifeState == CharacterLifeState.Alive &&
                x.FamilyId == familyId).ToArray();
            var assigned = members.Where(x => homes.ContainsKey(x.Id)).Select(x => homes[x.Id]).Distinct().ToArray();
            if (members.Length > 0 && assigned.Length == 1 && members.All(x => homes.ContainsKey(x.Id)))
                waitingFamilies.Remove(familyId.Value);
            else waitingFamilies.Add(familyId.Value);
        }

        private StableEntityId? FindFamilyHome(IReadOnlyCollection<Character> members)
        {
            foreach (var building in buildings.All.Where(x => x.IsOperational && (HousingBuildingEligible?.Invoke(x)??true) && x.DefinitionId.FamilyId != ChiefHut)
                         .OrderBy(x => x.Id))
            {
                var definition = definitions(building.DefinitionId);
                if (definition.Housing == null) continue;
                var memberIds = new HashSet<StableEntityId>(members.Select(x => x.Id));
                var otherResidents = homes.Count(x => x.Value == building.Id && !memberIds.Contains(x.Key));
                if (definition.Housing.ResidentCapacity - otherResidents >= members.Count)
                    return building.Id;
            }
            return null;
        }

        private StableEntityId? FindFreeOrdinaryHome()
        {
            foreach (var building in buildings.All.Where(x => x.IsOperational && (HousingBuildingEligible?.Invoke(x)??true) && x.DefinitionId.FamilyId != ChiefHut)
                         .OrderBy(x => x.Id))
            {
                var definition = definitions(building.DefinitionId);
                if (definition.Housing != null &&
                    homes.Count(x => x.Value == building.Id) < definition.Housing.ResidentCapacity)
                    return building.Id;
            }
            return null;
        }

        private void MoveRulingDynastyToChiefHouse()
        {
            if (!rulingDynastyId.HasValue) return;
            var chief = buildings.All.Where(x => x.IsOperational && (HousingBuildingEligible?.Invoke(x)??true) && x.DefinitionId.FamilyId == ChiefHut)
                .OrderBy(x => x.Id).FirstOrDefault();
            if (chief == null) return;
            foreach (var character in characters.GetAll().Where(x => x.LifeState == CharacterLifeState.Alive &&
                         x.DynastyId == rulingDynastyId).OrderBy(x => x.Id))
                homes[character.Id] = chief.Id;
        }

        private void PruneInvalidAssignments()
        {
            foreach (var id in homes.Where(x => !characters.TryGet(x.Key, out var character) ||
                         character.LifeState != CharacterLifeState.Alive || !buildings.TryGet(x.Value, out var building) ||
                         !building.IsOperational || !(HousingResidentEligible?.Invoke(character)??true) ||
                         !(HousingBuildingEligible?.Invoke(building)??true) || definitions(building.DefinitionId).Housing == null)
                         .Select(x => x.Key).ToArray()) homes.Remove(id);
            foreach (var id in workplaces.Where(x => !characters.TryGet(x.Key, out var character) ||
                         character.LifeState != CharacterLifeState.Alive || !buildings.TryGet(x.Value, out var building) ||
                         !building.IsOperational || definitions(building.DefinitionId).Workplace == null)
                         .Select(x => x.Key).ToArray()) workplaces.Remove(id);
        }

        private void ValidateRestoredAssignment(FacilityAssignmentState assignment, bool housing)
        {
            if (!characters.TryGet(assignment.CharacterId, out var character) ||
                character.LifeState != CharacterLifeState.Alive ||
                !buildings.TryGet(assignment.BuildingId, out var building) || !building.IsOperational ||
                (housing ? definitions(building.DefinitionId).Housing == null :
                    definitions(building.DefinitionId).Workplace == null))
                throw new ArgumentException("Facility save contains an invalid assignment.");
        }
    }
}
