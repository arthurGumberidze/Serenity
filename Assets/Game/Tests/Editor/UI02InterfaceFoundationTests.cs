using System.IO;
using System.Linq;
using Game.Presentation.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Editor
{
    [TestFixture, Category("UI02")]
    public sealed class UI02InterfaceFoundationTests
    {
        [Test]
        public void QuietBlackGoldTokensMeetReadableContrastFloor()
        {
            Assert.That(SerenityUiDesign.ContrastRatio(SerenityUiTokens.Bone, SerenityUiTokens.Surface),
                Is.GreaterThanOrEqualTo(7f));
            Assert.That(SerenityUiDesign.ContrastRatio(SerenityUiTokens.BoneMuted, SerenityUiTokens.Surface),
                Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(SerenityUiDesign.ContrastRatio(SerenityUiTokens.Gold, SerenityUiTokens.Surface),
                Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(SerenityUiDesign.ContrastRatio(SerenityUiTokens.InkOnGold, SerenityUiTokens.Gold),
                Is.GreaterThanOrEqualTo(4.5f));
        }

        [Test]
        public void PrimaryRoutesAreOrderedUniqueAndKeepLegacyStableNames()
        {
            var routes = GameplayUiRoutes.Ordered;
            Assert.That(routes.Count, Is.EqualTo(9));
            Assert.That(routes.Select(value => value.Id).Distinct().Count(), Is.EqualTo(routes.Count));
            Assert.That(routes.Select(value => value.StableElementName).Distinct().Count(), Is.EqualTo(routes.Count));
            Assert.That(GameplayUiRoutes.Get(GameplayUiRouteId.Construction).StableElementName, Is.EqualTo("nav-build"));
            Assert.That(GameplayUiRoutes.Get(GameplayUiRouteId.Attention).StableElementName, Is.EqualTo("nav-events"));
            Assert.That(GameplayUiRoutes.Get(GameplayUiRouteId.Realm).Panel, Is.EqualTo(GameplayPanel.Global));
        }

        [Test]
        public void TypographyUsesCyrillicSystemFallbacksWithoutAdmittingFontFiles()
        {
            Assert.That(SerenityUiTypography.InterfaceFamilies, Does.Contain("Segoe UI"));
            Assert.That(SerenityUiTypography.DisplayFamilies, Does.Contain("Georgia"));
            var gameRoot = Path.Combine(Application.dataPath, "Game");
            Assert.That(Directory.GetFiles(gameRoot, "*.ttf", SearchOption.AllDirectories), Is.Empty);
            Assert.That(Directory.GetFiles(gameRoot, "*.otf", SearchOption.AllDirectories), Is.Empty);
        }

        [Test]
        public void ThemeDefinesReusableInteractionAndSemanticStates()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath,
                "Game/Presentation/UI/Resources/SerenityRuntimeTheme.tss"));
            StringAssert.Contains("UI02 final cascade", source);
            StringAssert.Contains(".serenity-button:hover", source);
            StringAssert.Contains(".serenity-button:focus", source);
            StringAssert.Contains(".serenity-button:disabled", source);
            StringAssert.Contains(".serenity-nav-button.is-selected", source);
            StringAssert.Contains(".serenity-badge.is-success", source);
            StringAssert.Contains(".serenity-badge.is-warning", source);
            StringAssert.Contains(".serenity-badge.is-danger", source);
            StringAssert.Contains(".serenity-bottom-navigation", source);
        }

        [Test]
        public void EveryPrimaryRouteHasAProjectOwnedIcon()
        {
            Assert.That(SerenityIconLibrary.IsReady, Is.True);
            foreach (var route in GameplayUiRoutes.Ordered)
                Assert.That(SerenityIconLibrary.Get(route.IconKey), Is.Not.Null, route.Id.ToString());
        }
    }
}
