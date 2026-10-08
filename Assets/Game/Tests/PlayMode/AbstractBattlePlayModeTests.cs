using System.Collections;
using System.Linq;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Health;
using Game.Domain.Military;
using Game.Infrastructure;
using Game.Simulation.Combat;
using Game.Simulation.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("U23")]
    public sealed class AbstractBattlePlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocalScene()
        {
            Screen.SetResolution(1600, 900, false);
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AbstractDemoAdvancesFromGameClockPauseStopsAndSurvivorsReturnVisibly()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var battle = root.StartDemoUnitBattle(BattleMode.Abstract);
            var ids = battle.Combatants.Select(x => x.CharacterId).ToArray();
            Assert.That(battle.Mode, Is.EqualTo(BattleMode.Abstract));
            Assert.That(ids.All(id => !root.CharacterPresentations.TryGet(id, out _)), Is.True);

            root.AiRuntime.SetSpeed(GameSpeed.Maximum);
            root.AiRuntime.AdvanceForDebug(System.TimeSpan.FromSeconds(0.2));
            yield return null;
            var progressed = root.Combat.Encounters.Single(x => x.BattleId == battle.BattleId);
            Assert.That(progressed.LastProcessedCalendarTick, Is.GreaterThan(battle.LastProcessedCalendarTick));
            Assert.That(progressed.AttackHistory.Count, Is.GreaterThan(0));
            Assert.That(root.HealthFamily.Conditions.Count, Is.GreaterThan(0));

            root.AiRuntime.SetPaused(true);
            var pausedHash = root.Combat.ComputeStateHash();
            root.AiRuntime.AdvanceForDebug(System.TimeSpan.FromSeconds(1));
            yield return null;
            Assert.That(root.Combat.ComputeStateHash(), Is.EqualTo(pausedHash));
            root.AiRuntime.SetPaused(false);

            var transition = root.Battles.Transition(battle.BattleId, BattleMode.Physical,
                root.Clock.State.CalendarTicks);
            Assert.That(transition.Status, Is.EqualTo(BattleTransitionStatus.Changed));
            yield return null;
            var current = root.Combat.Encounters.Single(x => x.BattleId == battle.BattleId);
            foreach (var combatant in current.Combatants)
            {
                var alive = root.Characters.Get(combatant.CharacterId).LifeState == CharacterLifeState.Alive;
                var incapacitated = root.HealthFamily.GetCapabilities(combatant.CharacterId).IsIncapacitated;
                var visible = root.CharacterPresentations.TryGet(combatant.CharacterId, out _);
                if (!alive) Assert.That(visible, Is.False, "dead participants must not rematerialize");
                else if (!incapacitated) Assert.That(visible, Is.True, "combat-capable survivors must rematerialize");
            }
        }

        [UnityTest]
        public IEnumerator RepeatedPhysicalAbstractTransitionsKeepBattleUnitAndCharacterIdentity()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var started = root.StartDemoUnitBattle(BattleMode.Physical);
            var ids = started.Combatants.Select(x => x.CharacterId).ToArray();
            var unitIds = new[] { started.SideAUnitId.Value, started.SideBUnitId.Value };
            var tick = root.Clock.State.CalendarTicks;
            Assert.That(root.Battles.Transition(started.BattleId, BattleMode.Abstract, tick).Changed, Is.True);
            Assert.That(root.Battles.Transition(started.BattleId, BattleMode.Physical, tick).Changed, Is.True);
            Assert.That(root.Battles.Transition(started.BattleId, BattleMode.Abstract, tick).Changed, Is.True);
            Assert.That(root.Battles.Transition(started.BattleId, BattleMode.Physical, tick).Changed, Is.True);
            yield return null;

            var current = root.Combat.Encounters.Single(x => x.BattleId == started.BattleId);
            Assert.That(current.BattleId, Is.EqualTo(started.BattleId));
            Assert.That(current.Combatants.Select(x => x.CharacterId), Is.EqualTo(ids));
            Assert.That(new[] { current.SideAUnitId.Value, current.SideBUnitId.Value }, Is.EqualTo(unitIds));
            Assert.That(current.BattleHistory.Count(x => x.Kind == BattleEventKind.ModeChanged), Is.EqualTo(4));
            Assert.That(unitIds.All(id => root.MilitaryUnits.Get(id).Representation ==
                MilitaryUnitRepresentationMode.Detailed), Is.True);
        }

        [UnityTest]
        public IEnumerator TpsControlledCombatantBlocksPhysicalToAbstractUntilExit()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var battle = root.StartDemoUnitBattle(BattleMode.Physical);
            var captain = root.MilitaryUnits.Get(battle.SideAUnitId.Value).CaptainId.Value;
            Assert.That(root.ThirdPersonControl.TryEnter(captain).IsEligible, Is.True);
            Assert.That(root.Battles.Transition(battle.BattleId, BattleMode.Abstract,
                root.Clock.State.CalendarTicks).Status, Is.EqualTo(BattleTransitionStatus.BlockedByDirectControl));
            root.ThirdPersonControl.Exit();
            Assert.That(root.PlayerControl.State.Mode, Is.EqualTo(Game.Domain.Control.PlayerControlMode.Rts));
            Assert.That(root.Battles.Transition(battle.BattleId, BattleMode.Abstract,
                root.Clock.State.CalendarTicks).Status, Is.EqualTo(BattleTransitionStatus.Changed));
            yield return null;
        }
    }
}
