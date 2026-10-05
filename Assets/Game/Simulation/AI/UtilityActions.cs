using System;
using Game.Domain.AI;
using Game.Domain.Resources;
using Game.Simulation.Resources;

namespace Game.Simulation.AI
{
    internal interface IUtilityActionExecution
    {
        UtilityActionKind Kind { get; }
        AiActionPhase Phase { get; }
        bool IsFinished { get; }
        bool Succeeded { get; }
        bool IsInterruptible { get; }
        bool IsValid { get; }
        void Tick(TimeSpan simulationDelta);
        void Cancel();
    }

    internal sealed class RestActionExecution : IUtilityActionExecution
    {
        private readonly Tier1AiAgentState agent;
        private readonly Tier1AiSettings settings;

        public RestActionExecution(Tier1AiAgentState agent, Tier1AiSettings settings)
        {
            this.agent = agent;
            this.settings = settings;
            agent.SetAction(UtilityActionKind.Rest, AiActionPhase.Resting);
        }

        public UtilityActionKind Kind => UtilityActionKind.Rest;
        public AiActionPhase Phase => AiActionPhase.Resting;
        public bool IsFinished { get; private set; }
        public bool Succeeded => IsFinished;
        public bool IsInterruptible => true;
        public bool IsValid => !IsFinished;

        public void Tick(TimeSpan simulationDelta)
        {
            var days = simulationDelta.TotalDays;
            agent.Needs.RecoverEnergy(settings.RestRecoveryPerDay * days);
            if (agent.Needs.Energy >= settings.RestCompletionEnergy) IsFinished = true;
        }

        public void Cancel() => IsFinished = true;
    }

    internal sealed class HaulActionExecution : IUtilityActionExecution
    {
        private readonly Tier1AiAgentState agent;
        private readonly ITier1MovementDriver movement;
        private readonly Tier1AiWorld world;
        private readonly Tier1AiSettings settings;
        private HaulJobCandidate job;
        private readonly HaulClaim claim;
        private readonly UtilityActionKind kind;
        private TimeSpan phaseElapsed;

        public HaulActionExecution(Tier1AiAgentState agent, ITier1MovementDriver movement, Tier1AiWorld world,
            Tier1AiSettings settings, HaulJobCandidate job, HaulClaim claim,
            UtilityActionKind kind = UtilityActionKind.Haul)
        {
            if (kind != UtilityActionKind.Haul && kind != UtilityActionKind.ManualHaul)
                throw new ArgumentOutOfRangeException(nameof(kind));
            this.agent = agent;
            this.movement = movement;
            this.world = world;
            this.settings = settings;
            this.job = job;
            this.claim = claim;
            this.kind = kind;
            SetPhase(AiActionPhase.MoveToSource);
            movement.MoveTo(job.SourcePosition, 0.85f);
        }

        public UtilityActionKind Kind => kind;
        public AiActionPhase Phase { get; private set; }
        public bool IsFinished { get; private set; }
        public bool Succeeded { get; private set; }
        public bool IsInterruptible => claim.Phase != HaulClaimPhase.Carrying;
        public bool IsValid
        {
            get
            {
                if (IsFinished || !movement.IsAvailable || !world.Claims.IsActive(claim)) return false;
                var characterOwner = new InventoryOwner(InventoryOwnerKind.Character, agent.CharacterId);
                if (claim.Phase == HaulClaimPhase.Carrying)
                    return world.HaulJobs.InventoryExists(characterOwner, out var carried) &&
                        carried.CanRemove(job.ResourceId, job.Quantity);
                return world.HaulJobs.InventoryExists(job.Source, out var source) && source.CanRemove(job.ResourceId, job.Quantity);
            }
        }

        public void Tick(TimeSpan simulationDelta)
        {
            if (IsFinished) return;
            agent.UpdatePosition(movement.CurrentPosition);
            phaseElapsed += simulationDelta;
            if (!IsValid || movement.State == MovementState.Failed ||
                ((Phase == AiActionPhase.MoveToSource || Phase == AiActionPhase.MoveToDestination) &&
                 phaseElapsed >= settings.MovementTimeout))
            {
                Fail();
                return;
            }

            switch (Phase)
            {
                case AiActionPhase.MoveToSource:
                    if (movement.State == MovementState.Arrived) SetPhase(AiActionPhase.Pickup);
                    break;
                case AiActionPhase.Pickup:
                    Pickup();
                    break;
                case AiActionPhase.MoveToDestination:
                    if (!world.HaulJobs.BuildingStorageExists(job.Destination))
                    {
                        if (!TryReplanDestination()) Fail();
                    }
                    else if (movement.State == MovementState.Arrived) SetPhase(AiActionPhase.Dropoff);
                    break;
                case AiActionPhase.Dropoff:
                    Dropoff();
                    break;
            }
        }

        public void Cancel()
        {
            if (IsFinished) return;
            movement.Stop();
            world.Claims.Release(claim);
            IsFinished = true;
            Succeeded = false;
            SetPhase(AiActionPhase.Failed);
        }

        private void Pickup()
        {
            var characterOwner = new InventoryOwner(InventoryOwnerKind.Character, agent.CharacterId);
            var failure = world.Transfers.TransferExact(job.Source, characterOwner, job.ResourceId, job.Quantity);
            if (failure != TransferFailureReason.None)
            {
                Fail();
                return;
            }
            world.Claims.MarkPickedUp(claim);
            SetPhase(AiActionPhase.MoveToDestination);
            movement.MoveTo(job.DestinationPosition, 1.1f);
        }

        private void Dropoff()
        {
            var characterOwner = new InventoryOwner(InventoryOwnerKind.Character, agent.CharacterId);
            var failure = world.Transfers.TransferExact(characterOwner, job.Destination, job.ResourceId, job.Quantity);
            if (failure == TransferFailureReason.None)
            {
                movement.Stop();
                world.Claims.Release(claim);
                IsFinished = true;
                Succeeded = true;
                SetPhase(AiActionPhase.Completed);
                return;
            }
            if ((failure == TransferFailureReason.DestinationRejected || failure == TransferFailureReason.MissingDestination) &&
                TryReplanDestination()) return;
            Fail();
        }

        private bool TryReplanDestination()
        {
            if (!world.HaulJobs.TryFindDestinationForCarried(agent, job.ResourceId, job.Quantity.Units, claim,
                    out var destination, out var position)) return false;
            if (!world.HaulJobs.InventoryExists(destination, out var inventory) ||
                !world.Claims.TryChangeDestination(claim, destination, inventory.Capacity - inventory.TotalUnits)) return false;
            job = new HaulJobCandidate(job.Source, destination, job.ResourceId, job.Quantity, job.SourcePosition, position);
            SetPhase(AiActionPhase.MoveToDestination);
            movement.MoveTo(position, 1.1f);
            return true;
        }

        private void Fail()
        {
            movement.Stop();
            world.Claims.Release(claim);
            IsFinished = true;
            Succeeded = false;
            SetPhase(AiActionPhase.Failed);
        }

        private void SetPhase(AiActionPhase phase)
        {
            Phase = phase;
            phaseElapsed = TimeSpan.Zero;
            agent.SetAction(kind, phase);
        }
    }
}
