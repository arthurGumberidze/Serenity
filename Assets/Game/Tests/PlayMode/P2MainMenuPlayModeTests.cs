using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Infrastructure;
using Game.Presentation.Localization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode
{
    [TestFixture]
    [Category("P2")]
    public sealed class P2MainMenuPlayModeTests
    {
        private string directory;
        private string settingsPath;
        private string oldSaveDirectory;
        private string oldSettingsPath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "serenity-p2-menu-" + Guid.NewGuid().ToString("N"));
            settingsPath = Path.Combine(directory, "settings", "user-settings.json");
            Directory.CreateDirectory(directory);
            oldSaveDirectory = Environment.GetEnvironmentVariable("SERENITY_SAVE_DIRECTORY");
            oldSettingsPath = Environment.GetEnvironmentVariable("SERENITY_SETTINGS_PATH");
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", Path.Combine(directory, "saves"));
            Environment.SetEnvironmentVariable("SERENITY_SETTINGS_PATH", settingsPath);
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", oldSaveDirectory);
            Environment.SetEnvironmentVariable("SERENITY_SETTINGS_PATH", oldSettingsPath);
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [UnityTest]
        public IEnumerator CleanLaunchShowsFiveRussianActionsAndBackPaths()
        {
            var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
            Assert.That(menu, Is.Not.Null);
            Assert.That(menu.ContinueAvailable, Is.False);
            Assert.That(menu.RootVisual.Q<Button>("continue-button").enabledSelf, Is.False);
            var buttonTexts = menu.RootVisual.Query<Button>().ToList().Select(x => x.text).ToArray();
            Assert.That(buttonTexts, Does.Contain(RussianTextCatalog.Get("menu.continue")));
            Assert.That(buttonTexts, Does.Contain(RussianTextCatalog.Get("menu.new_game")));
            Assert.That(buttonTexts, Does.Contain(RussianTextCatalog.Get("menu.load")));
            Assert.That(buttonTexts, Does.Contain(RussianTextCatalog.Get("menu.settings")));
            Assert.That(buttonTexts, Does.Contain(RussianTextCatalog.Get("menu.exit")));

            menu.OpenNewGame();
            Assert.That(menu.CurrentView, Is.EqualTo(MainMenuView.NewGame));
            Assert.That(menu.RootVisual.Q<TextField>("world-seed"), Is.Not.Null);
            menu.ShowMain();
            menu.OpenSettings();
            Assert.That(menu.CurrentView, Is.EqualTo(MainMenuView.Settings));
            Assert.That(menu.RootVisual.Q<Button>("cancel-settings-button"), Is.Not.Null);
            menu.ShowMain();
            Assert.That(menu.CurrentView, Is.EqualTo(MainMenuView.Main));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CorruptSaveIsExplainedAndNeverBecomesContinueTarget()
        {
            var saveDirectory = Environment.GetEnvironmentVariable("SERENITY_SAVE_DIRECTORY");
            Directory.CreateDirectory(saveDirectory);
            File.WriteAllText(Path.Combine(saveDirectory, LocalGameplaySaveFacade.SlotId(1) + ".xml"), "broken");
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;

            var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
            Assert.That(menu.ContinueAvailable, Is.False);
            menu.OpenLoad();
            Assert.That(menu.RootVisual.Q<Button>("corrupt-slot-1"), Is.Not.Null);
            Assert.That(menu.RootVisual.Query<Label>().ToList().Select(x => x.text),
                Does.Contain(RussianTextCatalog.Get("menu.load.corrupt")));
            yield return null;
        }
    }
}
