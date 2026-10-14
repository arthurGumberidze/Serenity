using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Research;
using Game.Domain.Resources;
using Game.Domain.Weather;
using Game.Domain.Work;
using Game.Presentation.Environment;
using Game.Presentation.Production;
using Game.Presentation.Resources;
using Game.Presentation.UI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible normal-Player proof for the P9.4 Stone Age settlement presentation slice.</summary>
    public sealed class P94PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class SmokeResult
        {
            public bool passed;
            public bool settlement_assets;
            public bool worksite_readability;
            public bool resource_interaction;
            public bool gather_order;
            public bool haul_order;
            public bool building_order;
            public bool priority_indicator;
            public bool save_load_identity;
            public bool navigation;
            public bool weather_season;
            public bool rts_rendered;
            public bool tps_rendered;
            public int worksites;
            public int supply_presentations;
            public int shelter_renderers;
            public int storage_renderers;
            public int crop_renderers;
            public string layout_hash;
            public string rts_screenshot;
            public string tps_screenshot;
            public string error;
        }

        private LocalSceneCompositionRoot root;
        private string outputDirectory;

        public static void LaunchFromMenuIfRequested(MainMenuController menu)
        {
            if (Environment.GetCommandLineArgs().Any(x =>
                    string.Equals(x, "-p94Smoke", StringComparison.OrdinalIgnoreCase)))
                menu.StartNewGame(94004);
        }

        public static P94PlayerSmokeController AttachIfRequested(LocalSceneCompositionRoot compositionRoot)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(x => string.Equals(x, "-p94Smoke", StringComparison.OrdinalIgnoreCase))) return null;
            var output = ReadArgument(args, "-p94SmokeOutput");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("-p94SmokeOutput is required.");
            var controller = compositionRoot.gameObject.AddComponent<P94PlayerSmokeController>();
            controller.root = compositionRoot;
            controller.outputDirectory = output;
            controller.StartCoroutine(controller.Run());
            return controller;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(outputDirectory);
            var result = new SmokeResult();
            WorkOrder buildingOrder = null;
            StableEntityId buildingId = default;
            StableEntityId selectedSiteId = default;
            Try(result, () =>
            {
                root.Clock.Pause();
                root.GameplayUi.SetOnboardingVisible(false);
                root.GameplayUi.SetDeveloperUiVisible(false);
                var marker = root.StoneAgeSettlement ??
                    throw new InvalidOperationException("P9.4 Stone Age settlement is missing.");
                marker.ValidateConfiguration();

                result.worksites = marker.Worksites.Count;
                result.supply_presentations = marker.SupplyPresentations.Count;
                result.shelter_renderers = marker.ShelterPrefab.GetComponentsInChildren<Renderer>(true).Length;
                result.storage_renderers = marker.StoragePrefab.GetComponentsInChildren<Renderer>(true).Length;
                result.crop_renderers = marker.CropPlotPrefab.GetComponentsInChildren<Renderer>(true).Length;
                result.layout_hash = marker.LayoutHash;
                result.settlement_assets = result.shelter_renderers >= 18 && result.storage_renderers >= 14 &&
                    result.crop_renderers >= 12;
                result.worksite_readability = result.worksites >= 6 &&
                    marker.Worksites.Select(x => x.Kind).Distinct().Count() == 6 &&
                    marker.Worksites.All(x => x.VisualRoot.GetComponentsInChildren<Renderer>(true).Length >= 2);
                result.resource_interaction = marker.SupplyPresentations.Count == 4 &&
                    marker.SupplyPresentations.All(x => x.GetComponent<WorldResourcePilePresenter>() != null &&
                        x.GetComponentInChildren<Collider>(true) != null &&
                        x.GetComponentsInChildren<Renderer>(true).Length >= 3);

                CancelOpenOrders();
                var gatherWorker = root.FemaleDemoCharacter.Id;
                var haulWorker = root.FemaleDemoCharacter.Id;
                var buildWorker = root.MaleDemoCharacter.Id;
                var source = root.InhabitableRegion.ResourceAnchors.Select(x =>
                        x.VisualRoot.GetComponent<ProductionSourcePresenter>())
                    .Where(x => x != null && x.IsBound).OrderBy(x => x.Site.Id).First();
                selectedSiteId = source.Site.Id;
                CompleteTechnology("shelter_construction");
                if (!root.BuildingPlacementController.TryStartPlacement(new BuildingDefinitionId("primitive_shelter")) ||
                    !root.BuildingPlacementController.TryMovePreviewToWorldPoint(new Vector3(8f, 0f, -6f)))
                    throw new InvalidOperationException("P9.4 settlement clearing rejected shelter placement.");
                var placed = root.BuildingPlacementController.ConfirmPlacement() ??
                    throw new InvalidOperationException("P9.4 shelter placement did not commit.");
                buildingId = placed.Id;
                if (!root.BuildingPresentations.TryGet(buildingId, out var buildingPresenter))
                    throw new InvalidOperationException("P9.4 placed shelter did not receive a presenter.");
                var target = buildingPresenter.transform.position;
                Assign(buildWorker, WorkGroupRole.Builders);
                buildingOrder = root.DirectWorkCommands.Issue(WorkJobType.Build,
                    WorkTarget.ForEntity(WorkTargetKind.Building, buildingId,
                        new WorldPosition(target.x, target.y, target.z)), WorkPriority.Immediate);
                root.WorkOrderIndicators.RefreshNow();
                result.building_order = buildingOrder.AssigneeId == buildWorker &&
                    buildingOrder.Target.EntityId == buildingId && root.Buildings.TryGet(buildingId, out _);
                result.priority_indicator = root.WorkOrderIndicators.TryGetIndicator(buildingOrder.Id,
                    out var buildIndicator) && buildIndicator.name.Contains("Immediate") &&
                    buildIndicator.GetComponentsInChildren<Renderer>(true).Length >= 2;

                root.PlayerSaves.Save(2);
                root.Work.SetPriority(buildingOrder.Id, WorkPriority.Low);
                root.PlayerSaves.Load(2);
                root.WorkOrderIndicators.RefreshNow();
                var restored = root.Work.Get(buildingOrder.Id);
                result.save_load_identity = restored.Priority == WorkPriority.Immediate &&
                    restored.Target.EntityId == buildingId && restored.AssigneeId == buildWorker &&
                    root.Buildings.TryGet(buildingId, out _) && root.Production.TryGetSite(selectedSiteId, out var site) &&
                    root.InhabitableRegion.ResourceAnchors.Select(x =>
                            x.VisualRoot.GetComponent<ProductionSourcePresenter>())
                        .Any(x => x != null && ReferenceEquals(x.Site, site));

                Assign(gatherWorker, WorkGroupRole.Gatherers);
                root.SelectionProbe.SelectProductionSource(source);
                var gather = root.DirectWorkCommands.Issue(WorkJobType.Gather,
                    WorkTarget.ForEntity(WorkTargetKind.ProductionSite, source.Site.Id, source.Site.Position),
                    WorkPriority.High);
                root.WorkOrderIndicators.RefreshNow();
                result.gather_order = gather.AssigneeId == gatherWorker && gather.Priority == WorkPriority.High &&
                    root.WorkOrderIndicators.TryGetIndicator(gather.Id, out var gatherIndicator) &&
                    gatherIndicator.name.Contains("Gather");
                root.Work.Cancel(gather.Id);
                root.WorkOrderIndicators.RefreshNow();

                var pile = root.DemoPilePresenters.Where(x => x != null && x.Quantity.Units > 0)
                    .OrderBy(x => x.PileId).First();
                Assign(haulWorker, WorkGroupRole.Haulers);
                var pilePosition = pile.transform.position;
                var haul = root.DirectWorkCommands.IssueHaul(
                    new InventoryOwner(InventoryOwnerKind.WorldPile, pile.PileId),
                    new InventoryOwner(InventoryOwnerKind.BuildingStorage, root.DemoStorageBuilding.Id),
                    pile.ResourceId, new ResourceQuantity(1),
                    new WorldPosition(pilePosition.x, pilePosition.y, pilePosition.z), WorkPriority.Urgent);
                root.WorkOrderIndicators.RefreshNow();
                result.haul_order = haul.AssigneeId == haulWorker && haul.Priority == WorkPriority.Urgent &&
                    root.WorkOrderIndicators.TryGetIndicator(haul.Id, out var haulIndicator) &&
                    haulIndicator.name.Contains("Urgent");
                root.Work.Cancel(haul.Id);
                root.WorkOrderIndicators.RefreshNow();

                var path = new NavMeshPath();
                var camp = marker.Worksites.First(x => x.Kind == P94StoneAgeSettlement.WorksiteKind.Construction)
                    .VisualRoot.transform.position;
                result.navigation = NavMesh.CalculatePath(root.InhabitableRegion.PlayerTerritoryAnchor.position,
                    camp, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;

                var winter = Enumerable.Range(0, 1460).Select(x => root.Weather.GetWeatherForDay(x))
                    .First(x => x.Season == Season.Winter);
                root.WeatherPresentation.ShowPreview(winter);
                result.weather_season = root.InhabitableRegion.DisplayedSeason == Season.Winter &&
                    root.InhabitableRegion.SnowCoverVisible;

                root.GameplayUi.SetPanel(GameplayPanel.Work);
                root.GameplayUi.RefreshNow();
                root.CameraController.enabled = false;
                var focus = marker.BiomeWorld.BaseRegion.ProjectToGround(new Vector3(6f, 0f, 3f));
                Frame(focus, 34f, 57f, 58f);
            });

            if (string.IsNullOrEmpty(result.error))
            {
                yield return new WaitForSecondsRealtime(2.5f);
                yield return new WaitForEndOfFrame();
                Try(result, () =>
                {
                    result.rts_screenshot = Capture("p94-rts-work.png");
                    result.rts_rendered = File.Exists(result.rts_screenshot);
                    root.GameplayUi.RootVisual.style.display = DisplayStyle.None;
                    // The simulation is already paused. Stop its instanced low-detail draw pass so the clean TPS
                    // evidence frame shows the authored settlement rather than aggregate crowd diagnostics.
                    root.AiRuntime.enabled = false;
                    var enter = root.ThirdPersonControl.TryEnter(root.MaleDemoCharacter.Id);
                    if (!enter.IsEligible) throw new InvalidOperationException("TPS entry failed in the P9.4 settlement.");
                });
            }
            if (string.IsNullOrEmpty(result.error))
            {
                yield return new WaitForSecondsRealtime(1.5f);
                yield return new WaitForEndOfFrame();
                Try(result, () =>
                {
                    result.tps_screenshot = Capture("p94-tps-settlement.png");
                    result.tps_rendered = root.ThirdPersonControl.IsThirdPersonCameraActive &&
                        File.Exists(result.tps_screenshot);
                    root.ThirdPersonControl.Exit();
                });
            }

            result.passed = string.IsNullOrEmpty(result.error) && result.settlement_assets &&
                result.worksite_readability && result.resource_interaction && result.gather_order &&
                result.haul_order && result.building_order && result.priority_indicator &&
                result.save_load_identity && result.navigation && result.weather_season &&
                result.rts_rendered && result.tps_rendered;
            var resultPath = Path.Combine(outputDirectory, "p94-player-smoke.json");
            File.WriteAllText(resultPath, JsonUtility.ToJson(result, true));
            Debug.Log("P94_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + resultPath);
            Application.Quit(result.passed ? 0 : 2);
        }

        private void Assign(StableEntityId worker, WorkGroupRole role)
        {
            var group = root.DirectWorkCommands.EnsureRoleGroups().Single(x => x.Role == role);
            if (root.Work.Groups.TryGetForMember(worker, out var prior) && prior.Id != group.Id)
                root.Work.RemoveMember(prior.Id, worker);
            if (!group.Contains(worker)) root.Work.AddMember(group.Id, worker);
        }

        private void CancelOpenOrders()
        {
            foreach (var order in root.Work.Jobs.Where(x => !x.IsTerminal).ToArray()) root.Work.Cancel(order.Id);
        }

        private void CompleteTechnology(string id)
        {
            var technologyId = new TechnologyId(id);
            if (root.Research.IsCompleted(technologyId)) return;
            root.Research.TryAssignWorker(root.MaleDemoCharacter.Id);
            if (!root.Research.Start(technologyId))
                throw new InvalidOperationException("Research could not start: " + id);
            root.Research.AdvanceWorkerTo(root.MaleDemoCharacter.Id,
                root.Research.LastProcessedTick + root.Technologies.Get(technologyId).ResearchWorkRequiredTicks, true);
        }

        private void Frame(Vector3 target, float yaw, float pitch, float distance)
        {
            root.CameraController.transform.SetPositionAndRotation(target, Quaternion.Euler(0f, yaw, 0f));
            root.CameraController.CameraMount.localPosition = Quaternion.Euler(pitch, 0f, 0f) * Vector3.back * distance;
            root.CameraController.CameraMount.localRotation = Quaternion.Euler(pitch, 0f, 0f);
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
            for (var index = 0; index < args.Length - 1; index++)
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
            return null;
        }
    }
}
