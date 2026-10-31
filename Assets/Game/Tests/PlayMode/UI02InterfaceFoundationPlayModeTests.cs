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
    [TestFixture, Category("UI02")]
    public sealed class UI02InterfaceFoundationPlayModeTests
    {
        private string directory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "serenity-ui02-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", Path.Combine(directory, "saves"));
            Environment.SetEnvironmentVariable("SERENITY_SETTINGS_PATH", Path.Combine(directory, "settings.json"));
            Screen.SetResolution(1280, 720, false);
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
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

        [UnityTest, Timeout(600000)]
        public IEnumerator BottomShellRoutesAtMinimumResolutionAndKeepsWorldCenterFree()
        {
            GameplayUiController ui = null;
            var deadline = Time.realtimeSinceStartup + 600f;
            while (ui == null && Time.realtimeSinceStartup < deadline)
            {
                ui = Object.FindAnyObjectByType<LocalSceneCompositionRoot>()?.GameplayUi;
                if (ui == null) yield return null;
            }
            Assert.That(ui, Is.Not.Null, "Local gameplay UI did not initialize within 600 seconds.");
            ui.SetUiScale(1.25f);
            ui.RefreshNow();
            yield return null; yield return null;

            var navigation = ui.RootVisual.Q("navigation");
            Assert.That(navigation, Is.Not.Null);
            Assert.That(navigation.ClassListContains("serenity-bottom-navigation"), Is.True);
            Assert.That(navigation.worldBound.yMin,
                Is.GreaterThan(ui.RootVisual.worldBound.height * 0.72f));
            foreach (var route in GameplayUiRoutes.Ordered)
            {
                var button = navigation.Q<Button>(route.StableElementName);
                Assert.That(button, Is.Not.Null, route.Id.ToString());
                Assert.That(button.worldBound.height, Is.GreaterThanOrEqualTo(40f));
                Assert.That(button.tooltip, Does.Contain(route.Label));
                ui.Navigate(route.Id);
                Assert.That(ui.ActiveRoute, Is.EqualTo(route.Id));
            }
            ui.SetPanel(GameplayPanel.None);
            ui.RefreshNow();
            Assert.That(ui.IsGuiPointBlocked(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), Is.False);
            Assert.That(ui.RootVisual.Q<Button>("nav-codes"), Is.Not.Null);
            Assert.That(ui.RootVisual.Q<Button>("main-menu-button"), Is.Not.Null);
            ui.Navigate(GameplayUiRouteId.Attention);
            ui.RefreshNow();
            yield return null;

            var text = string.Join("\n", ui.RootVisual.Q("main-panel").Query<Label>().ToList().Select(x => x.text));
            Assert.That(text, Does.Contain("Центр внимания"));
            Assert.That(text, Does.Not.Contain("foreign tasks"));
            Assert.That(text, Does.Not.Contain("прерванная работа возвращается"));
            Assert.That(text, Does.Not.Contain("тестовый контракт"));
            Assert.That(ui.RootVisual.Q<Button>("nav-events").ClassListContains(SerenityUiDesign.SelectedClass), Is.True);
        }
    }
}
