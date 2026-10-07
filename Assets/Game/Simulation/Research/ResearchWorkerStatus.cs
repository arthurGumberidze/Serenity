using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;

namespace Game.Simulation.Research
{
    public sealed class ResearchWorkerStatus
    {
        internal ResearchWorkerStatus(StableEntityId[] assigned, StableEntityId[] active)
        {
            AssignedWorkerIds = Array.AsReadOnly(assigned ?? Array.Empty<StableEntityId>());
            ActiveWorkerIds = Array.AsReadOnly(active ?? Array.Empty<StableEntityId>());
        }

        public IReadOnlyList<StableEntityId> AssignedWorkerIds { get; }
        public IReadOnlyList<StableEntityId> ActiveWorkerIds { get; }
        public int AssignedCount => AssignedWorkerIds.Count;
        public int ActiveCount => ActiveWorkerIds.Count;
    }

    public static class ResearchWorkerStatusQuery
    {
        public static ResearchWorkerStatus Capture(ResearchService research, Tier1AiAgentRegistry agents)
        {
            if (research == null) throw new ArgumentNullException(nameof(research));
            if (agents == null) throw new ArgumentNullException(nameof(agents));
            var assigned = research.AssignedWorkers.ToArray();
            var active = research.ActiveProject == null
                ? Array.Empty<StableEntityId>()
                : assigned.Where(id => agents.TryGet(id, out var agent) &&
                        agent.CurrentAction == UtilityActionKind.Research &&
                        agent.Phase == AiActionPhase.Researching)
                    .ToArray();
            return new ResearchWorkerStatus(assigned, active);
        }
    }
}
