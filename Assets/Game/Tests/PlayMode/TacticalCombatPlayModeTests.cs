using System;
using System.Collections;
using System.Linq;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Infrastructure;
using Game.Presentation.Interaction;
using Game.Simulation.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("U20")]
    public sealed class TacticalCombatPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocalScene()
        {
            Screen.SetResolution(1600, 900, false);
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FourVersusFourPhysicalBattleApproachesAttacksInjuresAndFinishes()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var started = root.StartDemoCombat();
            Assert.That(started.Combatants.Count, Is.EqualTo(8));
            var deadline = Time.realtimeSinceStartup + 35f;
            while (root.Combat.Encounters.Single().Status == CombatEncounterStatus.Active &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;

            var finished = root.Combat.Encounters.Single();
            Assert.That(finished.AttackHistory.Count, Is.GreaterThan(0));
            Assert.That(root.HealthFamily.Conditions.Count, Is.GreaterThan(0));
            Assert.That(finished.Status, Is.EqualTo(CombatEncounterStatus.Finished),
                string.Join("; ", finished.Combatants.Select(x => x.CharacterId.ToString().Substring(28) + ":" +
                    x.State + "/m" + x.Morale + "/a" + x.AttackSequence + "/t" +
                    (x.TargetId.HasValue ? x.TargetId.Value.ToString().Substring(28) : "none"))));
            Assert.That(finished.Outcome, Is.Not.EqualTo(CombatOutcome.None));
            Assert.That(finished.Combatants.Any(x => x.State == CombatantState.Incapacitated ||
                x.State == CombatantState.Dead || x.State == CombatantState.Disengaged), Is.True);
        }

        [UnityTest]
        public IEnumerator SelectedGroupAttackCommandTargetsHostileAndCombatPanelBlocksWorldInput()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var encounter = root.StartDemoCombat();
            var player = encounter.Combatants.Where(x => x.SideId == new CombatSideId("player")).Take(2)
                .Select(x =>
                {
                    root.CharacterPresentations.TryGet(x.CharacterId, out var presenter);
                    return presenter;
                }).ToArray();
            var enemy = encounter.Combatants.First(x => x.SideId == new CombatSideId("enemy"));
            root.SelectionProbe.SelectCharacters(player);
            var result = root.CombatCommands.IssueAttack(root.SelectionProbe.SelectedCharacterIds,
                enemy.CharacterId, root.Clock.State.CalendarTicks);
            Assert.That(result.Status, Is.EqualTo(CombatCommandStatus.Assigned));
            Assert.That(result.ParticipantCount, Is.EqualTo(2));
            root.GameplayUi.SetDeveloperUiVisible(true);
            Assert.That(root.CombatDebug.TryGetUiBlockingRect(out var rect), Is.True);
            Assert.That(root.PointerRaycaster.IsPointerBlockedByUi(WorldPointerRaycaster.GuiToScreenPoint(rect.center)), Is.True);
            yield return null;
            Assert.That(root.SelectionProbe.SelectedCharacterIds.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ActiveEncounterPreventsTierDemotionAndFinishedEncounterReleasesPin()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var encounter = root.StartDemoCombat();
            var participant = encounter.Combatants[0].CharacterId;
            Assert.Throws<InvalidOperationException>(() =>
                root.Tiers.Transition(participant, CharacterSimulationTier.Tier2));
            Assert.That(root.Combat.CancelEncounter(encounter.EncounterId), Is.True);
            Assert.DoesNotThrow(() => root.Tiers.Transition(participant, CharacterSimulationTier.Tier2));
            Assert.That(root.Tiers.GetTier(participant), Is.EqualTo(CharacterSimulationTier.Tier2));
            yield return null;
        }
    }
}
