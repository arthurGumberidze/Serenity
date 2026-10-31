using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Infrastructure;
using Game.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    [TestFixture, Category("UI07")]
    public sealed class UI07InterfaceAcceptancePlayModeTests
    {
        private string directory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "serenity-ui07-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", Path.Combine(directory, "saves"));
            Environment.SetEnvironmentVariable("SERENITY_SETTINGS_PATH", Path.Combine(directory, "settings.json"));
            Screen.SetResolution(1280, 720, false);
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
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
        public IEnumerator MenuEditorSettingsAndKeyboardFocusFitCompactLayout()
        {
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            Assert.That(menu, Is.Not.Null);
            menu.SetUiScale(1.25f);
            menu.ShowMain();
            yield return null; yield return null;
            if (menu.RootVisual.resolvedStyle.width < 1380f)
                Assert.That(menu.RootVisual.ClassListContains("serenity-compact"), Is.True);
            Assert.That(menu.RootVisual.panel.focusController.focusedElement,
                Is.EqualTo(menu.RootVisual.Q<Button>("new-game-button")));

            menu.OpenNewGame();
            yield return null; yield return null;
            Assert.That(menu.RootVisual.panel.focusController.focusedElement,
                Is.EqualTo(menu.RootVisual.Q<TextField>("ruler-name")));
            AssertInside(menu.RootVisual, menu.RootVisual.Q("menu-content"));
            Assert.That(menu.RootVisual.Query<Button>().ToList().All(button => button.focusable), Is.True);

            menu.OpenSettings();
            yield return null; yield return null;
            Assert.That(menu.RootVisual.panel.focusController.focusedElement,
                Is.EqualTo(menu.RootVisual.Q<DropdownField>("settings-resolution")));
            AssertInside(menu.RootVisual, menu.RootVisual.Q("menu-content"));
        }

        private static void AssertInside(VisualElement root, VisualElement element)
        {
            Assert.That(element, Is.Not.Null);
            Assert.That(element.worldBound.xMin, Is.GreaterThanOrEqualTo(root.worldBound.xMin - 1f));
            Assert.That(element.worldBound.yMin, Is.GreaterThanOrEqualTo(root.worldBound.yMin - 1f));
            Assert.That(element.worldBound.xMax, Is.LessThanOrEqualTo(root.worldBound.xMax + 1f));
            Assert.That(element.worldBound.yMax, Is.LessThanOrEqualTo(root.worldBound.yMax + 1f));
        }
    }
}
