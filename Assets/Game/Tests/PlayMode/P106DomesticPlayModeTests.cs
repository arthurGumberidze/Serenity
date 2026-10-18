using System.Collections;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Domestic;
using Game.Presentation.UI;
using Game.Infrastructure;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode
{
    [TestFixture, Category("P106")]
    public sealed class P106DomesticPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocal()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Assigned_bed_drives_sleep_and_ui_exposes_player_facing_domestic_route()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var ruler = root.MaleDemoCharacter.Id;
            Assert.That(root.Domestic.EvaluateSleep(ruler).Allowed, Is.True);
            Assert.That(root.GameplayUi.TrySleep(ruler), Is.True);
            Assert.That(root.AiAgents.TryGet(ruler, out var agent), Is.True);
            Assert.That(agent.CurrentAction, Is.EqualTo(UtilityActionKind.Rest));
            Assert.That(agent.Phase, Is.EqualTo(AiActionPhase.MoveToTarget).Or.EqualTo(AiActionPhase.Resting));

            root.GameplayUi.SetPanel(GameplayPanel.Domestic);
            yield return null;
            Assert.That(root.GameplayUi.ActivePanel, Is.EqualTo(GameplayPanel.Domestic));
            Assert.That(root.GameplayUi.RootVisual.Q<Button>("domestic-sleep"), Is.Not.Null);
            Assert.That(root.GameplayUi.RootVisual.Q<Button>("domestic-consent-first"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Mutual_consent_fade_pregnancy_and_v23_save_round_trip_are_connected()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var first = root.MaleDemoCharacter.Id;
            var second = root.FemaleDemoCharacter.Id;
            root.Domestic.SetConsent(first, second, true);
            Assert.That(root.Domestic.EvaluateFamilyInteraction(first, second,
                root.Clock.State.BiologicalTicks).Reason, Is.EqualTo(FamilyInteractionDenialReason.ConsentMissing));
            root.Domestic.SetConsent(second, first, true);
            Assert.That(root.GameplayUi.RequestFamilyInteraction(first, second), Is.True);
            yield return null;
            Assert.That(root.GameplayUi.RootVisual.Q<Button>("domestic-fade-skip"), Is.Not.Null);
            Assert.That(root.HealthFamily.Pregnancies.Count(x => x.Status == Game.Domain.Health.PregnancyStatus.Active),
                Is.EqualTo(1));

            var snapshot = root.Saves.Capture();
            Assert.That(snapshot.FormatVersion, Is.EqualTo(23));
            Assert.That(snapshot.Domestic.Consents.Single().IsMutual, Is.True);
            Assert.That(snapshot.CharacterRuntime.Entries.Any(x => x.State.CharacterId == first), Is.True);
            Assert.That(snapshot.Appearance, Is.Not.Null);
            Assert.That(snapshot.Work, Is.Not.Null);
            Assert.That(snapshot.ConnectedWorld.GenerationId, Is.EqualTo("p10-connected-world-v1"));
            root.GameplayUi.CancelModal();
        }
    }
}
