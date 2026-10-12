using System;
using System.Collections;
using System.Linq;
using Game.Infrastructure;
using Game.Presentation.Localization;
using Game.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    [Category("P1")]
    public sealed class P1RussianLocalizationPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            RussianTextCatalog.ResetMissingDiagnosticsForTests();
            Screen.SetResolution(1280, 720, false);
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NormalPlayerRouteIsRussianAtMinimumResolutionAndIncreasedScale()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var ui = root.GameplayUi;
            Assert.That(ui.Locale, Is.EqualTo("ru-RU"));
            ui.SetUiScale(1.25f);
            Assert.That(ui.UiScale, Is.EqualTo(1.25f).Within(0.001f));
            Assert.That(ui.DeveloperUiVisible, Is.False);

            foreach (GameplayPanel panel in Enum.GetValues(typeof(GameplayPanel)))
            {
                if (panel == GameplayPanel.None) continue;
                ui.SetPanel(panel);
                if (ui.ActivePanel != panel) ui.SetPanel(panel);
                ui.RefreshNow();
                var text = string.Join("\n", ui.RootVisual.Query<Label>().ToList().Select(x => x.text)
                    .Concat(ui.RootVisual.Query<Button>().ToList().Select(x => x.text)));
                Assert.That(text.Any(c => c >= 'А' && c <= 'я'), Is.True, panel.ToString());
                Assert.That(text, Does.Not.Contain("Stone Age research"));
                Assert.That(text, Does.Not.Contain("Build menu"));
                Assert.That(text, Does.Not.Contain("Player Unit"));
                Assert.That(text, Does.Not.Contain("primitive_shelter"));
                Assert.That(text, Does.Not.Contain("shelter_construction"));
            }

            Assert.That(RussianTextCatalog.MissingKeys, Is.Empty,
                string.Join(", ", RussianTextCatalog.MissingKeys));
            yield return null;
        }
    }
}
