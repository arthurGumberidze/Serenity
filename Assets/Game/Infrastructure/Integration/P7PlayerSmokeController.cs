using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Domain.Campaign;
using Game.Domain.Dynasties;
using Game.Domain.Military;
using Game.Domain.World;
using Game.Presentation.UI;
using UnityEngine;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible Player proof for the connected Stone Age campaign and symmetric occupation rules.</summary>
    public sealed class P7PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class SmokeResult
        {
            public bool passed;
            public long seed;
            public bool stone_age_services_ready;
            public bool defended_requires_battle;
            public bool occupation_progress_visible;
            public bool mid_occupation_save_load;
            public bool defender_interrupts;
            public bool victory_once_and_continues;
            public bool defeat_reached;
            public bool pre_defeat_load_recovers;
            public string defended_screenshot;
            public string occupation_screenshot;
            public string interrupted_screenshot;
            public string victory_screenshot;
            public string defeat_screenshot;
            public string recovered_screenshot;
            public string error;
        }

        private LocalSceneCompositionRoot root;
        private string outputDirectory;
        private long seed;

        public static void LaunchFromMenuIfRequested(MainMenuController menu)
        {
            var arguments = Environment.GetCommandLineArgs();
            if (!arguments.Any(x => string.Equals(x, "-p7Smoke", StringComparison.OrdinalIgnoreCase))) return;
            if (!long.TryParse(ReadArgument(arguments, "-p7SmokeSeed"), out var requestedSeed) || requestedSeed <= 0)
                throw new InvalidOperationException("-p7SmokeSeed must be a positive whole number.");
            menu.StartNewGame(requestedSeed);
        }

        public static P7PlayerSmokeController AttachIfRequested(LocalSceneCompositionRoot compositionRoot)
        {
            var arguments = Environment.GetCommandLineArgs();
            if (!arguments.Any(x => string.Equals(x, "-p7Smoke", StringComparison.OrdinalIgnoreCase))) return null;
            var output = ReadArgument(arguments, "-p7SmokeOutput");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("-p7SmokeOutput is required.");
            var controller = compositionRoot.gameObject.AddComponent<P7PlayerSmokeController>();
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
            if (!TryRun(result, () =>
            {
                result.stone_age_services_ready = root.PlayerCharacterIds.Count >= 10 &&
                    root.Dynasty.CurrentMonarchId.HasValue && root.Research.CompletedTechnologies.Count > 0 &&
                    root.Buildings.All.Any(x => x.IsOperational) &&
                    root.Weather.GetWeatherAtCalendarTick(root.Clock.State.CalendarTicks).AbsoluteDay >= 0 &&
                    root.HealthFamily.Pregnancies != null;
                root.PlayerSaves.Save(1);

                CreatePlayerUnit("p7_defended_player", "enemy_settlement");
                CreateEnemyUnit("p7_defended_enemy", "enemy_settlement");
                root.GlobalMap.AdvanceTo(root.Clock.State.CalendarTicks);
                result.defended_requires_battle = root.Battles.Battles.Any(x => x.Status ==
                    Game.Domain.Combat.CombatEncounterStatus.Active) && root.GlobalMap.Occupations.Count == 0;
                root.GameplayUi.SetPanel(GameplayPanel.Military);
                root.GameplayUi.RefreshNow();
            })) { Finish(result); yield break; }
            yield return Frame();
            result.defended_screenshot = Capture("p7-defended-battle.png");

            if (!TryRun(result, () =>
            {
                root.PlayerSaves.Load(1);
                ReserveEnemyNonCombatants("p7_interrupted_reserve");
                CreatePlayerUnit("p7_interrupted_player", "enemy_settlement");
                root.GlobalMap.AdvanceTo(root.Clock.State.CalendarTicks);
                AdvanceSimulation(TimeSpan.FromMinutes(6));
                var mid = root.GlobalMap.Occupations.Single();
                root.GameplayUi.SetPanel(GameplayPanel.Global);
                root.GameplayUi.RefreshNow();
                result.occupation_progress_visible = mid.ProgressTicks == TimeSpan.FromHours(6).Ticks;
            })) { Finish(result); yield break; }
            yield return Frame();
            result.occupation_screenshot = Capture("p7-occupation-progress.png");

            if (!TryRun(result, () =>
            {
                var mid = root.GlobalMap.Occupations.Single();
                root.PlayerSaves.Save(2);
                AdvanceSimulation(TimeSpan.FromMinutes(1));
                root.PlayerSaves.Load(2);
                result.mid_occupation_save_load = root.GlobalMap.Occupations.Single().OccupationId == mid.OccupationId &&
                    root.GlobalMap.Occupations.Single().ProgressTicks == mid.ProgressTicks;
                CreateEnemyUnit("p7_interrupting_enemy", "enemy_settlement");
                root.GlobalMap.AdvanceTo(root.Clock.State.CalendarTicks);
                result.defender_interrupts = root.GlobalMap.Occupations.Count == 0 &&
                    root.GlobalMap.OccupationEvents.Last().InterruptionReason ==
                    SettlementOccupationInterruptionReason.DefendersArrived &&
                    root.Battles.Battles.Any(x => x.Status == Game.Domain.Combat.CombatEncounterStatus.Active);
                root.GameplayUi.SetPanel(GameplayPanel.Military);
                root.GameplayUi.RefreshNow();
            })) { Finish(result); yield break; }
            yield return Frame();
            result.interrupted_screenshot = Capture("p7-occupation-interrupted.png");

            if (!TryRun(result, () =>
            {
                root.PlayerSaves.Load(1);
                ReserveEnemyNonCombatants("p7_victory_reserve");
                CreatePlayerUnit("p7_victory_player", "enemy_settlement");
                root.GlobalMap.AdvanceTo(root.Clock.State.CalendarTicks);
                var victoryOccupation = root.GlobalMap.Occupations.Single();
                AdvanceSimulation(TimeSpan.FromMinutes(24));
                root.CampaignProgress.AdvanceTo(root.Clock.State.CalendarTicks);
                var sequence = root.CampaignProgress.State.EventSequence;
                AdvanceSimulation(TimeSpan.FromSeconds(1));
                root.CampaignProgress.AdvanceTo(root.Clock.State.CalendarTicks);
                result.victory_once_and_continues = root.CampaignProgress.State.MilitaryVictory &&
                    root.CampaignProgress.State.SourceOccupationId == victoryOccupation.OccupationId &&
                    root.CampaignProgress.State.SourceBattleId == null &&
                    root.CampaignProgress.State.EventSequence == sequence &&
                    root.GlobalMap.OccupationEvents.Count(x => x.Kind == SettlementOccupationEventKind.Completed) == 1 &&
                    root.GameRun.Status == GameRunStatus.Running;
                root.GameplayUi.SetPanel(GameplayPanel.Global);
                root.GameplayUi.RefreshNow();
            })) { Finish(result); yield break; }
            yield return Frame();
            result.victory_screenshot = Capture("p7-victory-continues.png");

            if (!TryRun(result, () =>
            {
                root.PlayerSaves.Load(1);
                CreateEnemyUnit("p7_defeat_enemy", "starter_settlement");
                root.GlobalMap.AdvanceTo(root.Clock.State.CalendarTicks);
                AdvanceSimulation(TimeSpan.FromMinutes(24));
                root.CampaignProgress.AdvanceTo(root.Clock.State.CalendarTicks);
                result.defeat_reached = root.GameRun.Status == GameRunStatus.GameOver &&
                    root.GameRun.GameOverReason == GameOverReason.NoControlledSettlement;
                root.GameplayUi.RefreshNow();
            })) { Finish(result); yield break; }
            yield return Frame();
            result.defeat_screenshot = Capture("p7-defeat.png");

            if (!TryRun(result, () =>
            {
                root.PlayerSaves.Load(1);
                root.GameplayUi.SetPanel(GameplayPanel.Dynasty);
                root.GameplayUi.RefreshNow();
                result.pre_defeat_load_recovers = root.GameRun.Status == GameRunStatus.Running &&
                    root.GlobalMap.Settlements.Any(x => x.OwnerSideId.Value == "player");
            })) { Finish(result); yield break; }
            yield return Frame();
            result.recovered_screenshot = Capture("p7-recovered-after-load.png");
            Finish(result);
        }

        private static bool TryRun(SmokeResult result, Action action)
        {
            try { action(); return true; }
            catch (Exception exception) { result.error = exception.ToString(); return false; }
        }

        private MilitaryUnitState CreatePlayerUnit(string key, string location)
        {
            var member = root.PlayerCharacterIds[0];
            return root.MilitaryUnits.Create(new MilitaryUnitId(DeterministicStableId.Create(seed, key)),
                new MilitarySideId("player"), new[] { member }, member,
                MilitaryUnitRepresentationMode.Aggregate, location);
        }

        private MilitaryUnitState CreateEnemyUnit(string key, string location)
        {
            var member = root.StarterEnemyAi.Residents[0];
            return root.MilitaryUnits.Create(new MilitaryUnitId(DeterministicStableId.Create(seed, key)),
                new MilitarySideId("enemy"), new[] { member }, member,
                MilitaryUnitRepresentationMode.Aggregate, location);
        }

        private void ReserveEnemyNonCombatants(string key)
        {
            var roster = root.StarterEnemyAi.Residents.Skip(1).ToArray();
            root.MilitaryUnits.Create(new MilitaryUnitId(DeterministicStableId.Create(seed, key)),
                new MilitarySideId("enemy"), roster, roster[0],
                MilitaryUnitRepresentationMode.Aggregate, "local");
        }

        private void AdvanceSimulation(TimeSpan realDelta)
        {
            root.Clock.Resume();
            root.AiRuntime.AdvanceForDebug(realDelta);
            root.Clock.Pause();
        }

        private static IEnumerator Frame()
        {
            yield return new WaitForSecondsRealtime(0.6f);
            yield return new WaitForEndOfFrame();
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
            result.passed = string.IsNullOrEmpty(result.error) && result.stone_age_services_ready &&
                result.defended_requires_battle && result.occupation_progress_visible &&
                result.mid_occupation_save_load && result.defender_interrupts &&
                result.victory_once_and_continues && result.defeat_reached && result.pre_defeat_load_recovers;
            var path = Path.Combine(outputDirectory, "p7-player-smoke.json");
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
            Debug.Log("P7_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + path);
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
