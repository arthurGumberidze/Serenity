using System;
using System.Collections;
using System.IO;
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
    [TestFixture, Category("P85")]
    public sealed class P85BeautifulUiPlayModeTests
    {
        private string directory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "serenity-p85-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", Path.Combine(directory, "saves"));
            Environment.SetEnvironmentVariable("SERENITY_SETTINGS_PATH", Path.Combine(directory, "settings.json"));
            Screen.SetResolution(1280, 720, false);
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null; yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", null);
            Environment.SetEnvironmentVariable("SERENITY_SETTINGS_PATH", null);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [UnityTest]
        public IEnumerator MainMenuAndEditorFitAtMinimumResolutionAnd125Percent()
        {
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            menu.SetUiScale(1.25f);
            menu.OpenNewGame();
            yield return null; yield return null;

            Assert.That(menu.UiScale, Is.EqualTo(1.25f).Within(0.01f));
            Assert.That(menu.RootVisual.ClassListContains(SerenityUiDesign.RootClass), Is.True);
            Assert.That(menu.RootVisual.Q("ruler-editor-grid").ClassListContains("serenity-editor-grid"), Is.True);
            Assert.That(menu.RootVisual.Q("ruler-identity-column"), Is.Not.Null);
            Assert.That(menu.RootVisual.Q("ruler-appearance-column"), Is.Not.Null);
            foreach (var button in menu.RootVisual.Query<Button>().ToList())
            {
                Assert.That(button.ClassListContains(SerenityUiDesign.ButtonClass), Is.True, button.name);
                Assert.That(button.focusable, Is.True, button.name);
            }
            AssertInside(menu.RootVisual, menu.RootVisual.Q("menu-content"));
            Assert.That(menu.RootVisual.Q<TextField>("ruler-name"), Is.Not.Null);
            Assert.That(menu.RootVisual.Q("appearance-height"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator GameplayPanelsKeepHooksReasonsAndResponsiveBounds()
        {
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            menu.StartNewGame(85006);
            while (SceneManager.GetActiveScene().name != "LocalGameplay") yield return null;
            yield return null; yield return null;
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var ui = root.GameplayUi;
            ui.SetUiScale(1.25f);
            yield return null; yield return null;

            Assert.That(ui.UiScale, Is.EqualTo(1.25f).Within(0.01f));
            foreach (var name in new[] { "top-hud", "navigation", "selection-inspector", "status-bar" })
                AssertInside(ui.RootVisual, ui.RootVisual.Q(name));
            Assert.That(ui.IsGuiPointBlocked(new Vector2(Screen.width * 0.5f, 20f)), Is.True,
                "Scaled UI hit testing must convert screen pixels to panel coordinates.");
            foreach (var panel in new[] { GameplayPanel.Economy, GameplayPanel.Research, GameplayPanel.Work,
                         GameplayPanel.Dynasty, GameplayPanel.Military, GameplayPanel.SaveLoad,
                         GameplayPanel.Global, GameplayPanel.Events })
            {
                ui.SetPanel(panel);
                ui.RefreshNow();
                yield return null;
                Assert.That(ui.RootVisual.Q("main-panel").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), panel.ToString());
                AssertInside(ui.RootVisual, ui.RootVisual.Q("main-panel"));
            }

            ui.SetPanel(GameplayPanel.Build);
            ui.RefreshNow();
            yield return null;
            var labels = string.Join("\n", ui.RootVisual.Q("main-panel").Query<Label>().ToList().Select(x => x.text));
            var disabled = ui.RootVisual.Q("main-panel").Query<Button>().ToList().Where(x => !x.enabledSelf).ToArray();
            Assert.That(disabled.Length, Is.GreaterThan(0));
            Assert.That(labels, Does.Contain("Нужно исследовать").Or.Contain("текущей эпохе"));
            Assert.That(ui.RootVisual.Q<Button>("pause-button"), Is.Not.Null);
            Assert.That(ui.RootVisual.Q<Button>("main-menu-button"), Is.Not.Null);
            Assert.That(ui.RootVisual.Query<Button>().ToList().All(x => x.focusable), Is.True);
            if (ui.RootVisual.resolvedStyle.width < 1380f)
                Assert.That(ui.RootVisual.ClassListContains("serenity-compact"), Is.True);
            Assert.That(RussianTextCatalog.Locale, Is.EqualTo("ru-RU"));
        }

        private static void AssertInside(VisualElement root, VisualElement element)
        {
            Assert.That(element, Is.Not.Null);
            var outer = root.worldBound;
            var inner = element.worldBound;
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - 1f), element.name + " left");
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - 1f), element.name + " top");
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + 1f), element.name + " right");
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + 1f), element.name + " bottom");
        }
    }
}
