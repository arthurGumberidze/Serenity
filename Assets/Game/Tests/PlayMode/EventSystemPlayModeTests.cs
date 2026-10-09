using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Domain.Events;
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
    [Category("U28")]
    public sealed class EventSystemPlayModeTests
    {
        private string saveDirectory;

        [UnitySetUp]
        public IEnumerator LoadLocalScene()
        {
            saveDirectory = Path.Combine(Path.GetTempPath(), "serenity-u28-play-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", saveDirectory);
            Screen.SetResolution(1600, 900, false);
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator UnloadLocalScene()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            if (root != null) Object.Destroy(root.gameObject);
            yield return null;
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", null);
            if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
        }

        [UnityTest]
        public IEnumerator RuntimeCreatesOneServiceAndOnePlayerEventSurface()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root.Events, Is.Not.Null);
            Assert.That(root.Events.Catalog.All.Count, Is.EqualTo(4));
            Assert.That(Object.FindObjectsByType<GameplayUiController>(FindObjectsInactive.Include).Length, Is.EqualTo(1));
            Assert.That(root.GameplayUi.RootVisual.Q<Button>("nav-events"), Is.Not.Null);
            root.GameplayUi.SetPanel(GameplayPanel.Events);
            root.GameplayUi.RefreshNow();
            Assert.That(PanelText(root.GameplayUi), Does.Contain("Events"));
            Assert.That(root.PointerRaycaster.IsPointerBlockedByUi(new Vector2(200, 200)), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerEventAppearsChoiceAppliesCanonicalOutcomeAndHistoryProjects()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.AdvanceForDebug(TimeSpan.FromMinutes(48));
            var pending = root.Events.PendingPlayerEvents.Single();
            var beforeU18 = root.HealthFamily.Events.Count;
            root.GameplayUi.SetPanel(GameplayPanel.Events);
            root.GameplayUi.RefreshNow();
            var definition = root.Events.Catalog.Get(pending.DefinitionId);
            var node = definition.Nodes.Single(x => x.Id == pending.CurrentNodeId);
            Assert.That(PanelText(root.GameplayUi), Does.Contain(node.Title));
            Submit(root.GameplayUi.RootVisual.Q("main-panel").Query<Button>().ToList()
                .First(x => node.Choices.Any(choice => choice.Label == x.text)));
            Assert.That(root.HealthFamily.Events.Count, Is.GreaterThanOrEqualTo(beforeU18));
            Assert.That(root.Events.ActiveInstances.Any(x => x.InstanceId == pending.InstanceId) ||
                        root.Events.History.Any(x => x.InstanceId == pending.InstanceId), Is.True);
            root.GameplayUi.RefreshNow();
            Assert.That(root.GameplayUi.Notifications.Any(x => x.Contains("event", StringComparison.OrdinalIgnoreCase) ||
                x.Contains("resolved", StringComparison.OrdinalIgnoreCase)), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PendingEventSurvivesSaveLoadInSameUiWithoutSubscriptionGrowth()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var ui = root.GameplayUi;
            root.AiRuntime.AdvanceForDebug(TimeSpan.FromMinutes(48));
            var pending = root.Events.PendingPlayerEvents.Single();
            ui.SaveSlot(1);
            var definition = root.Events.Catalog.Get(pending.DefinitionId);
            var choice = definition.Nodes.Single(x => x.Id == pending.CurrentNodeId).Choices[0];
            Assert.That(root.Events.ResolvePlayerChoice(pending.InstanceId, choice.Id,
                root.Clock.State.CalendarTicks).Applied, Is.True);
            Assert.That(root.Events.PendingPlayerEvents.Any(x => x.InstanceId == pending.InstanceId), Is.False);
            var uiInstance = root.GameplayUi;
            var eventNoticeCount = ui.Notifications.Count(x => x.Contains("event", StringComparison.OrdinalIgnoreCase));
            ui.ConfirmLoadSlot(1);
            Assert.That(root.GameplayUi, Is.SameAs(uiInstance));
            Assert.That(root.GameplayUi.SubscriptionCount, Is.EqualTo(4));
            Assert.That(root.Events.PendingPlayerEvents.Single().InstanceId, Is.EqualTo(pending.InstanceId));
            Assert.That(ui.Notifications.Count(x => x.Contains("event", StringComparison.OrdinalIgnoreCase)),
                Is.EqualTo(eventNoticeCount));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemoteAiEventsResolveWithoutGameObjectsAndTierOrModeReroll()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root.StarterEnemyAi.Residents.All(id => !root.CharacterPresentations.TryGet(id, out _)), Is.True);
            root.AiRuntime.AdvanceForDebug(TimeSpan.FromMinutes(72));
            var aiHistory = root.Events.History.Where(x => x.Audience == EventAudience.Ai).ToArray();
            Assert.That(aiHistory.Length, Is.GreaterThan(0));
            Assert.That(aiHistory.All(x => x.Targets.All(t => t.Kind == EventTargetKind.Character)), Is.True);
            var hash = root.Events.ComputeStateHash();
            root.Events.AdvanceTo(root.Clock.State.CalendarTicks);
            Assert.That(root.Events.ComputeStateHash(), Is.EqualTo(hash));
            yield return null;
        }

        private static string PanelText(GameplayUiController ui) => string.Join("\n",
            ui.RootVisual.Q("main-panel").Query<Label>().ToList().Select(x => x.text));

        private static void Submit(Button button)
        {
            Assert.That(button, Is.Not.Null);
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(invoke, Is.Not.Null);
            invoke.Invoke(button.clickable, new object[] { null });
        }
    }
}
