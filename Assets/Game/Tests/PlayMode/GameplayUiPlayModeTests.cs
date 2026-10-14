using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Infrastructure;
using Game.Domain.Control;
using Game.Domain.Military;
using Game.Domain.Work;
using Game.Presentation.AI;
using Game.Presentation.UI;
using Game.Presentation.Localization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    [Category("U27")]
    public sealed class GameplayUiPlayModeTests
    {
        private string saveDirectory;

        [UnitySetUp]
        public IEnumerator LoadLocalScene()
        {
            saveDirectory = Path.Combine(Path.GetTempPath(), "serenity-u27-play-" + Guid.NewGuid().ToString("N"));
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
        public IEnumerator RuntimeBootsOnePlayerUiWithDeveloperOverlaysHidden()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var ui = root.GameplayUi;

            Assert.That(ui, Is.Not.Null);
            Assert.That(Object.FindObjectsByType<GameplayUiController>(FindObjectsInactive.Include).Length, Is.EqualTo(1));
            Assert.That(ui.DeveloperUiVisible, Is.False);
            Assert.That(ui.SubscriptionCount, Is.EqualTo(4));
            Assert.That(root.PlayerSaves.ListSlots().Count, Is.EqualTo(LocalGameplaySaveFacade.SlotCount));
            Assert.That(Object.FindObjectsByType<StrategicAiDebugOverlay>(FindObjectsInactive.Include)
                .Single().enabled, Is.False);
            Assert.That(ui.RootVisual.Q("top-hud"), Is.Not.Null);
            Assert.That(ui.RootVisual.Q("selection-inspector"), Is.Not.Null);
            Assert.That(ui.RootVisual.Q("notifications"), Is.Not.Null);
            Assert.That(root.PointerRaycaster.IsPointerBlockedByUi(new Vector2(20, 20)), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NavigationProjectsCanonicalResearchAndLockedBuildReasons()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var ui = root.GameplayUi;

            ui.SetPanel(GameplayPanel.Research);
            ui.RefreshNow();
            var researchText = string.Join("\n", ui.RootVisual.Q("main-panel").Query<Label>().ToList()
                .Select(x => x.text));
            Assert.That(researchText, Does.Contain(RussianTextCatalog.Get("ui.research.title")));
            Assert.That(researchText, Does.Contain(RussianTextCatalog.Content("technology", root.Technologies.All.First().Id.ToString())));

            ui.SetPanel(GameplayPanel.Build);
            ui.RefreshNow();
            var buildText = string.Join("\n", ui.RootVisual.Q("main-panel").Query<Label>().ToList()
                .Select(x => x.text));
            Assert.That(buildText, Does.Contain(RussianTextCatalog.Get("ui.build.title")));
            Assert.That(buildText, Does.Contain("Нужно исследовать").Or.Contain("текущей эпохе"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SelectionInspectorAndDeveloperToggleRemainAdapters()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root.CharacterPresentations.TryGet(root.MaleDemoCharacter.Id, out var presenter), Is.True);

            root.SelectionProbe.SelectCharacters(new[] { presenter });
            root.GameplayUi.RefreshNow();
            var inspectorText = string.Join("\n", root.GameplayUi.RootVisual.Q("selection-inspector")
                .Query<Label>().ToList().Select(x => x.text));
            Assert.That(inspectorText, Does.Contain(RussianTextCatalog.PersonName(root.MaleDemoCharacter.Name.DisplayName)));
            Assert.That(inspectorText, Does.Contain("Состояние:"));
            Assert.That(inspectorText, Does.Contain("Рабочая группа:"));
            Assert.That(inspectorText, Does.Not.Contain(root.MaleDemoCharacter.Id.ToString()));

            root.GameplayUi.SetDeveloperUiVisible(true);
            root.GameplayUi.RefreshNow();
            Assert.That(root.GameplayUi.DeveloperUiVisible, Is.True);
            inspectorText = string.Join("\n", root.GameplayUi.RootVisual.Q("selection-inspector")
                .Query<Label>().ToList().Select(x => x.text));
            Assert.That(inspectorText, Does.Contain(root.MaleDemoCharacter.Id.ToString()));
            root.GameplayUi.SetDeveloperUiVisible(false);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SaveLoadRestoresClockAndRebindsExistingUiExactlyOnce()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            var savedTicks = root.Clock.State.CalendarTicks;

            root.GameplayUi.SaveSlot(1);
            root.AiRuntime.SetPaused(false);
            root.AiRuntime.AdvanceForDebug(TimeSpan.FromHours(12));
            root.AiRuntime.SetPaused(true);
            Assert.That(root.Clock.State.CalendarTicks, Is.GreaterThan(savedTicks));

            root.GameplayUi.ConfirmLoadSlot(1);

            Assert.That(root.Clock.State.CalendarTicks, Is.EqualTo(savedTicks));
            Assert.That(root.Saves.Clock, Is.SameAs(root.Clock));
            Assert.That(root.GameplayUi.SubscriptionCount, Is.EqualTo(4));
            Assert.That(root.PlayerSaves.ListSlots().Single(x => x.Index == 1).Exists, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerPanelsRouteTimeResearchWorkBuildMilitaryAndTpsCommands()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var ui = root.GameplayUi;
            Submit(ui.RootVisual.Q<Button>("pause-button"));
            Assert.That(root.Clock.IsPaused, Is.True);
            Submit(ui.RootVisual.Q<Button>("pause-button"));
            Assert.That(root.Clock.IsPaused, Is.False);

            root.SelectionProbe.SelectCharacters(new[] { root.DemoPresenters[0] });
            ui.SetPanel(GameplayPanel.Research);
            Submit(ButtonWithText(ui, "Assign selected"));
            Assert.That(root.Research.AssignedWorkers, Does.Contain(root.DemoPresenters[0].CharacterId));
            var technology = root.Research.AvailableTechnologies.First();
            Submit(ButtonWithText(ui, RussianTextCatalog.Format("ui.research.start",
                RussianTextCatalog.Content("technology", technology.Id.ToString()))));
            Assert.That(root.Research.ActiveProject.TechnologyId, Is.EqualTo(technology.Id));

            var researchTick = root.Research.LastProcessedTick;
            for (var attempt = 0; attempt < root.Technologies.All.Count &&
                 !root.BuildingCatalog.Entries.Any(x => root.Research.IsBuildingUnlocked(x.CreateDefinition().Id)); attempt++)
            {
                var active = root.Research.ActiveProject;
                if (active == null)
                {
                    ui.RefreshNow();
                    var next = root.Research.AvailableTechnologies
                        .OrderByDescending(x => x.Unlocks.Any(unlock => unlock.Kind == Game.Domain.Research.TechnologyUnlockKind.Building))
                        .First();
                    Submit(ButtonWithText(ui, RussianTextCatalog.Format("ui.research.start",
                        RussianTextCatalog.Content("technology", next.Id.ToString()))));
                    active = root.Research.ActiveProject;
                }
                researchTick = checked(researchTick + active.RequiredWorkTicks * 10);
                root.Research.AdvanceWorkerTo(root.DemoPresenters[0].CharacterId, researchTick, true);
            }

            root.SelectionProbe.SelectCharacters(root.DemoPresenters);
            ui.SetPanel(GameplayPanel.Work);
            Submit(ButtonWithText(ui, "Create from selected characters"));
            var group = root.Work.Groups.All.Single(x => x.Role == WorkGroupRole.General);
            var name = ui.RootVisual.Q<TextField>("work-group-name");
            name.value = "Builders";
            Submit(ButtonWithText(ui, "Rename"));
            Assert.That(group.DisplayName, Is.EqualTo("Builders"));
            Submit(ButtonWithText(ui, "Move to center"));
            Assert.That(root.Work.Jobs.Any(x => x.GroupId == group.Id && x.Type == WorkJobType.Move), Is.True);
            Submit(ButtonWithText(ui, "Cancel open jobs"));
            Assert.That(root.Work.Jobs.Where(x => x.GroupId == group.Id).All(x => x.IsTerminal), Is.True);
            Submit(ButtonWithText(ui, "Haul available"));
            Assert.That(root.Work.Jobs.Any(x => x.GroupId == group.Id && x.Type == WorkJobType.Haul), Is.True);

            var unlockedBuilding = root.BuildingCatalog.Entries.Select(x => x.CreateDefinition())
                .First(x => root.Research.IsBuildingUnlocked(x.Id));
            ui.SetPanel(GameplayPanel.Build);
            Submit(ui.RootVisual.Q("main-panel").Query<Button>().ToList()
                .First(x => x.text.StartsWith(RussianTextCatalog.Content("building", unlockedBuilding.Id.ToString()))));
            Assert.That(root.BuildingPlacementController.IsActive, Is.True);
            root.BuildingPlacementController.CancelPlacement();

            root.SelectionProbe.SelectCharacters(root.DemoPresenters);
            ui.SetPanel(GameplayPanel.Military);
            Submit(ButtonWithText(ui, "Create player unit from selected"));
            var unit = root.MilitaryUnits.Units.Single(x => x.OwnerSideId.Value == "player");
            root.SelectionProbe.SelectCharacters(new[] { root.DemoPresenters[0] });
            ui.RefreshNow();
            Submit(ButtonWithText(ui, "Captain = selected"));
            Assert.That(root.MilitaryUnits.Get(unit.Id).CaptainId,
                Is.EqualTo(root.DemoPresenters[0].CharacterId));

            var monarchId = root.Dynasty.CurrentMonarchId.Value;
            Assert.That(root.CharacterPresentations.TryGet(monarchId, out var monarch), Is.True);
            root.SelectionProbe.SelectCharacters(new[] { monarch });
            ui.RefreshNow();
            Submit(ButtonWithText(ui, "Enter TPS"));
            Assert.That(root.PlayerControl.State.Mode, Is.EqualTo(PlayerControlMode.ThirdPerson));
            root.ThirdPersonControl.Exit();
            Assert.That(root.PlayerControl.State.Mode, Is.EqualTo(PlayerControlMode.Rts));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerGlobalPanelOpensMapIssuesTravelAndReturnsToLocation()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var ui = root.GameplayUi;
            root.SelectionProbe.SelectCharacters(root.DemoPresenters);
            ui.SetPanel(GameplayPanel.Military);
            Submit(ButtonWithText(ui, "Create player unit from selected"));
            Submit(ButtonWithText(ui, "Aggregate"));
            var unit = root.MilitaryUnits.Units.Single(x => x.OwnerSideId.Value == "player");
            Assert.That(unit.Representation, Is.EqualTo(MilitaryUnitRepresentationMode.Aggregate));

            ui.SetPanel(GameplayPanel.Global);
            Submit(ButtonWithText(ui, "Open Global Map"));
            for (var i = 0; i < 20 && !root.GlobalMapBridge.IsGlobalSceneLoaded; i++) yield return null;
            Assert.That(root.GlobalMap.Mode, Is.EqualTo(Game.Domain.World.GameWorldMode.Global));
            Assert.That(root.GlobalMapBridge.IsGlobalSceneLoaded, Is.True);

            ui.RefreshNow();
            var active = root.GlobalMap.ActiveLocationId;
            var route = root.GlobalMap.GetRoutes(active).First();
            var destination = route.From == active ? route.To : route.From;
            Submit(ui.RootVisual.Q("main-panel").Query<Button>().ToList()
                .First(x => x.text.StartsWith(RussianTextCatalog.Content("location", destination.Value))));
            Submit(ui.RootVisual.Q("main-panel").Query<Button>().ToList()
                .First(x => x.text.Contains("Отряд игрока")));
            Submit(ButtonWithText(ui, "Travel to selected"));
            Assert.That(root.GlobalMap.Travels.Any(x => x.UnitId == unit.Id && x.Destination == destination), Is.True);

            var arrivalTick = root.GlobalMap.Travels.Single(x => x.UnitId == unit.Id).ArrivalTick;
            var realTicks = (arrivalTick - root.Clock.State.CalendarTicks) / (int)root.Clock.Speed;
            root.AiRuntime.AdvanceForDebug(TimeSpan.FromTicks(realTicks));
            ui.RefreshNow();
            Assert.That(root.GlobalMap.Travels.Any(x => x.UnitId == unit.Id), Is.False);
            Assert.That(root.MilitaryUnits.Get(unit.Id).AbstractLocationId, Is.EqualTo(destination.ToString()));
            Submit(ui.RootVisual.Q("main-panel").Query<Button>().ToList()
                .First(x => x.text.Contains(RussianTextCatalog.Content("location", active.Value))));
            Submit(ButtonWithText(ui, "Enter selected location"));
            for (var i = 0; i < 20 && root.GlobalMapBridge.IsGlobalSceneLoaded; i++) yield return null;
            Assert.That(root.GlobalMap.Mode, Is.EqualTo(Game.Domain.World.GameWorldMode.Local));
            Assert.That(root.GlobalMap.ActiveLocationId, Is.EqualTo(active));
        }

        private static Button ButtonWithText(GameplayUiController ui, string text) =>
            ui.RootVisual.Query<Button>().ToList().First(x => x.text == TranslateButton(text));

        private static string TranslateButton(string text)
        {
            switch (text)
            {
                case "Assign selected": return RussianTextCatalog.Get("ui.research.assign");
                case "Create from selected characters": return RussianTextCatalog.Get("ui.work.create");
                case "Rename": return RussianTextCatalog.Get("ui.work.rename");
                case "Move to center": return RussianTextCatalog.Get("ui.work.move_center");
                case "Cancel open jobs": return RussianTextCatalog.Get("ui.work.cancel_jobs");
                case "Haul available": return RussianTextCatalog.Get("ui.work.haul");
                case "Create player unit from selected": return RussianTextCatalog.Get("ui.military.create");
                case "Captain = selected": return RussianTextCatalog.Get("ui.military.captain_selected");
                case "Enter TPS": return RussianTextCatalog.Get("ui.character.enter_tps");
                case "Aggregate": return RussianTextCatalog.Get("ui.military.aggregate");
                case "Open Global Map": return RussianTextCatalog.Get("ui.map.open");
                case "Travel to selected": return RussianTextCatalog.Get("ui.map.travel");
                case "Enter selected location": return RussianTextCatalog.Get("ui.map.enter_location");
                default: return text;
            }
        }

        private static void Submit(Button button)
        {
            Assert.That(button, Is.Not.Null);
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(invoke, Is.Not.Null, "UI Toolkit Clickable.Invoke contract changed.");
            invoke.Invoke(button.clickable, new object[] { null });
        }
    }
}
