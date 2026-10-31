using System.IO;
using System.Linq;
using Game.Presentation.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Editor
{
    [TestFixture, Category("UI08")]
    public sealed class UI08ReleaseQualificationTests
    {
        private static string Read(string relative) => File.ReadAllText(Path.Combine(Application.dataPath, relative));

        [Test]
        public void ExactBinaryRouteUsesNormalPlayerBoundaries()
        {
            var source = Read("Game/Infrastructure/Integration/UI08PlayerSmokeController.cs");
            StringAssert.Contains("TryStartEditedNewGame", source);
            StringAssert.Contains("ThirdPersonControl.TryEnter", source);
            StringAssert.Contains("SaveSlot(1)", source);
            StringAssert.Contains("ConfirmLoadSlot(1)", source);
            StringAssert.Contains("GameplayUiRouteId.Realm", source);
            StringAssert.Contains("main-panel\").resolvedStyle.display == DisplayStyle.None", source);
            StringAssert.Contains("victory-continue-button", source);
            StringAssert.Contains("defeat-load-button", source);
            StringAssert.DoesNotContain("SubmitDiagnosticCode", source);
            StringAssert.DoesNotContain("SetDeveloperUiVisible(true)", source);
        }

        [Test]
        public void ReleaseRoutesKeepUniqueStableCompatibilityNames()
        {
            var routes = GameplayUiRoutes.Ordered;
            Assert.That(routes.Select(route => route.StableElementName).Distinct().Count(), Is.EqualTo(routes.Count));
            Assert.That(routes.All(route => !string.IsNullOrWhiteSpace(route.StableElementName)), Is.True);
            Assert.That(GameplayUiRoutes.Get(GameplayUiRouteId.Realm).StableElementName, Is.EqualTo("nav-global"));
            Assert.That(GameplayUiRoutes.Get(GameplayUiRouteId.Attention).StableElementName, Is.EqualTo("nav-events"));
        }
    }
}
