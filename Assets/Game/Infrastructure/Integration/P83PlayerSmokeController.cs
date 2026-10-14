using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Work;
using Game.Presentation.Production;
using Game.Presentation.UI;
using Game.Presentation.World;
using UnityEngine;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible Windows Player proof for P8.3 click-to-action work groups.</summary>
    public sealed class P83PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class SmokeResult
        {
            public bool passed;
            public bool nine_role_groups;
            public bool click_action_priority;
            public bool deterministic_assignment;
            public bool conflict_safe;
            public bool immediate_safe_preemption;
            public bool save_load_exact;
            public bool global_resource_action;
            public string local_screenshot;
            public string global_screenshot;
            public string error;
        }

        private LocalSceneCompositionRoot root;
        private string outputDirectory;

        public static void LaunchFromMenuIfRequested(MainMenuController menu)
        {
            if (Environment.GetCommandLineArgs().Any(x => string.Equals(x, "-p83Smoke", StringComparison.OrdinalIgnoreCase)))
                menu.StartNewGame(83003);
        }

        public static P83PlayerSmokeController AttachIfRequested(LocalSceneCompositionRoot compositionRoot)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(x => string.Equals(x, "-p83Smoke", StringComparison.OrdinalIgnoreCase))) return null;
            var output = ReadArgument(args, "-p83SmokeOutput");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("-p83SmokeOutput is required.");
            var controller = compositionRoot.gameObject.AddComponent<P83PlayerSmokeController>();
            controller.root = compositionRoot;
            controller.outputDirectory = output;
            controller.StartCoroutine(controller.Run());
            return controller;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(outputDirectory);
            var result = new SmokeResult();
            WorkOrder localOrder = null;
            ProductionSourcePresenter source = null;
            Try(result, () =>
            {
                root.Clock.Pause();
                root.GameplayUi.SetOnboardingVisible(false);
                root.GameplayUi.SetDeveloperUiVisible(false);
                var groups = root.DirectWorkCommands.EnsureRoleGroups();
                result.nine_role_groups = groups.Select(x => x.Role).Distinct().Count() == 9;
                var gatherers = groups.Single(x => x.Role == WorkGroupRole.Gatherers);
                var worker = root.MaleDemoCharacter.Id;
                if (root.Work.Groups.TryGetForMember(worker, out var previous) && previous.Id != gatherers.Id)
                    root.Work.RemoveMember(previous.Id, worker);
                if (!gatherers.Contains(worker)) root.Work.AddMember(gatherers.Id, worker);
                source = FindObjectsByType<ProductionSourcePresenter>(FindObjectsSortMode.None)
                    .Where(x => x.IsBound && x.Site.Kind == Game.Domain.Production.ProductionSiteKind.NaturalSource)
                    .OrderBy(x => x.Site.Id).First();
                root.SelectionProbe.SelectProductionSource(source);
                root.GameplayUi.SetPanel(GameplayPanel.Work);
                root.GameplayUi.RefreshNow();
                localOrder = root.DirectWorkCommands.Issue(WorkJobType.Gather,
                    WorkTarget.ForEntity(WorkTargetKind.ProductionSite, source.Site.Id, source.Site.Position),
                    WorkPriority.High);
                result.click_action_priority = localOrder.Priority == WorkPriority.High && localOrder.GroupId == gatherers.Id;
                result.deterministic_assignment = localOrder.AssigneeId == worker;
                try
                {
                    root.DirectWorkCommands.Issue(WorkJobType.Gather,
                        WorkTarget.ForEntity(WorkTargetKind.ProductionSite, source.Site.Id, source.Site.Position),
                        WorkPriority.Normal);
                }
                catch (InvalidOperationException) { result.conflict_safe = true; }
                root.PlayerSaves.Save(3);
                root.Work.SetPriority(localOrder.Id, WorkPriority.Immediate);
                root.PlayerSaves.Load(3);
                var restored = root.Work.Get(localOrder.Id);
                result.save_load_exact = restored.Priority == WorkPriority.High && restored.AssigneeId == worker &&
                    restored.Target.EntityId == source.Site.Id;
                root.Work.Cancel(restored.Id);
                var prior = root.Work.CreateMoveJob(worker, new WorldPosition(8f, 0f, 8f), WorkPriority.Normal,
                    gatherers.Id);
                var immediate = root.DirectWorkCommands.Issue(WorkJobType.Gather,
                    WorkTarget.ForEntity(WorkTargetKind.ProductionSite, source.Site.Id, source.Site.Position),
                    WorkPriority.Immediate);
                result.immediate_safe_preemption = immediate.AssigneeId == worker && prior.Status == WorkJobStatus.Queued &&
                    !prior.AssigneeId.HasValue;
                root.SelectionProbe.SelectProductionSource(source);
                root.GameplayUi.RefreshNow();
            });
            if (string.IsNullOrEmpty(result.error))
            {
                yield return new WaitForSecondsRealtime(1.2f);
                yield return new WaitForEndOfFrame();
                Try(result, () =>
                {
                    result.local_screenshot = Capture("p83-local-work.png");
                    foreach (var job in root.Work.Jobs.Where(x => !x.IsTerminal).ToArray()) root.Work.Cancel(job.Id);
                    root.GlobalMapBridge.RequestEnterGlobal();
                });
            }
            if (string.IsNullOrEmpty(result.error))
            {
                var deadline = Time.realtimeSinceStartup + 20f;
                while (!root.GlobalMapBridge.IsGlobalSceneLoaded && Time.realtimeSinceStartup < deadline) yield return null;
                yield return new WaitForSecondsRealtime(1f);
                Try(result, () =>
                {
                    var global = FindAnyObjectByType<GlobalMapPresentationController>();
                    if (global == null) throw new InvalidOperationException("Global presentation did not load.");
                    var marker = global.GetComponentsInChildren<GlobalMapPresentationController.GlobalResourceMarker>(true)
                        .OrderBy(x => x.ResourceId).First();
                    global.SelectResourceMarker(marker);
                    var order = global.IssueSelectedResourceWork();
                    result.global_resource_action = order.Type == WorkJobType.Gather && order.Target.EntityId.IsValid;
                });
                yield return new WaitForEndOfFrame();
                Try(result, () => result.global_screenshot = Capture("p83-global-work.png"));
            }
            result.passed = string.IsNullOrEmpty(result.error) && result.nine_role_groups &&
                result.click_action_priority && result.deterministic_assignment && result.conflict_safe &&
                result.immediate_safe_preemption && result.save_load_exact && result.global_resource_action &&
                File.Exists(result.local_screenshot) && File.Exists(result.global_screenshot);
            var path = Path.Combine(outputDirectory, "p83-player-smoke.json");
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
            Debug.Log("P83_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + path);
            Application.Quit(result.passed ? 0 : 2);
        }

        private static void Try(SmokeResult result, Action action)
        {
            try { action(); }
            catch (Exception exception) { result.error = exception.ToString(); }
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

        private static string ReadArgument(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
