using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Work;
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
    [TestFixture, Category("UI03")]
    public sealed class UI03WorldHudPlayModeTests
    {
        private string directory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "serenity-ui03-" + Guid.NewGuid().ToString("N"));
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
        public IEnumerator StableSelectionQueueAndContextRibbonSurvivePresentationRefresh()
        {
            LocalSceneCompositionRoot root = null;
            var deadline = Time.realtimeSinceStartup + 600f;
            while (root == null && Time.realtimeSinceStartup < deadline)
            {
                root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
                if (root == null || root.GameplayUi == null) { root = null; yield return null; }
            }
            Assert.That(root, Is.Not.Null, "Local gameplay did not initialize within 600 seconds.");
            var ui = root.GameplayUi;
            var presenter = root.DemoPresenters.First();
            var id = presenter.CharacterId;
            root.SelectionProbe.SelectCharacterIds(new[] { id });
            var position = presenter.transform.position;
            ui.OpenContextMenu(new ContextCommandTarget(ContextTargetKind.Ground,
                new WorldPosition(position.x + 2f, position.y, position.z + 2f)), Vector2.zero);
            var result = ui.ExecuteContextAction(ContextActionKind.Move, false);
            Assert.That(result.Orders, Is.Not.Empty);

            ui.RefreshAfterStateRebind();
            yield return null;
            Assert.That(root.SelectionProbe.SelectedCharacterIds, Is.EqualTo(new[] { id }));
            Assert.That(ui.RootVisual.Q("context-command-ribbon").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(ui.RootVisual.Q("inspector-title"), Is.Not.Null);
            Assert.That(ui.RootVisual.Q<Label>("selection-queue-label").text, Does.Contain("Очередь"));
            Assert.That(ui.RootVisual.Q("selection-inspector").Query<Label>().ToList()
                .Any(label => label.ClassListContains("serenity-queue-order")), Is.True);

            root.SelectionProbe.SelectCharacterIds(root.PlayerCharacterIds.Take(3));
            ui.RefreshNow();
            yield return null;
            var multiText = string.Join("\n", ui.RootVisual.Q("selection-inspector").Query<Label>().ToList()
                .Select(label => label.text));
            Assert.That(multiText, Does.Contain("Выбрано персонажей: 3").Or.Contain("3"));
            Assert.That(ui.IsGuiPointBlocked(new Vector2(Screen.width * .5f, Screen.height * .5f)), Is.False);
        }
    }
}
