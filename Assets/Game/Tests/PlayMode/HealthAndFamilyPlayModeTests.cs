using System.Collections;
using System.Linq;
using Game.Domain.Characters;
using Game.Domain.Health;
using Game.Infrastructure;
using Game.Presentation.Health;
using Game.Presentation.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("U18")]
    public sealed class HealthAndFamilyPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DebugOverlayBlocksWorldInputAndConditionChangesCapabilities()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var overlay = Object.FindAnyObjectByType<HealthFamilyDebugOverlay>();
            Assert.That(root, Is.Not.Null);
            Assert.That(overlay, Is.SameAs(root.HealthFamilyDebug));
            root.GameplayUi.SetDeveloperUiVisible(true);
            Assert.That(overlay.TryGetUiBlockingRect(out var rect), Is.True);
            Assert.That(root.PointerRaycaster.IsPointerBlockedByUi(
                WorldPointerRaycaster.GuiToScreenPoint(rect.center)), Is.True);

            root.HealthFamily.ApplyCondition(root.MaleDemoCharacter.Id,
                new HealthConditionId("serious_wound"), root.Clock.State.CalendarTicks);
            var capability = root.HealthFamily.GetCapabilities(root.MaleDemoCharacter.Id);
            Assert.That(capability.IsIncapacitated, Is.True);
            Assert.That(capability.CanPerformOrdinaryWork, Is.False);
            Assert.That(root.HealthFamily.RecoverAll(root.MaleDemoCharacter.Id), Is.EqualTo(1));
            Assert.That(root.HealthFamily.GetCapabilities(root.MaleDemoCharacter.Id).CanPerformOrdinaryWork, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OffCameraBirthCreatesTierThreeCanonicalCharacter()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var pregnancy = root.HealthFamily.StartPregnancy(root.FemaleDemoCharacter.Id,
                root.MaleDemoCharacter.Id, root.Clock.State.CalendarTicks, root.Clock.State.BiologicalTicks);
            root.Clock.State.CalendarTicks = pregnancy.DueCalendarTick;
            root.Clock.State.BiologicalTicks = pregnancy.DueBiologicalTick;
            var result = root.HealthFamily.AdvanceTo(pregnancy.DueCalendarTick, pregnancy.DueBiologicalTick);
            var childId = root.HealthFamily.Pregnancies.Single().ChildId.Value;
            Assert.That(result.Births, Is.EqualTo(1));
            Assert.That(root.Characters.TryGet(childId, out var child), Is.True);
            Assert.That(child.Parents.Count, Is.EqualTo(2));
            Assert.That(root.Tiers.GetTier(childId), Is.EqualTo(CharacterSimulationTier.Tier3));
            Assert.That(root.CharacterPresentations.TryGet(childId, out _), Is.False);
            Assert.That(root.Dynasty.Contains(childId), Is.True);
            yield return null;
        }
    }
}
