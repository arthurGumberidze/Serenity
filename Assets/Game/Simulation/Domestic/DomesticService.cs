using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Campaign;
using Game.Domain.Domestic;
using Game.Domain.Events;
using Game.Domain.Health;
using Game.Simulation.Health;

namespace Game.Simulation.Domestic
{
    /// <summary>Engine-free authority for homes, assigned beds, sleep occupancy and adult mutual consent.</summary>
    public sealed class DomesticService
    {
        public const int MinimumRelationshipAffinity = 0;
        private readonly CharacterRegistry characters;
        private readonly BuildingRegistry buildings;
        private readonly HealthAndFamilyService healthFamily;
        private readonly HashSet<BuildingDefinitionId> homeDefinitions;
        private readonly SortedDictionary<StableEntityId, HomeState> homes = new SortedDictionary<StableEntityId, HomeState>();
        private readonly SortedDictionary<StableEntityId, BedState> beds = new SortedDictionary<StableEntityId, BedState>();
        private readonly Dictionary<string, RelationshipConsentState> consents = new Dictionary<string, RelationshipConsentState>();
        private readonly SortedDictionary<StableEntityId, FamilyInteractionState> interactions =
            new SortedDictionary<StableEntityId, FamilyInteractionState>();

        public DomesticService(CharacterRegistry characterRegistry, BuildingRegistry buildingRegistry,
            HealthAndFamilyService healthAndFamilyService, IEnumerable<BuildingDefinitionId> eligibleHomeDefinitions = null)
        {
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            buildings = buildingRegistry ?? throw new ArgumentNullException(nameof(buildingRegistry));
            healthFamily = healthAndFamilyService ?? throw new ArgumentNullException(nameof(healthAndFamilyService));
            homeDefinitions = new HashSet<BuildingDefinitionId>(eligibleHomeDefinitions ??
                new[] { new BuildingDefinitionId("primitive_shelter") });
            if (homeDefinitions.Count == 0 || homeDefinitions.Any(x => !x.IsValid))
                throw new ArgumentException("At least one valid home definition is required.");
        }

        public IReadOnlyList<HomeState> Homes => homes.Values.ToArray();
        public IReadOnlyList<BedState> Beds => beds.Values.ToArray();
        public IReadOnlyList<RelationshipConsentState> Consents => consents.Values.OrderBy(x => x.FirstId).ThenBy(x => x.SecondId).ToArray();
        public IReadOnlyList<FamilyInteractionState> Interactions => interactions.Values.ToArray();

        public void RegisterHome(HomeState home)
        {
            if (home == null) throw new ArgumentNullException(nameof(home));
            var building = buildings.Get(home.BuildingId);
            characters.Get(home.OwnerId);
            foreach (var resident in home.ResidentIds) characters.Get(resident);
            if (!homeDefinitions.Contains(building.DefinitionId))
                throw new InvalidOperationException("Only an eligible shelter definition can be a home.");
            if (homes.ContainsKey(home.BuildingId)) throw new InvalidOperationException("Home is already registered.");
            homes.Add(home.BuildingId, home);
        }

        public void AddResident(StableEntityId homeBuildingId, StableEntityId residentId)
        {
            characters.Get(residentId);
            var home = GetHome(homeBuildingId);
            homes[homeBuildingId] = new HomeState(home.BuildingId, home.OwnerId, home.IndoorZone,
                home.ResidentIds.Concat(new[] { residentId }));
        }

        public void RegisterBed(BedState bed)
        {
            if (bed == null) throw new ArgumentNullException(nameof(bed));
            GetHome(bed.HomeBuildingId);
            if (beds.ContainsKey(bed.Id)) throw new InvalidOperationException("Bed is already registered.");
            beds.Add(bed.Id, bed);
        }

        public void AssignBed(StableEntityId bedId, StableEntityId residentId)
        {
            characters.Get(residentId);
            var bed = GetBed(bedId);
            var home = GetHome(bed.HomeBuildingId);
            if (!home.Allows(residentId)) throw new InvalidOperationException("Resident has no access to this home.");
            var prior = beds.Values.FirstOrDefault(x => x.AssignedResidentId == residentId && x.Id != bedId);
            if (prior != null) beds[prior.Id] = CopyBed(prior, assigned: null, setAssigned: true);
            beds[bedId] = CopyBed(bed, assigned: residentId, setAssigned: true);
        }

        public bool TryGetAssignedBed(StableEntityId characterId, out BedState bed)
        {
            bed = beds.Values.FirstOrDefault(x => x.AssignedResidentId == characterId);
            return bed != null;
        }

        public SleepEligibility EvaluateSleep(StableEntityId characterId)
        {
            if (!characters.TryGet(characterId, out var character) || character.LifeState != CharacterLifeState.Alive)
                return new SleepEligibility(SleepDenialReason.CharacterUnavailable);
            if (!TryGetAssignedBed(characterId, out var bed))
                return new SleepEligibility(SleepDenialReason.NoAssignedBed);
            return EvaluateSleepAtBed(characterId, bed.Id);
        }

        public SleepEligibility EvaluateSleepAtBed(StableEntityId characterId, StableEntityId bedId)
        {
            if (!characters.TryGet(characterId, out var character) || character.LifeState != CharacterLifeState.Alive)
                return new SleepEligibility(SleepDenialReason.CharacterUnavailable);
            if (!beds.TryGetValue(bedId, out var bed)) return new SleepEligibility(SleepDenialReason.NoAssignedBed);
            if (!homes.TryGetValue(bed.HomeBuildingId, out var home))
                return new SleepEligibility(SleepDenialReason.NoAssignedHome);
            if (!buildings.TryGet(home.BuildingId, out var building) || !homeDefinitions.Contains(building.DefinitionId) || building.IsDestroyed)
                return new SleepEligibility(SleepDenialReason.HomeUnavailable);
            if (building.ConstructionState != ConstructionState.Completed)
                return new SleepEligibility(SleepDenialReason.HomeIncomplete);
            if (!home.IndoorZone.IsClosed) return new SleepEligibility(SleepDenialReason.HomeNotClosed);
            if (!home.IndoorZone.IsHabitable) return new SleepEligibility(SleepDenialReason.HomeUninhabitable);
            if (!home.IndoorZone.Contains(bed.Coordinate)) return new SleepEligibility(SleepDenialReason.BedOutsideHome);
            if (!home.Allows(characterId)) return new SleepEligibility(SleepDenialReason.AccessDenied);
            if (bed.AssignedResidentId != characterId)
                return new SleepEligibility(SleepDenialReason.BedAssignedToAnotherResident);
            if (bed.OccupiedResidentId.HasValue && bed.OccupiedResidentId.Value != characterId)
                return new SleepEligibility(SleepDenialReason.BedOccupied);
            return new SleepEligibility(SleepDenialReason.None, bed);
        }

        public SleepEligibility BeginSleep(StableEntityId characterId)
        {
            var result = EvaluateSleep(characterId);
            if (!result.Allowed) return result;
            beds[result.Bed.Id] = CopyBed(result.Bed, occupied: characterId, setOccupied: true);
            return new SleepEligibility(SleepDenialReason.None, beds[result.Bed.Id]);
        }

        public void EndSleep(StableEntityId characterId)
        {
            var bed = beds.Values.FirstOrDefault(x => x.OccupiedResidentId == characterId);
            if (bed != null) beds[bed.Id] = CopyBed(bed, occupied: null, setOccupied: true);
        }

        public void SetConsent(StableEntityId characterId, StableEntityId partnerId, bool grantsConsent)
        {
            characters.Get(characterId); characters.Get(partnerId);
            if (characterId == partnerId) throw new ArgumentException("Consent partner must be another character.");
            CanonicalPair(characterId, partnerId, out var first, out var second);
            consents.TryGetValue(PairKey(first, second), out var current);
            var firstConsent = current != null && current.FirstConsents;
            var secondConsent = current != null && current.SecondConsents;
            if (characterId == first) firstConsent = grantsConsent; else secondConsent = grantsConsent;
            consents[PairKey(first, second)] = new RelationshipConsentState(first, second, firstConsent, secondConsent);
        }

        public FamilyInteractionEligibility EvaluateFamilyInteraction(StableEntityId firstId, StableEntityId secondId,
            long biologicalTick)
        {
            if (firstId == secondId) return new FamilyInteractionEligibility(FamilyInteractionDenialReason.SameCharacter);
            if (!characters.TryGet(firstId, out var first) || !characters.TryGet(secondId, out var second) ||
                first.LifeState != CharacterLifeState.Alive || second.LifeState != CharacterLifeState.Alive)
                return new FamilyInteractionEligibility(FamilyInteractionDenialReason.CharacterUnavailable);
            if (first.AgeInCompletedYears(biologicalTick) < HealthAndFamilyService.AdultAgeYears ||
                second.AgeInCompletedYears(biologicalTick) < HealthAndFamilyService.AdultAgeYears)
                return new FamilyInteractionEligibility(FamilyInteractionDenialReason.NotAdult);
            CanonicalPair(firstId, secondId, out var a, out var b);
            if (!consents.TryGetValue(PairKey(a, b), out var consent) || !consent.IsMutual)
                return new FamilyInteractionEligibility(FamilyInteractionDenialReason.ConsentMissing);
            if (first.SpouseId != secondId || second.SpouseId != firstId)
                return new FamilyInteractionEligibility(FamilyInteractionDenialReason.NotMutualSpouses);
            var affinityA = first.TryGetRelationship(secondId, out var relationA) ? relationA.Affinity.Value : 0;
            var affinityB = second.TryGetRelationship(firstId, out var relationB) ? relationB.Affinity.Value : 0;
            if (affinityA < MinimumRelationshipAffinity || affinityB < MinimumRelationshipAffinity)
                return new FamilyInteractionEligibility(FamilyInteractionDenialReason.RelationshipTooWeak);
            if (!healthFamily.GetCapabilitiesAt(firstId, biologicalTick).FertilityEligible ||
                !healthFamily.GetCapabilitiesAt(secondId, biologicalTick).FertilityEligible)
                return new FamilyInteractionEligibility(FamilyInteractionDenialReason.HealthPreventsPregnancy);
            var femaleId = first.Sex == CharacterSex.Female ? firstId : second.Sex == CharacterSex.Female ? secondId : default;
            var maleId = first.Sex == CharacterSex.Male ? firstId : second.Sex == CharacterSex.Male ? secondId : default;
            if (!femaleId.IsValid || !maleId.IsValid)
                return new FamilyInteractionEligibility(FamilyInteractionDenialReason.HealthPreventsPregnancy);
            if (healthFamily.Pregnancies.Any(x => x.MotherId == femaleId && x.Status == PregnancyStatus.Active))
                return new FamilyInteractionEligibility(FamilyInteractionDenialReason.PregnancyAlreadyActive);
            return new FamilyInteractionEligibility(FamilyInteractionDenialReason.None);
        }

        public FamilyInteractionState StartFamilyInteraction(StableEntityId firstId, StableEntityId secondId,
            long calendarTick, long biologicalTick)
        {
            var eligibility = EvaluateFamilyInteraction(firstId, secondId, biologicalTick);
            if (!eligibility.Allowed) throw new InvalidOperationException("Family interaction denied: " + eligibility.Reason);
            var first = characters.Get(firstId); var second = characters.Get(secondId);
            var motherId = first.Sex == CharacterSex.Female ? firstId : secondId;
            var fatherId = first.Sex == CharacterSex.Male ? firstId : secondId;
            healthFamily.ApplyRelationshipEvent(new CharacterEventId("marriage_bond_strengthened"), firstId, secondId,
                calendarTick);
            var pregnancy = healthFamily.StartPregnancy(motherId, fatherId, calendarTick, biologicalTick);
            var purpose = "family_interaction:" + firstId + ":" + secondId + ":" + calendarTick;
            var id = DeterministicStableId.Create(healthFamily.WorldSeed, purpose);
            if (interactions.ContainsKey(id)) throw new InvalidOperationException("Family interaction already recorded.");
            var state = new FamilyInteractionState(id, firstId, secondId, calendarTick, pregnancy.PregnancyId);
            interactions.Add(id, state);
            return state;
        }

        public DomesticWorldSaveState CaptureState() => new DomesticWorldSaveState(homes.Values, beds.Values,
            consents.Values, interactions.Values);

        public void RestoreState(DomesticWorldSaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var replacement = new DomesticService(characters, buildings, healthFamily, homeDefinitions);
            foreach (var home in state.Homes) replacement.RegisterHome(home);
            foreach (var bed in state.Beds)
            {
                replacement.RegisterBed(CopyBed(bed, assigned: null, occupied: null, setAssigned: true, setOccupied: true));
                if (bed.AssignedResidentId.HasValue) replacement.AssignBed(bed.Id, bed.AssignedResidentId.Value);
                if (bed.OccupiedResidentId.HasValue)
                {
                    var result = replacement.BeginSleep(bed.OccupiedResidentId.Value);
                    if (!result.Allowed) throw new InvalidOperationException("Saved bed occupancy is invalid: " + result.Reason);
                }
            }
            foreach (var consent in state.Consents) replacement.consents.Add(PairKey(consent.FirstId, consent.SecondId), consent);
            foreach (var interaction in state.Interactions) replacement.interactions.Add(interaction.Id, interaction);
            homes.Clear(); beds.Clear(); consents.Clear(); interactions.Clear();
            foreach (var item in replacement.homes) homes.Add(item.Key, item.Value);
            foreach (var item in replacement.beds) beds.Add(item.Key, item.Value);
            foreach (var item in replacement.consents) consents.Add(item.Key, item.Value);
            foreach (var item in replacement.interactions) interactions.Add(item.Key, item.Value);
        }

        private HomeState GetHome(StableEntityId id) => homes.TryGetValue(id, out var value) ? value :
            throw new KeyNotFoundException("Home was not found.");
        private BedState GetBed(StableEntityId id) => beds.TryGetValue(id, out var value) ? value :
            throw new KeyNotFoundException("Bed was not found.");
        private static BedState CopyBed(BedState bed, StableEntityId? assigned = null,
            StableEntityId? occupied = null, bool setAssigned = false, bool setOccupied = false) =>
            new BedState(bed.Id, bed.HomeBuildingId, bed.Coordinate, bed.SleepPosition,
                setAssigned ? assigned : bed.AssignedResidentId, setOccupied ? occupied : bed.OccupiedResidentId);
        private static void CanonicalPair(StableEntityId left, StableEntityId right,
            out StableEntityId first, out StableEntityId second)
        { if (left.CompareTo(right) <= 0) { first = left; second = right; } else { first = right; second = left; } }
        private static string PairKey(StableEntityId first, StableEntityId second) => first + ":" + second;
    }
}
