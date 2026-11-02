using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Work;

namespace Game.Simulation.Work
{
    /// <summary>
    /// Canonical profession/group transaction boundary. It has no frame loop; the session scheduler invokes
    /// reconciliation after simulation advances and after restore.
    /// </summary>
    public sealed class ProfessionGroupService
    {
        private static readonly WorkGroupRole[] Roles = Enum.GetValues(typeof(WorkGroupRole))
            .Cast<WorkGroupRole>().OrderBy(value => value).ToArray();

        private readonly CharacterRegistry characters;
        private readonly WorkManager work;
        private readonly Func<StableEntityId, bool> belongsToSettlement;
        private int knownCharacterCount = -1;
        private long nextAdultBiologicalTick = long.MinValue;
        public Func<WorkGroupRole, bool> CanAssignRole { get; set; }

        public ProfessionGroupService(CharacterRegistry characters, WorkManager work,
            Func<StableEntityId, bool> belongsToSettlement = null)
        {
            this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
            this.work = work ?? throw new ArgumentNullException(nameof(work));
            this.belongsToSettlement = belongsToSettlement ?? (_ => true);
        }

        public IReadOnlyList<WorkGroup> EnsureGroups()
        {
            foreach (var role in Roles)
                if (!work.Groups.All.Any(group => group.Role == role))
                    work.CreateGroup(DeterministicGroupId(role), DisplayName(role), Array.Empty<StableEntityId>(), role);
            return work.Groups.All.OrderBy(group => group.Role).ThenBy(group => group.Id).ToArray();
        }

        public int ReconcileAdults(long biologicalTick, bool force = false)
        {
            if (biologicalTick < 0) throw new ArgumentOutOfRangeException(nameof(biologicalTick));
            if (!force && characters.Count == knownCharacterCount && biologicalTick < nextAdultBiologicalTick)
                return 0;
            EnsureGroups();
            var changed = 0;
            var nextAdult = long.MaxValue;
            foreach (var character in characters.GetAll().Where(value =>
                         value.LifeState == CharacterLifeState.Alive && belongsToSettlement(value.Id))
                         .OrderBy(value => value.Id))
            {
                if (character.AgeInCompletedYears(biologicalTick) < 16)
                {
                    nextAdult = Math.Min(nextAdult, checked(character.BiologicalBirthTick +
                        Character.BiologicalTicksPerYear * 16L));
                    continue;
                }
                if (work.Groups.TryGetForMember(character.Id, out _)) continue;
                var role = RoleForProfession(character.ProfessionId);
                var group = Primary(role);
                work.MoveMember(group.Id, character.Id);
                character.AssignProfession(ProfessionForRole(role));
                changed++;
            }
            knownCharacterCount = characters.Count;
            nextAdultBiologicalTick = nextAdult;
            return changed;
        }

        public WorkGroup CreateGroup(WorkGroupRole role, string displayName)
        {
            if (!Enum.IsDefined(typeof(WorkGroupRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
            if (work.Groups.All.Any(group => group.Role == role))
                throw new InvalidOperationException("A professional group for this profession already exists.");
            return work.CreateGroup(displayName, Array.Empty<StableEntityId>(), role);
        }

        public void Reassign(StableEntityId characterId, WorkGroupId destinationGroupId)
        {
            if (!characters.TryGet(characterId, out var character))
                throw new InvalidOperationException("Profession reassignment requires a registered resident.");
            var destination = work.Groups.Get(destinationGroupId);
            if (CanAssignRole != null && !CanAssignRole(destination.Role))
                throw new InvalidOperationException("Сначала постройте Дом знаний, затем назначьте исследователей.");
            work.MoveMember(destinationGroupId, characterId);
            character.AssignProfession(ProfessionForRole(destination.Role));
        }

        public void DeleteGroup(WorkGroupId groupId, WorkGroupId replacementGroupId)
        {
            if (groupId == replacementGroupId) throw new ArgumentException("A replacement group must be different.");
            var group = work.Groups.Get(groupId);
            var replacement = work.Groups.Get(replacementGroupId);
            foreach (var memberId in group.Members.ToArray()) Reassign(memberId, replacement.Id);
            work.DeleteEmptyGroup(groupId);
        }

        public WorkGroup Primary(WorkGroupRole role) => work.Groups.All
            .Where(group => group.Role == role).OrderBy(group => group.Id).FirstOrDefault()
            ?? throw new InvalidOperationException("Required professional group is missing.");

        public static WorkGroupRole RoleForProfession(ProfessionId? profession)
        {
            var value = profession?.Value?.Trim().ToLowerInvariant() ?? string.Empty;
            if (value.Contains("build")) return WorkGroupRole.Builders;
            if (value.Contains("research") || value.Contains("scient")) return WorkGroupRole.Scientists;
            if (value.Contains("haul") || value.Contains("carrier")) return WorkGroupRole.Haulers;
            if (value.Contains("hunt")) return WorkGroupRole.Hunters;
            if (value.Contains("farm")) return WorkGroupRole.FarmersGatherers;
            if (value.Contains("craft") || value.Contains("cook")) return WorkGroupRole.Crafters;
            if (value.Contains("war") || value.Contains("soldier") || value.Contains("military")) return WorkGroupRole.Warriors;
            if (value.Contains("heal") || value.Contains("medic") || value.Contains("care")) return WorkGroupRole.Caregivers;
            if (value.Contains("scout")) return WorkGroupRole.Scouts;
            if (value.Contains("gather") || value.Contains("miner") || value.Contains("fisher")) return WorkGroupRole.Gatherers;
            return WorkGroupRole.General;
        }

        public static ProfessionId ProfessionForRole(WorkGroupRole role) => new ProfessionId(role switch
        {
            WorkGroupRole.Gatherers => "gatherer",
            WorkGroupRole.Builders => "builder",
            WorkGroupRole.Haulers => "hauler",
            WorkGroupRole.Crafters => "crafter",
            WorkGroupRole.FarmersGatherers => "farmer",
            WorkGroupRole.Scientists => "researcher",
            WorkGroupRole.Scouts => "scout",
            WorkGroupRole.Warriors => "military",
            WorkGroupRole.Caregivers => "healer",
            WorkGroupRole.Hunters => "hunter",
            _ => "worker"
        });

        public static WorkGroupId DeterministicGroupId(WorkGroupRole role) => new WorkGroupId(
            StableEntityId.FromDeterministicHash(0x503833574F524B00UL + (ulong)role, 0x47524F5550524F4CUL));

        public static string DisplayName(WorkGroupRole role) => role switch
        {
            WorkGroupRole.Gatherers => "Сборщики",
            WorkGroupRole.Builders => "Строители",
            WorkGroupRole.Haulers => "Переносчики",
            WorkGroupRole.Crafters => "Ремесленники",
            WorkGroupRole.FarmersGatherers => "Фермеры-собиратели",
            WorkGroupRole.Scientists => "Исследователи",
            WorkGroupRole.Scouts => "Разведчики",
            WorkGroupRole.Warriors => "Военные",
            WorkGroupRole.Caregivers => "Лекари",
            WorkGroupRole.Hunters => "Охотники",
            _ => "Разнорабочие"
        };
    }
}
