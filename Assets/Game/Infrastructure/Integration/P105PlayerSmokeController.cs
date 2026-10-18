using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Domain;
using Game.Domain.Campaign;
using Game.Domain.Characters;
using Game.Presentation.Characters;
using Game.Presentation.UI;
using Game.Simulation.Campaign;
using Game.Simulation.Tiers;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible Player proof for P10.5 role art, authored actions, LOD and continuity.</summary>
    public sealed class P105PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public bool passed;
            public bool menu_new_game;
            public bool worker_visual;
            public bool military_visual;
            public bool composed_heads;
            public bool authored_chop;
            public bool authored_combat;
            public bool age_groups;
            public bool mid_lod;
            public bool far_lod;
            public bool save_load_tier_continuity;
            public int width;
            public int height;
            public int character_count;
            public int tier1_count;
            public int mid_lod_count;
            public int far_lod_count;
            public string roles_screenshot;
            public string lod_screenshot;
            public string after_load_screenshot;
            public string error;
        }

        private MainMenuController menu;
        private LocalSceneCompositionRoot root;
        private string outputDirectory;
        private int requestedWidth;
        private int requestedHeight;

        public static P105PlayerSmokeController LaunchFromMenuIfRequested(MainMenuController source)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(x => string.Equals(x, "-p105Smoke", StringComparison.OrdinalIgnoreCase))) return null;
            var output = ReadArgument(args, "-p105SmokeOutput");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("-p105SmokeOutput is required.");
            var host = new GameObject("P10.5 Character Player Smoke");
            DontDestroyOnLoad(host);
            var controller = host.AddComponent<P105PlayerSmokeController>();
            controller.menu = source;
            controller.outputDirectory = output;
            int.TryParse(ReadArgument(args, "-p105SmokeWidth"), out controller.requestedWidth);
            int.TryParse(ReadArgument(args, "-p105SmokeHeight"), out controller.requestedHeight);
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
                var catalog = StoneAgeAppearanceProfiles.CreateCatalog();
                var profile = catalog.Get(StoneAgeAppearanceProfiles.UmaFemale);
                var configuration = new NewGameConfiguration(10518, rulerName: "Мира",
                    dynastyName: "Дом Ясного Огня", rulerSex: CharacterSex.Female,
                    rulerAppearanceProfileId: profile.Id, rulerAppearance: profile.CreateDetailedState(
                        DeterministicStableId.Create(10518, "player_character", 0)).With(
                        ageId: new AppearancePartId("age_aged")));
                menu.OpenNewGame();
                result.menu_new_game = menu.CurrentView == MainMenuView.NewGame;
                menu.StartNewGame(configuration);
            });
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            var deadline = Time.realtimeSinceStartup + 40f;
            while (SceneManager.GetActiveScene().name != "LocalGameplay" && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;
            root = FindAnyObjectByType<LocalSceneCompositionRoot>();
            if (root == null) { result.error = "LocalGameplay did not initialize."; Finish(result); yield break; }

            Try(result, () =>
            {
                result.character_count = root.Characters.GetAll().Count;
                var savedAppearance = root.Appearances.CaptureState();
                result.age_groups = savedAppearance.Characters.Select(x => x.AgeId).Distinct().Count() == 3;
                foreach (var appearance in savedAppearance.Characters)
                    root.Tiers.Transition(appearance.CharacterId, CharacterSimulationTier.Tier1);
                result.tier1_count = root.CharacterPresentations.Count;
                root.AiRuntime.SetPaused(true);
                root.AiRuntime.enabled = false;
                root.GameplayUi.SetPanel(GameplayPanel.None);
                root.GameplayUi.SetOnboardingVisible(false);
                root.CameraController.enabled = false;
                root.enabled = false;
            });
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }
            yield return null;

            CharacterPresenter worker = null;
            CharacterPresenter military = null;
            var workerId = default(StableEntityId);
            var militaryId = default(StableEntityId);
            Try(result, () =>
            {
                foreach (var appearance in root.Appearances.CaptureState().Characters)
                {
                    if (!root.CharacterPresentations.TryGet(appearance.CharacterId, out var presenter)) continue;
                    if (appearance.ClothingId == StoneAgeAppearanceProfiles.WorkerClothing && worker == null)
                    {
                        worker = presenter;
                        workerId = appearance.CharacterId;
                    }
                    if (appearance.ClothingId == StoneAgeAppearanceProfiles.MilitaryClothing && military == null)
                    {
                        military = presenter;
                        militaryId = appearance.CharacterId;
                    }
                }
                if (worker == null || military == null) throw new InvalidOperationException("Role presenters are missing.");
                result.worker_visual = HasRoleVisual(worker);
                result.military_visual = HasRoleVisual(military);
                result.composed_heads = HasComposedHead(worker) && HasComposedHead(military);
                var focus = root.StoneAgeSettlement.BiomeWorld.BaseRegion.ProjectToGround(new Vector3(6f, 0f, 3f));
                Place(worker, focus + Vector3.left * 1.05f, 180f);
                Place(military, focus + Vector3.right * 1.05f, 180f);
                worker.SetAutomaticAnimationAllowed(true);
                military.SetAutomaticAnimationAllowed(true);
                worker.SetSimulationPaused(false);
                military.SetSimulationPaused(false);
                worker.Animator.enabled = true;
                military.Animator.enabled = true;
                worker.SetAnimationIntent(CharacterAnimationIntent.Chop);
                military.SetAnimationIntent(CharacterAnimationIntent.Combat);
                Frame(focus + Vector3.up * 0.85f, 0f, 4f, 0f);
            });
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }
            yield return new WaitForSecondsRealtime(1.5f);
            result.authored_chop = worker.Animator.GetInteger("ActionIntent") == (int)CharacterAnimationIntent.Chop &&
                                    worker.Animator.GetCurrentAnimatorStateInfo(0).IsName("Chop");
            result.authored_combat = military.Animator.GetInteger("ActionIntent") == (int)CharacterAnimationIntent.Combat &&
                                      military.Animator.GetCurrentAnimatorStateInfo(0).IsName("Combat");
            yield return new WaitForEndOfFrame();
            result.roles_screenshot = Capture("p105-role-actions.png");

            Try(result, () =>
            {
                root.AiRuntime.enabled = true;
                var camera = Camera.main ?? throw new InvalidOperationException("Main camera is missing.");
                var forward = camera.transform.forward.normalized;
                var right = camera.transform.right.normalized;
                var movements = root.AiRuntime.Movements.Where(x => x.Presenter != null).ToArray();
                for (var index = 0; index < movements.Length; index++)
                {
                    var presenter = movements[index].Presenter;
                    presenter.SetSelected(false);
                    var distance = index < 2 ? 18f : index == 2 ? 46f : 82f;
                    var point = camera.transform.position + forward * distance + right * ((index % 3) - 1) * 2f;
                    if (index >= 2)
                    {
                        movements[index].SetCrowdDetail(false);
                        presenter.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, 180f, 0f));
                    }
                    else
                    {
                        movements[index].SetCrowdDetail(true);
                        Place(presenter, point, 180f);
                    }
                }
            });
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }
            yield return new WaitForSecondsRealtime(1f);
            var lodMovements = root.AiRuntime.Movements.Where(x => x.Presenter != null).ToArray();
            var lodCamera = Camera.main;
            if (lodCamera != null && lodMovements.Length >= 4)
            {
                lodMovements[2].SetCrowdDetail(false);
                lodMovements[2].Presenter.transform.position = lodCamera.transform.position +
                    lodCamera.transform.forward.normalized * 46f;
                lodMovements[3].SetCrowdDetail(false);
                lodMovements[3].Presenter.transform.position = lodCamera.transform.position +
                    lodCamera.transform.forward.normalized * 82f;
            }
            typeof(Game.Presentation.AI.Tier1AiRuntimeDriver)
                .GetMethod("DrawLightweightPresenters", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(root.AiRuntime, null);
            result.mid_lod_count = root.AiRuntime.LastMidLodDrawCount;
            result.far_lod_count = root.AiRuntime.LastFarLodDrawCount;
            result.mid_lod = result.mid_lod_count > 0;
            result.far_lod = result.far_lod_count > 0;
            yield return new WaitForEndOfFrame();
            result.lod_screenshot = Capture("p105-lod-chain.png");

            Try(result, () =>
            {
                root.Tiers.Transition(workerId, CharacterSimulationTier.Tier1);
                root.Tiers.Transition(militaryId, CharacterSimulationTier.Tier1);
                var focus = root.StoneAgeSettlement.BiomeWorld.BaseRegion.ProjectToGround(new Vector3(6f, 0f, 3f));
                if (root.CharacterPresentations.TryGet(workerId, out var workerBeforeSave))
                    Place(workerBeforeSave, focus + Vector3.left, 180f);
                if (root.CharacterPresentations.TryGet(militaryId, out var militaryBeforeSave))
                    Place(militaryBeforeSave, focus + Vector3.right, 180f);
                root.Tiers.Transition(workerId, CharacterSimulationTier.Tier2);
                root.Tiers.Transition(workerId, CharacterSimulationTier.Tier1);
                root.Tiers.Transition(militaryId, CharacterSimulationTier.Tier2);
                root.Tiers.Transition(militaryId, CharacterSimulationTier.Tier1);
                root.PlayerSaves.Save(3);
                root.Tiers.Transition(workerId, CharacterSimulationTier.Tier2);
                root.Tiers.Transition(workerId, CharacterSimulationTier.Tier1);
                root.PlayerSaves.Load(3);
            });
            yield return null;
            Try(result, () =>
            {
                var workerAppearance = root.Appearances.Get(workerId);
                var militaryAppearance = root.Appearances.Get(militaryId);
                if (workerAppearance.ClothingId != StoneAgeAppearanceProfiles.WorkerClothing ||
                    militaryAppearance.ClothingId != StoneAgeAppearanceProfiles.MilitaryClothing ||
                    !root.CharacterPresentations.TryGet(workerId, out var restoredWorker) ||
                    !root.CharacterPresentations.TryGet(militaryId, out var restoredMilitary) ||
                    !HasComposedHeadStructure(restoredWorker) || !HasComposedHeadStructure(restoredMilitary)) return;
                var focus = root.StoneAgeSettlement.BiomeWorld.BaseRegion.ProjectToGround(new Vector3(6f, 0f, 3f));
                restoredWorker.SetSelected(true);
                restoredMilitary.SetSelected(true);
                restoredWorker.SetAutomaticAnimationAllowed(true);
                restoredMilitary.SetAutomaticAnimationAllowed(true);
                Place(restoredWorker, focus + Vector3.left * 1.05f, 180f);
                Place(restoredMilitary, focus + Vector3.right * 1.05f, 180f);
                Frame(focus + Vector3.up * 0.85f, 0f, 4f, 0f);
                result.save_load_tier_continuity = HasComposedHead(restoredWorker) && HasComposedHead(restoredMilitary);
            });
            yield return new WaitForEndOfFrame();
            result.after_load_screenshot = Capture("p105-after-load.png");
            Finish(result);
        }

        private IEnumerator EnsureResolution(Result result)
        {
            if (requestedWidth <= 0 || requestedHeight <= 0) yield break;
            Screen.SetResolution(requestedWidth, requestedHeight, FullScreenMode.Windowed);
            var deadline = Time.realtimeSinceStartup + 8f;
            while ((Screen.width != requestedWidth || Screen.height != requestedHeight) && Time.realtimeSinceStartup < deadline)
                yield return null;
            result.width = Screen.width;
            result.height = Screen.height;
            if (result.width != requestedWidth || result.height != requestedHeight)
                result.error = $"Expected {requestedWidth}x{requestedHeight}, got {result.width}x{result.height}.";
        }

        private static bool HasRoleVisual(CharacterPresenter presenter) =>
            presenter.transform.Find("RoleVisual/SourceVisual") != null;

        private static bool HasComposedHead(CharacterPresenter presenter)
        {
            var head = presenter.transform.Find("RoleVisual/SourceVisual/P105_Head");
            var eyes = presenter.transform.Find("RoleVisual/SourceVisual/P105_Eyes");
            return head != null && eyes != null && head.GetComponent<SkinnedMeshRenderer>()?.enabled == true;
        }

        private static bool HasComposedHeadStructure(CharacterPresenter presenter) =>
            presenter != null && presenter.transform.Find("RoleVisual/SourceVisual/P105_Head") != null &&
            presenter.transform.Find("RoleVisual/SourceVisual/P105_Eyes") != null;

        private static void Place(CharacterPresenter presenter, Vector3 position, float yaw)
        {
            var agent = presenter.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(position);
            else presenter.transform.position = position;
            presenter.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private void Frame(Vector3 target, float yaw, float distance, float pitch)
        {
            root.CameraController.transform.SetPositionAndRotation(target, Quaternion.Euler(0f, yaw, 0f));
            root.CameraController.CameraMount.localPosition = Quaternion.Euler(pitch, 0f, 0f) * Vector3.back * distance;
            root.CameraController.CameraMount.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private string Capture(string fileName)
        {
            var path = Path.Combine(outputDirectory, fileName);
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Destroy(texture);
            return path;
        }

        private void Finish(Result result)
        {
            result.passed = string.IsNullOrEmpty(result.error) && result.menu_new_game && result.worker_visual &&
                            result.military_visual && result.composed_heads && result.authored_chop &&
                            result.authored_combat && result.age_groups && result.mid_lod && result.far_lod &&
                            result.save_load_tier_continuity && result.width == requestedWidth &&
                            result.height == requestedHeight && File.Exists(result.roles_screenshot) &&
                            File.Exists(result.lod_screenshot) && File.Exists(result.after_load_screenshot);
            var report = Path.Combine(outputDirectory, "p105-player-smoke.json");
            File.WriteAllText(report, JsonUtility.ToJson(result, true));
            Debug.Log("P105_PLAYER_SMOKE " + (result.passed ? "PASS " : "FAILED ") + report);
            Application.Quit(result.passed ? 0 : 2);
        }

        private static void Try(Result result, Action action)
        {
            try { action(); }
            catch (Exception exception) { result.error = exception.ToString(); }
        }

        private static string ReadArgument(string[] args, string name)
        {
            for (var index = 0; index < args.Length - 1; index++)
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
            return null;
        }
    }
}
