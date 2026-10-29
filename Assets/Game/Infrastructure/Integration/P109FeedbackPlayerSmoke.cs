using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.World;
using UnityEngine;

namespace Game.Infrastructure.Integration
{
    /// <summary>Opt-in actual Player bootstrap check. Never enabled in an ordinary user session.</summary>
    public sealed class P109FeedbackPlayerSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Environment.GetCommandLineArgs().Contains("-p109FeedbackSmoke")) return;
            var host = new GameObject("P109 feedback player smoke");
            DontDestroyOnLoad(host);
            host.AddComponent<P109FeedbackPlayerSmoke>().StartCoroutine(Run());
        }

        private static IEnumerator Run()
        {
            var started = Time.realtimeSinceStartup;
            MainMenuController menu = null;
            while (menu == null && Time.realtimeSinceStartup - started < 60f)
            { menu = FindAnyObjectByType<MainMenuController>(); yield return null; }
            if (menu == null) { Debug.LogError("P109_FEEDBACK_SMOKE_FAILED: no main menu"); Application.Quit(2); yield break; }
            menu.StartNewGame(20261021);
            LocalSceneCompositionRoot root = null;
            while ((root == null || root.GameplayUi == null) && Time.realtimeSinceStartup - started < 240f)
            { root = FindAnyObjectByType<LocalSceneCompositionRoot>(); yield return null; }
            while (root != null && !root.TiledWorld.BackgroundWarmupComplete &&
                   Time.realtimeSinceStartup - started < 600f) yield return null;
            yield return new WaitForEndOfFrame();
            try
            {
                if (root == null || root.GameplayUi == null || root.MaleDemoCharacter == null || root.FemaleDemoCharacter == null)
                    throw new InvalidOperationException("Missing gameplay UI or starter residents.");
                if (!root.TiledWorld.BackgroundWarmupComplete ||
                    root.TiledWorld.LoadedTiles.Count != root.WorldDefinition.Tiles.Count)
                    throw new InvalidOperationException("The full map did not finish its one-time warmup.");
                root.Clock.Pause();
                root.Research.CompleteAllForDiagnostics();
                var maximumRefreshSeconds = 0f;
                foreach (var tile in root.WorldDefinition.Tiles)
                {
                    var origin = root.WorldDefinition.TileOrigin(tile);
                    var center = new WorldPosition(origin.X + root.WorldDefinition.TileSizeMeters * 0.5f, 0f,
                        origin.Z + root.WorldDefinition.TileSizeMeters * 0.5f);
                    var before = Time.realtimeSinceStartup;
                    root.TiledWorld.RefreshNow(center, Array.Empty<WorldPosition>(),
                        Array.Empty<Tuple<WorldPosition, WorldPosition>>());
                    maximumRefreshSeconds = Mathf.Max(maximumRefreshSeconds, Time.realtimeSinceStartup - before);
                    if (root.TiledWorld.LoadedTiles.Count != root.WorldDefinition.Tiles.Count)
                        throw new InvalidOperationException("Camera movement discarded a resident terrain tile.");
                    if (root.WorldPopulation.ActiveTileViewCount != root.TiledWorld.VisualTiles.Count)
                        throw new InvalidOperationException("Pooled population views do not match visible tiles.");
                }
                var markers = root.GetComponentsInChildren<Transform>(true)
                    .Where(x => x.name.StartsWith("Fishing landing ")).ToArray();
                var lakeIds = root.WorldDefinition.MapData.Hydrology.Lakes.Select(value => value.Id).ToArray();
                if (lakeIds.Any(id => !markers.Any(marker => marker.name.EndsWith(id, StringComparison.Ordinal))))
                    throw new InvalidOperationException("A lake has no landing marker.");
                foreach (var marker in markers)
                {
                    var controller = root.BuildingPlacementController;
                    if (!controller.TryStartPlacement(new BuildingDefinitionId("fishing_camp")))
                        throw new InvalidOperationException("Fishing camp placement did not start.");
                    controller.TryMovePreviewToWorldPoint(marker.position);
                    if (controller.LastPlacementFailure != Game.Simulation.Buildings.PlacementFailureReason.None)
                    {
                        var nearby = Physics.OverlapBox(marker.position + Vector3.up * 1.1f,
                                new Vector3(6f, 1f, 6f), Quaternion.identity,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                            .Select(value => value.name + "[" + value.GetType().Name + "]")
                            .Distinct().OrderBy(value => value).Take(24).ToArray();
                        throw new InvalidOperationException(marker.name + ": " + controller.LastPlacementFailure +
                            "; surface=" + controller.SurfaceBlockedReason +
                            "; nearby=" + string.Join(", ", nearby));
                    }
                    controller.CancelPlacement();
                }
                Debug.Log("P109_FEEDBACK_SMOKE_PASSED: LocalGameplay, UI, starter residents, valid lake landings=" +
                          markers.Length + ", map cache startup=" +
                          (root.TiledWorld.PreparationCacheWasCompleteAtStartup ? "hit" : "miss") +
                          ", cached tiles=" + root.TiledWorld.PreparationCacheHitCount +
                          ", worst warm camera refresh ms=" + (maximumRefreshSeconds * 1000f).ToString("F1"));
                var args = Environment.GetCommandLineArgs();
                var index = Array.IndexOf(args, "-p109FeedbackScreenshot");
                if (index >= 0 && index + 1 < args.Length)
                {
                    var path = Path.GetFullPath(args[index + 1]);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                    texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                    texture.Apply();
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                    Destroy(texture);
                }
            }
            catch (Exception error)
            { Debug.LogError("P109_FEEDBACK_SMOKE_FAILED: " + error); Application.Quit(2); yield break; }
            yield return new WaitForSecondsRealtime(3f);
            Application.Quit(0);
        }
    }
}
