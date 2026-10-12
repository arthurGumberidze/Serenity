using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Military;
using Game.Domain.Resources;
using Game.Domain.Research;
using Game.Domain.Time;
using Game.Infrastructure;
using Game.Presentation.UI;
using Game.Simulation.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    [Category("U31")]
    public sealed class U31IntegrationPlayModeTests
    {
        private string saveDirectory;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            saveDirectory = Path.Combine(Path.GetTempPath(), "serenity-u31-play-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", saveDirectory);
            Screen.SetResolution(1600, 900, false);
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            if (root != null) Object.Destroy(root.gameObject);
            yield return null;
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", null);
            if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
        }

        [UnityTest]
        public IEnumerator PublicUiNewGameAndSaveLoadKeepOneAuthorityGraph()
        {
            var root = Root();
            root.GameplayUi.SetPanel(GameplayPanel.SaveLoad);
            root.GameplayUi.RefreshNow();
            var seed = root.GameplayUi.RootVisual.Q<TextField>("new-game-seed");
            seed.value = "310031";
            Submit(root.GameplayUi.RootVisual.Q<Button>("new-game-button"));
            Assert.That(root.GameplayUi.RootVisual.Q<Button>("confirm-new-game"), Is.Not.Null);
            Submit(root.GameplayUi.RootVisual.Q<Button>("confirm-new-game"));
            yield return null;
            yield return null;

            root = Root();
            Assert.That(root.ActiveNewGameConfiguration.WorldSeed, Is.EqualTo(310031));
            Assert.That(root.PlayerCharacterIds.Count, Is.EqualTo(10));
            Assert.That(root.StarterEnemyAi.Residents.Count, Is.EqualTo(6));
            Assert.That(root.Dynasty.CurrentMonarchId, Is.EqualTo(root.NewGame.MonarchId));
            Assert.That(root.Succession.Family.GetOrderedSuccessionCandidates(root.Dynasty,
                root.NewGame.MonarchId).Take(2).Select(x => x.Id),
                Is.EqualTo(new[] { root.NewGame.FirstHeirId, root.NewGame.SecondHeirId }));
            Assert.That(Object.FindObjectsByType<LocalSceneCompositionRoot>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<GameplayUiController>(FindObjectsInactive.Include).Length, Is.EqualTo(1));
            Assert.That(root.GameplayUi.SubscriptionCount, Is.EqualTo(4));
            Assert.That(root.CampaignProgress.State.RivalDiscovered, Is.False);
            Assert.That(root.CampaignProgress.CanRevealSettlement(root.CampaignProgress.State.RivalHomeSettlementId),
                Is.False);

            root.GameplayUi.SetPanel(GameplayPanel.SaveLoad);
            root.GameplayUi.SaveSlot(1);
            root.GameplayUi.RequestLoadSlot(1);
            root.GameplayUi.ConfirmLoadSlot(1);
            Assert.That(root.PlayerSaves.ListSlots().Single(x => x.Index == 1).Exists, Is.True);
            Assert.That(root.GameplayUi.SubscriptionCount, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator ConnectedPlayerJourneyUsesUiCommandsAndCanonicalServices()
        {
            var root = Root();
            var ui = root.GameplayUi;
            var berries = new ResourceId("berries");
            var producedBefore = root.Resources.All.Sum(definition => root.Production.GetProducedTotal(definition.Id));
            var aiBuildingsBefore = root.Buildings.All.Count(x => x.OwnerSideId == root.StarterEnemyAi.Strategic.SideId);

            ui.SetPanel(GameplayPanel.Economy);
            ui.RefreshNow();
            for (var i = 0; i < 20 && root.Production.GetTarget(berries) <=
                 root.Production.GetCanonicalTotal(berries); i++)
            {
                Submit(RowButton(ui, "Berries", "+5"));
                ui.RefreshNow();
            }
            Assert.That(root.Production.GetTarget(berries),
                Is.GreaterThan(root.Production.GetCanonicalTotal(berries)));
            root.SelectionProbe.SelectCharacters(new[] { root.DemoPresenters[0] });
            ui.SetPanel(GameplayPanel.Work);
            Submit(ButtonWithText(ui, "Create from selected characters"));
            Assert.That(root.Work.Groups.Count, Is.EqualTo(1));

            ui.SetPanel(GameplayPanel.Research);
            Submit(ButtonWithText(ui, "Assign selected"));
            Submit(ButtonWithText(ui, "Start Shelter Construction"));
            Submit(ui.RootVisual.Q<Button>("speed-10"));
            Assert.That(root.Clock.Speed, Is.EqualTo(GameSpeed.Maximum));

            var productionWorker = root.FemaleDemoCharacter.Id == root.DemoPresenters[0].CharacterId
                ? root.MaleDemoCharacter.Id
                : root.FemaleDemoCharacter.Id;
            root.Tiers.Transition(productionWorker, CharacterSimulationTier.Tier3);
            Assert.That(root.Production.TryReserve(productionWorker, default, out var productionJob), Is.True,
                "A canonical aggregate worker must accept the UI-requested production target.");
            var productionCompletion = root.Production.AdvanceTask(productionJob.Task.Id, TimeSpan.FromDays(1));
            Assert.That(productionCompletion.Completed, Is.True);
            Assert.That(productionCompletion.ProducedOutput, Is.True);

            for (var i = 0; i < 40 && (!root.Research.IsCompleted(new TechnologyId("shelter_construction")) ||
                 root.Resources.All.Sum(definition => root.Production.GetProducedTotal(definition.Id)) <= producedBefore ||
                 root.Buildings.All.Count(x => x.OwnerSideId == root.StarterEnemyAi.Strategic.SideId) <= aiBuildingsBefore); i++)
                AdvanceSixGameHours(root);

            Assert.That(root.Research.IsCompleted(new TechnologyId("shelter_construction")), Is.True);
            Assert.That(root.Resources.All.Sum(definition => root.Production.GetProducedTotal(definition.Id)),
                Is.GreaterThan(producedBefore));
            Assert.That(root.Buildings.All.Count(x => x.OwnerSideId == root.StarterEnemyAi.Strategic.SideId),
                Is.GreaterThan(aiBuildingsBefore), "The AI must fund and construct through its scoped economy.");
            Assert.That(root.StarterEnemyAi.Inventories, Is.Not.SameAs(root.Inventories));

            ui.SetPanel(GameplayPanel.Build);
            Submit(ButtonStartingWith(ui, "Primitive Shelter"));
            Assert.That(root.BuildingPlacementController.TryMovePreviewToWorldPoint(new Vector3(30, 0, 30)), Is.True);
            var shelter = root.BuildingPlacementController.ConfirmPlacement();
            Assert.That(shelter, Is.Not.Null);
            Assert.That(shelter.DefinitionId, Is.EqualTo(new BuildingDefinitionId("primitive_shelter")));

            for (var i = 0; i < 4; i++) AdvanceSixGameHours(root);
            var playerPresenters = root.PlayerCharacterIds.Select(id =>
            {
                root.CharacterPresentations.TryGet(id, out var presenter);
                return presenter;
            }).Where(x => x != null).ToArray();
            Assert.That(playerPresenters.Length, Is.GreaterThanOrEqualTo(2));
            root.SelectionProbe.SelectCharacters(playerPresenters);
            ui.SetPanel(GameplayPanel.Military);
            Submit(ButtonWithText(ui, "Create player unit from selected"));
            var playerUnit = root.MilitaryUnits.Units.Single(x => x.OwnerSideId.Value == "player");
            Assert.That(playerUnit.Supply.Initialized, Is.True);
            Assert.That(playerUnit.Supply.Get(berries), Is.GreaterThan(0));
            Submit(ButtonWithText(ui, "Aggregate"));
            var supplyBeforeTravel = root.MilitaryUnits.Get(playerUnit.Id).Supply.Get(berries);

            var route = root.GlobalMap.GetRoutes(new Game.Domain.World.WorldLocationId(playerUnit.AbstractLocationId)).First();
            var destination = route.From.Value == playerUnit.AbstractLocationId ? route.To : route.From;
            var travel = root.GlobalMap.IssueTravelOrder(playerUnit.Id, destination, root.Clock.State.CalendarTicks);
            Assert.That(travel.Issued, Is.True);
            Assert.That(root.MilitaryUnits.Get(playerUnit.Id).Supply.Get(berries), Is.LessThan(supplyBeforeTravel));
            Assert.That(root.GlobalMap.IssueTravelOrder(playerUnit.Id, destination, root.Clock.State.CalendarTicks).Issued,
                Is.False, "A repeated command must not charge travel supply twice.");
            AdvanceTo(root, travel.Travel.ArrivalTick);
            Assert.That(root.MilitaryUnits.Get(playerUnit.Id).AbstractLocationId, Is.EqualTo(destination.Value));

            root.MilitaryUnits.SetAbstractLocation(playerUnit.Id, "local");
            var battle = root.StartDemoUnitBattle(BattleMode.Abstract);
            root.MilitaryUnits.SetAbstractLocation(battle.SideAUnitId.Value, "enemy_settlement");
            root.MilitaryUnits.SetAbstractLocation(battle.SideBUnitId.Value, "enemy_settlement");
            for (var i = 0; i < 40 && root.Combat.Encounters.Single(x => x.BattleId == battle.BattleId).Status ==
                 CombatEncounterStatus.Active; i++)
                AdvanceSixGameHours(root);
            Assert.That(root.Combat.Encounters.Single(x => x.BattleId == battle.BattleId).Outcome,
                Is.EqualTo(CombatOutcome.SideAVictory));
            Assert.That(root.CampaignProgress.State.RivalDiscovered, Is.True);
            Assert.That(root.CampaignProgress.State.MilitaryVictory, Is.True);
            Assert.That(root.StarterEnemyAi.Strategic.IsDefeated, Is.True);

            var victoryTick = root.CampaignProgress.State.VictoryTick;
            var eventSequence = root.CampaignProgress.State.EventSequence;
            var beforeContinuation = root.Clock.State.CalendarTicks;
            AdvanceSixGameHours(root);
            Assert.That(root.Clock.State.CalendarTicks, Is.GreaterThan(beforeContinuation));
            Assert.That(root.CampaignProgress.State.VictoryTick, Is.EqualTo(victoryTick));
            Assert.That(root.CampaignProgress.State.EventSequence, Is.EqualTo(eventSequence));
            ui.RefreshNow();
            Assert.That(AllUiText(ui), Does.Contain("Military victory"));

            ui.SetPanel(GameplayPanel.SaveLoad);
            ui.SaveSlot(1);
            ui.RequestLoadSlot(1);
            ui.ConfirmLoadSlot(1);
            Assert.That(root.CampaignProgress.State.MilitaryVictory, Is.True);
            Assert.That(root.CampaignProgress.State.RivalDiscovered, Is.True);
            Assert.That(root.CampaignProgress.State.EventSequence, Is.EqualTo(eventSequence));
            Assert.That(root.GameplayUi.SubscriptionCount, Is.EqualTo(4));
            yield return null;
        }

        private static void AdvanceSixGameHours(LocalSceneCompositionRoot root) =>
            root.AiRuntime.AdvanceForDebug(TimeSpan.FromSeconds(36));

        private static void AdvanceTo(LocalSceneCompositionRoot root, long targetCalendarTick)
        {
            var remaining = targetCalendarTick - root.Clock.State.CalendarTicks;
            if (remaining <= 0) return;
            var realTicks = remaining / ((int)root.Clock.Speed * 60L);
            root.AiRuntime.AdvanceForDebug(TimeSpan.FromTicks(Math.Max(1, realTicks)));
        }

        private static LocalSceneCompositionRoot Root()
        {
            var value = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(value, Is.Not.Null);
            return value;
        }

        private static Button ButtonWithText(GameplayUiController ui, string text) =>
            ui.RootVisual.Query<Button>().ToList().First(x => x.text == text);

        private static Button ButtonStartingWith(GameplayUiController ui, string text) =>
            ui.RootVisual.Query<Button>().ToList().First(x => x.text.StartsWith(text, StringComparison.Ordinal));

        private static Button RowButton(GameplayUiController ui, string label, string button)
        {
            foreach (var element in ui.RootVisual.Q("main-panel").Children())
                if (element.Query<Label>().ToList().Any(x => x.text.Contains(label)))
                    return element.Query<Button>().ToList().First(x => x.text == button);
            throw new AssertionException("UI row was not found: " + label);
        }

        private static string AllUiText(GameplayUiController ui) =>
            string.Join("\n", ui.RootVisual.Query<Label>().ToList().Select(x => x.text));

        private static void Submit(Button button)
        {
            Assert.That(button, Is.Not.Null);
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(invoke, Is.Not.Null);
            invoke.Invoke(button.clickable, new object[] { null });
        }
    }
}
