using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Domain.Campaign;
using Game.Domain.Military;
using Game.Domain.Research;
using Game.Domain.Resources;
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
    [Category("P6")]
    public sealed class P6FirstHourPlayModeTests
    {
        private string saveDirectory;

        [UnitySetUp]
        public IEnumerator LoadLocalScene()
        {
            saveDirectory = Path.Combine(Path.GetTempPath(), "serenity-p6-play-" + Guid.NewGuid().ToString("N"));
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
        public IEnumerator FirstHourCardIsVisibleOptionalAndSettlementExplainsTheStartingSituation()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var ui = root.GameplayUi;

            Assert.That(ui.OnboardingVisible, Is.True);
            Assert.That(ui.OnboardingStage, Is.EqualTo(FirstHourStage.InspectGroup));
            Assert.That(ui.RootVisual.Q("onboarding-card").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Text(ui.RootVisual.Q("onboarding-card")), Does.Contain("ПЕРВЫЙ ЧАС"));

            Submit(ui.RootVisual.Q<Button>("onboarding-hide"));
            Assert.That(ui.OnboardingVisible, Is.False);
            Assert.That(ui.RootVisual.Q("onboarding-card").style.display.value, Is.EqualTo(DisplayStyle.None));
            Submit(ui.RootVisual.Q<Button>("onboarding-toggle"));
            Assert.That(ui.OnboardingVisible, Is.True);

            ui.SetPanel(GameplayPanel.Economy);
            var economy = Text(ui.RootVisual.Q("main-panel"));
            Assert.That(economy, Does.Contain("Начальная группа"));
            Assert.That(economy, Does.Contain("жителей: 10").And.Not.Contain("жителей: 16"));
            Assert.That(Text(ui.RootVisual.Q("top-hud")), Does.Contain("Жители: 10"));
            Assert.That(economy, Does.Contain("Укрытие: готово · склад: готово"));
            Assert.That(economy, Does.Contain("Ягоды:"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CanonicalCommandsAdvanceRouteAndP4P5IdentitySurvivesSaveLoad()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var ui = root.GameplayUi;
            root.Clock.Pause();

            root.SelectionProbe.SelectCharacters(new[] { root.DemoPresenters[0] });
            ui.RefreshNow();
            Assert.That(ui.OnboardingStage, Is.EqualTo(FirstHourStage.AssignWork));

            ui.SetPanel(GameplayPanel.Work);
            Submit(Button(ui, "ui.work.create"));
            Submit(Button(ui, "ui.work.move_center"));
            ui.RefreshNow();
            Assert.That(ui.OnboardingStage, Is.EqualTo(FirstHourStage.InspectShelterAndStorage));

            ui.SetPanel(GameplayPanel.Economy);
            Assert.That(ui.OnboardingStage, Is.EqualTo(FirstHourStage.SecureFood));
            var berries = new ResourceId("berries");
            root.Production.SetTarget(berries, root.Production.GetCanonicalTotal(berries) + 20);
            var berryAnchor = root.InhabitableRegion.ResourceAnchors.First(x => x.RecipeId == "gather_berries");
            var berrySiteId = DeterministicStableId.Create(root.ActiveNewGameConfiguration.WorldSeed,
                "player_source", berryAnchor.StableIndex);
            var berrySite = root.Production.Sites.Single(x => x.Id == berrySiteId);
            Assert.That(root.Production.TryReserve(root.PlayerCharacterIds[2], berrySite.Position, out var production), Is.True);
            Assert.That(root.Production.AdvanceTask(production.Task.Id, TimeSpan.FromDays(30)).ProducedOutput, Is.True);
            ui.RefreshNow();
            Assert.That(ui.OnboardingStage, Is.EqualTo(FirstHourStage.StartResearch));

            root.SelectionProbe.SelectCharacters(new[] { root.DemoPresenters[0] });
            ui.SetPanel(GameplayPanel.Research);
            Submit(Button(ui, "ui.research.assign"));
            var technology = root.Research.AvailableTechnologies.First();
            Submit(ui.RootVisual.Q("main-panel").Query<Button>().ToList().First(x =>
                x.text == RussianTextCatalog.Format("ui.research.start",
                    RussianTextCatalog.Content("technology", technology.Id.ToString()))));
            ui.RefreshNow();
            Assert.That(root.Research.ActiveProject, Is.Not.Null);
            Assert.That(ui.OnboardingStage, Is.EqualTo(FirstHourStage.ResolveEventOrDanger));

            root.Clock.Resume();
            root.AiRuntime.AdvanceForDebug(TimeSpan.FromMinutes(48));
            root.Clock.Pause();
            for (var guard = 0; guard < 4 && root.Events.History.Count == 0; guard++)
            {
                ui.SetPanel(GameplayPanel.Events);
                var pending = root.Events.PendingPlayerEvents.First();
                var node = root.Events.Catalog.Get(pending.DefinitionId).Nodes.Single(x => x.Id == pending.CurrentNodeId);
                Submit(ui.RootVisual.Q("main-panel").Query<Button>().ToList().First(x => node.Choices.Any(choice =>
                    x.text == RussianTextCatalog.EventChoice(pending.DefinitionId.ToString(), pending.CurrentNodeId.ToString(), choice.Id.ToString()))));
            }
            ui.RefreshNow();
            Assert.That(root.Events.History.Count, Is.GreaterThan(0));
            Assert.That(ui.OnboardingStage, Is.EqualTo(FirstHourStage.BeginScouting));

            root.SelectionProbe.SelectCharacters(root.DemoPresenters);
            ui.SetPanel(GameplayPanel.Military);
            Submit(Button(ui, "ui.military.create"));
            Submit(Button(ui, "ui.military.aggregate"));
            var unit = root.MilitaryUnits.Units.Single(x => x.OwnerSideId.Value == "player");
            ui.SetPanel(GameplayPanel.Global);
            Submit(Button(ui, "ui.map.open"));
            for (var i = 0; i < 30 && !root.GlobalMapBridge.IsGlobalSceneLoaded; i++) yield return null;
            ui.RefreshNow();
            var active = root.GlobalMap.ActiveLocationId;
            var route = root.GlobalMap.GetRoutes(active).First();
            var destination = route.From == active ? route.To : route.From;
            Submit(ui.RootVisual.Q("main-panel").Query<Button>().ToList().First(x =>
                x.text.StartsWith(RussianTextCatalog.Content("location", destination.Value))));
            Submit(ui.RootVisual.Q("main-panel").Query<Button>().ToList().First(x => x.text.Contains("Отряд игрока")));
            Submit(Button(ui, "ui.map.travel"));
            ui.RefreshNow();
            Assert.That(ui.OnboardingStage, Is.EqualTo(FirstHourStage.Complete));

            var rulerId = root.MaleDemoCharacter.Id;
            var profile = root.Appearances.Get(rulerId).ProfileId;
            var remaining = root.Production.Sites.Single(x => x.Id == berrySiteId).RemainingAmount;
            ui.SaveSlot(1);
            ui.ConfirmLoadSlot(1);
            Assert.That(root.MaleDemoCharacter.Id, Is.EqualTo(rulerId));
            Assert.That(root.Appearances.Get(rulerId).ProfileId, Is.EqualTo(profile));
            Assert.That(root.Production.Sites.Single(x => x.Id == berrySiteId).RemainingAmount, Is.EqualTo(remaining));
            Assert.That(root.GlobalMap.Travels.Any(x => x.UnitId == unit.Id && x.Destination == destination), Is.True);
        }

        private static string Text(VisualElement element) => string.Join("\n", element.Query<Label>().ToList().Select(x => x.text));

        private static Button Button(GameplayUiController ui, string key) =>
            ui.RootVisual.Query<Button>().ToList().First(x => x.text == RussianTextCatalog.Get(key));

        private static void Submit(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.enabledSelf, Is.True, "Button is unexpectedly disabled: " + button.text);
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(invoke, Is.Not.Null);
            invoke.Invoke(button.clickable, new object[] { null });
        }
    }
}
