using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Diagnostics;
using Game.Domain.Military;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Presentation.Buildings;
using Game.Presentation.Characters;
using Game.Presentation.Production;
using Game.Simulation.Military;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    /// <summary>
    /// Opt-in real Player route for the 2026-10-29 user retest. It exercises the reported paths through
    /// their normal runtime services and produces readable screenshots; it owns no game state or saves.
    /// </summary>
    public sealed class WM15UserRetestPlayerSmoke : MonoBehaviour
    {
        private readonly List<string> checks = new();
        private readonly List<string> errors = new();
        private string output;
        private float deadline;
        private bool finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-wm15UserRetest");
            if (index < 0 || index + 1 >= args.Length) return;
            Application.runInBackground = true;
            var host = new GameObject("WM15 user retest Player smoke");
            DontDestroyOnLoad(host);
            var runner = host.AddComponent<WM15UserRetestPlayerSmoke>();
            runner.output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(runner.output);
            runner.deadline = Time.realtimeSinceStartup + 900f;
            Application.logMessageReceived += runner.OnLog;
            runner.StartCoroutine(runner.Run());
        }

        private void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errors.Add(message + "\n" + stack);
            File.WriteAllLines(Path.Combine(output, "runtime-errors.txt"), errors);
        }

        private void Update()
        {
            if (!finished && Time.realtimeSinceStartup > deadline)
            {
                Check(false, "Player route finished before the timeout");
                Finish();
            }
        }

        private IEnumerator Run()
        {
            MainMenuController menu;
            while ((menu = FindAnyObjectByType<MainMenuController>()) == null) yield return null;
            menu.StartNewGame(1380062136);

            LocalSceneCompositionRoot root;
            while ((root = FindAnyObjectByType<LocalSceneCompositionRoot>()) == null || !root.IsInitialized)
                yield return null;
            root.Clock.Pause();
            root.GameplayUi.SetOnboardingVisible(false);
            root.Diagnostics.Execute(DiagnosticCommandKind.UnlockAll);
            var resources = root.Diagnostics.Execute(DiagnosticCommandKind.GrantResources);
            Check(resources.Succeeded, "Diagnostic resources are available for training and construction");

            var house = root.BuildingCatalog.ResolveDefinition(new BuildingDefinitionId("clan_house"));
            Check(house.Housing != null && house.Housing.ResidentCapacity == 6,
                "Stone house has six resident places");

            yield return ShoreBuilding(root, "fishing_camp", "fishing-camp");
            yield return ShoreBuilding(root, "fishing_pier", "fishing-pier");
            yield return PastureConstruction(root);
            yield return Military(root);
            yield return Lake(root);

            Check(errors.Count == 0, "No runtime errors occurred during the retest route");
            Finish();
        }

        private IEnumerator ShoreBuilding(LocalSceneCompositionRoot root, string definitionId, string imageName)
        {
            var placement = root.BuildingPlacementController;
            placement.enabled = false;
            Building placed = null;
            BuildingPresenter presenter = null;
            Check(placement.TryStartPlacement(new BuildingDefinitionId(definitionId)),
                definitionId + " is unlocked and enters placement mode");

            foreach (var lake in root.WorldDefinition.MapData.Hydrology.Lakes)
            {
                foreach (var shore in lake.Shoreline.Points.Where((_, index) => index % 2 == 0))
                {
                    var bank = root.WorldDefinition.MapData.CoordinateTransform.ToUnity(shore);
                    var point = new Vector3(bank.X, bank.Y, bank.Z);
                    root.CameraController.FocusPosition(point, 42f);
                    root.TiledWorld.RefreshNow(new WorldPosition(point.x, point.y, point.z),
                        Array.Empty<WorldPosition>(), Array.Empty<Tuple<WorldPosition, WorldPosition>>());
                    yield return null;
                    var retryUntil = Time.realtimeSinceStartup + .8f;
                    while (!placement.TryMovePreviewToWorldPoint(point) &&
                           Time.realtimeSinceStartup < retryUntil) yield return null;
                    if (!placement.IsPreviewValid || placement.PreviewClearanceSourceIds.Count > 0) continue;
                    placed = placement.ConfirmPlacement();
                    if (placed == null) continue;
                    root.BuildingPresentations.TryGet(placed.Id, out presenter);
                    break;
                }
                if (placed != null) break;
            }
            placement.CancelPlacement();
            Check(placed != null, definitionId + " has a valid real shoreline construction position");
            if (placed == null) { placement.enabled = true; yield break; }

            var builders = root.Work.Groups.All.Single(x => x.Role == WorkGroupRole.Builders);
            var workerIds = root.Characters.GetAll().Where(x => root.CanCommandResident(x.Id) &&
                    !root.MilitaryTraining.GetRank(x.Id).HasValue && root.CharacterPresentations.TryGet(x.Id, out _))
                .Select(x => x.Id).Take(3).ToArray();
            foreach (var id in workerIds) if (!builders.Contains(id)) root.Work.AddMember(builders.Id, id);
            var workerViews = workerIds.Select(id =>
            {
                root.CharacterPresentations.TryGet(id, out var view); return view;
            }).Where(x => x != null).ToArray();
            if (presenter != null && workerViews.Length > 0)
            {
                var approach = presenter.transform.position;
                if (NavMesh.SamplePosition(approach, out var hit, 24f, NavMesh.AllAreas))
                {
                    foreach (var workerId in workerIds)
                    {
                        var movement = root.AiRuntime.TryGetMovement(workerId, out var adapter) ? adapter : null;
                        var agent = movement?.Presenter.GetComponent<NavMeshAgent>();
                        if (agent != null && agent.isOnNavMesh) agent.Warp(hit.position);
                        if (root.AiAgents.TryGet(workerId, out var state))
                            state.UpdatePosition(new WorldPosition(hit.position.x, hit.position.y, hit.position.z));
                    }
                }
                root.SelectionProbe.SelectCharacters(workerViews);
                root.GameplayUi.OpenContextMenu(new ContextCommandTarget(ContextTargetKind.Building,
                    new WorldPosition(approach.x, approach.y, approach.z), placed.Id), new Vector2(640, 360));
                var hasAction = root.GameplayUi.ContextActionStates.TryGetValue(ContextActionKind.Build, out var action);
                Check(hasAction && action.Enabled,
                    definitionId + " exposes a reachable Build order" + (hasAction ? ": " + action.Reason : string.Empty));
                root.GameplayUi.CloseContextMenu();
            }
            else Check(false, definitionId + " presenter and builders materialized");

            root.CameraController.FocusPosition(presenter != null ? presenter.transform.position : Vector3.zero, 28f);
            yield return Capture(root, imageName + ".png");
            placement.enabled = true;
        }

        private IEnumerator PastureConstruction(LocalSceneCompositionRoot root)
        {
            var placement = root.BuildingPlacementController;
            placement.enabled = false;
            var origin = new Vector3(root.WorldDefinition.PlayerStart.X, 0f, root.WorldDefinition.PlayerStart.Z);
            Building pasture = null;
            placement.TryStartPlacement(new BuildingDefinitionId("animal_pasture"));
            for (var radius = 20; radius <= 60 && pasture == null; radius += 8)
            for (var step = 0; step < 24 && pasture == null; step++)
            {
                var angle = step * Mathf.PI * 2f / 24f;
                var point = origin + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                root.CameraController.FocusPosition(point, 35f);
                yield return null;
                if (placement.TryMovePreviewToWorldPoint(point) && placement.PreviewClearanceSourceIds.Count == 0)
                    pasture = placement.ConfirmPlacement();
            }
            placement.CancelPlacement();
            Check(pasture != null, "Pasture construction has a valid dry footprint");
            if (pasture != null && root.BuildingPresentations.TryGet(pasture.Id, out var view))
            {
                yield return null;
                var crop = view.GetComponent<CropPlotPresentation>();
                var generatedField = view.transform.Find("Cultivated field");
                Check(crop != null && generatedField != null && !generatedField.gameObject.activeSelf,
                    "Unfinished pasture does not render the completed crop field");
                Check(!view.GetComponentsInChildren<Transform>(true).Any(x =>
                        x.gameObject.activeInHierarchy && x.name == "Growing plant"),
                    "Unfinished pasture has no visible crop plants");
                root.CameraController.FocusPosition(view.transform.position, 28f);
                yield return Capture(root, "pasture-construction.png");
            }
            placement.enabled = true;
        }

        private IEnumerator Military(LocalSceneCompositionRoot root)
        {
            var ruler = root.PlayerRealm.CurrentRulerId;
            Check(ruler.HasValue && root.MilitaryTraining.GetRank(ruler.Value) == MilitaryRank.Generalissimo,
                "The reigning ruler is the Generalissimo");
            var before = root.MilitaryTraining.Records.Count;
            var diagnostic = root.Diagnostics.Execute(DiagnosticCommandKind.CreateArmy);
            Check(diagnostic.Succeeded && diagnostic.AffectedCount == 9,
                "Army cheat creates the nine non-ruler ranks");
            yield return new WaitForSecondsRealtime(1f);
            var graduates = root.MilitaryTraining.Records.Skip(before).ToArray();
            Check(graduates.All(x => x.Rank != MilitaryRank.Generalissimo),
                "Army cheat never creates another Generalissimo");
            Check(graduates.All(x => root.Characters.Get(x.CharacterId).ProfessionId.HasValue &&
                    root.Characters.Get(x.CharacterId).ProfessionId.Value.Equals(new ProfessionId("military")) &&
                    root.Appearances.Get(x.CharacterId).ClothingId == StoneAgeAppearanceProfiles.MilitaryClothing),
                "Every diagnostic graduate has military profession and clothing");
            Check(graduates.All(x => root.Work.Groups.All.Where(group => group.Role != WorkGroupRole.Warriors)
                    .All(group => !group.Contains(x.CharacterId))) &&
                  graduates.All(x => root.Work.Groups.All.Single(group => group.Role == WorkGroupRole.Warriors)
                    .Contains(x.CharacterId)),
                "Diagnostic soldiers are excluded from civilian work and assigned to the military group");
            Check(graduates.All(x => MilitaryRankNames.Russian(x.Rank) != x.Rank.ToString()),
                "Every diagnostic rank has a Russian display name");

            var visibleArmour = 0;
            var colouredArmour = 0;
            var visibleGraduates = new List<CharacterPresenter>();
            foreach (var graduate in graduates)
            {
                if (!root.CharacterPresentations.TryGet(graduate.CharacterId, out var view)) continue;
                visibleGraduates.Add(view);
                var armour = view.transform.Find("RoleVisual");
                var authoredSuit = armour == null ? Array.Empty<Renderer>() :
                    armour.GetComponentsInChildren<Renderer>(true).Where(renderer => renderer.enabled &&
                        (renderer.name.IndexOf("Armor_01_A", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         renderer.name.EndsWith("Armor_head_01", StringComparison.OrdinalIgnoreCase))).ToArray();
                if (authoredSuit.Length > 0)
                {
                    visibleArmour++;
                    var block = new MaterialPropertyBlock();
                    authoredSuit[0].GetPropertyBlock(block);
                    var steel = block.GetColor(Shader.PropertyToID("_METAL2COLOR"));
                    if (steel.a > .9f && steel.maxColorComponent < .8f) colouredArmour++;
                }
            }
            Check(visibleArmour == graduates.Length,
                "All nine diagnostic soldiers visibly use the authored armour suit");
            Check(colouredArmour == graduates.Length,
                "All nine authored armour suits use an opaque non-white military palette");
            if (visibleGraduates.Count > 0)
            {
                var center = visibleGraduates.Aggregate(Vector3.zero, (sum, view) => sum + view.transform.position) /
                             visibleGraduates.Count;
                root.CameraController.FocusPosition(center, 24f);
                yield return Capture(root, "military-armour.png");
            }

            var trainee = root.Characters.GetAll().First(x => root.CanCommandResident(x.Id) && x.Id != ruler &&
                !root.MilitaryTraining.GetRank(x.Id).HasValue && root.HealthFamily.GetCapabilities(x.Id).IsAdult);
            var funding = root.Inventories.All.Where(x => x.Owner.Kind == InventoryOwnerKind.BuildingStorage)
                .Select(x => x.Owner).ToArray();
            var start = root.MilitaryTraining.TryStart(trainee.Id, root.DemoBarracksBuilding.Id,
                new CombatSideId("player"), root.Clock.State.CalendarTicks, funding);
            Check(start.Started && start.Record.Rank == MilitaryRank.Private,
                "A civilian starts the Private course in the barracks");
            yield return null;
            root.SelectionProbe.SelectBuilding(root.DemoBarracksPresenter);
            root.GameplayUi.RefreshNow();
            var labels = root.GameplayUi.RootVisual.Query<Label>().ToList().Select(x => x.text ?? string.Empty).ToArray();
            var traineeCard = root.GameplayUi.RootVisual.Q<Button>("barracks-trainee-" + trainee.Id);
            Check(labels.Any(x => x.Contains("В казарме: 1/6")) && traineeCard != null &&
                  traineeCard.text.Contains("Рядовой"),
                "Barracks card lists the trainee and the target rank");
            root.SelectionProbe.SelectCharacterIds(new[] { trainee.Id });
            root.GameplayUi.RefreshNow();
            labels = root.GameplayUi.RootVisual.Query<Label>().ToList().Select(x => x.text ?? string.Empty).ToArray();
            Check(labels.Any(x => x.Contains("В казарме")),
                "Trainee character status is 'В казарме'");
            yield return Capture(root, "barracks-trainee.png");

            Check(root.MilitaryTraining.Catalog.Get(MilitaryRank.Sergeant).MinimumServiceTicks == 0 &&
                  root.MilitaryTraining.Catalog.Get(MilitaryRank.WarrantOfficer).MinimumServiceTicks > 0,
                "Sergeant has no service delay; every later promotion starts requiring service time");
        }

        private IEnumerator Lake(LocalSceneCompositionRoot root)
        {
            var lake = root.WorldDefinition.MapData.Hydrology.Lakes.First();
            var center = root.WorldDefinition.MapData.CoordinateTransform.ToUnity(lake.Center);
            root.CameraController.FocusPosition(new Vector3(center.X, root.WorldHeight.WaterSurfaceLevel(lake.Id), center.Z), 46f);
            root.WeatherPresentation.ShowPreview(root.Weather.GetWeatherForDay(0), 12 * TimeSpan.TicksPerHour);
            yield return new WaitForSecondsRealtime(2f);
            var materials = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude)
                .SelectMany(x => x.sharedMaterials).Where(x => x != null &&
                    x.name.IndexOf("Procedural Water - Lake", StringComparison.OrdinalIgnoreCase) >= 0).Distinct().ToArray();
            Check(materials.Length > 0 && materials.All(x =>
                    (!x.HasProperty("_RippleStrength") || x.GetFloat("_RippleStrength") <= .1f) &&
                    (!x.HasProperty("_Shininess") || x.GetFloat("_Shininess") <= 100f) &&
                    (!x.HasProperty("_UseVertexDepth") || x.GetFloat("_UseVertexDepth") >= .99f) &&
                    (!x.HasProperty("_RefractionStrength") || x.GetFloat("_RefractionStrength") >= .99f)),
                "Ordinary lake uses authored depth without the banding screen samples");
            yield return Capture(root, "ordinary-lake.png");
            root.WeatherPresentation.ClearPreview(root.Clock.State.CalendarTicks);
        }

        private IEnumerator Capture(LocalSceneCompositionRoot root, string name)
        {
            root.WeatherPresentation.ShowPreview(root.Weather.GetWeatherForDay(0), 12 * TimeSpan.TicksPerHour);
            yield return null;
            yield return new WaitForEndOfFrame();
            var image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG());
            Destroy(image);
            root.WeatherPresentation.ClearPreview(root.Clock.State.CalendarTicks);
        }

        private void Check(bool passed, string label)
        {
            checks.Add((passed ? "PASS " : "FAIL ") + label);
            File.WriteAllLines(Path.Combine(output, "checks.txt"), checks);
            Debug.Log("WM15_USER_RETEST " + checks[^1]);
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            Application.logMessageReceived -= OnLog;
            File.WriteAllLines(Path.Combine(output, "runtime-errors.txt"), errors);
            var failed = checks.Count(x => x.StartsWith("FAIL ", StringComparison.Ordinal));
            Debug.Log("WM15_USER_RETEST_" + (failed == 0 && errors.Count == 0 ? "PASSED" : "FAILED") +
                      " checks=" + checks.Count + " failures=" + failed + " errors=" + errors.Count);
            Application.Quit(failed == 0 && errors.Count == 0 ? 0 : 2);
        }
    }
}
