using System;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Health;

namespace Game.Simulation.Health
{
    public sealed partial class HealthAndFamilyService
    {
        public static readonly HealthConditionId StarvationId = new HealthConditionId("starvation");
        // Reads canonical needs, regardless of the character's current presentation tier.
        public Func<StableEntityId, double?> HungerForCharacter { get; set; }
        private long nextNutritionTick;
        public NutritionPenalty NutritionFor(StableEntityId id) =>
            NutritionPenalty.ForHunger(HungerForCharacter?.Invoke(id) ?? 0d);

        public double? StarvationHoursRemaining(StableEntityId id, long now)
        {
            var condition = activeConditions.Values.FirstOrDefault(x => x.TargetId == id && x.DefinitionId.Equals(StarvationId));
            return condition == null ? (double?)null : Math.Max(0d, TimeSpan.FromTicks(condition.ResolveCalendarTick - now).TotalHours);
        }

        private void SynchronizeNutrition(long now)
        {
            if (HungerForCharacter == null) return;
            // Eating must cancel a pending death before the next minute scan, including at its deadline.
            foreach (var condition in activeConditions.Values.Where(x => x.DefinitionId.Equals(StarvationId)).ToArray())
            {
                var hunger = HungerForCharacter(condition.TargetId);
                if (!hunger.HasValue || hunger.Value > .90d + 1e-9) continue;
                conditions[condition.InstanceId] = CopyCondition(condition, state: HealthConditionState.Recovered);
                activeConditions.Remove(condition.InstanceId);
            }
            // Reset on save/load clock rewind; no catch-up may add a second starvation timer.
            if (now < nextNutritionTick && now >= LastCalendarTick && nextNutritionTick <= LastCalendarTick + TimeSpan.TicksPerMinute)
                return;
            nextNutritionTick = checked(now + TimeSpan.TicksPerMinute);
            var starving = activeConditions.Values.Where(x => x.DefinitionId.Equals(StarvationId))
                .ToDictionary(x => x.TargetId);
            foreach (var character in characters.GetAll())
            {
                if (character.LifeState != CharacterLifeState.Alive) continue;
                var hunger = HungerForCharacter(character.Id);
                if (!hunger.HasValue) continue;
                starving.TryGetValue(character.Id, out var current);
                if (hunger.Value > .90d + 1e-9)
                {
                    if (current == null) ApplyCondition(character.Id, StarvationId, now);
                }
                else if (current != null)
                {
                    conditions[current.InstanceId] = CopyCondition(current, state: HealthConditionState.Recovered);
                    activeConditions.Remove(current.InstanceId);
                }
            }
        }
    }
}
