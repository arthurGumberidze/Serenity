using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Work;
using Game.Domain.Resources;
using Game.Simulation.AI;

namespace Game.Simulation.Work
{
    public sealed class WorkCandidateDiagnostic
    {
        public WorkCandidateDiagnostic(StableEntityId characterId, int suitability, double distanceSquared,
            bool available, string reason)
        {
            CharacterId = characterId;
            Suitability = suitability;
            DistanceSquared = distanceSquared;
            Available = available;
            Reason = reason ?? string.Empty;
        }
        public StableEntityId CharacterId { get; }
        public int Suitability { get; }
        public double DistanceSquared { get; }
        public bool Available { get; }
        public string Reason { get; }
    }

    /// <summary>
    /// Pure command/planning boundary for click -> action -> priority orders. It owns no frame loop;
    /// callers trigger deterministic re-evaluation from the central simulation tick.
    /// </summary>
    public sealed class DirectWorkCommandService
    {
        private static readonly WorkGroupRole[] RequiredRoles =
        {
            WorkGroupRole.Gatherers, WorkGroupRole.Builders, WorkGroupRole.Haulers,
            WorkGroupRole.Crafters, WorkGroupRole.FarmersGatherers, WorkGroupRole.Scientists,
            WorkGroupRole.Scouts, WorkGroupRole.Warriors, WorkGroupRole.Caregivers
        };

        private readonly CharacterRegistry characters;
        private readonly Tier1AiAgentRegistry agents;
        private readonly Tier1AiScheduler scheduler;
        private readonly WorkManager work;

        public DirectWorkCommandService(CharacterRegistry characters, Tier1AiAgentRegistry agents,
            Tier1AiScheduler scheduler, WorkManager work)
        {
            this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
            this.agents = agents ?? throw new ArgumentNullException(nameof(agents));
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            this.work = work ?? throw new ArgumentNullException(nameof(work));
        }

        public static WorkGroupRole RoleFor(WorkJobType type)
        {
            switch (type)
            {
                case WorkJobType.Gather: return WorkGroupRole.Gatherers;
                case WorkJobType.Build: return WorkGroupRole.Builders;
                case WorkJobType.Haul: return WorkGroupRole.Haulers;
                case WorkJobType.Craft: return WorkGroupRole.Crafters;
                case WorkJobType.Farm: return WorkGroupRole.FarmersGatherers;
                case WorkJobType.Research: return WorkGroupRole.Scientists;
                case WorkJobType.Scout: return WorkGroupRole.Scouts;
                case WorkJobType.Military: return WorkGroupRole.Warriors;
                case WorkJobType.Care: return WorkGroupRole.Caregivers;
                default: return WorkGroupRole.General;
            }
        }

        public IReadOnlyList<WorkGroup> EnsureRoleGroups()
        {
            foreach (var role in RequiredRoles)
            {
                if (work.Groups.All.Any(x => x.Role == role)) continue;
                work.CreateGroup(DeterministicGroupId(role), DisplayName(role), Array.Empty<StableEntityId>(), role);
            }
            return work.Groups.All.Where(x => x.Role != WorkGroupRole.General).OrderBy(x => x.Role).ToArray();
        }

        public WorkOrder Issue(WorkJobType type, WorkTarget target, WorkPriority priority)
        {
            if (type == WorkJobType.Move || type == WorkJobType.Haul)
                throw new ArgumentException("Direct role orders require an object action.", nameof(type));
            EnsureRoleGroups();
            if (work.TryGetOpenJobForTarget(target, out var existing))
                throw new InvalidOperationException("Этот объект уже зарезервирован другим заданием.");
            var role = RoleFor(type);
            var group = work.Groups.All.Single(x => x.Role == role);
            var order = work.CreateDirectJob(type, null, target, priority, group.Id, true);
            AssignBest(order, priority == WorkPriority.Immediate);
            return order;
        }

        public WorkOrder IssueHaul(InventoryOwner source, InventoryOwner destination, ResourceId resourceId,
            ResourceQuantity quantity, WorldPosition sourcePosition, WorkPriority priority)
        {
            EnsureRoleGroups();
            var target = WorkTarget.ForEntity(WorkTargetKind.WorldPile, source.Id, sourcePosition);
            if (work.TryGetOpenJobForTarget(target, out _))
                throw new InvalidOperationException("Эта куча уже зарезервирована другим заданием.");
            var group = work.Groups.All.Single(x => x.Role == WorkGroupRole.Haulers);
            var order = work.CreateHaulJob(null, source, destination, resourceId, quantity, priority,
                group.Id, sourcePosition, true);
            AssignBest(order, priority == WorkPriority.Immediate);
            return order;
        }

        public void ReevaluateQueued()
        {
            foreach (var job in work.Jobs.Where(x => x.Status == WorkJobStatus.Queued)
                         .OrderByDescending(x => x.Priority).ThenBy(x => x.Id).ToArray())
                AssignBest(job, job.Priority == WorkPriority.Immediate);
        }

        public IReadOnlyList<WorkCandidateDiagnostic> GetCandidates(WorkJobType type, WorldPosition target)
        {
            var role = RoleFor(type);
            var group = work.Groups.All.FirstOrDefault(x => x.Role == role);
            if (group == null) return Array.Empty<WorkCandidateDiagnostic>();
            return group.Members.Select(id => Diagnose(id, role, target))
                .OrderBy(x => x.DistanceSquared).ThenByDescending(x => x.Suitability)
                .ThenBy(x => x.CharacterId).ToArray();
        }

        private bool AssignBest(WorkOrder job, bool allowSafeInterruption)
        {
            if (!job.GroupId.HasValue || !work.Groups.TryGet(job.GroupId.Value, out var group)) return false;
            var candidates = group.Members.Select(id => Diagnose(id, group.Role, job.Target.Position))
                .Where(x => allowSafeInterruption
                    ? scheduler.CanInterruptForDirectWork(x.CharacterId)
                    : x.Available)
                .OrderBy(x => x.DistanceSquared).ThenByDescending(x => x.Suitability)
                .ThenBy(x => x.CharacterId).ToArray();
            if (candidates.Length == 0) return false;
            var selected = candidates[0].CharacterId;
            if (work.HasOpenJob(selected))
            {
                if (!allowSafeInterruption || !scheduler.TryInterruptForDirectWork(selected)) return false;
                if (work.TryGetAssigned(selected, out var interrupted) && interrupted.Id != job.Id)
                    work.Unassign(interrupted.Id);
            }
            return work.Assign(job.Id, selected);
        }

        private WorkCandidateDiagnostic Diagnose(StableEntityId id, WorkGroupRole role, WorldPosition target)
        {
            if (!characters.TryGet(id, out var character) || character.LifeState != CharacterLifeState.Alive)
                return new WorkCandidateDiagnostic(id, 0, double.MaxValue, false, "недоступен");
            var distance = agents.TryGet(id, out var agent) ? agent.Position.DistanceSquared(target) : double.MaxValue;
            var busy = work.HasOpenJob(id);
            var score = Suitability(character, role);
            return new WorkCandidateDiagnostic(id, score, distance, !busy, busy ? "занят" : "доступен");
        }

        public static int Suitability(Character character, WorkGroupRole role)
        {
            if (character == null) return 0;
            var score = role == WorkGroupRole.Scientists || role == WorkGroupRole.Caregivers
                ? character.Attributes.Intelligence.Value : character.Attributes.PhysicalStrength.Value;
            var key = SkillKey(role);
            if (key != null && character.TryGetSkill(new SkillId(key), out var skill)) score += skill.Level.Value * 2;
            if (character.ProfessionId.HasValue && ProfessionMatches(character.ProfessionId.Value.Value, role)) score += 100;
            return score;
        }

        private static bool ProfessionMatches(string profession, WorkGroupRole role)
        {
            var text = profession ?? string.Empty;
            switch (role)
            {
                case WorkGroupRole.Scientists: return text.Contains("research") || text.Contains("scient");
                case WorkGroupRole.FarmersGatherers: return text.Contains("farm");
                case WorkGroupRole.Warriors: return text.Contains("war") || text.Contains("soldier");
                case WorkGroupRole.Builders: return text.Contains("build");
                case WorkGroupRole.Crafters: return text.Contains("craft");
                default: return text.Contains("worker");
            }
        }

        private static string SkillKey(WorkGroupRole role)
        {
            switch (role)
            {
                case WorkGroupRole.Scientists: return "research";
                case WorkGroupRole.FarmersGatherers: return "farming";
                case WorkGroupRole.Warriors: return "combat";
                case WorkGroupRole.Builders: return "construction";
                case WorkGroupRole.Crafters: return "crafting";
                case WorkGroupRole.Caregivers: return "medicine";
                default: return null;
            }
        }

        private static WorkGroupId DeterministicGroupId(WorkGroupRole role) => new WorkGroupId(
            StableEntityId.FromDeterministicHash(0x503833574F524B00UL + (ulong)role,
                0x47524F5550524F4CUL));

        private static string DisplayName(WorkGroupRole role)
        {
            switch (role)
            {
                case WorkGroupRole.Gatherers: return "Сборщики";
                case WorkGroupRole.Builders: return "Строители";
                case WorkGroupRole.Haulers: return "Переносчики";
                case WorkGroupRole.Crafters: return "Ремесленники";
                case WorkGroupRole.FarmersGatherers: return "Фермеры-собиратели";
                case WorkGroupRole.Scientists: return "Учёные";
                case WorkGroupRole.Scouts: return "Разведчики";
                case WorkGroupRole.Warriors: return "Воины";
                case WorkGroupRole.Caregivers: return "Опекуны";
                default: return "Рабочие";
            }
        }
    }
}
