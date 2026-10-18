using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Campaign;
using Game.Domain.Combat;
using Game.Domain.Military;
using Game.Simulation.AI;
using Game.Simulation.Buildings;

namespace Game.Simulation.Military
{
    public readonly struct MilitaryAttackCommandResult
    {
        public MilitaryAttackCommandResult(MilitaryAttackAvailability availability,
            MilitaryAttackOrderState order = null)
        { Availability = availability; Order = order; }
        public MilitaryAttackAvailability Availability { get; }
        public MilitaryAttackOrderState Order { get; }
        public bool Issued => Availability.Enabled && Order != null;
    }

    /// <summary>Military-only direct attack authority. Orders move Tier1 soldiers through the shared world.</summary>
    public sealed class MilitaryAttackCommandService
    {
        private const float EngagementRangeSquared = 9f;
        private readonly CharacterRegistry characters;
        private readonly BuildingRegistry buildings;
        private readonly MilitaryUnitService units;
        private readonly MilitaryTrainingService training;
        private readonly MilitaryUnitCommandService movement;
        private readonly MilitaryUnitCombatService combat;
        private readonly BuildingDamageService buildingDamage;
        private readonly Tier1AiAgentRegistry agents;
        private readonly long worldSeed;
        private SettlementFlagCaptureService settlementCapture;
        private readonly List<MilitaryAttackOrderState> orders = new List<MilitaryAttackOrderState>();
        private ulong sequence;

        public MilitaryAttackCommandService(CharacterRegistry characterRegistry, BuildingRegistry buildingRegistry,
            MilitaryUnitService militaryUnits, MilitaryUnitCommandService movementCommands,
            MilitaryUnitCombatService combatService, BuildingDamageService damageService,
            Tier1AiAgentRegistry agentRegistry, MilitaryTrainingService trainingService, long seed)
        {
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            buildings = buildingRegistry ?? throw new ArgumentNullException(nameof(buildingRegistry));
            units = militaryUnits ?? throw new ArgumentNullException(nameof(militaryUnits));
            training = trainingService ?? throw new ArgumentNullException(nameof(trainingService));
            movement = movementCommands ?? throw new ArgumentNullException(nameof(movementCommands));
            combat = combatService ?? throw new ArgumentNullException(nameof(combatService));
            buildingDamage = damageService ?? throw new ArgumentNullException(nameof(damageService));
            agents = agentRegistry ?? throw new ArgumentNullException(nameof(agentRegistry));
            worldSeed = seed;
        }

        public IReadOnlyList<MilitaryAttackOrderState> Orders => orders.OrderBy(x => x.Sequence).ToArray();
        public ulong Sequence => sequence;

        public void BindSettlementCapture(SettlementFlagCaptureService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (settlementCapture != null && !ReferenceEquals(settlementCapture, service))
                throw new InvalidOperationException("Settlement capture is already bound.");
            settlementCapture = service;
        }

        public MilitaryAttackAvailability Evaluate(IEnumerable<StableEntityId> attackerIds,
            MilitaryDirectTarget target, bool physicalPath = true)
        {
            var ids = (attackerIds ?? Enumerable.Empty<StableEntityId>()).Where(x => x.IsValid).Distinct().ToArray();
            var memberships = ids.Select(id => units.TryGetForMember(id, out var unit) ? unit : null).ToArray();
            var allMilitary = ids.Length > 0 && ids.All(id =>
                training.GetRank(id).HasValue && units.TryGetForMember(id, out var memberUnit) &&
                memberUnit.ActiveMemberIds.Contains(id));
            var attackerUnits = memberships.Where(x => x != null).Select(x => x.Id).Distinct().ToArray();
            var sameSide = attackerUnits.Length == 1 && memberships.Where(x => x != null)
                .Select(x => x.OwnerSideId).Distinct().Count() == 1;
            var side = sameSide ? memberships.First(x => x != null).OwnerSideId : default;
            var exists = target.Kind == MilitaryDirectTargetKind.Area;
            var hostile = target.Kind == MilitaryDirectTargetKind.Area;
            var militaryBuilding = target.Kind != MilitaryDirectTargetKind.EnemyMilitaryBuilding;
            if (target.Kind == MilitaryDirectTargetKind.EnemyMilitary || target.Kind == MilitaryDirectTargetKind.Civilian)
            {
                exists = characters.TryGet(target.EntityId, out var person) && person.LifeState == CharacterLifeState.Alive;
                if (exists && units.TryGetForMember(target.EntityId, out var targetUnit))
                    hostile = sameSide && targetUnit.OwnerSideId != side;
                else hostile = false;
            }
            else if (target.Kind == MilitaryDirectTargetKind.EnemyMilitaryBuilding)
            {
                exists = buildings.TryGet(target.EntityId, out var building) && !building.IsDestroyed;
                hostile = exists && sameSide && building.OwnerSideId.Value != side.Value;
                militaryBuilding = exists && building.DefinitionId == MilitaryTrainingService.BarracksDefinitionId;
            }
            return MilitaryDirectTargetRules.Evaluate(ids.Length > 0, allMilitary, sameSide,
                target.Kind, exists, hostile, militaryBuilding, physicalPath);
        }

        public MilitaryAttackCommandResult Issue(IEnumerable<StableEntityId> attackerIds,
            MilitaryDirectTarget target, long calendarTick, bool physicalPath = true)
        {
            var ids = (attackerIds ?? Enumerable.Empty<StableEntityId>()).Where(x => x.IsValid).Distinct().ToArray();
            var availability = Evaluate(ids, target, physicalPath);
            if (!availability.Enabled) return new MilitaryAttackCommandResult(availability);
            units.TryGetForMember(ids[0], out var attackerUnit);
            if (attackerUnit.Representation != MilitaryUnitRepresentationMode.Detailed || attackerUnit.ActiveEncounterId.HasValue)
                return new MilitaryAttackCommandResult(new MilitaryAttackAvailability(false,
                    MilitaryAttackDisabledReason.UnitUnavailable));
            movement.Move(attackerUnit.Id, target.Position);
            var state = new MilitaryAttackOrderState(++sequence, attackerUnit.Id, target, calendarTick, false);
            orders.Add(state);
            TryResolve(orders.Count - 1, calendarTick);
            return new MilitaryAttackCommandResult(availability, orders[orders.Count - 1]);
        }

        public int AdvanceTo(long calendarTick)
        {
            var resolved = 0;
            for (var i = 0; i < orders.Count; i++)
                if (!orders[i].Completed && TryResolve(i, calendarTick)) resolved++;
            return resolved;
        }

        public void RestoreState(IEnumerable<MilitaryAttackOrderState> saved, ulong savedSequence)
        {
            var values = (saved ?? Enumerable.Empty<MilitaryAttackOrderState>()).ToArray();
            if (values.Any(x => x == null || x.Sequence > savedSequence || !units.TryGet(x.AttackerUnitId, out _)) ||
                values.Select(x => x.Sequence).Distinct().Count() != values.Length)
                throw new ArgumentException("Saved attack orders are invalid.", nameof(saved));
            orders.Clear(); orders.AddRange(values); sequence = savedSequence;
        }

        private bool TryResolve(int index, long calendarTick)
        {
            var order = orders[index];
            if (!units.TryGet(order.AttackerUnitId, out var unit) || unit.ActiveEncounterId.HasValue) return false;
            var member = unit.ActiveMemberIds.FirstOrDefault(id => agents.TryGet(id, out _));
            if (!member.IsValid || !agents.TryGet(member, out var agent) ||
                DistanceSquared(agent.Position, order.Target.Position) > EngagementRangeSquared) return false;
            var claimedFlags = settlementCapture?.TryClaimFlagsNear(unit.Id, agent.Position,
                (float)Math.Sqrt(EngagementRangeSquared), calendarTick) ?? 0;
            if (order.Target.Kind == MilitaryDirectTargetKind.EnemyMilitary &&
                units.TryGetForMember(order.Target.EntityId, out var targetUnit) && !targetUnit.ActiveEncounterId.HasValue)
            {
                combat.Start(unit.Id, targetUnit.Id, DeterministicStableId.Create(worldSeed,
                    "direct_attack:" + order.Sequence), calendarTick);
            }
            else if (order.Target.Kind == MilitaryDirectTargetKind.EnemyMilitaryBuilding)
            {
                if (claimedFlags == 0)
                    buildingDamage.ApplyDamage(order.Target.EntityId, DeterministicStableId.Create(worldSeed,
                        "building_attack:" + order.Sequence), 100, BuildingDamageType.Combat, calendarTick);
            }
            orders[index] = new MilitaryAttackOrderState(order.Sequence, order.AttackerUnitId,
                order.Target, order.IssuedTick, true);
            units.SetOrder(unit.Id, MilitaryUnitOrderKind.Hold);
            return true;
        }

        private static float DistanceSquared(WorldPosition left, WorldPosition right)
        {
            var x = left.X - right.X; var y = left.Y - right.Y; var z = left.Z - right.Z;
            return x * x + y * y + z * z;
        }
    }
}
