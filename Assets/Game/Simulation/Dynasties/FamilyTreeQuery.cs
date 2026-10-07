using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Dynasties;

namespace Game.Simulation.Dynasties
{
    public enum SuccessionIneligibilityReason
    {
        None = 0,
        MissingCharacter = 1,
        NotDynastyMember = 2,
        Deceased = 3,
        CurrentMonarch = 4,
        FemaleUnderAgnaticRule = 5
    }

    public readonly struct SuccessionEligibility
    {
        public bool IsEligible => Reason == SuccessionIneligibilityReason.None;
        public SuccessionIneligibilityReason Reason { get; }
        public SuccessionEligibility(SuccessionIneligibilityReason reason) => Reason = reason;
    }

    /// <summary>Read-only family graph built from canonical Character parent and spouse references.</summary>
    public sealed class FamilyTreeQuery
    {
        private readonly CharacterRegistry characters;

        public FamilyTreeQuery(CharacterRegistry characterRegistry) =>
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));

        public IReadOnlyList<Character> GetParents(StableEntityId characterId) => characters.Get(characterId).Parents
            .Select(link => characters.TryGet(link.ParentId, out var parent) ? parent : null)
            .Where(parent => parent != null).Distinct().OrderBy(Birth).ThenBy(IdText, StringComparer.Ordinal).ToArray();

        public Character GetSpouse(StableEntityId characterId)
        {
            var spouse = characters.Get(characterId).SpouseId;
            return spouse.HasValue && characters.TryGet(spouse.Value, out var value) ? value : null;
        }

        public IReadOnlyList<Character> GetChildren(StableEntityId parentId) => characters.GetChildren(parentId)
            .OrderBy(Birth).ThenBy(IdText, StringComparer.Ordinal).ToArray();

        public IReadOnlyList<Character> GetSiblings(StableEntityId characterId)
        {
            var person = characters.Get(characterId);
            var parentIds = new HashSet<StableEntityId>(person.Parents.Select(x => x.ParentId));
            return characters.GetAll().Where(candidate => candidate.Id != characterId &&
                    candidate.Parents.Any(parent => parentIds.Contains(parent.ParentId)))
                .OrderBy(Birth).ThenBy(IdText, StringComparer.Ordinal).ToArray();
        }

        public IReadOnlyList<Character> GetDescendants(StableEntityId characterId)
        {
            characters.Get(characterId);
            var result = new List<Character>();
            var visited = new HashSet<StableEntityId> { characterId };
            AppendDescendants(characterId, result, visited);
            return result;
        }

        public bool IsDescendant(StableEntityId possibleDescendantId, StableEntityId ancestorId) =>
            GetDescendants(ancestorId).Any(x => x.Id == possibleDescendantId);

        public SuccessionEligibility CheckEligibility(DynastyState dynasty, StableEntityId candidateId,
            StableEntityId deceasedMonarchId)
        {
            if (!characters.TryGet(candidateId, out var candidate))
                return new SuccessionEligibility(SuccessionIneligibilityReason.MissingCharacter);
            if (!dynasty.Contains(candidateId) || candidate.DynastyId != dynasty.Id)
                return new SuccessionEligibility(SuccessionIneligibilityReason.NotDynastyMember);
            if (candidate.Id == deceasedMonarchId)
                return new SuccessionEligibility(SuccessionIneligibilityReason.CurrentMonarch);
            if (candidate.LifeState != CharacterLifeState.Alive)
                return new SuccessionEligibility(SuccessionIneligibilityReason.Deceased);
            if (string.Equals(dynasty.SuccessionRuleId, SuccessionRuleIds.AgnaticPrimogenitureV1,
                    StringComparison.Ordinal) && candidate.Sex != CharacterSex.Male)
                return new SuccessionEligibility(SuccessionIneligibilityReason.FemaleUnderAgnaticRule);
            return new SuccessionEligibility(SuccessionIneligibilityReason.None);
        }

        public IReadOnlyList<Character> GetDirectHeirs(DynastyState dynasty, StableEntityId monarchId) =>
            GetChildren(monarchId).Where(x => CheckEligibility(dynasty, x.Id, monarchId).IsEligible).ToArray();

        public IReadOnlyList<Character> GetOrderedSuccessionCandidates(DynastyState dynasty,
            StableEntityId deceasedMonarchId)
        {
            if (dynasty == null) throw new ArgumentNullException(nameof(dynasty));
            ValidateDynasty(dynasty);
            characters.Get(deceasedMonarchId);
            var ordered = new List<Character>();
            var traversed = new HashSet<StableEntityId> { deceasedMonarchId };

            foreach (var child in GetChildren(deceasedMonarchId))
                AppendBranch(child, dynasty, deceasedMonarchId, ordered, traversed);
            foreach (var sibling in GetSiblings(deceasedMonarchId))
                AppendBranch(sibling, dynasty, deceasedMonarchId, ordered, traversed);

            var ancestors = GetParents(deceasedMonarchId).ToArray();
            var seenAncestors = new HashSet<StableEntityId>();
            while (ancestors.Length > 0)
            {
                var next = new List<Character>();
                foreach (var ancestor in ancestors.OrderBy(Birth).ThenBy(IdText, StringComparer.Ordinal))
                {
                    if (!seenAncestors.Add(ancestor.Id)) continue;
                    AddIfEligible(ancestor, dynasty, deceasedMonarchId, ordered, traversed);
                    foreach (var branch in GetSiblings(ancestor.Id))
                        AppendBranch(branch, dynasty, deceasedMonarchId, ordered, traversed);
                    next.AddRange(GetParents(ancestor.Id));
                }
                ancestors = next.GroupBy(x => x.Id).Select(x => x.First()).ToArray();
            }

            foreach (var member in dynasty.MemberIds.Select(id => characters.Get(id))
                         .OrderBy(x => KinshipDistance(deceasedMonarchId, x.Id))
                         .ThenBy(Birth).ThenBy(IdText, StringComparer.Ordinal))
                AddIfEligible(member, dynasty, deceasedMonarchId, ordered, traversed);
            return ordered;
        }

        public void ValidateDynasty(DynastyState dynasty)
        {
            if (dynasty == null) throw new ArgumentNullException(nameof(dynasty));
            foreach (var memberId in dynasty.MemberIds)
            {
                if (!characters.TryGet(memberId, out var member))
                    throw new InvalidOperationException("Dynasty references a missing character: " + memberId);
                if (member.DynastyId != dynasty.Id)
                    throw new InvalidOperationException("Dynasty member has a different canonical dynasty reference: " + memberId);
            }
            if (!characters.TryGet(dynasty.FounderId, out _))
                throw new InvalidOperationException("Dynasty founder is missing.");
            if (dynasty.CurrentMonarchId.HasValue && !characters.TryGet(dynasty.CurrentMonarchId.Value, out _))
                throw new InvalidOperationException("Current monarch is missing.");
            ValidateFamilyReferences();
        }

        public void ValidateFamilyReferences()
        {
            var all = characters.GetAll();
            foreach (var person in all)
            {
                foreach (var parent in person.Parents)
                    if (!characters.TryGet(parent.ParentId, out _))
                        throw new InvalidOperationException("Character references a missing parent: " + parent.ParentId);
                if (person.SpouseId.HasValue)
                {
                    if (!characters.TryGet(person.SpouseId.Value, out var spouse))
                        throw new InvalidOperationException("Character references a missing spouse: " + person.SpouseId.Value);
                    if (spouse.SpouseId != person.Id)
                        throw new InvalidOperationException("Spouse references must be symmetric.");
                }
            }

            var colors = new Dictionary<StableEntityId, int>();
            foreach (var person in all)
                VisitParents(person, colors);
        }

        private void VisitParents(Character person, IDictionary<StableEntityId, int> colors)
        {
            if (colors.TryGetValue(person.Id, out var color))
            {
                if (color == 1) throw new InvalidOperationException("Cyclic family parent references are not allowed.");
                if (color == 2) return;
            }
            colors[person.Id] = 1;
            foreach (var parent in person.Parents)
                VisitParents(characters.Get(parent.ParentId), colors);
            colors[person.Id] = 2;
        }

        private void AppendDescendants(StableEntityId parentId, ICollection<Character> result,
            ISet<StableEntityId> visited)
        {
            foreach (var child in GetChildren(parentId))
            {
                if (!visited.Add(child.Id)) throw new InvalidOperationException("Cyclic family graph detected.");
                result.Add(child);
                AppendDescendants(child.Id, result, visited);
            }
        }

        private void AppendBranch(Character member, DynastyState dynasty, StableEntityId deceasedMonarchId,
            ICollection<Character> ordered, ISet<StableEntityId> traversed)
        {
            if (!traversed.Add(member.Id)) return;
            if (CheckEligibility(dynasty, member.Id, deceasedMonarchId).IsEligible) ordered.Add(member);
            foreach (var child in GetChildren(member.Id))
                AppendBranch(child, dynasty, deceasedMonarchId, ordered, traversed);
        }

        private void AddIfEligible(Character member, DynastyState dynasty, StableEntityId deceasedMonarchId,
            ICollection<Character> ordered, ISet<StableEntityId> traversed)
        {
            if (!traversed.Add(member.Id)) return;
            if (CheckEligibility(dynasty, member.Id, deceasedMonarchId).IsEligible) ordered.Add(member);
        }

        private int KinshipDistance(StableEntityId source, StableEntityId target)
        {
            if (source == target) return 0;
            var queue = new Queue<KeyValuePair<StableEntityId, int>>();
            var visited = new HashSet<StableEntityId> { source };
            queue.Enqueue(new KeyValuePair<StableEntityId, int>(source, 0));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var neighbors = GetParents(current.Key).Select(x => x.Id)
                    .Concat(GetChildren(current.Key).Select(x => x.Id))
                    .Distinct().OrderBy(x => x.ToString(), StringComparer.Ordinal);
                foreach (var neighbor in neighbors)
                {
                    if (!visited.Add(neighbor)) continue;
                    if (neighbor == target) return current.Value + 1;
                    queue.Enqueue(new KeyValuePair<StableEntityId, int>(neighbor, current.Value + 1));
                }
            }
            return int.MaxValue;
        }

        private static long Birth(Character value) => value.BiologicalBirthTick;
        private static string IdText(Character value) => value.Id.ToString();
    }
}
