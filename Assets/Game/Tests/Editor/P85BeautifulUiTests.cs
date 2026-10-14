using System.IO;
using Game.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Tests.Editor
{
    [TestFixture, Category("P85")]
    public sealed class P85BeautifulUiTests
    {
        [Test]
        public void ThemeShipsAndUsesOneSharedVisualLanguage()
        {
            Assert.That(Resources.Load<ThemeStyleSheet>("SerenityRuntimeTheme"), Is.Not.Null);
            Assert.That(SerenityUiDesign.RootClass, Is.EqualTo("serenity-root"));
            Assert.That(SerenityUiDesign.CardClass, Is.EqualTo("serenity-card"));
            Assert.That(SerenityUiDesign.ButtonClass, Is.EqualTo("serenity-button"));
        }

        [Test]
        public void PlayerTextMeetsReadableContrastTargets()
        {
            Assert.That(SerenityUiDesign.ContrastRatio(SerenityUiDesign.Text, SerenityUiDesign.Surface),
                Is.GreaterThanOrEqualTo(7f), "Primary text should exceed WCAG AA at normal sizes.");
            Assert.That(SerenityUiDesign.ContrastRatio(SerenityUiDesign.TextMuted, SerenityUiDesign.Surface),
                Is.GreaterThanOrEqualTo(4.5f), "Muted explanatory text must remain readable.");
            Assert.That(SerenityUiDesign.ContrastRatio(new Color(0.1f, 0.11f, 0.08f), SerenityUiDesign.Accent),
                Is.GreaterThanOrEqualTo(4.5f), "Primary-button text must be readable on the accent.");
        }

        [Test]
        public void ThemeEncodesKeyboardDisabledAndLongTextStates()
        {
            var path = Path.Combine(Application.dataPath,
                "Game/Presentation/UI/Resources/SerenityRuntimeTheme.tss");
            var source = File.ReadAllText(path);
            StringAssert.Contains(".serenity-button:focus", source);
            StringAssert.Contains(".serenity-button:disabled", source);
            StringAssert.Contains(".serenity-nav-button.is-selected", source);
            StringAssert.Contains("white-space: normal", source);
            StringAssert.Contains(".serenity-compact", source);
        }
    }
}
