using System;
using Game.Domain.AI;
using Game.Domain.Resources;
using Game.Simulation.Resources;
using Game.Simulation.Production;
using Game.Simulation.Research;
using Game.Domain.Domestic;
using Game.Simulation.Domestic;

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
        private readonly bool groundSleep;

        public RestActionExecution(Tier1AiAgentState agent, Tier1AiSettings settings, bool groundSleep = false)
        {
            this.agent = agent;
            this.settings = settings;
            this.groundSleep = groundSleep;
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
            if (groundSleep) agent.Needs.AdjustMood(-0.12d * days);
            if (agent.Needs.Energy >= settings.RestCompletionEnergy) IsFinished = true;
        }

        public void Cancel() => IsFinished = true;
    }

    internal sealed class SleepActionExecution : IUtilityActionExecution
    {
        private readonly Tier1AiAgentState agent;
        private readonly ITier1MovementDriver movement;
        private readonly Tier1AiSettings settings;
        private readonly DomesticService domestic;
        private TimeSpan moveElapsed;
        private WorldPosition lastProgressPosition;
        private bool occupying;

        public SleepActionExecution(Tier1AiAgentState agent, ITier1MovementDriver movement,
            Tier1AiSettings settings, DomesticService domestic, BedState bed)
        {
            this.agent = agent ?? throw new ArgumentNullException(nameof(agent));
            this.movement = movement ?? throw new ArgumentNullException(nameof(movement));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.domestic = domestic ?? throw new ArgumentNullException(nameof(domestic));
            if (bed == null) throw new ArgumentNullException(nameof(bed));
            Phase = AiActionPhase.MoveToTarget;
            agent.SetAction(UtilityActionKind.Rest, Phase);
            movement.MoveTo(bed.SleepPosition, 0.35f);
            lastProgressPosition=movement.CurrentPosition;
        }

        public UtilityActionKind Kind => UtilityActionKind.Rest;
        public AiActionPhase Phase { get; private set; }
        public bool IsFinished { get; private set; }
        public bool Succeeded { get; private set; }
        public bool IsInterruptible => true;
        public bool IsValid => !IsFinished && domestic.EvaluateSleep(agent.CharacterId).Allowed;

        public void Tick(TimeSpan simulationDelta)
        {
            if (IsFinished) return;
            agent.UpdatePosition(movement.CurrentPosition);
            if (Phase == AiActionPhase.MoveToTarget)
            {
                moveElapsed += simulationDelta;
                if(movement.CurrentPosition.DistanceSquared(lastProgressPosition)>=.25f)
                {moveElapsed=TimeSpan.Zero;lastProgressPosition=movement.CurrentPosition;}
                if (movement.State == MovementState.Failed || moveElapsed >= settings.MovementTimeout) { Fail(); return; }
                if (movement.State != MovementState.Arrived) return;
                movement.Stop();
                var start = domestic.BeginSleep(agent.CharacterId);
                if (!start.Allowed) { Fail(); return; }
                occupying = true;
                Phase = AiActionPhase.Resting;
                agent.SetPhase(Phase);
            }
            if (Phase != AiActionPhase.Resting) return;
            agent.Needs.RecoverEnergy(settings.RestRecoveryPerDay * simulationDelta.TotalDays);
            agent.Needs.AdjustMood(0.08d * simulationDelta.TotalDays);
            if (agent.Needs.Energy < settings.RestCompletionEnergy) return;
            Finish();
        }

        public void Cancel() { if (!IsFinished) Fail(); }

        private void Finish()
        {
            if (occupying) domestic.EndSleep(agent.CharacterId);
            occupying = false; movement.Stop(); IsFinished = true; Succeeded = true;
            Phase = AiActionPhase.Completed; agent.SetPhase(Phase);
        }

        private void Fail()
        {
            if (occupying) domestic.EndSleep(agent.CharacterId);
            occupying = false; movement.Stop(); IsFinished = true; Succeeded = false;
            Phase = AiActionPhase.Failed; agent.SetPhase(Phase);
        }
    }

    internal sealed class EatActionExecution : IUtilityActionExecution
    {
        private readonly Tier1AiAgentState agent;
        private readonly Tier1AiSettings settings;
        private readonly FoodConsumptionService food;
        private bool mealConsumed;
        private bool succeeded;

        public EatActionExecution(Tier1AiAgentState agent, Tier1AiSettings settings,
            FoodConsumptionService food = null)
        {
            this.agent = agent ?? throw new ArgumentNullException(nameof(agent));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.food = food;
            agent.SetAction(UtilityActionKind.Eat, AiActionPhase.Eating);
        }

        public UtilityActionKind Kind => UtilityActionKind.Eat;
        public AiActionPhase Phase => AiActionPhase.Eating;
        public bool IsFinished { get; private set; }
        public bool Succeeded => succeeded;
        public bool IsInterruptible => true;
        public bool IsValid => !IsFinished;

        public void Tick(TimeSpan simulationDelta)
        {
            if (!mealConsumed)
            {
                if (food != null && !food.TryConsumeMeal())
                {
                    IsFinished = true;
                    succeeded = false;
                    agent.SetPhase(AiActionPhase.Failed);
                    return;
                }
                mealConsumed = true;
                agent.Needs.AdjustMood(0.08d);
            }
            agent.Needs.SatisfyHunger(settings.HungerRecoveryPerDay * simulationDelta.TotalDays);
            if (agent.Needs.Hunger <= Tier1AiScheduler.CriticalHungerCompletion)
            {
                IsFinished = true;
                succeeded = true;
                agent.SetPhase(AiActionPhase.Completed);
            }
        }

        public void Cancel() => IsFinished = true;
    }

    internal sealed class DrinkActionExecution : IUtilityActionExecution
    {
        private readonly Tier1AiAgentState agent;
        private readonly ITier1MovementDriver movement;
        private readonly TimeSpan timeout;
        private TimeSpan elapsed;

        public DrinkActionExecution(Tier1AiAgentState agent, ITier1MovementDriver movement,
            WorldPosition well, TimeSpan movementTimeout)
        {
            this.agent = agent ?? throw new ArgumentNullException(nameof(agent));
            this.movement = movement ?? throw new ArgumentNullException(nameof(movement));
            timeout = movementTimeout;
            agent.SetAction(UtilityActionKind.Drink, AiActionPhase.MoveToTarget);
            movement.MoveTo(well, 0.9f);
        }

        public UtilityActionKind Kind => UtilityActionKind.Drink;
        public AiActionPhase Phase { get; private set; } = AiActionPhase.MoveToTarget;
        public bool IsFinished { get; private set; }
        public bool Succeeded { get; private set; }
        public bool IsInterruptible => true;
        public bool IsValid => !IsFinished && movement.IsAvailable;

        public void Tick(TimeSpan simulationDelta)
        {
            if (IsFinished) return;
            elapsed += simulationDelta;
            agent.UpdatePosition(movement.CurrentPosition);
            if (movement.State == MovementState.Failed || elapsed >= timeout) { Finish(false); return; }
            if (movement.State != MovementState.Arrived) return;
            movement.Stop();
            Phase = AiActionPhase.Drinking;
            agent.SetPhase(Phase);
            agent.Needs.SatisfyThirst(1d);
            Finish(true);
        }

        public void Cancel() => Finish(false);

        private void Finish(bool success)
        {
            movement.Stop();
            IsFinished = true;
            Succeeded = success;
            Phase = success ? AiActionPhase.Completed : AiActionPhase.Failed;
            agent.SetPhase(Phase);
        }
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
        private WorldPosition lastProgressPosition;

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
            lastProgressPosition = movement.CurrentPosition;
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
                var characterOwner = world.HaulJobs.WorkerOwner(agent.CharacterId);
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
            if ((Phase == AiActionPhase.MoveToSource || Phase == AiActionPhase.MoveToDestination) &&
                movement.CurrentPosition.DistanceSquared(lastProgressPosition) >= 0.25f)
            {
                lastProgressPosition = movement.CurrentPosition;
                phaseElapsed = TimeSpan.Zero;
            }
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
                    if (!world.HaulJobs.DestinationExists(job.Destination))
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
            world.HaulJobs.DropTransportLoad(agent.CharacterId, movement.CurrentPosition);
            world.Claims.Release(claim);
            IsFinished = true;
            Succeeded = false;
            SetPhase(AiActionPhase.Failed);
        }

        private void Pickup()
        {
            var characterOwner = world.HaulJobs.WorkerOwner(agent.CharacterId);
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
            var characterOwner = world.HaulJobs.WorkerOwner(agent.CharacterId);
            var failure = world.Transfers.TransferExact(characterOwner, job.Destination, job.ResourceId, job.Quantity);
            if (failure == TransferFailureReason.None)
            {
                movement.Stop();
                world.ResourceDelivered?.Invoke(job.Destination);
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
            world.HaulJobs.DropTransportLoad(agent.CharacterId, movement.CurrentPosition);
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

    internal sealed class ProductionActionExecution : IUtilityActionExecution
    {
        private readonly Tier1AiAgentState agent;
        private readonly ITier1MovementDriver movement;
        private readonly ProductionService production;
        private readonly Tier1AiSettings settings;
        private readonly ProductionJob job;
        private TimeSpan phaseElapsed;
        private ProductionCompletion completion;
        private InventoryOwner destination;
        private readonly int workRatePerThousand;
        private readonly Action onWorkExperience;
        private WorldPosition lastSourceTarget;
        private bool approachingCorpse;
        private WorldPosition lastMovementPosition;
        private bool Hunting => job.Task.RecipeId.ToString() == "hunt_animal";
        private float StopDistance => Hunting && production.Animals.IsAlive(job.Task.SiteId) ? 8f : .25f;

        public ProductionActionExecution(Tier1AiAgentState agent, ITier1MovementDriver movement,
            ProductionService production, Tier1AiSettings settings, ProductionJob job,
            int workRatePerThousand = 1000, Action onWorkExperience = null)
        {
            this.agent = agent ?? throw new ArgumentNullException(nameof(agent));
            this.movement = movement ?? throw new ArgumentNullException(nameof(movement));
            this.production = production ?? throw new ArgumentNullException(nameof(production));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.job = job;
            if (workRatePerThousand <= 0) throw new ArgumentOutOfRangeException(nameof(workRatePerThousand));
            this.workRatePerThousand = workRatePerThousand;
            this.onWorkExperience = onWorkExperience;
            SetPhase(AiActionPhase.MoveToSource);
            lastSourceTarget = job.SourcePosition;
            lastMovementPosition = movement.CurrentPosition;
            movement.MoveTo(lastSourceTarget, StopDistance);
        }

        public UtilityActionKind Kind => UtilityActionKind.Produce;
        public AiActionPhase Phase { get; private set; }
        public bool IsFinished { get; private set; }
        public bool Succeeded { get; private set; }
        public bool IsInterruptible => Phase == AiActionPhase.MoveToSource ||
            production.TryGetSite(job.Task.SiteId, out var site) && site.Continuous;
        public bool IsValid => !IsFinished && (completion.Completed || production.IsTaskActive(job.Task.Id));

        public void Tick(TimeSpan simulationDelta)
        {
            if (IsFinished) return;
            agent.UpdatePosition(movement.CurrentPosition);
            phaseElapsed += simulationDelta;
            if (Hunting && !completion.Completed && production.TryGetSite(job.Task.SiteId, out var prey))
            {
                var alive = production.Animals.IsAlive(prey.Id);
                var distance = movement.CurrentPosition.DistanceSquared(prey.Position);
                if ((!alive && !approachingCorpse) ||
                    (alive && Phase == AiActionPhase.Working &&
                     distance > production.Animals.Policy.Range * production.Animals.Policy.Range * .8f) ||
                    (Phase == AiActionPhase.MoveToSource && prey.Position.DistanceSquared(lastSourceTarget) > 4f))
                {
                    approachingCorpse = !alive;
                    lastSourceTarget = !alive && production.WorkPosition != null
                        ? production.WorkPosition(agent.CharacterId,prey) : prey.Position;
                    SetPhase(AiActionPhase.MoveToSource);
                    movement.MoveTo(lastSourceTarget, StopDistance);
                }
            }
            if (job.Task.RecipeId.ToString()=="fish_water" && !completion.Completed &&
                (Phase==AiActionPhase.Working || movement.State==MovementState.Arrived) &&
                movement.CurrentPosition.DistanceSquared(lastSourceTarget)>1.5f*1.5f)
            {
                SetPhase(AiActionPhase.MoveToSource);
                movement.MoveTo(lastSourceTarget,StopDistance);
            }
            if ((Phase == AiActionPhase.MoveToSource || Phase == AiActionPhase.DeliverProduction) &&
                movement.CurrentPosition.DistanceSquared(lastMovementPosition) > .04f)
            { phaseElapsed = TimeSpan.Zero; lastMovementPosition = movement.CurrentPosition; }
            if (movement.State == MovementState.Failed ||
                ((Phase == AiActionPhase.MoveToSource || Phase == AiActionPhase.DeliverProduction) &&
                 phaseElapsed >= settings.MovementTimeout))
            {
                Fail();
                return;
            }

            switch (Phase)
            {
                case AiActionPhase.MoveToSource:
                    if (!production.IsTaskActive(job.Task.Id)) { Fail(); return; }
                    if (movement.State == MovementState.Arrived)
                    {
                        movement.Stop();
                        SetPhase(AiActionPhase.Working);
                    }
                    break;
                case AiActionPhase.Working:
                    if (job.Task.RecipeId.ToString() == "fish_water" &&
                        production.ManualFishingAccess != null &&
                        !production.ManualFishingAccess(job.Task.SiteId))
                    {
                        production.Cancel(job.Task.Id);
                        Finish();
                        break;
                    }
                    completion = production.AdvanceTask(job.Task.Id, TimeSpan.FromTicks(checked(
                        Hunting && production.Animals.IsAlive(job.Task.SiteId) ? simulationDelta.Ticks :
                        simulationDelta.Ticks * workRatePerThousand / 1000L)));
                    if (!completion.Completed) break;
                    if (!completion.ProducedOutput || completion.OutputOwner.Kind == InventoryOwnerKind.BuildingStorage ||
                        completion.OutputOwner.Kind == InventoryOwnerKind.WorldPile ||
                        completion.OutputOwner.Kind == InventoryOwnerKind.ProductionOutput)
                    {
                        Finish();
                        break;
                    }
                    if (production.TryFindStorage(completion.ResourceId, completion.Units, agent.Position,
                            out destination, out var destinationPosition))
                    {
                        SetPhase(AiActionPhase.DeliverProduction);
                        movement.MoveTo(destinationPosition, 1.1f);
                    }
                    else Finish(); // Full storage: output remains in the canonical character inventory.
                    break;
                case AiActionPhase.DeliverProduction:
                    if (movement.State != MovementState.Arrived) break;
                    production.TryDeposit(agent.CharacterId, destination, completion.ResourceId, completion.Units);
                    Finish(); // A capacity race leaves the output with the character; nothing is lost.
                    break;
            }
        }

        public void Cancel()
        {
            if (IsFinished) return;
            movement.Stop();
            if (!completion.Completed) production.Cancel(job.Task.Id);
            IsFinished = true;
            Succeeded = false;
            SetPhase(AiActionPhase.Failed);
        }

        private void Finish()
        {
            movement.Stop();
            onWorkExperience?.Invoke();
            IsFinished = true;
            Succeeded = true;
            SetPhase(AiActionPhase.Completed);
        }

        private void Fail()
        {
            movement.Stop();
            if (!completion.Completed) production.Cancel(job.Task.Id);
            IsFinished = true;
            Succeeded = false;
            SetPhase(AiActionPhase.Failed);
        }

        private void SetPhase(AiActionPhase phase)
        {
            Phase = phase;
            phaseElapsed = TimeSpan.Zero;
            agent.SetAction(UtilityActionKind.Produce, phase);
        }
    }

    internal sealed class ResearchActionExecution : IUtilityActionExecution
    {
        private readonly Tier1AiAgentState agent;
        private readonly ITier1MovementDriver movement;
        private readonly ResearchService research;

        public ResearchActionExecution(Tier1AiAgentState agent, ITier1MovementDriver movement,
            ResearchService research)
        {
            this.agent = agent ?? throw new ArgumentNullException(nameof(agent));
            this.movement = movement ?? throw new ArgumentNullException(nameof(movement));
            this.research = research ?? throw new ArgumentNullException(nameof(research));
            movement.Stop();
            agent.SetAction(UtilityActionKind.Research, AiActionPhase.Researching);
        }

        public UtilityActionKind Kind => UtilityActionKind.Research;
        public AiActionPhase Phase => AiActionPhase.Researching;
        public bool IsFinished { get; private set; }
        public bool Succeeded => IsFinished && research.ActiveProject == null;
        public bool IsInterruptible => true;
        public bool IsValid => !IsFinished && research.ActiveProject != null && research.IsAssigned(agent.CharacterId);

        public void Tick(TimeSpan simulationDelta)
        {
            if (!IsValid) IsFinished = true;
        }

        public void Cancel()
        {
            IsFinished = true;
            movement.Stop();
        }
    }
}
