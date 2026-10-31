using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Editor
{
    [TestFixture, Category("UI06")]
    public sealed class UI06WorldWorkspaceTests
    {
        private static string UiSource => File.ReadAllText(Path.Combine(Application.dataPath,
            "Game/Presentation/UI/GameplayUiController.cs"));

        [Test]
        public void MiniMapAndRealmWorkspaceReadExistingAuthorities()
        {
            StringAssert.Contains("context.ConnectedWorld.IsRevealed", UiSource);
            StringAssert.Contains("context.FocusWorld", UiSource);
            StringAssert.Contains("context.RequestEnterGlobal", UiSource);
            StringAssert.Contains("context.GlobalMap.IssueTravelOrder", UiSource);
            StringAssert.Contains("context.RequestEnterLocation", UiSource);
            StringAssert.Contains("local-mini-map-grid", UiSource);
            StringAssert.Contains("realm-map-overlay", UiSource);
        }

        [Test]
        public void RouteSummaryUsesCanonicalWeatherSupplyAndKnownRiskFacts()
        {
            StringAssert.Contains("context.Weather.GetWeatherAtCalendarTick", UiSource);
            StringAssert.Contains("context.Military.SupplySettings", UiSource);
            StringAssert.Contains("context.GlobalMap.GetRoutes", UiSource);
            StringAssert.Contains("Только подтверждённые факторы", UiSource);
            StringAssert.Contains("realm-route-summary", UiSource);
        }

        [Test]
        public void DiplomacyIsAnUnavailableNonMutatingFutureContract()
        {
            StringAssert.Contains("Дипломатия — будущая возможность", UiSource);
            StringAssert.Contains("Отношения, договоры, торговля, требования и последствия пока не моделируются", UiSource);
            StringAssert.Contains("button.SetEnabled(false)", UiSource);
            StringAssert.DoesNotContain("TrySendGift", UiSource);
            StringAssert.DoesNotContain("SetRelation(player", UiSource);
            StringAssert.DoesNotContain("Дар: 10 ягод", UiSource);
        }

        [Test]
        public void StrategicViewRemainsASameWorldCameraProjection()
        {
            var bridge = File.ReadAllText(Path.Combine(Application.dataPath,
                "Game/Infrastructure/GlobalMapRuntimeBridge.cs"));
            var view = File.ReadAllText(Path.Combine(Application.dataPath,
                "Game/Presentation/World/P102StrategicWorldView.cs"));
            StringAssert.Contains("strategicView.Enter()", bridge);
            StringAssert.Contains("strategicView.Exit()", bridge);
            StringAssert.Contains("savedPosition", view);
            StringAssert.Contains("setStrategicOverview?.Invoke", view);
            StringAssert.DoesNotContain("SceneManager.LoadScene", view);
        }
    }
}
