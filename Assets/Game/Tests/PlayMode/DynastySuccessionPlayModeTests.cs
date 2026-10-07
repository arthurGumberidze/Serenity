using System.Collections;
using Game.Domain.Characters;
using Game.Domain.AI;
using Game.Domain.Dynasties;
using Game.Infrastructure;
using Game.Presentation.Dynasties;
using Game.Simulation.Dynasties;
using Game.Simulation.Tiers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("U17")]
    public sealed class DynastySuccessionPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LocalGameplayShowsDynastyAndDebugDeathsReachSuccessionThenExtinction()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var overlay = Object.FindAnyObjectByType<DynastyDebugOverlay>();
            Assert.That(root, Is.Not.Null);
            Assert.That(overlay, Is.SameAs(root.DynastyDebug));
            Assert.That(root.Characters.Count, Is.EqualTo(4));
            Assert.That(root.Dynasty.CurrentMonarchId, Is.EqualTo(root.MaleDemoCharacter.Id));
            Assert.That(root.Succession.Family.GetOrderedSuccessionCandidates(root.Dynasty,
                root.MaleDemoCharacter.Id)[0].Id, Is.EqualTo(root.FirstDemoHeir.Id));
            Assert.That(overlay.TryGetUiBlockingRect(out var rect), Is.True);
            Assert.That(root.PointerRaycaster.IsPointerBlockedByUi(
                Game.Presentation.Interaction.WorldPointerRaycaster.GuiToScreenPoint(rect.center)), Is.True);

            var first = overlay.KillCurrentMonarch();
            Assert.That(first.Outcome, Is.EqualTo(SuccessionOutcome.NewMonarch));
            Assert.That(root.Dynasty.CurrentMonarchId, Is.EqualTo(root.FirstDemoHeir.Id));
            Assert.That(root.PlayerRealm.CurrentRulerId, Is.EqualTo(root.FirstDemoHeir.Id));
            overlay.KillCurrentMonarch();
            Assert.That(root.Dynasty.CurrentMonarchId, Is.EqualTo(root.SecondDemoHeir.Id));
            overlay.KillCurrentMonarch();
            Assert.That(root.Dynasty.Status, Is.EqualTo(DynastyStatus.Extinct));
            Assert.That(root.GameRun.GameOverReason, Is.EqualTo(GameOverReason.DynastyExtinction));
            Assert.That(root.Dynasty.History.Count, Is.EqualTo(3));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TierThreeHeirBecomesRulerWithoutMaterializingGameObject()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var state = new CharacterRuntimeState(root.FirstDemoHeir.Id, new WorldPosition(50, 0, 50),
                default, false, "remote", root.Clock.State.CalendarTicks, root.Clock.State.BiologicalTicks,
                0, 0d, 1d);
            root.Tiers.Materialize(root.FirstDemoHeir, CharacterSimulationTier.Tier3, state);
            var before = root.Succession.Family.GetOrderedSuccessionCandidates(root.Dynasty,
                root.MaleDemoCharacter.Id)[0].Id;
            root.Tiers.Transition(root.MaleDemoCharacter.Id, CharacterSimulationTier.Tier3);
            var result = root.DynastyDebug.KillCurrentMonarch();

            Assert.That(before, Is.EqualTo(root.FirstDemoHeir.Id));
            Assert.That(result.NewMonarchId, Is.EqualTo(root.FirstDemoHeir.Id));
            Assert.That(root.Tiers.GetTier(root.FirstDemoHeir.Id), Is.EqualTo(CharacterSimulationTier.Tier3));
            Assert.That(root.CharacterPresentations.TryGet(root.FirstDemoHeir.Id, out _), Is.False);
            yield return null;
        }
    }
}
