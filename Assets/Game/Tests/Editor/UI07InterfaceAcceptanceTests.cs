using System.IO;
using Game.Presentation.UI;
using Game.Presentation.World;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Editor
{
    [TestFixture, Category("UI07")]
    public sealed class UI07InterfaceAcceptanceTests
    {
        private static string Read(string relative) => File.ReadAllText(Path.Combine(Application.dataPath, relative));

        [Test]
        public void ThemeKeepsMinimumTargetsFocusAndVisibleScrollAffordances()
        {
            var theme = Read("Game/Presentation/UI/Resources/SerenityRuntimeTheme.tss");
            var strategicView = Read("Game/Presentation/World/P102StrategicWorldView.cs");
            StringAssert.Contains("UI07 acceptance and accessibility polish", theme);
            StringAssert.Contains("min-height: 40px", theme);
            StringAssert.Contains(".serenity-root .serenity-button:focus", theme);
            StringAssert.Contains(".unity-scroller--vertical", theme);
            StringAssert.Contains("if (!applicationQuitting) Exit();", strategicView);
            Assert.That(SerenityUiTokens.MinimumTarget, Is.EqualTo(40f));
        }

        [Test]
        public void StableRoutesAndOutcomeActionsRemainExplicit()
        {
            var source = Read("Game/Presentation/UI/GameplayUiController.cs");
            StringAssert.Contains("victory-continue-button", source);
            StringAssert.Contains("defeat-load-button", source);
            StringAssert.Contains("outcome-main-menu-button", source);
            StringAssert.Contains("FocusWhenReady(\"defeat-load-button\", campaignOutcome)", source);
            StringAssert.Contains("alertLabel.tooltip = alertText", source);
            StringAssert.DoesNotContain("alertLabel.text = compactLayout", source);
            StringAssert.Contains("context.Campaign?.State.MilitaryVictory", source);
            StringAssert.Contains("context.GameRun.Status == GameRunStatus.GameOver", source);
            Assert.That(GameplayUiRoutes.Get(GameplayUiRouteId.Realm).StableElementName, Is.EqualTo("nav-global"));
            Assert.That(GameplayUiRoutes.Get(GameplayUiRouteId.Attention).StableElementName, Is.EqualTo("nav-events"));
        }

        [Test]
        public void StrategicViewportCanFollowTheUiCanvasWithoutOwningWorldState()
        {
            var host = new GameObject("UI07 viewport test");
            try
            {
                var view = host.AddComponent<P102StrategicWorldView>();
                var expected = new Rect(.18f, .31f, .59f, .48f);
                view.SetViewport(expected);
                Assert.That(view.CurrentViewport, Is.EqualTo(expected));
                Assert.That(view.IsActive, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RulerEditorCopyDoesNotExposeImplementationTerms()
        {
            var source = Read("Game/Infrastructure/MainMenuController.cs");
            StringAssert.DoesNotContain("параметры применяются к UMA", source);
            StringAssert.DoesNotContain("Проверенный совместимый комплект", source);
            StringAssert.Contains("Внешность не меняет правила новой партии", source);
            StringAssert.Contains("FocusWhenReady", source);
        }
    }
}
