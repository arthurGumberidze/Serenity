using System;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Control;
using Game.Domain.Dynasties;
using Game.Domain.Health;
using Game.Domain.Military;
using Game.Simulation.AI;
using Game.Simulation.Dynasties;
using Game.Simulation.Military;
using Game.Simulation.Tiers;

namespace Game.Simulation.Control
{
    public enum DirectControlEligibilityFailure
    {
        None = 0,
        AlreadyControllingAnotherCharacter = 1,
        MissingCharacter = 2,
        Dead = 3,
        Child = 4,
        Incapacitated = 5,
        NotPlayerControlledRole = 6,
        NotManaged = 7,
        AggregateMilitaryUnit = 8,
        TierTransitionFailed = 9
    }

    public enum PlayerControlExitReason
    {
        Requested = 0,
        Incapacitated = 1,
        Died = 2,
        InvalidAfterLoad = 3
    }

    public readonly struct DirectControlEligibility
    {
        public DirectControlEligibility(DirectControlEligibilityFailure failure)
        {
            Failure = failure;
        }

        public DirectControlEligibilityFailure Failure { get; }
        public bool IsEligible => Failure == DirectControlEligibilityFailure.None;
    }

    /// <summary>
    /// Engine-independent authority for who owns one Character's locomotion. It never creates a Character,
    /// presentation object, camera or combat state.
    /// </summary>
    public sealed class PlayerControlService : IExternalCharacterActivityOwner, ICharacterTierTransitionGuard
    {
        private readonly CharacterRegistry characters;
        private readonly ICharacterCapabilityProvider capabilities;
        private readonly TierManager tiers;
        private readonly DynastyState dynasty;
        private readonly FamilyTreeQuery family;
        private readonly MilitaryUnitService military;
        private readonly MilitarySideId playerMilitarySide;
        private readonly Tier1AiScheduler scheduler;

        public PlayerControlService(CharacterRegistry characterRegistry,
            ICharacterCapabilityProvider capabilityProvider, TierManager tierManager, DynastyState dynastyState,
            MilitaryUnitService militaryUnitService, Tier1AiScheduler aiScheduler,
            MilitarySideId? controlledMilitarySide = null)
        {
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            capabilities = capabilityProvider ?? throw new ArgumentNullException(nameof(capabilityProvider));
            tiers = tierManager ?? throw new ArgumentNullException(nameof(tierManager));
            dynasty = dynastyState ?? throw new ArgumentNullException(nameof(dynastyState));
            family = new FamilyTreeQuery(characters);
            military = militaryUnitService;
            scheduler = aiScheduler ?? throw new ArgumentNullException(nameof(aiScheduler));
            playerMilitarySide = controlledMilitarySide ?? new MilitarySideId("player");
        }

        public PlayerControlState State { get; } = new PlayerControlState();
        public PlayerControlExitReason LastExitReason { get; private set; } = PlayerControlExitReason.Requested;
        public event Action ControlChanged;

        public DirectControlEligibility Evaluate(StableEntityId characterId)
        {
            if (!characterId.IsValid || !characters.TryGet(characterId, out var character))
                return new DirectControlEligibility(DirectControlEligibilityFailure.MissingCharacter);
            if (character.LifeState != CharacterLifeState.Alive)
                return new DirectControlEligibility(DirectControlEligibilityFailure.Dead);
            var capability = capabilities.GetCapabilities(characterId);
            if (!capability.IsAdult) return new DirectControlEligibility(DirectControlEligibilityFailure.Child);
            if (capability.IsIncapacitated || capability.MovementPerThousand <= 0)
                return new DirectControlEligibility(DirectControlEligibilityFailure.Incapacitated);
            if (!IsPlayerControlledRole(characterId))
                return new DirectControlEligibility(DirectControlEligibilityFailure.NotPlayerControlledRole);
            if (!tiers.TryGetTier(characterId, out _))
                return new DirectControlEligibility(DirectControlEligibilityFailure.NotManaged);
            if (military != null && military.TryGetForMember(characterId, out var unit) &&
                unit.Representation == MilitaryUnitRepresentationMode.Aggregate)
                return new DirectControlEligibility(DirectControlEligibilityFailure.AggregateMilitaryUnit);
            return new DirectControlEligibility(DirectControlEligibilityFailure.None);
        }

        public DirectControlEligibility TryEnterThirdPerson(StableEntityId characterId)
        {
            if (State.Mode == PlayerControlMode.ThirdPerson)
            {
                return State.ControlledCharacterId == characterId
                    ? new DirectControlEligibility(DirectControlEligibilityFailure.None)
                    : new DirectControlEligibility(DirectControlEligibilityFailure.AlreadyControllingAnotherCharacter);
            }
            var eligibility = Evaluate(characterId);
            if (!eligibility.IsEligible) return eligibility;
            try
            {
                if (tiers.GetTier(characterId) != CharacterSimulationTier.Tier1)
                    tiers.Transition(characterId, CharacterSimulationTier.Tier1);
            }
            catch (InvalidOperationException)
            {
                return new DirectControlEligibility(DirectControlEligibilityFailure.TierTransitionFailed);
            }
            scheduler.SuspendForExternalControl(characterId);
            State.EnterThirdPerson(characterId);
            ControlChanged?.Invoke();
            return new DirectControlEligibility(DirectControlEligibilityFailure.None);
        }

        public bool ExitThirdPerson(PlayerControlExitReason reason = PlayerControlExitReason.Requested)
        {
            if (State.Mode != PlayerControlMode.ThirdPerson) return false;
            var id = State.ControlledCharacterId.Value;
            State.ReturnToRts();
            LastExitReason = reason;
            scheduler.ResumeAfterExternalControl(id);
            ControlChanged?.Invoke();
            return true;
        }

        public bool RefreshValidity()
        {
            if (State.Mode != PlayerControlMode.ThirdPerson) return true;
            var id = State.ControlledCharacterId.Value;
            if (!characters.TryGet(id, out var character) || character.LifeState != CharacterLifeState.Alive)
            {
                ExitThirdPerson(PlayerControlExitReason.Died);
                return false;
            }
            if (capabilities.GetCapabilities(id).IsIncapacitated)
            {
                ExitThirdPerson(PlayerControlExitReason.Incapacitated);
                return false;
            }
            return true;
        }

        public PlayerControlSaveState CaptureState() => State.CaptureState();

        public bool RestoreState(PlayerControlSaveState saved)
        {
            if (saved == null || saved.Mode == PlayerControlMode.Rts)
            {
                if (State.Mode == PlayerControlMode.ThirdPerson)
                    ExitThirdPerson(PlayerControlExitReason.Requested);
                return true;
            }
            if (State.Mode == PlayerControlMode.ThirdPerson) ExitThirdPerson(PlayerControlExitReason.Requested);
            var result = TryEnterThirdPerson(saved.ControlledCharacterId.Value);
            if (result.IsEligible) return true;
            State.ReturnToRts();
            LastExitReason = PlayerControlExitReason.InvalidAfterLoad;
            ControlChanged?.Invoke();
            return false;
        }

        public bool IsExternallyControlled(StableEntityId characterId) =>
            State.Mode == PlayerControlMode.ThirdPerson && State.ControlledCharacterId == characterId;

        public bool CanTransition(StableEntityId characterId, CharacterSimulationTier targetTier) =>
            targetTier == CharacterSimulationTier.Tier1 || !IsExternallyControlled(characterId);

        private bool IsPlayerControlledRole(StableEntityId characterId)
        {
            if (dynasty.CurrentMonarchId == characterId) return true;
            if (dynasty.CurrentMonarchId.HasValue)
            {
                var heir = family.GetOrderedSuccessionCandidates(dynasty, dynasty.CurrentMonarchId.Value)
                    .FirstOrDefault();
                if (heir != null && heir.Id == characterId) return true;
            }
            return military != null && military.TryGetForMember(characterId, out var unit) &&
                   unit.OwnerSideId == playerMilitarySide && unit.CaptainId == characterId;
        }
    }

    public sealed class CharacterActivityOwnerSet : IExternalCharacterActivityOwner
    {
        private readonly IExternalCharacterActivityOwner[] owners;
        public CharacterActivityOwnerSet(params IExternalCharacterActivityOwner[] activityOwners) =>
            owners = (activityOwners ?? Array.Empty<IExternalCharacterActivityOwner>())
                .Where(x => x != null).ToArray();
        public bool IsExternallyControlled(StableEntityId characterId) =>
            owners.Any(x => x.IsExternallyControlled(characterId));
    }

    public sealed class CharacterTierTransitionGuardSet : ICharacterTierTransitionGuard
    {
        private readonly ICharacterTierTransitionGuard[] guards;
        public CharacterTierTransitionGuardSet(params ICharacterTierTransitionGuard[] transitionGuards) =>
            guards = (transitionGuards ?? Array.Empty<ICharacterTierTransitionGuard>())
                .Where(x => x != null).ToArray();
        public bool CanTransition(StableEntityId characterId, CharacterSimulationTier targetTier) =>
            guards.All(x => x.CanTransition(characterId, targetTier));
    }
}
