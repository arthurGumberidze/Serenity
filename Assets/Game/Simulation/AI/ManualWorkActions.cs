using System;
using Game.Domain.AI;
using Game.Domain.Work;
using Game.Simulation.Work;
using Game.Simulation.Production;

namespace Game.Simulation.AI
{
    internal sealed class ManualDirectActionExecution : IUtilityActionExecution
    {
        private readonly Tier1AiAgentState agent;
        private readonly ITier1MovementDriver movement;
        private readonly WorkManager work;
        private readonly WorkOrder job;
        private readonly TimeSpan timeout;
        private readonly Action onCompleted;
        private TimeSpan elapsed;
        private TimeSpan workElapsed;

        public ManualDirectActionExecution(Tier1AiAgentState agent, ITier1MovementDriver movement,
            WorkManager work, WorkOrder job, TimeSpan timeout, Action onCompleted = null)
        {
            this.agent = agent ?? throw new ArgumentNullException(nameof(agent));
            this.movement = movement ?? throw new ArgumentNullException(nameof(movement));
            this.work = work ?? throw new ArgumentNullException(nameof(work));
            this.job = job ?? throw new ArgumentNullException(nameof(job));
            this.timeout = timeout;
            this.onCompleted = onCompleted;
            if (!work.Start(job.Id)) throw new InvalidOperationException("Direct work order cannot start.");
            agent.SetAction(UtilityActionKind.ManualTask, AiActionPhase.MoveToTarget);
            movement.MoveTo(job.Target.Position, 0.9f);
        }

        public UtilityActionKind Kind => UtilityActionKind.ManualTask;
        public AiActionPhase Phase => agent.Phase;
        public bool IsFinished { get; private set; }
        public bool Succeeded { get; private set; }
        public bool IsInterruptible => agent.Phase == AiActionPhase.MoveToTarget;
        public bool IsValid => !IsFinished && !job.IsTerminal && movement.IsAvailable;

        public void Tick(TimeSpan simulationDelta)
        {
            if (IsFinished) return;
            agent.UpdatePosition(movement.CurrentPosition);
            elapsed += simulationDelta;
            if (job.Status == WorkJobStatus.Cancelled) { Cancel(); return; }
            if (!movement.IsAvailable || movement.State == MovementState.Failed || elapsed >= timeout)
            {
                movement.Stop();
                work.Fail(job.Id);
                IsFinished = true;
                agent.SetAction(Kind, AiActionPhase.Failed);
                return;
            }
            if (agent.Phase == AiActionPhase.MoveToTarget)
            {
                if (movement.State != MovementState.Arrived) return;
                movement.Stop();
                agent.SetAction(Kind, AiActionPhase.Working);
            }
            workElapsed += simulationDelta;
            if (workElapsed < TimeSpan.FromMinutes(10)) return;
            onCompleted?.Invoke();
            work.Complete(job.Id);
            IsFinished = true;
            Succeeded = true;
            agent.SetAction(Kind, AiActionPhase.Completed);
        }

        public void Cancel()
        {
            if (IsFinished) return;
            movement.Stop();
            if (!job.IsTerminal) work.RequeueAssigned(job.Id);
            IsFinished = true;
            agent.SetAction(Kind, AiActionPhase.Failed);
        }
    }

    internal sealed class ManualProductionActionExecution : IUtilityActionExecution
    {
        private readonly ProductionActionExecution inner;
        private readonly WorkManager work;
        private readonly WorkOrder job;

        public ManualProductionActionExecution(ProductionActionExecution inner, WorkManager work, WorkOrder job)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.work = work ?? throw new ArgumentNullException(nameof(work));
            this.job = job ?? throw new ArgumentNullException(nameof(job));
            if (!work.Start(job.Id)) throw new InvalidOperationException("Production work order cannot start.");
            work.SetClaimState(job.Id, WorkClaimState.Reserved);
        }
        public UtilityActionKind Kind => UtilityActionKind.ManualTask;
        public AiActionPhase Phase => inner.Phase;
        public bool IsFinished => inner.IsFinished;
        public bool Succeeded => inner.Succeeded;
        public bool IsInterruptible => inner.IsInterruptible;
        public bool IsValid => !job.IsTerminal && inner.IsValid;
        public void Tick(TimeSpan simulationDelta)
        {
            inner.Tick(simulationDelta);
            if (!inner.IsFinished || job.IsTerminal) return;
            work.SetClaimState(job.Id, WorkClaimState.Released);
            if (inner.Succeeded) work.Complete(job.Id); else work.Fail(job.Id);
        }
        public void Cancel()
        {
            if (!inner.IsFinished) inner.Cancel();
            work.SetClaimState(job.Id, WorkClaimState.Released);
            if (!job.IsTerminal) work.RequeueAssigned(job.Id);
        }
    }

    internal sealed class ManualMoveActionExecution : IUtilityActionExecution
    {
        private readonly Tier1AiAgentState agent;
        private readonly ITier1MovementDriver movement;
        private readonly WorkManager work;
        private readonly WorkOrder job;
        private readonly TimeSpan timeout;
        private TimeSpan elapsed;

        public ManualMoveActionExecution(Tier1AiAgentState agent, ITier1MovementDriver movement,
            WorkManager workManager, WorkOrder order, TimeSpan movementTimeout)
        {
            this.agent = agent ?? throw new ArgumentNullException(nameof(agent));
            this.movement = movement ?? throw new ArgumentNullException(nameof(movement));
            work = workManager ?? throw new ArgumentNullException(nameof(workManager));
            job = order ?? throw new ArgumentNullException(nameof(order));
            timeout = movementTimeout;
            if (job.Type != WorkJobType.Move || job.AssigneeId != agent.CharacterId ||
                !work.Start(job.Id)) throw new InvalidOperationException("Move work order cannot start.");
            agent.SetAction(UtilityActionKind.ManualMove, AiActionPhase.MoveToTarget);
            movement.MoveTo(job.Target.Position, 0.85f);
        }

        public UtilityActionKind Kind => UtilityActionKind.ManualMove;
        public AiActionPhase Phase => agent.Phase;
        public bool IsFinished { get; private set; }
        public bool Succeeded { get; private set; }
        public bool IsInterruptible => true;
        public bool IsValid => !IsFinished && movement.IsAvailable &&
            (job.Status == WorkJobStatus.Active || job.Status == WorkJobStatus.Assigned);

        public void Tick(TimeSpan simulationDelta)
        {
            if (IsFinished) return;
            agent.UpdatePosition(movement.CurrentPosition);
            elapsed += simulationDelta;
            if (job.Status == WorkJobStatus.Cancelled)
            {
                Cancel();
                return;
            }
            if (!movement.IsAvailable || movement.State == MovementState.Failed || elapsed >= timeout)
            {
                movement.Stop();
                work.Fail(job.Id);
                IsFinished = true;
                agent.SetAction(Kind, AiActionPhase.Failed);
                return;
            }
            if (movement.State != MovementState.Arrived) return;
            movement.Stop();
            work.Complete(job.Id);
            IsFinished = true;
            Succeeded = true;
            agent.SetAction(Kind, AiActionPhase.Completed);
        }

        public void Cancel()
        {
            if (IsFinished) return;
            movement.Stop();
            if (!job.IsTerminal) work.RequeueAssigned(job.Id);
            IsFinished = true;
            agent.SetAction(Kind, AiActionPhase.Failed);
        }
    }

    internal sealed class ManualHaulActionExecution : IUtilityActionExecution
    {
        private readonly HaulActionExecution inner;
        private readonly HaulClaim claim;
        private readonly WorkManager work;
        private readonly WorkOrder job;

        public ManualHaulActionExecution(HaulActionExecution execution, HaulClaim haulClaim,
            WorkManager workManager, WorkOrder order)
        {
            inner = execution ?? throw new ArgumentNullException(nameof(execution));
            claim = haulClaim ?? throw new ArgumentNullException(nameof(haulClaim));
            work = workManager ?? throw new ArgumentNullException(nameof(workManager));
            job = order ?? throw new ArgumentNullException(nameof(order));
            if (!work.Start(job.Id)) throw new InvalidOperationException("Haul work order cannot start.");
            work.SetClaimState(job.Id, WorkClaimState.Reserved);
        }

        public UtilityActionKind Kind => UtilityActionKind.ManualHaul;
        public AiActionPhase Phase => inner.Phase;
        public bool IsFinished => inner.IsFinished;
        public bool Succeeded => inner.Succeeded;
        public bool IsInterruptible => inner.IsInterruptible;
        public bool IsValid => !job.IsTerminal && inner.IsValid;

        public void Tick(TimeSpan simulationDelta)
        {
            if (inner.IsFinished) return;
            inner.Tick(simulationDelta);
            if (!inner.IsFinished)
            {
                work.SetClaimState(job.Id, claim.Phase == HaulClaimPhase.Carrying
                    ? WorkClaimState.Carrying : WorkClaimState.Reserved);
                return;
            }
            work.SetClaimState(job.Id, WorkClaimState.Released);
            if (job.IsTerminal) return;
            if (inner.Succeeded) work.Complete(job.Id);
            else work.Fail(job.Id);
        }

        public void Cancel()
        {
            if (inner.IsFinished) return;
            inner.Cancel();
            work.SetClaimState(job.Id, WorkClaimState.Released);
            if (!job.IsTerminal) work.RequeueAssigned(job.Id);
        }
    }
}
