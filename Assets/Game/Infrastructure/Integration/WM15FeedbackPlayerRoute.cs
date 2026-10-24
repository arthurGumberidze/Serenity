using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain.AI;
using Game.Presentation.Buildings;
using Game.Presentation.Environment;
using Game.Presentation.Localization;
using UnityEngine;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

namespace Game.Infrastructure.Integration
{
    public sealed class WM15FeedbackPlayerRoute : MonoBehaviour
    {
        private readonly List<string> errors = new List<string>();
        private string output;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-wm15FeedbackRoute");
            if (index < 0 || index + 1 >= args.Length) return;
            Application.runInBackground = true;
            var host = new GameObject("WM15 feedback route");
            DontDestroyOnLoad(host);
            var runner = host.AddComponent<WM15FeedbackPlayerRoute>();
            runner.output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(runner.output);
            Application.logMessageReceived += runner.RecordError;
            runner.StartCoroutine(runner.Run());
        }

        private void RecordError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                errors.Add(message + "\n" + stack);
        }

        private IEnumerator Run()
        {
            var menu = FindAnyObjectByType<MainMenuController>();
            while (menu == null) { yield return null; menu = FindAnyObjectByType<MainMenuController>(); }
            menu.StartNewGame(20261021);
            LocalSceneCompositionRoot root = null;
            while (root == null || root.GameplayUi == null)
            { yield return null; root = FindAnyObjectByType<LocalSceneCompositionRoot>(); }
            root.Clock.Pause();
            var initial = root.TiledWorld.LoadedTiles.ToArray();
            var terrainIds = initial.Select(id => { root.TiledWorld.TryGetTerrain(id, out var t); return t; }).ToArray();
            var points = new List<WorldPosition> { root.WorldDefinition.PlayerStart };
            points.AddRange(root.TiledWorld.RoadNetworkPlan.Crossings.Select(c => new WorldPosition(
                (c.Link.Start.X + c.Link.End.X) * .5f, (c.Link.Start.Y + c.Link.End.Y) * .5f,
                (c.Link.Start.Z + c.Link.End.Z) * .5f)));
            points.AddRange(root.WorldPopulation.PoiPlan.Points.Select(p => p.Position));
            points.AddRange(root.WorldDefinition.MapData.Hydrology.Lakes.Select(l =>
                root.WorldDefinition.MapData.CoordinateTransform.ToUnity(l.Center)));
            var enemyLocation = root.GlobalMap.GetLocation(new Game.Domain.World.WorldLocationId("enemy_settlement"));
            points.Add(new WorldPosition(enemyLocation.MapX, 0, enemyLocation.MapY));
            points.AddRange(root.WorldPopulation.Plan.Industry.Select(v => v.Position));
            points.AddRange(root.WorldDefinition.Tiles.Select(id => root.WorldDefinition.TileOrigin(id)));
            var timing = new List<string> { "point,refresh_ms,max_frame_ms" };
            for (var index = 0; index < points.Count; index++)
            {
                var point = points[index];
                root.CameraController.FocusPosition(new Vector3(point.X, point.Y, point.Z), 120f);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                root.TiledWorld.RefreshNow(point, Array.Empty<WorldPosition>(), Array.Empty<Tuple<WorldPosition, WorldPosition>>());
                watch.Stop();
                // Exclude the preceding screenshot's CPU encoding from the next point's frame timings.
                yield return null;
                yield return null;
                var maximum = 0f;
                for (var frame = 0; frame < 30; frame++)
                { yield return null; maximum = Mathf.Max(maximum, Time.unscaledDeltaTime * 1000f); }
                timing.Add(index + "," + watch.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "," + maximum.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                if (index < 20)
                {
                    yield return new WaitForEndOfFrame();
                    var image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); image.Apply();
                    File.WriteAllBytes(Path.Combine(output, "route-" + index.ToString("D2") + ".png"), image.EncodeToPNG());
                    Destroy(image);
                }
            }
            // Continuous panning across tile boundaries after loading, not just isolated startup screenshots.
            var origin = root.WorldDefinition.PlayerStart;
            var panFrames = new List<float>();
            for (var frame = 0; frame < 240; frame++)
            {
                root.CameraController.FocusPosition(new Vector3(origin.X + frame * 2f, origin.Y, origin.Z - frame), 120f);
                yield return null;
                if (frame > 1) panFrames.Add(Time.unscaledDeltaTime * 1000f);
            }
            panFrames.Sort();
            File.WriteAllText(Path.Combine(output, "pan-timing.txt"),
                "MedianMs=" + panFrames[panFrames.Count / 2] + "; P95Ms=" + panFrames[(int)(panFrames.Count * .95f)] +
                "; MaxMs=" + panFrames.Last());
            var floraRenderer = root.TiledWorld.GetComponent<WorldMapVegetationRenderer>();
            var diagnosticCamera = Camera.main;
            // Stop the periodic streaming refresh from restoring visibility during isolation measurements.
            root.TiledWorld.enabled = false;
            var renderProfiles = new List<string>();
            var recorderNames = new List<string>();
            var recorders = new List<ProfilerRecorder>();
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                var name = ProfilerRecorderHandle.GetDescription(handle).Name;
                if (!new[] { "Main Thread", "Render Thread", "GPU Frame Time", "Update.ScriptRunBehaviourUpdate",
                    "PreUpdate.AIUpdate", "FixedUpdate.PhysicsFixedUpdate", "WaitForTargetFPS",
                    "Gfx.WaitForPresentOnGfxThread", "PostLateUpdate.FinishFrameRendering", "Camera.Render" }.Contains(name)) continue;
                if (recorderNames.Contains(name)) continue;
                var recorder = new ProfilerRecorder(handle, 2048, ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.StartImmediately);
                if (recorder.Valid) { recorderNames.Add(name); recorders.Add(recorder); } else recorder.Dispose();
            }
            for (var mode = 0; mode < 5; mode++)
            {
                floraRenderer.enabled = mode < 2;
                floraRenderer.DiagnosticDisableProbes = mode == 1;
                foreach (var terrain in terrainIds) terrain.drawHeightmap = mode < 3;
                diagnosticCamera.enabled = mode < 4;
                var frames = new List<float>();
                var submissions = new List<double>();
                var samples = recorders.Select(_ => new List<long>()).ToArray();
                for (var frame = 0; frame < 150; frame++)
                {
                    root.CameraController.FocusPosition(new Vector3(origin.X + frame * 2f, origin.Y, origin.Z - frame), 120f);
                    yield return null;
                    if (frame < 15) continue;
                    frames.Add(Time.unscaledDeltaTime * 1000f);
                    submissions.Add(floraRenderer.LastSubmissionMilliseconds);
                    for (var i = 0; i < recorders.Count; i++) samples[i].Add(recorders[i].LastValue);
                }
                frames.Sort(); submissions.Sort();
                renderProfiles.Add("Mode=" + mode + "; MedianMs=" + frames[frames.Count / 2] +
                    "; P95Ms=" + frames[(int)(frames.Count * .95f)] +
                    "; SubmissionMedianMs=" + submissions[submissions.Count / 2]);
                for (var i = 0; i < samples.Length; i++)
                { samples[i].Sort(); renderProfiles.Add(recorderNames[i] + "=" + samples[i][samples[i].Count / 2] / 1000000d + "ms"); }
            }
            foreach (var recorder in recorders) recorder.Dispose();
            diagnosticCamera.enabled = true;
            foreach (var terrain in terrainIds) terrain.drawHeightmap = true;
            root.TiledWorld.enabled = true;
            floraRenderer.enabled = true;
            floraRenderer.DiagnosticDisableProbes = false;
            File.WriteAllLines(Path.Combine(output, "render-comparison.txt"), renderProfiles);
            if (root.TiledWorld.LoadedCollisionTileCount != root.WorldDefinition.Tiles.Count)
                errors.Add("Full terrain was not retained.");
            for (var i = 0; i < initial.Length; i++)
                if (!root.TiledWorld.TryGetTerrain(initial[i], out var terrain) || terrain != terrainIds[i])
                    errors.Add("Terrain regenerated during route: " + initial[i]);
            var animal = FindObjectsByType<WorldMapWildlifeView>(FindObjectsInactive.Exclude).FirstOrDefault();
            if (animal == null) errors.Add("No selectable wildlife.");
            else
            {
                root.CameraController.FocusPosition(animal.transform.position, 35f);
                yield return null;
                Physics.SyncTransforms();
                var screen = Camera.main.WorldToScreenPoint(animal.GetComponent<Collider>().bounds.center);
                if (!root.SelectionProbe.TrySelectAt(new Vector2(screen.x, screen.y), false) ||
                    root.SelectionProbe.SelectedWildlife != animal)
                    errors.Add("Wildlife pointer selection failed: " + animal.SpeciesId + "; screen=" + screen +
                        "; bounds=" + animal.GetComponent<Collider>().bounds);
            }
            foreach (var poi in FindObjectsByType<WorldMapPoiPresenter>(FindObjectsInactive.Exclude))
                if (poi.GetComponentInChildren<BuildingPresenter>(true) != null)
                    errors.Add("Decorative POI retains a BuildingPresenter: " + poi.Point.Id);
            foreach (var poi in root.WorldPopulation.PoiPlan.Points)
            {
                var id = Game.Simulation.World.WorldMapPoiEventCatalog.DefinitionId(poi).ToString();
                if (RussianTextCatalog.EventTitle(id, "discovered") == RussianTextCatalog.MissingText ||
                    RussianTextCatalog.EventDescription(id, "discovered") == RussianTextCatalog.MissingText ||
                    RussianTextCatalog.EventChoice(id, "discovered", "acknowledge") == "неизвестно")
                    errors.Add("Missing POI translation: " + id);
            }
            root.CameraController.FocusPosition(new Vector3(origin.X, origin.Y, origin.Z), 35f);
            // The Editor P105 case times out while loading the full map; exercise the same animator contract
            // on the actual Player after returning the camera and allowing character atlases to finish.
            yield return new WaitForSecondsRealtime(10f);
            if (root.CharacterPresentations.TryGet(root.MaleDemoCharacter.Id, out var characterView))
            {
                characterView.SetSimulationPaused(false);
                characterView.SetMovementSpeed(3.4f);
                if (Mathf.Abs(characterView.Animator.speed - 1f) > .08f) errors.Add("Character walk playback speed mismatch.");
                characterView.SetMovementSpeed(5.61f);
                if (Mathf.Abs(characterView.Animator.speed - 1.65f) > .1f) errors.Add("Character run playback speed mismatch.");
                characterView.SetAnimationIntent(Game.Presentation.Characters.CharacterAnimationIntent.Combat);
                if (characterView.Animator.GetInteger("ActionIntent") != (int)Game.Presentation.Characters.CharacterAnimationIntent.Combat)
                    errors.Add("Character combat animation intent mismatch.");
                characterView.SetMovementSpeed(0f);
                characterView.SetSimulationPaused(true);
            }
            else errors.Add("Starter character did not rematerialize for animation verification.");
            var materialAudit = new List<string>();
            var nature = Resources.Load<StarterNatureCatalog>("World/StarterNature");
            var flora = nature.WorldMapTrees.Concat(nature.WorldMapShrubs).Concat(nature.WorldMapGroundCover)
                .SelectMany(prefab => prefab.GetComponentsInChildren<Renderer>(true));
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude).Where(r => r.enabled).Concat(flora))
            for (var materialIndex = 0; materialIndex < renderer.sharedMaterials.Length; materialIndex++)
            {
                var material = renderer.sharedMaterials[materialIndex];
                var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh != null && (materialIndex >= mesh.subMeshCount || mesh.GetIndexCount(materialIndex) == 0)) continue;
                if (material == null || material.shader == null || !material.shader.isSupported)
                    errors.Add("Missing/unsupported material: " + renderer.name);
                else if (material.HasProperty("_BaseColor") && material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") == null)
                {
                    var color = material.GetColor("_BaseColor");
                    if (color.r > .92f && color.g > .92f && color.b > .92f)
                        materialAudit.Add(renderer.name + " | " + material.name + " | " + material.shader.name);
                }
            }
            File.WriteAllLines(Path.Combine(output, "white-material-audit.txt"), materialAudit.Distinct());
            if (materialAudit.Count > 0) errors.Add("Untextured white scenery remains; see white-material-audit.txt.");
            var waterMeshes = FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude)
                .Where(filter => filter.name.StartsWith("World Map V2 Local Water ")).ToArray();
            if (waterMeshes.Length == 0 || waterMeshes.Any(filter => filter.sharedMesh.bounds.size.x < .1f ||
                filter.sharedMesh.bounds.size.z < .1f)) errors.Add("Missing or collapsed local water surfaces.");
            var enemy = root.GlobalMap.GetSettlementAt(new Game.Domain.World.WorldLocationId("enemy_settlement"));
            if (enemy.ResidentCharacterIds.Count < 6 || enemy.BuildingIds.Count < 4)
                errors.Add("Enemy has no real starter camp.");
            if (!root.WorldPopulation.Plan.Industry.Any(v => v.DefinitionId == "stone_quarry"))
                errors.Add("No ridge quarry.");
            var before = root.StarterEnemyAi.Strategic.DecisionSequence;
            root.AiRuntime.SetSpeed(Game.Simulation.Time.GameSpeed.Maximum);
            root.AiRuntime.SetPaused(false);
            root.AiRuntime.AdvanceForDebug(TimeSpan.FromSeconds(50));
            root.AiRuntime.SetPaused(true);
            yield return null;
            if (root.StarterEnemyAi.Strategic.DecisionSequence <= before)
                errors.Add("Enemy simulation did not advance.");
            var enemyInitialBuildings = enemy.BuildingIds.Count;
            var future = root.Clock.State.CalendarTicks + TimeSpan.FromDays(12).Ticks;
            root.StrategicAi.AdvanceTo(future, root.Clock.State.BiologicalTicks + TimeSpan.FromDays(12).Ticks);
            enemy = root.GlobalMap.GetSettlementAt(new Game.Domain.World.WorldLocationId("enemy_settlement"));
            File.WriteAllLines(Path.Combine(output, "enemy-decisions.txt"),
                root.StarterEnemyAi.Strategic.RecentDecisions.Select(decision => decision.Kind + ": " + decision.Reason));
            if (enemy.BuildingIds.Count <= enemyInitialBuildings)
                errors.Add("Enemy did not develop its settlement in twelve simulation days.");
            if (root.GlobalMap.Travels.Any(x => root.MilitaryUnits.Get(x.UnitId).OwnerSideId.Value == "enemy") ||
                root.StarterEnemyAi.Strategic.PendingOrders.Any(x => x.Kind == AiDecisionKind.PrepareAttack))
                errors.Add("Neutral enemy launched an unprovoked expedition.");
            root.StrategicAi.AdvanceTo(TimeSpan.FromDays(30).Ticks, root.Clock.State.BiologicalTicks);
            var training = root.StarterEnemyAi.Training;
            File.WriteAllText(Path.Combine(output, "enemy-training.txt"),
                "Completed=" + training.Records.Count(x => x.Completed) + "; active=" + training.Active.Count +
                "; residents=" + root.StarterEnemyAi.Residents.Count + "; relation=" + root.StarterEnemyAi.RelationTo(new Game.Domain.Combat.CombatSideId("player")));
            if (!training.Records.Any(x => x.Completed && x.Rank >= Game.Domain.Military.MilitaryRank.Soldier))
                errors.Add("Enemy did not autonomously train soldiers within thirty simulation days.");
            File.WriteAllText(Path.Combine(output, "content-audit.txt"),
                "Lakes=" + root.WorldDefinition.MapData.Hydrology.Lakes.Count +
                "; vegetation=" + root.TiledWorld.ActiveVegetationViewCount +
                "; wildlife=" + root.WorldPopulation.WildlifePopulation.Count +
                "; species=" + string.Join(",", root.WorldPopulation.WildlifePopulation.Animals.Select(a => a.SpeciesId).Distinct()) +
                "; water meshes=" + waterMeshes.Length +
                "; enemy residents=" + enemy.ResidentCharacterIds.Count + "; enemy buildings=" + enemyInitialBuildings + " -> " + enemy.BuildingIds.Count +
                "; enemy decisions=" + before + " -> " + root.StarterEnemyAi.Strategic.DecisionSequence);
            File.WriteAllLines(Path.Combine(output, "route-timing.csv"), timing);
            File.WriteAllLines(Path.Combine(output, "errors.txt"), errors);
            Debug.Log("WM15_FEEDBACK_ROUTE_" + (errors.Count == 0 ? "PASSED" : "FAILED") + ": points=" + points.Count + ", errors=" + errors.Count);
            Application.Quit(errors.Count == 0 ? 0 : 2);
        }

        private void OnDestroy() { Application.logMessageReceived -= RecordError; }
    }
}
