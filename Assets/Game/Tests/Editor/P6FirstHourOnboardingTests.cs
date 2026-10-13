using System.Linq;
using Game.Presentation.Localization;
using Game.Presentation.UI;
using NUnit.Framework;

namespace Game.Tests
{
    [Category("P6")]
    public sealed class P6FirstHourOnboardingTests
    {
        [Test]
        public void RouteAccumulatesObservedCanonicalSignalsWithoutSkippingItsReadableOrder()
        {
            var progress = new FirstHourOnboardingProgress();

            progress.Observe(Signals(characterSelected: true, foodAvailable: true));
            Assert.That(progress.CurrentStage, Is.EqualTo(FirstHourStage.AssignWork));
            Assert.That(progress.IsComplete(FirstHourStage.SecureFood), Is.False,
                "Starting berries alone are not proof that the player established food production.");

            progress.Observe(Signals(workAssigned: true, settlementViewed: true, shelter: true, storage: true,
                foodAvailable: true, foodProduced: true, researchStarted: true, dangerHandled: true,
                scoutingStarted: true));

            Assert.That(progress.CurrentStage, Is.EqualTo(FirstHourStage.Complete));
            Assert.That(progress.CompletedCount, Is.EqualTo(progress.StepCount));
            Assert.That(progress.Completed.Distinct().Count(), Is.EqualTo(progress.StepCount));
        }

        [Test]
        public void HintVisibilityIsOptionalAndDoesNotEraseProgress()
        {
            var progress = new FirstHourOnboardingProgress();
            progress.Observe(Signals(characterSelected: true));

            progress.SetVisible(false);
            Assert.That(progress.Visible, Is.False);
            Assert.That(progress.CurrentStage, Is.EqualTo(FirstHourStage.AssignWork));
            progress.SetVisible(true);
            Assert.That(progress.Visible, Is.True);
            Assert.That(progress.CurrentStage, Is.EqualTo(FirstHourStage.AssignWork));
        }

        [Test]
        public void EveryPlayerFacingRouteStageHasRussianCopy()
        {
            foreach (var stage in new[] { "inspect", "work", "shelter", "food", "research", "event", "scout", "complete" })
            {
                Assert.That(RussianTextCatalog.Get("ui.onboarding." + stage + ".title"), Is.Not.EqualTo(RussianTextCatalog.MissingText));
                Assert.That(RussianTextCatalog.Get("ui.onboarding." + stage + ".body"), Is.Not.EqualTo(RussianTextCatalog.MissingText));
            }
        }

        private static FirstHourSignals Signals(bool characterSelected = false, bool workAssigned = false,
            bool settlementViewed = false, bool shelter = false, bool storage = false,
            bool foodAvailable = false, bool foodProduced = false, bool researchStarted = false,
            bool dangerHandled = false, bool scoutingStarted = false) =>
            new FirstHourSignals(characterSelected, workAssigned, settlementViewed, shelter, storage,
                foodAvailable, foodProduced, researchStarted, dangerHandled, scoutingStarted);
    }
}
