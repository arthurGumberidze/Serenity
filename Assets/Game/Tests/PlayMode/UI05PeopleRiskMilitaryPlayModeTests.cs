using System;
using System.Collections;
using System.IO;
using Game.Domain.Combat;
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
    [TestFixture, Category("UI05")]
    public sealed class UI05PeopleRiskMilitaryPlayModeTests
    {
        private string directory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "serenity-ui05-" + Guid.NewGuid().ToString("N"));
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
        public IEnumerator Ui05RoutesProjectCanonicalStateAtMinimumLayout()
        {
            LocalSceneCompositionRoot root = null;
            var deadline = Time.realtimeSinceStartup + 600f;
            while (root == null && Time.realtimeSinceStartup < deadline)
            {
                root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
                if (root == null || root.GameplayUi == null) { root = null; yield return null; }
            }
            Assert.That(root, Is.Not.Null);
            while (Object.FindAnyObjectByType<WorldLoadingOverlay>() != null && Time.realtimeSinceStartup < deadline)
                yield return null;

            var ui = root.GameplayUi;
            ui.SetUiScale(1.25f);
            root.SelectionProbe.SelectCharacterIds(new[] { root.Dynasty.CurrentMonarchId.Value });
            ui.Navigate(GameplayUiRouteId.People);
            ui.RefreshNow();
            yield return null;
            Assert.That(ui.RootVisual.Q<ListView>("people-roster"), Is.Not.Null);
            Assert.That(ui.RootVisual.Q("people-home-card"), Is.Not.Null);
            Assert.That(ui.RootVisual.Q("people-family-card"), Is.Not.Null);

            ui.Navigate(GameplayUiRouteId.Dynasty);
            ui.RefreshNow();
            yield return null;
            Assert.That(ui.RootVisual.Q("dynasty-family-tree"), Is.Not.Null);
            Assert.That(ui.RootVisual.Q("dynasty-succession-line"), Is.Not.Null);

            ui.Navigate(GameplayUiRouteId.Attention);
            if (root.Events.PendingPlayerEvents.Count == 0)
                root.AiRuntime.AdvanceForDebug(TimeSpan.FromMinutes(48));
            ui.RefreshNow();
            yield return null;
            Assert.That(ui.RootVisual.Q<Button>("attention-decisions"), Is.Not.Null);
            Assert.That(ui.RootVisual.Q<Button>("attention-journal"), Is.Not.Null);
            Assert.That(ui.RootVisual.Q("attention-decision-card"), Is.Not.Null);

            root.StartDemoUnitBattle(BattleMode.Abstract);
            ui.Navigate(GameplayUiRouteId.Military);
            ui.RefreshNow();
            yield return null;
            Assert.That(ui.RootVisual.Q<ListView>("military-unit-roster"), Is.Not.Null);
            Assert.That(ui.RootVisual.Q<ListView>("military-member-roster"), Is.Not.Null);
            Assert.That(ui.RootVisual.Q("military-readiness-summary"), Is.Not.Null);
            Assert.That(ui.RootVisual.Q("military-combat-status"), Is.Not.Null);
            Assert.That(ui.RootVisual.Q("main-panel").resolvedStyle.width, Is.GreaterThan(700f));
        }
    }
}
