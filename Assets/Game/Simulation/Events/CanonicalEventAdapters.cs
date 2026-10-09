using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Events;
using Game.Domain.Health;
using Game.Simulation.Health;

namespace Game.Simulation.Events
{
    public sealed class CharacterEventTargetSource : IEventTargetSource
    {
        private readonly CharacterRegistry characters;
        private readonly Func<IEnumerable<StableEntityId>> playerCharacters;
        private readonly Func<IEnumerable<StableEntityId>> aiCharacters;
        private readonly Func<long> biologicalTick;

        public CharacterEventTargetSource(CharacterRegistry characterRegistry,
            Func<IEnumerable<StableEntityId>> playerCharacterIds,
            Func<IEnumerable<StableEntityId>> aiCharacterIds, Func<long> currentBiologicalTick = null)
        {
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            playerCharacters = playerCharacterIds ?? throw new ArgumentNullException(nameof(playerCharacterIds));
            aiCharacters = aiCharacterIds ?? throw new ArgumentNullException(nameof(aiCharacterIds));
            biologicalTick = currentBiologicalTick ?? (() => long.MaxValue);
        }

        public IEnumerable<EventTargetSet> GetTargets(EventDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (definition.TargetSelector == EventTargetSelector.PlayerCharacter)
                return Existing(playerCharacters()).Select(id => new EventTargetSet(new[] { EventTargetRef.Character(id) })).ToArray();
            if (definition.TargetSelector == EventTargetSelector.AiCharacter)
                return Existing(aiCharacters()).Select(id => new EventTargetSet(new[] { EventTargetRef.Character(id) })).ToArray();
            if (definition.TargetSelector == EventTargetSelector.PlayerCouple)
            {
                var player = new HashSet<StableEntityId>(Existing(playerCharacters()));
                var result = new List<EventTargetSet>();
                foreach (var id in player.OrderBy(x => x))
                {
                    var character = characters.Get(id);
                    if (!character.SpouseId.HasValue || !player.Contains(character.SpouseId.Value) || id.CompareTo(character.SpouseId.Value) >= 0)
                        continue;
                    result.Add(new EventTargetSet(new[] { EventTargetRef.Character(id), EventTargetRef.Character(character.SpouseId.Value) }));
                }
                return result;
            }
            return Array.Empty<EventTargetSet>();
        }

        public bool Evaluate(EventConditionDefinition condition, IReadOnlyList<EventTargetRef> values)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            if (values == null || values.Count == 0) return false;
            if (condition.Kind == EventConditionKind.Always) return true;
            if (condition.TargetIndex >= values.Count) return false;
            var target = values[condition.TargetIndex];
            if (target.Kind != EventTargetKind.Character || !target.TryGetStableEntityId(out var id) || !characters.TryGet(id, out var character))
                return false;
            if (condition.Kind == EventConditionKind.TargetExists) return true;
            if (condition.Kind == EventConditionKind.TargetAlive) return character.LifeState == CharacterLifeState.Alive;
            if (condition.Kind == EventConditionKind.TargetAdult)
                return character.LifeState == CharacterLifeState.Alive && character.AgeInCompletedYears(biologicalTick()) >= 16;
            if (condition.Kind == EventConditionKind.TargetsAreSpouses)
            {
                if (values.Count < 2 || !values[1].TryGetStableEntityId(out var otherId)) return false;
                return character.SpouseId == otherId && characters.TryGet(otherId, out var other) && other.SpouseId == id &&
                    character.LifeState == CharacterLifeState.Alive && other.LifeState == CharacterLifeState.Alive;
            }
            return false;
        }

        public bool IsValid(EventTargetRef target) => target.Kind == EventTargetKind.Character &&
            target.TryGetStableEntityId(out var id) && characters.TryGet(id, out _);

        public string GetDisplayName(EventTargetRef target)
        {
            if (target.Kind == EventTargetKind.Character && target.TryGetStableEntityId(out var id) &&
                characters.TryGet(id, out var character)) return character.Name.DisplayName;
            return target.Kind.ToString();
        }

        private IEnumerable<StableEntityId> Existing(IEnumerable<StableEntityId> source) =>
            (source ?? Array.Empty<StableEntityId>()).Where(x => x.IsValid && characters.TryGet(x, out _)).Distinct().OrderBy(x => x);
    }

    public sealed class CanonicalEventEffectExecutor : IEventEffectExecutor
    {
        private readonly CharacterRegistry characters;
        private readonly HealthAndFamilyService health;

        public CanonicalEventEffectExecutor(CharacterRegistry characterRegistry, HealthAndFamilyService healthService)
        {
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            health = healthService ?? throw new ArgumentNullException(nameof(healthService));
        }

        public bool Supports(EventEffectKind kind) => kind == EventEffectKind.None ||
            kind == EventEffectKind.RelationshipEvent || kind == EventEffectKind.HealthCondition;

        public bool CanApply(EventEffectDefinition effect, IReadOnlyList<EventTargetRef> targets)
        {
            if (effect == null || targets == null || effect.TargetIndex >= targets.Count) return false;
            if (effect.Kind == EventEffectKind.None) return true;
            if (!TryCharacter(targets[effect.TargetIndex], out var primary) || primary.LifeState != CharacterLifeState.Alive) return false;
            if (effect.Kind == EventEffectKind.HealthCondition)
            {
                var id = new HealthConditionId(effect.Parameter);
                try { health.Catalog.Get(id); }
                catch (KeyNotFoundException) { return false; }
                return health.Conditions.All(x => x.TargetId != primary.Id || !x.DefinitionId.Equals(id) ||
                    x.State == HealthConditionState.Recovered || x.State == HealthConditionState.Fatal);
            }
            if (effect.SecondaryTargetIndex >= targets.Count ||
                !TryCharacter(targets[effect.SecondaryTargetIndex], out var secondary) ||
                secondary.LifeState != CharacterLifeState.Alive || primary.Id == secondary.Id) return false;
            return primary.SpouseId == secondary.Id || secondary.Parents.Any(x => x.ParentId == primary.Id) ||
                   primary.Parents.Any(x => x.ParentId == secondary.Id);
        }

        public bool TryApply(EventEffectDefinition effect, IReadOnlyList<EventTargetRef> targets,
            long calendarTick, out string outcomeCode)
        {
            outcomeCode = string.Empty;
            if (!CanApply(effect, targets)) return false;
            if (effect.Kind == EventEffectKind.None)
            {
                outcomeCode = effect.Parameter == null || effect.Parameter == "none" ? "narrative_resolved" : effect.Parameter;
                return true;
            }
            targets[effect.TargetIndex].TryGetStableEntityId(out var primaryId);
            if (effect.Kind == EventEffectKind.HealthCondition)
            {
                health.ApplyCondition(primaryId, new HealthConditionId(effect.Parameter), calendarTick);
                outcomeCode = "health_" + effect.Parameter;
                return true;
            }
            targets[effect.SecondaryTargetIndex].TryGetStableEntityId(out var secondaryId);
            var applied = health.ApplyRelationshipEvent(new CharacterEventId(effect.Parameter), primaryId, secondaryId, calendarTick);
            outcomeCode = applied ? "relationship_" + effect.Parameter : "relationship_already_applied";
            return true;
        }

        private bool TryCharacter(EventTargetRef target, out Character character)
        {
            character = null;
            return target.Kind == EventTargetKind.Character && target.TryGetStableEntityId(out var id) &&
                   characters.TryGet(id, out character);
        }
    }

    public static class StoneAgeEventCatalog
    {
        public static EventCatalog Create()
        {
            var day = TimeSpan.FromDays(1).Ticks;
            var alive = new[] { new EventConditionDefinition(EventConditionKind.TargetAlive) };
            var spouses = new[] { new EventConditionDefinition(EventConditionKind.TargetAlive),
                new EventConditionDefinition(EventConditionKind.TargetsAreSpouses) };

            return new EventCatalog(new[]
            {
            new EventDefinition(
                new EventDefinitionId("family_dispute_chain"), EventCategory.Family, EventAudience.Player,
                new EventOwnerId("player"), EventTargetSelector.PlayerCouple,
                new EventTriggerDefinition(EventTriggerKind.Periodic, day * 2, day * 10), 60, day * 20,
                new EventNodeId("dispute"), new[]
                {
                    new EventNodeDefinition(new EventNodeId("dispute"), "A family dispute",
                        "A disagreement has divided two members of the ruling household.", new[]
                        {
                            new EventChoiceDefinition(new EventChoiceId("seek_reconciliation"), "Seek reconciliation", 80,
                                new[] { new EventEffectDefinition(EventEffectKind.RelationshipEvent, "family_support", 0, 1) },
                                new[] { new EventTransitionDefinition(new EventNodeId("aftermath"), 3, day),
                                    new EventTransitionDefinition(new EventNodeId("lingering_tension"), 1, day) }),
                            new EventChoiceDefinition(new EventChoiceId("stand_firm"), "Stand firm", 20,
                                new[] { new EventEffectDefinition(EventEffectKind.RelationshipEvent, "family_dispute", 0, 1) },
                                new[] { new EventTransitionDefinition(new EventNodeId("lingering_tension"), 1, day) })
                        }),
                    new EventNodeDefinition(new EventNodeId("aftermath"), "Reconciliation",
                        "The household gathers again after a day of reflection.", new[]
                        {
                            new EventChoiceDefinition(new EventChoiceId("share_meal"), "Share a meal", 70,
                                new[] { new EventEffectDefinition(EventEffectKind.None) }),
                            new EventChoiceDefinition(new EventChoiceId("return_to_work"), "Return to work", 30,
                                new[] { new EventEffectDefinition(EventEffectKind.None) })
                        }),
                    new EventNodeDefinition(new EventNodeId("lingering_tension"), "Lingering tension",
                        "The disagreement remains, but the household must move forward.", new[]
                        {
                            new EventChoiceDefinition(new EventChoiceId("let_time_pass"), "Let time pass", 50,
                                new[] { new EventEffectDefinition(EventEffectKind.None) })
                        })
                }, spouses, InvalidEventTargetPolicy.Expire),

            new EventDefinition(
                new EventDefinitionId("family_support_offer"), EventCategory.Family, EventAudience.Player,
                new EventOwnerId("player"), EventTargetSelector.PlayerCouple,
                new EventTriggerDefinition(EventTriggerKind.Periodic, day * 2, day * 10), 40, day * 12,
                new EventNodeId("offer"), new[]
                {
                    new EventNodeDefinition(new EventNodeId("offer"), "Support within the household",
                        "One relative asks how the household should answer a difficult day.", new[]
                        {
                            new EventChoiceDefinition(new EventChoiceId("offer_support"), "Offer support", 90,
                                new[] { new EventEffectDefinition(EventEffectKind.RelationshipEvent, "family_support", 0, 1) }),
                            new EventChoiceDefinition(new EventChoiceId("keep_distance"), "Keep some distance", 10,
                                new[] { new EventEffectDefinition(EventEffectKind.None) })
                        })
                }, spouses, InvalidEventTargetPolicy.Expire),

            new EventDefinition(
                new EventDefinitionId("foraging_mishap"), EventCategory.Character, EventAudience.Ai,
                new EventOwnerId("enemy"), EventTargetSelector.AiCharacter,
                new EventTriggerDefinition(EventTriggerKind.Periodic, day * 3, day * 7), 30, day * 14,
                new EventNodeId("injury"), new[]
                {
                    new EventNodeDefinition(new EventNodeId("injury"), "A foraging mishap",
                        "A remote forager suffers a minor injury.", new[]
                        {
                            new EventChoiceDefinition(new EventChoiceId("treat_cut"), "Treat the cut", 100,
                                new[] { new EventEffectDefinition(EventEffectKind.HealthCondition, "minor_cut") })
                        })
                }, alive, InvalidEventTargetPolicy.Cancel),

            new EventDefinition(
                new EventDefinitionId("campfire_story"), EventCategory.Settlement, EventAudience.Ai,
                new EventOwnerId("enemy"), EventTargetSelector.AiCharacter,
                new EventTriggerDefinition(EventTriggerKind.Periodic, day * 3, day * 7), 70, day * 7,
                new EventNodeId("story"), new[]
                {
                    new EventNodeDefinition(new EventNodeId("story"), "A story by the fire",
                        "A familiar tale steadies the remote camp after sunset.", new[]
                        {
                            new EventChoiceDefinition(new EventChoiceId("remember_ancestors"), "Remember the ancestors", 60,
                                new[] { new EventEffectDefinition(EventEffectKind.None) }),
                            new EventChoiceDefinition(new EventChoiceId("plan_tomorrow"), "Plan tomorrow", 40,
                                new[] { new EventEffectDefinition(EventEffectKind.None) })
                        })
                }, alive, InvalidEventTargetPolicy.Cancel)
            });
        }
    }
}
