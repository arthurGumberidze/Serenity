using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Campaign;
using Game.Domain.Military;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Domain.World;
using Game.Presentation.UI;
using Game.Simulation.Time;
using UnityEngine;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible Player smoke for the P6 first-hour route over canonical simulation services.</summary>
    public sealed class P6PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class SmokeResult
        {
            public bool passed;
            public long seed;
            public bool onboarding_visible;
            public bool inspected_group;
            public bool work_assigned;
            public bool shelter_storage_explained;
            public bool food_produced;
            public bool research_started;
            public bool event_resolved;
            public bool scouting_started;
            public bool route_complete;
            public bool save_load_identity_preserved;
            public string ruler_stable_id;
            public string source_stable_id;
            public string start_screenshot;
            public string settlement_screenshot;
            public string event_screenshot;
            public string scouting_screenshot;
            public string after_load_screenshot;
            public string error;
        }

        private LocalSceneCompositionRoot root;
        private string outputDirectory;
        private long seed;

        public static void LaunchFromMenuIfRequested(MainMenuController menu)
        {
            var arguments = Environment.GetCommandLineArgs();
            if (!arguments.Any(x => string.Equals(x, "-p6Smoke", StringComparison.OrdinalIgnoreCase))) return;
            if (!long.TryParse(ReadArgument(arguments, "-p6SmokeSeed"), out var requestedSeed) || requestedSeed <= 0)
                throw new InvalidOperationException("-p6SmokeSeed must be a positive whole number.");
            menu.StartNewGame(requestedSeed);
        }

        public static P6PlayerSmokeController AttachIfRequested(LocalSceneCompositionRoot compositionRoot)
        {
            var arguments = Environment.GetCommandLineArgs();
            if (!arguments.Any(x => string.Equals(x, "-p6Smoke", StringComparison.OrdinalIgnoreCase))) return null;
            var output = ReadArgument(arguments, "-p6SmokeOutput");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("-p6SmokeOutput is required.");
            var controller = compositionRoot.gameObject.AddComponent<P6PlayerSmokeController>();
            controller.root = compositionRoot;
            controller.outputDirectory = output;
            controller.seed = compositionRoot.ActiveNewGameConfiguration.WorldSeed;
            controller.StartCoroutine(controller.Run());
            return controller;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(outputDirectory);
            var result = new SmokeResult { seed = seed };
            root.Clock.Pause();
            yield return new WaitForSecondsRealtime(1f);
            yield return new WaitForEndOfFrame();
            StableEntityId sourceId = default;
            MilitaryUnitId unitId = default;
            WorldLocationId destination = default;
            try
            {
                result.onboarding_visible = root.GameplayUi.OnboardingVisible &&
                    root.GameplayUi.OnboardingStage == FirstHourStage.InspectGroup;
                result.start_screenshot = Capture("p6-onboarding-start.png");

                root.SelectionProbe.SelectCharacters(new[] { root.DemoPresenters[0] });
                root.GameplayUi.RefreshNow();
                result.inspected_group = root.GameplayUi.OnboardingStage == FirstHourStage.AssignWork;
                var group = root.Work.CreateGroup("Первая рабочая группа", new[] { root.MaleDemoCharacter.Id });
                root.ManualMoveCommands.IssueForGroup(group.Id, new WorldPosition(0f, 0f, 10f), WorkPriority.Normal);
                root.GameplayUi.RefreshNow();
                result.work_assigned = root.Work.Jobs.Any(x => x.GroupId == group.Id);

                root.GameplayUi.SetPanel(GameplayPanel.Economy);
                result.shelter_storage_explained = root.GameplayUi.OnboardingStage == FirstHourStage.SecureFood;
                var berries = new ResourceId("berries");
                var berryAnchor = root.InhabitableRegion.ResourceAnchors.First(x => x.RecipeId == "gather_berries");
                sourceId = DeterministicStableId.Create(seed, "player_source", berryAnchor.StableIndex);
                var site = root.Production.Sites.Single(x => x.Id == sourceId);
                root.Production.SetTarget(berries, root.Production.GetCanonicalTotal(berries) + 20);
                if (!root.Production.TryReserve(root.PlayerCharacterIds[2], site.Position, out var production))
                    throw new InvalidOperationException("P6 food production could not be reserved.");
                var completion = root.Production.AdvanceTask(production.Task.Id, TimeSpan.FromDays(30));
                result.food_produced = completion.Completed && completion.ProducedOutput &&
                    root.Production.GetProducedTotal(berries) > 0;
                root.GameplayUi.RefreshNow();
                result.ruler_stable_id = root.MaleDemoCharacter.Id.ToString();
                result.source_stable_id = sourceId.ToString();
            }
            catch (Exception exception) { result.error = exception.ToString(); }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            yield return new WaitForSecondsRealtime(0.7f);
            yield return new WaitForEndOfFrame();
            result.settlement_screenshot = Capture("p6-settlement-food.png");
            try
            {
                root.Research.TryAssignWorker(root.MaleDemoCharacter.Id);
                var technology = root.Research.AvailableTechnologies.First();
                if (!root.Research.Start(technology.Id)) throw new InvalidOperationException("P6 research did not start.");
                root.GameplayUi.RefreshNow();
                result.research_started = root.Research.ActiveProject != null &&
                    root.GameplayUi.OnboardingStage == FirstHourStage.ResolveEventOrDanger;
                root.Clock.SetSpeed(GameSpeed.Normal);
                root.Clock.Resume();
                root.AiRuntime.AdvanceForDebug(TimeSpan.FromMinutes(48));
                root.Clock.Pause();
                for (var guard = 0; guard < 4 && root.Events.History.Count == 0; guard++)
                {
                    var pending = root.Events.PendingPlayerEvents.First();
                    var definition = root.Events.Catalog.Get(pending.DefinitionId);
                    var choice = definition.Nodes.Single(x => x.Id == pending.CurrentNodeId).Choices[0];
                    if (!root.Events.ResolvePlayerChoice(pending.InstanceId, choice.Id, root.Clock.State.CalendarTicks).Applied)
                        throw new InvalidOperationException("P6 event choice was rejected.");
                }
                root.GameplayUi.SetPanel(GameplayPanel.Events);
                result.event_resolved = root.Events.History.Count > 0 &&
                    root.GameplayUi.OnboardingStage == FirstHourStage.BeginScouting;
            }
            catch (Exception exception) { result.error = exception.ToString(); }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            yield return new WaitForSecondsRealtime(0.7f);
            yield return new WaitForEndOfFrame();
            result.event_screenshot = Capture("p6-event-result.png");
            try
            {
                var roster = root.DemoPresenters.Select(x => x.CharacterId).ToArray();
                var unit = root.MilitaryUnits.Create(new MilitarySideId("player"), roster, root.MaleDemoCharacter.Id);
                unitId = unit.Id;
                root.MilitaryUnits.SetAbstractLocation(unit.Id, root.GlobalMap.ActiveLocationId.Value);
                root.MilitaryUnits.LoadInitialSupply(unit.Id, root.Inventories, root.Inventories.All.Select(x => x.Owner));
                root.MilitaryRepresentations.Transition(unit.Id, MilitaryUnitRepresentationMode.Aggregate);
                root.GlobalMapBridge.RequestEnterGlobal();
            }
            catch (Exception exception) { result.error = exception.ToString(); }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            var deadline = Time.realtimeSinceStartup + 8f;
            while (!root.GlobalMapBridge.IsGlobalSceneLoaded && Time.realtimeSinceStartup < deadline) yield return null;
            try
            {
                if (!root.GlobalMapBridge.IsGlobalSceneLoaded) throw new InvalidOperationException("P6 global map did not load.");
                var active = root.GlobalMap.ActiveLocationId;
                var route = root.GlobalMap.GetRoutes(active).First();
                destination = route.From == active ? route.To : route.From;
                var travel = root.GlobalMap.IssueTravelOrder(unitId, destination, root.Clock.State.CalendarTicks);
                if (!travel.Issued) throw new InvalidOperationException("P6 scouting travel was rejected: " + travel.Status);
                root.GameplayUi.SetPanel(GameplayPanel.Global);
                root.GameplayUi.RefreshNow();
                result.scouting_started = root.GlobalMap.Travels.Any(x => x.UnitId == unitId);
                result.route_complete = root.GameplayUi.OnboardingStage == FirstHourStage.Complete;
            }
            catch (Exception exception) { result.error = exception.ToString(); }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            yield return new WaitForSecondsRealtime(0.7f);
            yield return new WaitForEndOfFrame();
            result.scouting_screenshot = Capture("p6-scouting-map.png");
            try
            {
                var profile = root.Appearances.Get(root.MaleDemoCharacter.Id).ProfileId;
                var remaining = root.Production.Sites.Single(x => x.Id == sourceId).RemainingAmount;
                root.GameplayUi.SaveSlot(1);
                root.MaleDemoCharacter.Rename(new Game.Domain.Characters.CharacterName("Временный"));
                root.GameplayUi.ConfirmLoadSlot(1);
                result.save_load_identity_preserved = root.MaleDemoCharacter.Id.ToString() == result.ruler_stable_id &&
                    root.Appearances.Get(root.MaleDemoCharacter.Id).ProfileId == profile &&
                    root.Production.Sites.Single(x => x.Id == sourceId).RemainingAmount == remaining &&
                    root.GlobalMap.Travels.Any(x => x.UnitId == unitId && x.Destination == destination);
                root.GameplayUi.RefreshNow();
            }
            catch (Exception exception) { result.error = exception.ToString(); }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            yield return new WaitForSecondsRealtime(0.7f);
            yield return new WaitForEndOfFrame();
            result.after_load_screenshot = Capture("p6-after-load.png");
            Finish(result);
        }

        private string Capture(string filename)
        {
            var path = Path.Combine(outputDirectory, filename);
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Destroy(texture);
            return path;
        }

        private void Finish(SmokeResult result)
        {
            result.passed = string.IsNullOrEmpty(result.error) && result.onboarding_visible &&
                result.inspected_group && result.work_assigned && result.shelter_storage_explained &&
                result.food_produced && result.research_started && result.event_resolved &&
                result.scouting_started && result.route_complete && result.save_load_identity_preserved;
            var path = Path.Combine(outputDirectory, "p6-player-smoke.json");
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
            Debug.Log("P6_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + path);
            Application.Quit(result.passed ? 0 : 2);
        }

        private static string ReadArgument(string[] arguments, string name)
        {
            for (var i = 0; i < arguments.Length - 1; i++)
                if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase)) return arguments[i + 1];
            return null;
        }
    }
}
