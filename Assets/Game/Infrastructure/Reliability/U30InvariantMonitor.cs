using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Combat;
using Game.Domain.Military;

namespace Game.Infrastructure.Reliability
{
    /// <summary>Read-only cross-system assertions evaluated at a committed simulation barrier.</summary>
    public static class U30InvariantMonitor
    {
        public static string[] Validate(LocalSceneCompositionRoot root)
        {
            if (root == null) return new[] { "missing_composition_root" };
            var failures = new List<string>();
            var characters = root.Characters.GetAll();
            if (characters.Select(x => x.Id).Distinct().Count() != characters.Count)
                failures.Add("duplicate_character_stable_id");

            foreach (var character in characters)
            {
                if (!root.Tiers.TryGetTier(character.Id, out var declared) ||
                    root.Tiers.CountActiveRepresentations(character.Id) != 1)
                    failures.Add("invalid_tier_representation:" + character.Id);
                else if (!Enum.IsDefined(typeof(Game.Domain.Characters.CharacterSimulationTier), declared))
                    failures.Add("invalid_tier_value:" + character.Id);
                var parents = character.Parents;
                if (parents.Select(x => new { x.ParentId, x.Role, x.Kind }).Distinct().Count() != parents.Count)
                    failures.Add("duplicate_parent_link:" + character.Id);
            }

            ValidateInventoryScope("player", root.Inventories, failures);
            foreach (var faction in root.StrategicAi.Factions)
                ValidateInventoryScope("ai:" + faction.Strategic.Id, faction.Inventories, failures);

            var health = root.HealthFamily.CaptureState();
            var childIds = health.Pregnancies.Where(x => x.ChildId.HasValue).Select(x => x.ChildId.Value).ToArray();
            if (childIds.Distinct().Count() != childIds.Length) failures.Add("duplicate_birth_child_id");
            var activeMothers = health.Pregnancies.Where(x => x.Status == Game.Domain.Health.PregnancyStatus.Active)
                .Select(x => x.MotherId).ToArray();
            if (activeMothers.Distinct().Count() != activeMothers.Length) failures.Add("duplicate_active_pregnancy");

            var dynasty = root.Succession.CaptureState();
            var known = new HashSet<StableEntityId>(characters.Select(x => x.Id));
            if (dynasty.Dynasty.MemberIds.Any(id => !known.Contains(id))) failures.Add("missing_dynasty_member");
            if (dynasty.Dynasty.CurrentMonarchId.HasValue &&
                !known.Contains(dynasty.Dynasty.CurrentMonarchId.Value)) failures.Add("missing_dynasty_monarch");
            if (dynasty.Dynasty.ProcessedMonarchDeathIds.Distinct().Count() !=
                dynasty.Dynasty.ProcessedMonarchDeathIds.Count) failures.Add("duplicate_processed_succession");
            var successionDeaths = dynasty.Dynasty.History.Select(x => x.PreviousMonarchId).ToArray();
            if (successionDeaths.Distinct().Count() != successionDeaths.Length)
                failures.Add("repeated_death_or_succession");

            var activeMembership = root.MilitaryUnits.Units.SelectMany(unit => unit.Roster
                .Where(member => member.IsActive).Select(member => member.CharacterId)).ToArray();
            if (activeMembership.Distinct().Count() != activeMembership.Length)
                failures.Add("duplicate_active_military_membership");

            var activeParticipants = new HashSet<StableEntityId>();
            foreach (var battle in root.Combat.Encounters)
            {
                if (battle.Combatants.Select(x => x.CharacterId).Distinct().Count() != battle.Combatants.Count)
                    failures.Add("duplicate_battle_participant:" + battle.EncounterId);
                if (battle.Status == CombatEncounterStatus.Active)
                    foreach (var combatant in battle.Combatants)
                        if (!activeParticipants.Add(combatant.CharacterId))
                            failures.Add("participant_in_multiple_active_battles:" + combatant.CharacterId);
                ValidateBattleRepresentation(root, battle, failures);
            }

            foreach (var building in root.Buildings.All)
            {
                if (!building.OwnerSideId.IsValid) failures.Add("invalid_building_owner:" + building.Id);
                var buildingState = building.CaptureState();
                if (buildingState.ProcessedDamageEvents.Distinct().Count() != buildingState.ProcessedDamageEvents.Count)
                    failures.Add("duplicate_building_damage_event:" + building.Id);
                if (building.LastFireProcessedTick > root.Clock.State.CalendarTicks)
                    failures.Add("fire_checkpoint_in_future:" + building.Id);
            }

            var eventState = root.Events.CaptureState();
            if (eventState.Instances.Select(x => x.InstanceId).Distinct().Count() != eventState.Instances.Count)
                failures.Add("duplicate_event_instance");
            foreach (var instance in eventState.Instances)
                if (instance.AppliedEffectMarkers.Distinct(StringComparer.Ordinal).Count() !=
                    instance.AppliedEffectMarkers.Count)
                    failures.Add("duplicate_event_effect_marker:" + instance.InstanceId);

            var locations = new HashSet<Game.Domain.World.WorldLocationId>(root.GlobalMap.Locations.Select(x => x.Id));
            var settlements = new HashSet<Game.Domain.World.SettlementId>(root.GlobalMap.Settlements.Select(x => x.Id));
            foreach (var faction in root.StrategicAi.Factions)
            {
                var state = faction.Strategic;
                if (state.TargetSettlementId.HasValue && !settlements.Contains(state.TargetSettlementId.Value))
                    failures.Add("ai_invalid_target_settlement:" + state.Id);
                if (state.TargetLocationId.HasValue && !locations.Contains(state.TargetLocationId.Value))
                    failures.Add("ai_invalid_target_location:" + state.Id);
                var sequences = state.PendingOrders.Select(x => x.Sequence).ToArray();
                if (sequences.Distinct().Count() != sequences.Length)
                    failures.Add("ai_duplicate_pending_order:" + state.Id);
            }

            return failures.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }

        private static void ValidateInventoryScope(string scope, Game.Domain.Resources.ResourceInventoryRegistry registry,
            ICollection<string> failures)
        {
            var owners = new HashSet<Game.Domain.Resources.InventoryOwner>();
            foreach (var inventory in registry.All)
            {
                if (!owners.Add(inventory.Owner)) failures.Add("duplicate_inventory_owner:" + scope + ":" + inventory.Owner);
                var state = inventory.CaptureState();
                if (state.Amounts.Any(x => x.Quantity.Units < 0))
                    failures.Add("negative_resource_quantity:" + scope + ":" + inventory.Owner);
                if (state.Amounts.Select(x => x.ResourceId).Distinct().Count() != state.Amounts.Count)
                    failures.Add("duplicate_resource_quantity:" + scope + ":" + inventory.Owner);
            }
        }

        private static void ValidateBattleRepresentation(LocalSceneCompositionRoot root, CombatEncounterState battle,
            ICollection<string> failures)
        {
            if (battle.Status != CombatEncounterStatus.Active ||
                !battle.SideAUnitId.HasValue || !battle.SideBUnitId.HasValue) return;
            var expected = battle.Mode == BattleMode.Physical
                ? MilitaryUnitRepresentationMode.Detailed
                : MilitaryUnitRepresentationMode.Aggregate;
            foreach (var unitId in new[] { battle.SideAUnitId.Value, battle.SideBUnitId.Value })
            {
                if (!root.MilitaryUnits.TryGet(unitId, out var unit) || unit.Representation != expected)
                    failures.Add("battle_representation_half_state:" + battle.EncounterId + ":" + unitId);
            }
        }
    }
}
