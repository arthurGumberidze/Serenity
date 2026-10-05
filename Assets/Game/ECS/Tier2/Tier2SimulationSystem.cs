using System;
using Unity.Burst;
using Unity.Entities;

namespace Game.ECS.Tier2
{
    [BurstCompile]
    public partial struct Tier2AgentSimulationJob : IJobEntity
    {
        public float DeltaGameDays;
        public long CalendarDeltaTicks;
        public long BiologicalDeltaTicks;

        private void Execute(ref Tier2Position position, in Tier2Movement movement,
            ref Tier2SimulationProgress progress)
        {
            if (movement.IsMoving != 0)
                position.Value += movement.UnitsPerGameDay * DeltaGameDays;
            progress.ProcessedCalendarTicks += CalendarDeltaTicks;
            progress.ProcessedBiologicalTicks += BiologicalDeltaTicks;
            progress.StepCount++;
        }
    }

    /// <summary>One batched, Burst/job-backed update for every active Tier 2 character projection.</summary>
    public partial class Tier2SimulationSystem : SystemBase
    {
        private EntityQuery agentQuery;

        public int LastScheduledEntityCount { get; private set; }
        public uint LastSequence { get; private set; }

        protected override void OnCreate()
        {
            agentQuery = GetEntityQuery(
                ComponentType.ReadWrite<Tier2Position>(), ComponentType.ReadOnly<Tier2Movement>(),
                ComponentType.ReadWrite<Tier2SimulationProgress>());
            RequireForUpdate<Tier2SimulationStep>();
        }

        protected override void OnUpdate()
        {
            var step = SystemAPI.GetSingleton<Tier2SimulationStep>();
            LastSequence = step.Sequence;
            if (step.CalendarDeltaTicks == 0 && step.BiologicalDeltaTicks == 0)
            {
                LastScheduledEntityCount = 0;
                return;
            }

            LastScheduledEntityCount = agentQuery.CalculateEntityCount();
            var deltaGameDays = (float)((double)step.CalendarDeltaTicks / TimeSpan.TicksPerDay);
            Dependency = new Tier2AgentSimulationJob
            {
                DeltaGameDays = deltaGameDays,
                CalendarDeltaTicks = step.CalendarDeltaTicks,
                BiologicalDeltaTicks = step.BiologicalDeltaTicks
            }.ScheduleParallel(agentQuery, Dependency);
        }
    }
}
