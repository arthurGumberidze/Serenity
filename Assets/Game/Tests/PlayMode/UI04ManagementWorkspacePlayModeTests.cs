using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.Work;
using Game.Infrastructure;
using Game.Presentation.UI;
using Game.Simulation.Work;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    [TestFixture, Category("UI04")]
    public sealed class UI04ManagementWorkspacePlayModeTests
    {
        private string directory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "serenity-ui04-" + Guid.NewGuid().ToString("N"));
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
        public IEnumerator ManagementRoutesProjectCanonicalStateAtMinimumLayout()
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

            foreach (var characterId in root.PlayerCharacterIds)
            {
                var character = root.Characters.Get(characterId);
                if (character.AgeInCompletedYears(root.Clock.State.BiologicalTicks) < 16) continue;
                Assert.That(root.Work.Groups.TryGetForMember(characterId, out var group), Is.True);
                Assert.That(group.Role, Is.EqualTo(ProfessionGroupService.RoleForProfession(character.ProfessionId)));
            }

            var ui = root.GameplayUi;
            ui.SetUiScale(1.25f);
            ui.Navigate(GameplayUiRouteId.Settlement);
            yield return null;
            Assert.That(ui.RootVisual.Q("main-panel").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(ui.RootVisual.Q("main-panel").Query<Label>().ToList().Any(label => label.text == "Ресурсы и склады"), Is.True);

            ui.Navigate(GameplayUiRouteId.People);
            yield return null;
            Assert.That(ui.RootVisual.Q<ListView>("people-roster"), Is.Not.Null);

            ui.Navigate(GameplayUiRouteId.WorkGroups);
            yield return null;
            Assert.That(ui.RootVisual.Q<Button>("work-create-profession-group"), Is.Not.Null);
            Assert.That(ui.RootVisual.Q("main-panel").Query<Label>().ToList()
                .Any(label => label.text.Contains("Профессия")), Is.True);

            ui.Navigate(GameplayUiRouteId.Construction);
            yield return null;
            Assert.That(ui.RootVisual.Q("main-panel").resolvedStyle.height, Is.LessThanOrEqualTo(292.1f));

            ui.Navigate(GameplayUiRouteId.Knowledge);
            yield return null;
            Assert.That(ui.RootVisual.Q<Button>("research-category-survival"), Is.Not.Null);
            Assert.That(ui.RootVisual.Q<Button>("research-category-production"), Is.Not.Null);
            Assert.That(ui.RootVisual.Q<Button>("research-category-settlement"), Is.Not.Null);
        }
    }
}
