using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Military;
using Game.Domain.Work;
using Game.Presentation.UI;
using Game.Simulation.Military;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible 1920x1080 D3D11 proof for the complete P10.7 player route.</summary>
    public sealed class P107PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public bool passed;
            public bool menu_new_game;
            public bool training_ui;
            public bool training_started;
            public bool same_character_identity;
            public bool recruit_rank;
            public bool combat_skill;
            public bool military_clothing;
            public bool military_roster;
            public bool civilian_attack_denied;
            public bool attack_ui;
            public bool physical_walk_order;
            public bool physical_position_changed;
            public bool strategic_attack_ui;
            public bool both_flags_required;
            public bool capture_progress;
            public bool save_load_v24;
            public int width;
            public int height;
            public float average_fps;
            public float minimum_fps;
            public string training_screenshot;
            public string attack_screenshot;
            public string capture_screenshot;
            public string after_load_screenshot;
            public string frame_sequence_directory;
            public string error;
        }

        private MainMenuController menu;
        private LocalSceneCompositionRoot root;
        private string outputDirectory;
        private int requestedWidth;
        private int requestedHeight;

        public static P107PlayerSmokeController LaunchFromMenuIfRequested(MainMenuController source)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(x => string.Equals(x, "-p107Smoke", StringComparison.OrdinalIgnoreCase))) return null;
            var output = ReadArgument(args, "-p107SmokeOutput");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("-p107SmokeOutput is required.");
            var host = new GameObject("P10.7 Military Player Smoke");
            DontDestroyOnLoad(host);
            var controller = host.AddComponent<P107PlayerSmokeController>();
            controller.menu = source;
            controller.outputDirectory = output;
            int.TryParse(ReadArgument(args, "-p107SmokeWidth"), out controller.requestedWidth);
            int.TryParse(ReadArgument(args, "-p107SmokeHeight"), out controller.requestedHeight);
            controller.StartCoroutine(controller.Run());
            return controller;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(outputDirectory);
            var result = new Result();
            yield return EnsureResolution(result);
            Try(result, () =>
            {
                menu.OpenNewGame();
                result.menu_new_game = menu.CurrentView == MainMenuView.NewGame;
                menu.StartNewGame(new Game.Domain.Campaign.NewGameConfiguration(10718,
                    rulerName: "Рада", dynastyName: "Дом Каменного Знамени"));
            });
            var deadline = Time.realtimeSinceStartup + 45f;
            while (SceneManager.GetActiveScene().name != "LocalGameplay" && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;
            root = FindAnyObjectByType<LocalSceneCompositionRoot>();
            if (root == null) { result.error = "LocalGameplay did not initialize."; Finish(result); yield break; }

            var traineeId = root.MaleDemoCharacter.Id;
            var characterCount = root.Characters.Count;
            var originalCharacter = root.Characters.Get(traineeId);
            Try(result, () =>
            {
                root.GameplayUi.SetOnboardingVisible(false);
                root.SelectionProbe.SelectCharacterIds(new[] { traineeId });
                root.GameplayUi.SetPanel(GameplayPanel.Military);
                root.GameplayUi.RefreshNow();
                var train = root.GameplayUi.RootVisual.Q<Button>("military-train");
                result.training_ui = train != null && train.enabledSelf;
                Invoke(train);
                result.training_started = root.MilitaryTraining.IsExternallyControlled(traineeId);
            });
            yield return new WaitForEndOfFrame();
            result.training_screenshot = Capture("p107-training.png");

            Try(result, () => root.AiRuntime.AdvanceForDebug(TimeSpan.FromMinutes(4)));
            yield return null;
            Try(result, () =>
            {
                result.same_character_identity = root.Characters.Count == characterCount &&
                    ReferenceEquals(originalCharacter, root.Characters.Get(traineeId));
                result.recruit_rank = root.MilitaryTraining.GetRank(traineeId) == MilitaryRank.Recruit;
                result.combat_skill = root.Characters.Get(traineeId)
                    .TryGetSkill(MilitaryTrainingService.CombatSkillId, out var combatSkill) && combatSkill.Level.Value >= 20;
                result.military_clothing = root.Appearances.Get(traineeId).ClothingId ==
                    StoneAgeAppearanceProfiles.MilitaryClothing;
                result.military_roster = root.MilitaryUnits.TryGetForMember(traineeId, out var trainedUnit) &&
                    trainedUnit.ActiveMemberIds.Contains(traineeId);
            });

            WorldPosition destination = default;
            WorldPosition startPosition = default;
            Try(result, () =>
            {
                var civilianId = root.FemaleDemoCharacter.Id;
                if (!root.AiAgents.TryGet(civilianId, out var civilianAgent))
                    throw new InvalidOperationException("Civilian presentation agent is unavailable.");
                var civilianTarget = new ContextCommandTarget(ContextTargetKind.Character,
                    civilianAgent.Position, civilianId);
                root.SelectionProbe.SelectCharacterIds(new[] { traineeId });
                root.GameplayUi.OpenContextMenu(civilianTarget, new Vector2(800, 450));
                result.civilian_attack_denied = !root.GameplayUi.ContextActionStates[ContextActionKind.Attack].Enabled;
                root.GameplayUi.CloseContextMenu();

                if (!root.AiAgents.TryGet(traineeId, out var agent))
                    throw new InvalidOperationException("Trained presentation agent is unavailable.");
                startPosition = agent.Position;
                var desired = new Vector3(agent.Position.X + 10f, agent.Position.Y, agent.Position.Z + 5f);
                if (!NavMesh.SamplePosition(desired, out var hit, 24f, NavMesh.AllAreas))
                    throw new InvalidOperationException("No physical NavMesh attack destination was available.");
                destination = new WorldPosition(hit.position.x, hit.position.y, hit.position.z);
                var area = new ContextCommandTarget(ContextTargetKind.Ground, destination);
                root.GameplayUi.OpenContextMenu(area, new Vector2(800, 450));
                var attackButton = root.GameplayUi.RootVisual.Q<Button>("context-attack");
                result.attack_ui = attackButton != null && attackButton.enabledSelf;
                Invoke(attackButton);
                result.physical_walk_order = root.Work.HasOpenJob(traineeId) &&
                    root.MilitaryUnits.TryGetForMember(traineeId, out var unit) &&
                    unit.Order.Kind == MilitaryUnitOrderKind.Move;
            });
            yield return new WaitForEndOfFrame();
            result.attack_screenshot = Capture("p107-attack-order.png");

            var frameDirectory = Path.Combine(outputDirectory, "p107-movement-frames");
            Directory.CreateDirectory(frameDirectory);
            result.frame_sequence_directory = frameDirectory;
            var frameTimes = new List<float>();
            for (var i = 0; i < 120; i++)
            {
                yield return new WaitForEndOfFrame();
                if (Time.unscaledDeltaTime > 0f) frameTimes.Add(Time.unscaledDeltaTime);
                if (i % 20 == 0) Capture(Path.Combine("p107-movement-frames", "frame-" + i.ToString("D3") + ".png"));
            }
            Try(result, () =>
            {
                if (!root.AiAgents.TryGet(traineeId, out var movedAgent))
                    throw new InvalidOperationException("Trained presentation agent disappeared.");
                var moved = movedAgent.Position;
                var dx = moved.X - startPosition.X; var dz = moved.Z - startPosition.Z;
                result.physical_position_changed = dx * dx + dz * dz > 0.25f;
                result.average_fps = frameTimes.Count == 0 ? 0f : 1f / frameTimes.Average();
                result.minimum_fps = frameTimes.Count == 0 ? 0f : 1f / frameTimes.Max();
            });

            Try(result, () =>
            {
                root.GlobalMapBridge.RequestEnterGlobal();
                root.GameplayUi.SetPanel(GameplayPanel.Global);
                root.GameplayUi.RefreshNow();
                result.strategic_attack_ui = root.GameplayUi.RootVisual.Q<Button>("strategic-attack") != null;
            });
            yield return null;

            Try(result, () =>
            {
                var state = root.SettlementCapture.Get(root.StarterSettlementId);
                var enemyId = root.StarterEnemyAi.Residents.First(x => !root.MilitaryUnits.TryGetForMember(x, out _));
                var enemy = root.MilitaryUnits.Create(new MilitarySideId("enemy"), new[] { enemyId }, enemyId);
                if (!root.SettlementCapture.TryClaimFlag(root.StarterSettlementId, state.MainBuildingId,
                        enemy.Id, root.Clock.State.CalendarTicks))
                    throw new InvalidOperationException("Main capture flag could not be claimed.");
                root.AiRuntime.AdvanceForDebug(TimeSpan.FromMinutes(1));
                result.both_flags_required = root.SettlementCapture.Get(root.StarterSettlementId).ProgressTicks == 0;
                if (!root.SettlementCapture.TryClaimFlag(root.StarterSettlementId, root.DemoBarracksBuilding.Id,
                        enemy.Id, root.Clock.State.CalendarTicks))
                    throw new InvalidOperationException("Barracks capture flag could not be claimed.");
                root.AiRuntime.AdvanceForDebug(TimeSpan.FromMinutes(1));
                var progressing = root.SettlementCapture.Get(root.StarterSettlementId);
                result.capture_progress = progressing.ProgressTicks == TimeSpan.FromHours(1).Ticks &&
                    progressing.Status == SettlementCaptureClockStatus.Advancing;
                root.GameplayUi.SetPanel(GameplayPanel.Military);
                root.GameplayUi.RefreshNow();
            });
            yield return new WaitForEndOfFrame();
            result.capture_screenshot = Capture("p107-capture-progress.png");

            Try(result, () =>
            {
                var progressBefore = root.SettlementCapture.Get(root.StarterSettlementId).ProgressTicks;
                root.GameplayUi.SetPanel(GameplayPanel.SaveLoad);
                root.GameplayUi.RefreshNow();
                var card = root.GameplayUi.RootVisual.Q<VisualElement>("save-slot-3");
                Invoke(card.Query<Button>().ToList().First(x => x.text == "Сохранить"));
                card = root.GameplayUi.RootVisual.Q<VisualElement>("save-slot-3");
                Invoke(card.Query<Button>().ToList().First(x => x.text == "Загрузить"));
                var modal = root.GameplayUi.RootVisual.Q<VisualElement>("confirmation-modal");
                Invoke(modal.Query<Button>().ToList().First(x => x.text == "Загрузить"));
                var snapshot = root.Saves.Capture();
                result.save_load_v24 = snapshot.FormatVersion >= SaveSnapshot.MilitaryCampaignVersion &&
                    root.MilitaryTraining.GetRank(traineeId) == MilitaryRank.Recruit &&
                    root.SettlementCapture.Get(root.StarterSettlementId).ProgressTicks == progressBefore &&
                    snapshot.MilitaryCampaign.Training.Records.Any(x => x.CharacterId == traineeId && x.Completed);
                root.GameplayUi.SetPanel(GameplayPanel.Military);
                root.GameplayUi.RefreshNow();
            });
            yield return new WaitForEndOfFrame();
            result.after_load_screenshot = Capture("p107-after-load.png");
            Finish(result);
        }

        private IEnumerator EnsureResolution(Result result)
        {
            Screen.SetResolution(requestedWidth, requestedHeight, FullScreenMode.Windowed);
            var deadline = Time.realtimeSinceStartup + 8f;
            while ((Screen.width != requestedWidth || Screen.height != requestedHeight) && Time.realtimeSinceStartup < deadline)
                yield return null;
            result.width = Screen.width; result.height = Screen.height;
            if (result.width != requestedWidth || result.height != requestedHeight)
                result.error = "Unexpected resolution: " + result.width + "x" + result.height;
        }

        private string Capture(string relativePath)
        {
            var path = Path.Combine(outputDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG()); Destroy(texture); return path;
        }

        private void Finish(Result result)
        {
            result.passed = string.IsNullOrEmpty(result.error) && result.menu_new_game && result.training_ui &&
                result.training_started && result.same_character_identity && result.recruit_rank && result.combat_skill &&
                result.military_clothing && result.military_roster && result.civilian_attack_denied && result.attack_ui &&
                result.physical_walk_order && result.physical_position_changed && result.strategic_attack_ui &&
                result.both_flags_required && result.capture_progress && result.save_load_v24 &&
                result.width == requestedWidth && result.height == requestedHeight && result.average_fps > 0f &&
                File.Exists(result.training_screenshot) && File.Exists(result.attack_screenshot) &&
                File.Exists(result.capture_screenshot) && File.Exists(result.after_load_screenshot) &&
                Directory.GetFiles(result.frame_sequence_directory, "*.png").Length >= 6;
            var report = Path.Combine(outputDirectory, "p107-player-smoke.json");
            File.WriteAllText(report, JsonUtility.ToJson(result, true));
            Debug.Log("P107_PLAYER_SMOKE " + (result.passed ? "PASS " : "FAILED ") + report);
            Application.Quit(result.passed ? 0 : 2);
        }

        private static void Invoke(Button button)
        {
            if (button == null) throw new InvalidOperationException("Expected UI button is missing.");
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic);
            if (invoke == null) throw new MissingMethodException("Clickable.Invoke");
            invoke.Invoke(button.clickable, new object[] { null });
        }

        private static void Try(Result result, Action action)
        { try { action(); } catch (Exception exception) { result.error = exception.ToString(); } }

        private static string ReadArgument(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
