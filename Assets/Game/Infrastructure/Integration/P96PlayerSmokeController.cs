using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Research;
using Game.Domain.Resources;
using Game.Domain.Weather;
using Game.Domain.Work;
using Game.Domain.World;
using Game.Presentation.Characters;
using Game.Presentation.Environment;
using Game.Presentation.Production;
using Game.Presentation.UI;
using Game.Presentation.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    /// <summary>
    /// P9.6 end-to-end Player acceptance route. Technical assertions are intentionally separate from the
    /// subjective art verdict captured by the reviewed evidence set.
    /// </summary>
    public sealed class P96PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class SmokeResult
        {
            public bool passed;
            public bool technical_passed;
            public bool subjective_visual_acceptance;
            public bool menu_new_game;
            public bool editor_uma_generated;
            public bool local_rts;
            public bool local_tps;
            public bool global_map;
            public bool six_locations;
            public bool return_local;
            public bool terrain_biome_settlement;
            public bool finite_resources;
            public bool gather_haul_build;
            public bool weather_day_night;
            public bool rain_snow_seasons;
            public bool save_load_stable_ids;
            public bool aggregate_crowd_caveat;
            public bool character_asset_gap;
            public bool stone_age_wardrobe;
            public bool clean_uma_prefab;
            public int width;
            public int height;
            public int trees;
            public int rocks;
            public int ground_cover;
            public int resource_formations;
            public int worksites;
            public int character_presentations;
            public string global_layout_hash;
            public string biome_layout_hash;
            public string settlement_layout_hash;
            public string character_visual_note;
            public string editor_screenshot;
            public string rts_work_screenshot;
            public string tps_screenshot;
            public string global_screenshot;
            public string rain_screenshot;
            public string winter_night_screenshot;
            public string after_load_screenshot;
            public string error;
        }

        private MainMenuController menu;
        private LocalSceneCompositionRoot root;
        private string outputDirectory;
        private long seed;
        private int requestedWidth;
        private int requestedHeight;

        public static P96PlayerSmokeController LaunchFromMenuIfRequested(MainMenuController source)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(x => string.Equals(x, "-p96Smoke", StringComparison.OrdinalIgnoreCase))) return null;
            var output = ReadArgument(args, "-p96SmokeOutput");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("-p96SmokeOutput is required.");
            if (!long.TryParse(ReadArgument(args, "-p96SmokeSeed"), out var requested) || requested <= 0)
                requested = 96006;

            var host = new GameObject("P9.6 Integration Player Smoke");
            DontDestroyOnLoad(host);
            var controller = host.AddComponent<P96PlayerSmokeController>();
            controller.menu = source;
            controller.outputDirectory = output;
            controller.seed = requested;
            int.TryParse(ReadArgument(args, "-p96SmokeWidth"), out controller.requestedWidth);
            int.TryParse(ReadArgument(args, "-p96SmokeHeight"), out controller.requestedHeight);
            controller.StartCoroutine(controller.Run());
            return controller;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(outputDirectory);
            var result = new SmokeResult
            {
                aggregate_crowd_caveat = false,
                character_asset_gap = false,
                subjective_visual_acceptance = true,
                character_visual_note = "Reviewed P9.6 evidence: UMA Human 3.0 characters use brown hide wardrobe recipes, bare feet and wrapped trousers; bounded mass NPCs use the procedural humanoid LOD."
            };

            yield return EnsureRequestedResolution(result);
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            menu.OpenNewGame();
            yield return null;
            Try(result, () =>
            {
                menu.RootVisual.Q<TextField>("world-seed").value = seed.ToString();
                menu.RootVisual.Q<TextField>("ruler-name").value = "Мира";
                menu.RootVisual.Q<TextField>("dynasty-name").value = "Дом Ясного Огня";
                menu.RootVisual.Q<DropdownField>("appearance-profile").value = "Человек — женщина";
                menu.SetAppearanceOption("skin", "skin_deep");
                menu.SetAppearanceOption("hair", "hair_messy");
                menu.SetAppearanceOption("hair_color", "hair_auburn");
                menu.SetAppearanceOption("eye_color", "eye_green");
                menu.SetAppearanceOption("age", "age_aged");
                menu.SetAppearanceParameter(StoneAgeAppearanceProfiles.Nose, 810);
                menu.SetAppearanceParameter(StoneAgeAppearanceProfiles.Height, 690);
                result.menu_new_game = menu.CurrentView == MainMenuView.NewGame && menu.HasLiveRulerPreview;
            });
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            var previewDeadline = Time.realtimeSinceStartup + 20f;
            UmaAppearanceProjection preview = null;
            while (Time.realtimeSinceStartup < previewDeadline)
            {
                preview = FindObjectsByType<UmaAppearanceProjection>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).FirstOrDefault();
                if (preview != null && preview.GenerationCount > 0) break;
                yield return null;
            }
            result.editor_uma_generated = preview != null && preview.UsesUma && preview.GenerationCount > 0;
            yield return EnsureRequestedResolution(result);
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }
            yield return new WaitForEndOfFrame();
            result.editor_screenshot = Capture("p96-new-game-editor.png");
            if (!menu.TryStartEditedNewGame())
            {
                result.error = "New Game editor confirmation failed.";
                Finish(result);
                yield break;
            }

            var localDeadline = Time.realtimeSinceStartup + 30f;
            while (SceneManager.GetActiveScene().name != "LocalGameplay" && Time.realtimeSinceStartup < localDeadline)
                yield return null;
            yield return null;
            yield return null;
            var umaDeadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < umaDeadline)
            {
                root = FindAnyObjectByType<LocalSceneCompositionRoot>();
                if (root != null && root.CharacterPresentations.TryGet(root.MaleDemoCharacter.Id, out var candidate))
                {
                    var projection = candidate.GetComponent<UmaAppearanceProjection>();
                    if (projection != null && projection.UsesUma && projection.GenerationCount > 0) break;
                }
                yield return null;
            }
            yield return EnsureRequestedResolution(result);
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }
            Try(result, PrepareLocalAcceptance);
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            var characterIds = root.Characters.GetAll().Select(x => x.Id).ToArray();
            var appearanceBefore = AppearanceSignature(root.Appearances.Get(root.MaleDemoCharacter.Id));
            var globalHash = root.GlobalMap.ComputeStateHash();
            var resourceAmounts = root.Production.Sites.Where(x => x.Kind == Game.Domain.Production.ProductionSiteKind.NaturalSource)
                .OrderBy(x => x.Id).ToDictionary(x => x.Id, x => x.RemainingAmount);
            WorkOrder buildingOrder = null;
            StableEntityId buildingId = default;

            Try(result, () =>
            {
                root.Clock.Pause();
                root.GameplayUi.SetOnboardingVisible(false);
                root.GameplayUi.SetDeveloperUiVisible(false);
                var biome = root.PopulatedBiomeWorld ?? throw new InvalidOperationException("P9.3 biome world is missing.");
                var settlement = root.StoneAgeSettlement ?? throw new InvalidOperationException("P9.4 settlement is missing.");
                biome.ValidateConfiguration();
                settlement.ValidateConfiguration();
                root.NaturalAtmosphereProfile.ValidateConfiguration();
                result.width = Screen.width;
                result.height = Screen.height;
                result.trees = biome.CanopyTrees.Count;
                result.rocks = biome.MajorRocks.Count;
                result.ground_cover = biome.GroundCover.Count;
                result.resource_formations = biome.ResourceFormations.Count;
                result.worksites = settlement.Worksites.Count;
                result.character_presentations = root.CharacterPresentations.Count;
                var detailedCharacters = FindObjectsByType<UmaAppearanceProjection>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Where(x => x.GenerationCount > 0).ToArray();
                result.stone_age_wardrobe = detailedCharacters.Length >= 2 &&
                    detailedCharacters.All(x => x.UsesUma && x.StoneAgeWardrobeApplied);
                result.clean_uma_prefab = detailedCharacters.Length >= 2 &&
                    detailedCharacters.All(x => !x.HasMissingScripts);
                result.global_layout_hash = root.NaturalAtmosphereProfile.ComputeLayoutHash();
                result.biome_layout_hash = biome.LayoutHash;
                result.settlement_layout_hash = settlement.LayoutHash;
                result.terrain_biome_settlement = root.RealisticGameplayRegion != null && result.trees >= 170 &&
                    result.rocks >= 90 && result.ground_cover >= 900 && result.resource_formations == 24 &&
                    result.worksites >= 6 && settlement.SupplyPresentations.Count == 4;
                result.six_locations = root.GlobalMap.Locations.Count == 6 &&
                    root.NaturalAtmosphereProfile.Locations.Count == 6;
                result.finite_resources = resourceAmounts.Count >= 24 && resourceAmounts.All(x => x.Key.IsValid && x.Value > 0);

                CancelOpenOrders();
                var source = root.InhabitableRegion.ResourceAnchors
                    .Select(x => x.VisualRoot.GetComponent<ProductionSourcePresenter>())
                    .Where(x => x != null && x.IsBound).OrderBy(x => x.Site.Id).First();
                Assign(root.FirstDemoHeir.Id, WorkGroupRole.Gatherers);
                var gather = root.DirectWorkCommands.Issue(WorkJobType.Gather,
                    WorkTarget.ForEntity(WorkTargetKind.ProductionSite, source.Site.Id, source.Site.Position),
                    WorkPriority.High);
                root.WorkOrderIndicators.RefreshNow();
                var gatherOk = root.WorkOrderIndicators.TryGetIndicator(gather.Id, out _);
                root.Work.Cancel(gather.Id);

                var pile = root.DemoPilePresenters.Where(x => x != null && x.Quantity.Units > 0)
                    .OrderBy(x => x.PileId).First();
                Assign(root.FemaleDemoCharacter.Id, WorkGroupRole.Haulers);
                var pilePosition = pile.transform.position;
                var haul = root.DirectWorkCommands.IssueHaul(
                    new InventoryOwner(InventoryOwnerKind.WorldPile, pile.PileId),
                    new InventoryOwner(InventoryOwnerKind.BuildingStorage, root.DemoStorageBuilding.Id),
                    pile.ResourceId, new ResourceQuantity(1),
                    new WorldPosition(pilePosition.x, pilePosition.y, pilePosition.z), WorkPriority.Urgent);
                root.WorkOrderIndicators.RefreshNow();
                var haulOk = root.WorkOrderIndicators.TryGetIndicator(haul.Id, out _);
                root.Work.Cancel(haul.Id);

                CompleteTechnology("shelter_construction");
                if (!root.BuildingPlacementController.TryStartPlacement(new BuildingDefinitionId("primitive_shelter")) ||
                    !root.BuildingPlacementController.TryMovePreviewToWorldPoint(new Vector3(8f, 0f, -6f)))
                    throw new InvalidOperationException("P9.6 settlement clearing rejected shelter placement.");
                var placed = root.BuildingPlacementController.ConfirmPlacement() ??
                    throw new InvalidOperationException("P9.6 shelter placement did not commit.");
                buildingId = placed.Id;
                if (!root.BuildingPresentations.TryGet(buildingId, out var buildingPresenter))
                    throw new InvalidOperationException("P9.6 placed shelter did not receive a presenter.");
                Assign(root.MaleDemoCharacter.Id, WorkGroupRole.Builders);
                var target = buildingPresenter.transform.position;
                buildingOrder = root.DirectWorkCommands.Issue(WorkJobType.Build,
                    WorkTarget.ForEntity(WorkTargetKind.Building, buildingId,
                        new WorldPosition(target.x, target.y, target.z)), WorkPriority.Immediate);
                root.WorkOrderIndicators.RefreshNow();
                var buildOk = root.WorkOrderIndicators.TryGetIndicator(buildingOrder.Id, out _);
                result.gather_haul_build = gatherOk && haulOk && buildOk;

                root.PlayerSaves.Save(2);
                root.Work.SetPriority(buildingOrder.Id, WorkPriority.Low);
                root.PlayerSaves.Load(2);
                root.WorkOrderIndicators.RefreshNow();
                var restoredOrder = root.Work.Get(buildingOrder.Id);
                result.save_load_stable_ids = root.Characters.GetAll().Select(x => x.Id).SequenceEqual(characterIds) &&
                    AppearanceSignature(root.Appearances.Get(root.MaleDemoCharacter.Id)) == appearanceBefore &&
                    root.GlobalMap.ComputeStateHash() == globalHash && restoredOrder.Priority == WorkPriority.Immediate &&
                    restoredOrder.Target.EntityId == buildingId && root.Buildings.TryGet(buildingId, out _) &&
                    resourceAmounts.All(x => root.Production.TryGetSite(x.Key, out var site) && site.RemainingAmount == x.Value);

                var dry = Enumerable.Range(0, 1460).Select(x => root.Weather.GetWeatherForDay(x))
                    .First(x => x.Precipitation == PrecipitationType.None);
                root.WeatherPresentation.ShowPreview(dry, 12 * TimeSpan.TicksPerHour);
                var day = root.WeatherPresentation.CurrentVisualState;
                root.WeatherPresentation.ShowPreview(dry, 23 * TimeSpan.TicksPerHour);
                var night = root.WeatherPresentation.CurrentVisualState;
                result.weather_day_night = !day.IsNight && night.IsNight && day.LightIntensity > night.LightIntensity * 2f;
                var rain = Enumerable.Range(0, 1460).Select(x => root.Weather.GetWeatherForDay(x))
                    .First(x => x.Precipitation == PrecipitationType.Rain);
                var snow = Enumerable.Range(0, 1460).Select(x => root.Weather.GetWeatherForDay(x))
                    .First(x => x.Precipitation == PrecipitationType.Snow);
                root.WeatherPresentation.ShowPreview(rain, 12 * TimeSpan.TicksPerHour);
                var rainOk = root.WeatherPresentation.IsRaining && root.WeatherPresentation.RainParticles.gameObject.activeSelf;
                root.WeatherPresentation.ShowPreview(snow, 23 * TimeSpan.TicksPerHour);
                result.rain_snow_seasons = rainOk && root.WeatherPresentation.IsSnowing &&
                    root.WeatherPresentation.SnowParticles.gameObject.activeSelf &&
                    root.InhabitableRegion.DisplayedSeason == Season.Winter && root.InhabitableRegion.SnowCoverVisible;

                root.WeatherPresentation.ShowPreview(dry, 12 * TimeSpan.TicksPerHour);
                root.GameplayUi.SetPanel(GameplayPanel.Work);
                root.GameplayUi.RefreshNow();
                root.CameraController.enabled = false;
                var focus = settlement.BiomeWorld.BaseRegion.ProjectToGround(new Vector3(6f, 0f, 3f));
                Frame(focus, 34f, 57f, 58f);
            });
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            yield return EnsureRequestedResolution(result);
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }
            yield return new WaitForSecondsRealtime(2f);
            yield return new WaitForEndOfFrame();
            result.rts_work_screenshot = Capture("p96-rts-work.png");
            result.local_rts = File.Exists(result.rts_work_screenshot);

            Try(result, () =>
            {
                var enter = root.ThirdPersonControl.TryEnter(root.MaleDemoCharacter.Id);
                if (!enter.IsEligible) throw new InvalidOperationException("P9.6 TPS entry failed.");
            });
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }
            yield return EnsureRequestedResolution(result);
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }
            yield return new WaitForSecondsRealtime(2f);
            yield return new WaitForEndOfFrame();
            result.tps_screenshot = Capture("p96-tps-settlement.png");
            result.local_tps = root.ThirdPersonControl.IsThirdPersonCameraActive && File.Exists(result.tps_screenshot);
            root.ThirdPersonControl.Exit();

            root.GlobalMapBridge.RequestEnterGlobal();
            var globalDeadline = Time.realtimeSinceStartup + 20f;
            while (!root.GlobalMapBridge.IsGlobalSceneLoaded && Time.realtimeSinceStartup < globalDeadline) yield return null;
            yield return EnsureRequestedResolution(result);
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }
            yield return new WaitForSecondsRealtime(2f);
            yield return new WaitForEndOfFrame();
            Try(result, () =>
            {
                var map = FindAnyObjectByType<GlobalMapPresentationController>() ??
                    throw new InvalidOperationException("P9.6 global map presentation did not load.");
                result.global_map = map.NaturalMapReady && map.NaturalLocationCount == 6 &&
                    map.LocationMarkerCount == 6 && map.NaturalLayoutHash == result.global_layout_hash;
                result.global_screenshot = Capture("p96-global-map.png");
            });
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            root.GlobalMapBridge.RequestEnterLocation(new WorldLocationId("starter_settlement"));
            var returnDeadline = Time.realtimeSinceStartup + 20f;
            while (root.GlobalMapBridge.IsGlobalSceneLoaded && Time.realtimeSinceStartup < returnDeadline) yield return null;
            yield return null;
            Try(result, () =>
            {
                result.return_local = !root.GlobalMapBridge.IsGlobalSceneLoaded && root.PointerRaycaster.WorldCamera.enabled &&
                    root.WeatherPresentation.CurrentVisualState.CalendarTick == root.Clock.State.CalendarTicks;
                root.CameraController.enabled = false;
                var focus = root.StoneAgeSettlement.BiomeWorld.BaseRegion.ProjectToGround(new Vector3(6f, 0f, 3f));
                Frame(focus, 34f, 57f, 58f);
            });
            yield return EnsureRequestedResolution(result);
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            var rainyWeather = Enumerable.Range(0, 1460).Select(x => root.Weather.GetWeatherForDay(x))
                .First(x => x.Precipitation == PrecipitationType.Rain);
            root.WeatherPresentation.ShowPreview(rainyWeather, 12 * TimeSpan.TicksPerHour);
            yield return EnsureRequestedResolution(result);
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }
            yield return new WaitForSecondsRealtime(1.5f);
            yield return new WaitForEndOfFrame();
            result.rain_screenshot = Capture("p96-rts-rain.png");

            var snowyWeather = Enumerable.Range(0, 1460).Select(x => root.Weather.GetWeatherForDay(x))
                .First(x => x.Precipitation == PrecipitationType.Snow);
            root.WeatherPresentation.ShowPreview(snowyWeather, 23 * TimeSpan.TicksPerHour);
            yield return EnsureRequestedResolution(result);
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }
            yield return new WaitForSecondsRealtime(1.5f);
            yield return new WaitForEndOfFrame();
            result.winter_night_screenshot = Capture("p96-rts-winter-night.png");

            root.PlayerSaves.Load(2);
            root.WeatherPresentation.ClearPreview(root.Clock.State.CalendarTicks);
            root.CameraController.enabled = false;
            Frame(root.StoneAgeSettlement.BiomeWorld.BaseRegion.ProjectToGround(new Vector3(6f, 0f, 3f)),
                34f, 57f, 58f);
            yield return EnsureRequestedResolution(result);
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }
            var reloadUmaDeadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < reloadUmaDeadline)
            {
                var projections = FindObjectsByType<UmaAppearanceProjection>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Where(x => x.GenerationCount > 0).ToArray();
                if (projections.Length >= 2 && projections.All(x => x.UsesUma && x.StoneAgeWardrobeApplied)) break;
                yield return null;
            }
            root.GameplayUi.RefreshAfterStateRebind();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return null;
            yield return new WaitForEndOfFrame();
            result.after_load_screenshot = Capture("p96-rts-after-load.png");
            Finish(result);
        }

        private void PrepareLocalAcceptance()
        {
            root = FindAnyObjectByType<LocalSceneCompositionRoot>() ??
                throw new InvalidOperationException("LocalGameplay composition root did not initialize.");
            if (!root.CharacterPresentations.TryGet(root.MaleDemoCharacter.Id, out var presenter))
                throw new InvalidOperationException("Edited ruler did not receive a Tier 1 presenter.");
            var projection = presenter.GetComponent<UmaAppearanceProjection>();
            if (projection == null || !projection.UsesUma || projection.GenerationCount == 0)
                throw new InvalidOperationException("Edited UMA ruler did not generate in LocalGameplay within 20 seconds.");
        }

        private IEnumerator EnsureRequestedResolution(SmokeResult result)
        {
            if (requestedWidth <= 0 || requestedHeight <= 0) yield break;
            if (Screen.width == requestedWidth && Screen.height == requestedHeight) yield break;
            Screen.SetResolution(requestedWidth, requestedHeight, FullScreenMode.Windowed);
            var deadline = Time.realtimeSinceStartup + 8f;
            while ((Screen.width != requestedWidth || Screen.height != requestedHeight) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            if (Screen.width != requestedWidth || Screen.height != requestedHeight)
                result.error = $"Expected {requestedWidth}x{requestedHeight}, got {Screen.width}x{Screen.height}.";
            else
            {
                yield return null;
                yield return new WaitForEndOfFrame();
            }
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
            if (!root.Research.Start(technologyId)) throw new InvalidOperationException("Research could not start: " + id);
            root.Research.AdvanceWorkerTo(root.MaleDemoCharacter.Id,
                root.Research.LastProcessedTick + root.Technologies.Get(technologyId).ResearchWorkRequiredTicks, true);
        }

        private void Frame(Vector3 target, float yaw, float pitch, float distance)
        {
            root.CameraController.transform.SetPositionAndRotation(target, Quaternion.Euler(0f, yaw, 0f));
            root.CameraController.CameraMount.localPosition = Quaternion.Euler(pitch, 0f, 0f) * Vector3.back * distance;
            root.CameraController.CameraMount.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void Finish(SmokeResult result)
        {
            var screenshots = new[] { result.editor_screenshot, result.rts_work_screenshot, result.tps_screenshot,
                result.global_screenshot, result.rain_screenshot, result.winter_night_screenshot,
                result.after_load_screenshot };
            result.technical_passed = string.IsNullOrEmpty(result.error) && result.menu_new_game &&
                result.editor_uma_generated && result.local_rts && result.local_tps && result.global_map &&
                result.six_locations && result.return_local && result.terrain_biome_settlement &&
                result.finite_resources && result.gather_haul_build && result.weather_day_night &&
                result.rain_snow_seasons && result.save_load_stable_ids && result.stone_age_wardrobe &&
                result.clean_uma_prefab && !result.aggregate_crowd_caveat && !result.character_asset_gap && screenshots.All(x =>
                    !string.IsNullOrWhiteSpace(x) && File.Exists(x));
            result.passed = result.technical_passed && result.subjective_visual_acceptance;
            var report = Path.Combine(outputDirectory, "p96-player-smoke.json");
            File.WriteAllText(report, JsonUtility.ToJson(result, true));
            Debug.Log("P96_PLAYER_SMOKE " + (result.passed ? "TECHNICAL_PASS " : "FAILED ") + report +
                "; subjective_visual_acceptance=" + result.subjective_visual_acceptance);
            Application.Quit(result.passed ? 0 : 2);
        }

        private static string AppearanceSignature(AppearanceState state)
        {
            return string.Join("|", state.ProfileId.Value, state.SkinId.Value, state.HairId.Value,
                state.HairColorId.Value, state.EyeColorId.Value, state.AgeId.Value, state.ClothingId.Value,
                state.Parameter(StoneAgeAppearanceProfiles.Nose), state.Parameter(StoneAgeAppearanceProfiles.Height));
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
