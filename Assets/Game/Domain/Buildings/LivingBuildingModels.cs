using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Game.Domain.Buildings
{
    public enum BuildingWorkPriority { Low = 1, Normal = 2, High = 3, Emergency = 4 }
    public enum WorkShiftKind { Day = 1, Night = 2 }
    public enum GateAccessMode { Open = 1, Closed = 2, AlliesOnly = 3, Nobody = 4 }
    public enum GatePassageResult { Allowed = 1, Denied = 2, DetainedAwaitingPlayer = 3 }
    public enum HouseholdHousingStatus { HousedTogether = 1, AwaitingFamilyHousing = 2 }
    public enum LivestockOperationMode { Breeding = 1, Products = 2, Balanced = 3 }
    public enum HuntingOperationMode { Hunt = 1, RestorePopulation = 2 }

    public readonly struct FacilityAssignmentState
    {
        public FacilityAssignmentState(StableEntityId characterId, StableEntityId buildingId)
        {
            if (!characterId.IsValid || !buildingId.IsValid)
                throw new ArgumentException("Facility assignment references must be valid.");
            CharacterId = characterId;
            BuildingId = buildingId;
        }
        public StableEntityId CharacterId { get; }
        public StableEntityId BuildingId { get; }
    }

    public sealed class LivingBuildingPolicyState
    {
        public LivingBuildingPolicyState(StableEntityId buildingId,
            BuildingWorkPriority priority = BuildingWorkPriority.Normal,
            WorkShiftKind shift = WorkShiftKind.Day, bool workPaused = false,
            GateAccessMode gateAccess = GateAccessMode.Open)
        {
            if (!buildingId.IsValid) throw new ArgumentException("Building ID is required.", nameof(buildingId));
            if (!Enum.IsDefined(typeof(BuildingWorkPriority), priority)) throw new ArgumentOutOfRangeException(nameof(priority));
            if (!Enum.IsDefined(typeof(WorkShiftKind), shift)) throw new ArgumentOutOfRangeException(nameof(shift));
            if (!Enum.IsDefined(typeof(GateAccessMode), gateAccess)) throw new ArgumentOutOfRangeException(nameof(gateAccess));
            BuildingId = buildingId;
            Priority = priority;
            Shift = shift;
            WorkPaused = workPaused;
            GateAccess = gateAccess;
        }
        public StableEntityId BuildingId { get; }
        public BuildingWorkPriority Priority { get; }
        public WorkShiftKind Shift { get; }
        public bool WorkPaused { get; }
        public GateAccessMode GateAccess { get; }
    }

    public sealed class ClinicPatientState
    {
        public ClinicPatientState(StableEntityId patientId, StableEntityId clinicId,
            int severity, long admittedTick, long completionTick = 0)
        {
            if (!patientId.IsValid || !clinicId.IsValid) throw new ArgumentException("Clinic references are required.");
            if (severity < 1 || severity > 4) throw new ArgumentOutOfRangeException(nameof(severity));
            if (admittedTick < 0 || completionTick < 0) throw new ArgumentOutOfRangeException(nameof(admittedTick));
            PatientId = patientId;
            ClinicId = clinicId;
            Severity = severity;
            AdmittedTick = admittedTick;
            CompletionTick = completionTick;
        }
        public StableEntityId PatientId { get; }
        public StableEntityId ClinicId { get; }
        public int Severity { get; }
        public long AdmittedTick { get; }
        public long CompletionTick { get; }
        public bool InTreatment => CompletionTick > 0;
    }

    public sealed class SettlementFacilitySaveState
    {
        public SettlementFacilitySaveState(IEnumerable<FacilityAssignmentState> homes,
            IEnumerable<FacilityAssignmentState> workplaces,
            IEnumerable<StableEntityId> waitingFamilyIds = null,
            StableEntityId? rulingDynastyId = null,
            IEnumerable<LivingBuildingPolicyState> policies = null,
            IEnumerable<ClinicPatientState> clinicPatients = null)
        {
            Homes = CopyAssignments(homes, nameof(homes));
            Workplaces = CopyAssignments(workplaces, nameof(workplaces));
            var waiting = (waitingFamilyIds ?? Array.Empty<StableEntityId>()).Distinct().OrderBy(x => x).ToArray();
            if (waiting.Any(x => !x.IsValid)) throw new ArgumentException("Waiting family IDs must be valid.", nameof(waitingFamilyIds));
            WaitingFamilyIds = new ReadOnlyCollection<StableEntityId>(waiting);
            if (rulingDynastyId.HasValue && !rulingDynastyId.Value.IsValid)
                throw new ArgumentException("Ruling dynasty ID must be valid.", nameof(rulingDynastyId));
            RulingDynastyId = rulingDynastyId;
            var policyArray = (policies ?? Array.Empty<LivingBuildingPolicyState>()).OrderBy(x => x.BuildingId).ToArray();
            if (policyArray.Any(x => x == null) || policyArray.Select(x => x.BuildingId).Distinct().Count() != policyArray.Length)
                throw new ArgumentException("Building policies must be non-null and unique.", nameof(policies));
            Policies = new ReadOnlyCollection<LivingBuildingPolicyState>(policyArray);
            var clinicArray = (clinicPatients ?? Array.Empty<ClinicPatientState>()).OrderByDescending(x => x.Severity)
                .ThenBy(x => x.AdmittedTick).ThenBy(x => x.PatientId).ToArray();
            if (clinicArray.Any(x => x == null) || clinicArray.Select(x => x.PatientId).Distinct().Count() != clinicArray.Length)
                throw new ArgumentException("Clinic patients must be non-null and unique.", nameof(clinicPatients));
            ClinicPatients = new ReadOnlyCollection<ClinicPatientState>(clinicArray);
        }

        public IReadOnlyList<FacilityAssignmentState> Homes { get; }
        public IReadOnlyList<FacilityAssignmentState> Workplaces { get; }
        public IReadOnlyList<StableEntityId> WaitingFamilyIds { get; }
        public StableEntityId? RulingDynastyId { get; }
        public IReadOnlyList<LivingBuildingPolicyState> Policies { get; }
        public IReadOnlyList<ClinicPatientState> ClinicPatients { get; }

        private static IReadOnlyList<FacilityAssignmentState> CopyAssignments(
            IEnumerable<FacilityAssignmentState> source, string parameterName)
        {
            var values = (source ?? Array.Empty<FacilityAssignmentState>())
                .OrderBy(x => x.CharacterId).ToArray();
            if (values.Select(x => x.CharacterId).Distinct().Count() != values.Length)
                throw new ArgumentException("A character may have only one assignment of each kind.", parameterName);
            return new ReadOnlyCollection<FacilityAssignmentState>(values);
        }
    }
}
