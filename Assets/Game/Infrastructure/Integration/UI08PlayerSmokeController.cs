using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Domain.Campaign;
using Game.Domain.Characters;
using Game.Domain.Work;
using Game.Domain.Dynasties;
using Game.Domain.Military;
using Game.Domain.World;
using Game.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace Game.Infrastructure.Integration
{
    /// <summary>Exact-binary release qualification for the accepted UI01-UI07 player flow.</summary>
    public sealed class UI08PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public bool passed;
            public bool normal_menu_new_game;
            public bool ruler_and_settings;
            public bool hud_and_selection;
            public bool management_routes;
            public bool people_dynasty_events;
            public bool military_and_tps;
            public bool realm_and_local_return;
            public bool save_load_stable_ids;
            public bool pause_and_focus;
            public bool victory_occupation_started;
            public bool victory_occupation_completed;
            public bool victory_continues;
            public bool defeat_recovery_actions;
            public bool no_debug_shortcuts;
            public bool resolution_1920x1080;
            public bool resolution_1280x720_125;
            public bool performance_measured;
            public float ui_refresh_average_ms;
            public float ui_refresh_max_ms;
            public float frame_average_ms;
            public float frame_p95_ms;
            public string menu_capture;
            public string management_capture;
            public string research_capture;
            public bool research_fullscreen_fits;
            public float research_viewport_height_px;
            public string people_capture;
            public string realm_capture;
            public string tps_capture;
            public string victory_capture;
            public string defeat_capture;
            public string error;
        }

        private MainMenuController menu;
        private string output;

        public static void LaunchFromMenuIfRequested(MainMenuController source)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(value => string.Equals(value, "-ui08Smoke", StringComparison.OrdinalIgnoreCase))) return;
            var target = Arg(args, "-ui08SmokeOutput");
            if (string.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("-ui08SmokeOutput is required.");
            var host = new GameObject("UI08 Player Smoke");
            DontDestroyOnLoad(host);
            var controller = host.AddComponent<UI08PlayerSmokeController>();
            controller.menu = source;
            controller.output = Path.GetFullPath(target);
            Application.runInBackground = true;
            controller.StartCoroutine(controller.Run());
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(output);
            var result = new Result();
            var route = RunCore(result);
            while (true)
            {
                object current;
                try
                {
                    if (!route.MoveNext()) break;
                    current = route.Current;
                }
                catch (Exception exception)
                {
                    result.error = exception.ToString();
                    break;
                }
                yield return current;
            }

            result.passed = string.IsNullOrEmpty(result.error) && result.normal_menu_new_game &&
                result.ruler_and_settings && result.hud_and_selection && result.management_routes &&
                result.people_dynasty_events && result.military_and_tps && result.realm_and_local_return &&
                result.save_load_stable_ids && result.pause_and_focus && result.victory_continues &&
                result.defeat_recovery_actions && result.no_debug_shortcuts && result.resolution_1920x1080 &&
                result.resolution_1280x720_125 && result.performance_measured &&
                result.research_fullscreen_fits &&
                Existing(result.menu_capture, result.management_capture, result.research_capture, result.people_capture,
                    result.realm_capture, result.tps_capture, result.victory_capture, result.defeat_capture);
            var report = Path.Combine(output, "ui08-player-smoke.json");
            File.WriteAllText(report, JsonUtility.ToJson(result, true));
            Debug.Log("UI08_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + report);
            Application.Quit(result.passed ? 0 : 2);
        }

        private IEnumerator RunCore(Result result)
        {
            yield return SetResolution(1920, 1080);
            menu.SetUiScale(1f);
            menu.ShowMain();
            yield return null; yield return null;
            result.resolution_1920x1080 = Screen.width == 1920 && Screen.height == 1080;
            result.normal_menu_new_game = new[] { "continue-button", "new-game-button", "load-button", "settings-button", "exit-button" }
                .All(name => menu.RootVisual.Q<Button>(name) != null);
            result.menu_capture = Path.Combine(output, "ui08-menu-1920x1080.png");
            yield return Capture(result.menu_capture);

            yield return SetResolution(1280, 720);
            menu.SetUiScale(1.25f);
            menu.OpenSettings();
            yield return null; yield return null;
            var settings = menu.RootVisual.Q<DropdownField>("settings-resolution");
            var settingsFocus = menu.RootVisual.panel.focusController.focusedElement == settings;
            menu.OpenNewGame();
            yield return null; yield return null;
            var rulerName = menu.RootVisual.Q<TextField>("ruler-name");
            result.ruler_and_settings = settings != null && settingsFocus && rulerName != null &&
                                        menu.RootVisual.panel.focusController.focusedElement == rulerName;
            result.resolution_1280x720_125 = Screen.width == 1280 && Screen.height == 720 &&
                                             Mathf.Abs(menu.UiScale - 1.25f) < .01f;
            menu.RootVisual.Q<TextField>("world-seed").value = "20261031";
            rulerName.value = "Арина";
            menu.RootVisual.Q<TextField>("dynasty-name").value = "Дом Тихой Реки";
            if (!menu.TryStartEditedNewGame()) throw new InvalidOperationException("Ruler editor rejected a valid normal new game.");

            while (SceneManager.GetActiveScene().name != "LocalGameplay") yield return null;
            var deadline = Time.realtimeSinceStartup + 600f;
            LocalSceneCompositionRoot root = null;
            while (root == null && Time.realtimeSinceStartup < deadline)
            {
                root = FindAnyObjectByType<LocalSceneCompositionRoot>();
                if (root == null || root.GameplayUi == null) { root = null; yield return null; }
            }
            if (root == null) throw new TimeoutException("Local gameplay did not initialize within 600 seconds.");
            while (FindAnyObjectByType<WorldLoadingOverlay>() != null && Time.realtimeSinceStartup < deadline)
                yield return null;
            if(!root.IsInitialized)throw new TimeoutException("The canonical world preparation gate did not finish.");

            var ui = root.GameplayUi;
            ui.SetUiScale(1.25f);
            if (!root.Clock.IsPaused) ui.TogglePause();
            var monarchId = root.Dynasty.CurrentMonarchId.Value;
            root.SelectionProbe.SelectCharacterIds(new[] { monarchId });
            ui.RefreshNow();
            yield return null; yield return null;
            result.hud_and_selection = ui.RootVisual.Q("resource-strip") != null &&
                                       ui.RootVisual.Q("local-mini-map") != null &&
                                       ui.RootVisual.Q("selection-inspector") != null &&
                                       root.SelectionProbe.SelectedCharacterId == monarchId;
            result.no_debug_shortcuts = !ui.DeveloperUiVisible &&
                                        !Environment.GetCommandLineArgs().Any(value => value.IndexOf("debug", StringComparison.OrdinalIgnoreCase) >= 0);

            var refreshTimes = new List<float>();
            var workReady = false;
            var researchReady = false;
            var peopleReady = false;
            var dynastyReady = false;
            var attentionReady = false;
            var militaryReady = false;
            foreach (var routeId in new[] { GameplayUiRouteId.Settlement, GameplayUiRouteId.People,
                         GameplayUiRouteId.WorkGroups, GameplayUiRouteId.Construction, GameplayUiRouteId.Knowledge,
                         GameplayUiRouteId.Dynasty, GameplayUiRouteId.Attention, GameplayUiRouteId.Military })
            {
                var watch = Stopwatch.StartNew();
                ui.Navigate(routeId);
                ui.RefreshNow();
                watch.Stop();
                refreshTimes.Add((float)watch.Elapsed.TotalMilliseconds);
                yield return null;
                workReady |= routeId == GameplayUiRouteId.WorkGroups &&
                             ui.RootVisual.Q<Button>("work-create-profession-group") != null;
                researchReady |= routeId == GameplayUiRouteId.Knowledge &&
                                 ui.RootVisual.Q<Button>("research-category-survival") != null;
                peopleReady |= routeId == GameplayUiRouteId.People &&
                               ui.RootVisual.Q<ListView>("people-roster") != null;
                dynastyReady |= routeId == GameplayUiRouteId.Dynasty &&
                                ui.RootVisual.Q("dynasty-family-tree") != null;
                attentionReady |= routeId == GameplayUiRouteId.Attention &&
                                  ui.RootVisual.Q<Button>("attention-decisions") != null;
                militaryReady |= routeId == GameplayUiRouteId.Military &&
                                 ui.RootVisual.Q<ListView>("military-unit-roster") != null;
            }
            result.management_routes = workReady && researchReady &&
                                       GameplayUiRoutes.Ordered.All(route => ui.RootVisual.Q<Button>(route.StableElementName) != null);
            result.people_dynasty_events = peopleReady && dynastyReady && attentionReady;
            result.management_capture = Path.Combine(output, "ui08-management-1280x720-125.png");
            ui.Navigate(GameplayUiRouteId.Knowledge); ui.RefreshNow();
            yield return Capture(result.management_capture);
            yield return SetResolution(1920,1080);
            ui.SetUiScale(1f);ui.RefreshNow();
            for(var frame=0;frame<20;frame++)yield return null;
            var researchViewport=ui.RootVisual.Q("research-viewport").worldBound;
            var researchTree=ui.RootVisual.Q("research-dependency-tree").worldBound;
            result.research_viewport_height_px=researchViewport.height*Screen.height/ui.RootVisual.worldBound.height;
            result.research_fullscreen_fits=Screen.width==1920&&Screen.height==1080&&
                result.research_viewport_height_px>=600&&
                researchTree.xMin>=researchViewport.xMin-1&&researchTree.xMax<=researchViewport.xMax+1&&
                researchTree.yMin>=researchViewport.yMin-1&&researchTree.yMax<=researchViewport.yMax+1;
            result.research_capture=Path.Combine(output,"ui08-research-1920x1080.png");
            yield return Capture(result.research_capture);
            yield return SetResolution(1280,720);ui.SetUiScale(1.25f);ui.RefreshNow();
            result.people_capture = Path.Combine(output, "ui08-people-1280x720-125.png");
            ui.Navigate(GameplayUiRouteId.People); ui.RefreshNow();
            yield return Capture(result.people_capture);

            root.SelectionProbe.SelectCharacterIds(new[] { monarchId });
            var enter = root.ThirdPersonControl.TryEnter(monarchId);
            ui.RefreshNow();
            yield return null;
            result.military_and_tps = militaryReady && enter.IsEligible &&
                                      root.PlayerControl.State.Mode == Game.Domain.Control.PlayerControlMode.ThirdPerson &&
                                      ui.RootVisual.Q("tps-hud").resolvedStyle.display == DisplayStyle.Flex;
            result.tps_capture = Path.Combine(output, "ui08-tps-1280x720-125.png");
            yield return Capture(result.tps_capture);
            root.ThirdPersonControl.Exit();

            var wasPaused = root.Clock.IsPaused;
            ui.TogglePause();
            var toggled = root.Clock.IsPaused != wasPaused;
            ui.TogglePause();
            ui.SaveSlot(1);
            var session = root.Saves.SessionId;
            ui.RequestLoadSlot(1);
            yield return null; yield return null;
            var focusPresent = ui.RootVisual.panel.focusController.focusedElement != null;
            ui.ConfirmLoadSlot(1);
            result.pause_and_focus = toggled && root.Clock.IsPaused == wasPaused && focusPresent;
            result.save_load_stable_ids = root.PlayerSaves.ListSlots().Single(slot => slot.Index == 1).Exists &&
                                          root.Saves.SessionId == session && root.Dynasty.CurrentMonarchId == monarchId &&
                                          root.Characters.TryGet(monarchId, out _);

            root.ConnectedWorld.RevealAll();
            var unit = CreatePlayerUnit(root, "ui08_realm_player", root.GlobalMap.ActiveLocationId.Value);
            root.MilitaryUnits.SetAbstractLocation(unit.Id, root.GlobalMap.ActiveLocationId.Value);
            unit = root.MilitaryUnits.Get(unit.Id);
            if (unit.Representation != MilitaryUnitRepresentationMode.Aggregate)
                unit = root.MilitaryRepresentations.Transition(unit.Id, MilitaryUnitRepresentationMode.Aggregate);
            var origin = new WorldLocationId(unit.AbstractLocationId);
            var mapRoute = root.GlobalMap.GetRoutes(origin).First();
            var destination = mapRoute.From == origin ? mapRoute.To : mapRoute.From;
            ui.Navigate(GameplayUiRouteId.Realm);
            ui.SelectMapUnit(unit.Id);
            ui.SelectMapLocation(destination);
            ui.RefreshNow();
            yield return null; yield return null;
            var strategicView = root.GetComponent<Game.Presentation.World.P102StrategicWorldView>();
            result.realm_and_local_return = strategicView != null && strategicView.IsActive &&
                                            ui.RootVisual.Q("realm-workspace").resolvedStyle.display == DisplayStyle.Flex &&
                                            ui.RootVisual.Q("main-panel").resolvedStyle.display == DisplayStyle.None &&
                                            ui.RootVisual.Q("main-panel").resolvedStyle.visibility == Visibility.Hidden &&
                                            ui.RootVisual.Q("realm-bottom-backdrop") != null &&
                                            ui.RootVisual.Query<Button>("diplomacy-unavailable").ToList().All(button => !button.enabledSelf);
            result.realm_capture = Path.Combine(output, "ui08-realm-1280x720-125.png");
            yield return Capture(result.realm_capture);
            ui.SetPanel(GameplayPanel.Global);
            ui.RefreshNow();
            yield return null;
            result.realm_and_local_return &= root.GlobalMap.Mode == GameWorldMode.Local && !root.GlobalMapBridge.IsGlobalSceneLoaded;

            var frameTimes = new List<float>();
            for (var index = 0; index < 120; index++)
            {
                frameTimes.Add(Time.unscaledDeltaTime * 1000f);
                yield return null;
            }
            refreshTimes.Sort();
            frameTimes.Sort();
            result.ui_refresh_average_ms = refreshTimes.Average();
            result.ui_refresh_max_ms = refreshTimes.Last();
            result.frame_average_ms = frameTimes.Average();
            result.frame_p95_ms = frameTimes[Mathf.Min(frameTimes.Count - 1, Mathf.FloorToInt(frameTimes.Count * .95f))];
            result.performance_measured = refreshTimes.Count == 8 && frameTimes.Count == 120 &&
                                          float.IsFinite(result.ui_refresh_average_ms) && float.IsFinite(result.frame_p95_ms);

            root.PlayerSaves.Load(1);
            var runtimeWasEnabled = root.AiRuntime.enabled;
            root.AiRuntime.enabled = false;
            ReserveEnemyNonCombatants(root, "ui08_victory_reserve");
            CreatePlayerUnit(root, "ui08_victory_player", "enemy_settlement");
            root.GlobalMap.AdvanceTo(root.Clock.State.CalendarTicks);
            var victoryOccupation = root.GlobalMap.Occupations.Single();
            result.victory_occupation_started = victoryOccupation.ClaimantSideId.Value == "player";
            root.GlobalMap.AdvanceTo(victoryOccupation.CompletionTick);
            root.CampaignProgress.AdvanceTo(victoryOccupation.CompletionTick);
            ui.RefreshNow();
            yield return null; yield return null;
            result.victory_occupation_completed = root.GlobalMap.OccupationEvents.Any(value =>
                value.OccupationId == victoryOccupation.OccupationId && value.Kind == SettlementOccupationEventKind.Completed);
            result.victory_continues = result.victory_occupation_started && result.victory_occupation_completed &&
                                       root.CampaignProgress.State.MilitaryVictory &&
                                       root.GameRun.Status == GameRunStatus.Running &&
                                       ui.RootVisual.Q<Button>("victory-continue-button") != null;
            result.victory_capture = Path.Combine(output, "ui08-victory-1920x1080.png");
            yield return SetResolution(1920, 1080);
            ui.SetUiScale(1f); ui.RefreshNow();
            yield return Capture(result.victory_capture);

            root.PlayerSaves.Load(1);
            root.AiRuntime.enabled = runtimeWasEnabled;
            CreateEnemyUnit(root, "ui08_defeat_enemy", "starter_settlement");
            root.GlobalMap.AdvanceTo(root.Clock.State.CalendarTicks);
            AdvanceSimulation(root, TimeSpan.FromMinutes(24));
            root.CampaignProgress.AdvanceTo(root.Clock.State.CalendarTicks);
            ui.RefreshNow();
            yield return null; yield return null;
            result.defeat_recovery_actions = root.GameRun.Status == GameRunStatus.GameOver &&
                                             root.GameRun.GameOverReason == GameOverReason.NoControlledSettlement &&
                                             ui.RootVisual.Q<Button>("defeat-load-button") != null &&
                                             ui.RootVisual.Q<Button>("outcome-main-menu-button") != null;
            result.defeat_capture = Path.Combine(output, "ui08-defeat-1920x1080.png");
            yield return Capture(result.defeat_capture);
        }

        private static MilitaryUnitState CreatePlayerUnit(LocalSceneCompositionRoot root, string key, string location)
        {
            var members = root.PlayerCharacterIds.Where(id => root.HealthFamily.GetCapabilities(id).CanFight).ToArray();
            if (members.Length == 0) throw new InvalidOperationException("No combat-capable player resident is available.");
            var warriors=root.DirectWorkCommands.ProfessionGroups.Primary(WorkGroupRole.Warriors);
            foreach(var id in members)root.DirectWorkCommands.ProfessionGroups.Reassign(id,warriors.Id);
            var member = members[0];
            return root.MilitaryUnits.Create(new MilitaryUnitId(DeterministicStableId.Create(20261031, key)),
                new MilitarySideId("player"), members, member,
                MilitaryUnitRepresentationMode.Aggregate, location);
        }

        private static MilitaryUnitState CreateEnemyUnit(LocalSceneCompositionRoot root, string key, string location)
        {
            var member = root.StarterEnemyAi.Residents[0];
            root.Characters.Get(member).AssignProfession(new ProfessionId("military"));
            return root.MilitaryUnits.Create(new MilitaryUnitId(DeterministicStableId.Create(20261031, key)),
                new MilitarySideId("enemy"), new[] { member }, member,
                MilitaryUnitRepresentationMode.Aggregate, location);
        }

        private static void ReserveEnemyNonCombatants(LocalSceneCompositionRoot root, string key)
        {
            var roster = root.StarterEnemyAi.Residents.Skip(1).ToArray();
            foreach(var id in roster)root.Characters.Get(id).AssignProfession(new ProfessionId("military"));
            root.MilitaryUnits.Create(new MilitaryUnitId(DeterministicStableId.Create(20261031, key)),
                new MilitarySideId("enemy"), roster, roster[0], MilitaryUnitRepresentationMode.Aggregate, "local");
        }

        private static void AdvanceSimulation(LocalSceneCompositionRoot root, TimeSpan realDelta)
        {
            root.Clock.Resume();
            root.AiRuntime.AdvanceForDebug(realDelta);
            root.Clock.Pause();
        }

        private static bool Existing(params string[] paths) =>
            paths.All(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));

        private static IEnumerator SetResolution(int width, int height)
        {
            Screen.SetResolution(width, height, false);
            var deadline = Time.realtimeSinceStartup + 15f;
            while ((Screen.width != width || Screen.height != height) && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;
        }

        private static IEnumerator Capture(string path)
        {
            if (File.Exists(path)) File.Delete(path);
            for (var frame = 0; frame < 15; frame++) yield return null;
            yield return new WaitForEndOfFrame();
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            var pixels = texture.GetPixels32();
            Destroy(texture);
            if (pixels.Length == 0 || pixels.Where((_, index) => index % Math.Max(1, pixels.Length / 2048) == 0)
                    .Average(pixel => (pixel.r + pixel.g + pixel.b) / 3d) < 10d)
                throw new InvalidOperationException("Screenshot is effectively black: " + path);
        }

        private static string Arg(string[] args, string key)
        {
            for (var index = 0; index < args.Length - 1; index++)
                if (string.Equals(args[index], key, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
            return null;
        }
    }
}
