using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Game.Domain.AI;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Profiling;

namespace Game.Infrastructure.Integration
{
    /// <summary>Explicit Player-only evidence capture; no gameplay or save authority.</summary>
    public sealed partial class SR02SampleReview : MonoBehaviour
    {
        private string output;
        private LocalSceneCompositionRoot root;
        private float deadline;
        private readonly List<string> errors = new();
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-sr02Review");
            if (index < 0 || index + 1 >= args.Length) return;
            var host = new GameObject("SR02 sample review");
            DontDestroyOnLoad(host);
            var review = host.AddComponent<SR02SampleReview>();
            review.output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(review.output);
            Application.runInBackground = true;
            review.deadline = Time.realtimeSinceStartup + 1800;
            Application.logMessageReceived += review.OnLog;
            review.StartCoroutine(review.Run());
        }
        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            { errors.Add(message + "\n" + stack); File.WriteAllLines(output + "/errors.txt", errors); }
        }
        private void Update()
        {
            if (output != null && Time.realtimeSinceStartup > deadline)
            { errors.Add("Review timed out"); Finish(); }
        }
        private void Finish()
        {
            if(root!=null&&root.IsInitialized)
                foreach(var mover in root.AiRuntime.Movements)mover.FailureObserved=null;
            File.WriteAllLines(output + "/errors.txt", errors);
            File.WriteAllText(output + "/complete.txt", errors.Count == 0 ? "CAPTURE_COMPLETE (not gameplay acceptance)" : "FAILED");
            Application.logMessageReceived -= OnLog;
            output = null;
            if(Environment.GetCommandLineArgs().Contains("-sr02KeepOpen"))
            {
                if(root!=null)
                {
                    root.Clock.Pause();root.ThirdPersonControl.Exit();root.CameraController.enabled=true;
                    foreach(var component in Camera.main.GetComponents<Behaviour>())
                        if(component.GetType().FullName=="Unity.Cinemachine.CinemachineBrain")component.enabled=true;
                    root.WeatherPresentation.ClearPreview(root.Clock.State.CalendarTicks);
                    root.CameraController.FocusPosition(Point(450,610),35);
                }
                enabled=false;return;
            }
            Application.Quit(errors.Count == 0 ? 0 : 2);
        }
        private Vector3 Point(float x, float z) => new(x - 570, root.WorldHeight.SampleHeight(x - 570, z - 120), z - 120);
        private IEnumerator Run()
        {
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            MainMenuController menu;
            while ((menu = FindAnyObjectByType<MainMenuController>()) == null) yield return null;
            yield return new WaitForSecondsRealtime(2);
            yield return Capture("00-menu");
            menu.OpenNewGame(); yield return null;
            menu.StartNewGame(1380062136);
            while ((root = FindAnyObjectByType<LocalSceneCompositionRoot>()) == null || !root.IsInitialized) yield return null;
            root.Clock.Pause(); root.GameplayUi.SetOnboardingVisible(false);
            while (!root.TiledWorld.BackgroundWarmupComplete) yield return null;
            File.WriteAllText(output + "/session.txt", "seed=1380062136\nloadSeconds=" + Time.realtimeSinceStartup.ToString(Inv) +
                "\nresolution=" + Screen.width + "x" + Screen.height + "\ngpu=" + SystemInfo.graphicsDeviceName +
                "\nquality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] + "\nvsync=" + QualitySettings.vSyncCount +
                "\nresidents=" + root.Characters.GetAll().Count(x => root.CanCommandResident(x.Id)));
            Audit();
            if(Environment.GetCommandLineArgs().Contains("-sr02ResumeWork"))
            { yield return ResumeWork(); Finish(); yield break; }
            if (Environment.GetCommandLineArgs().Contains("-sr02Routes"))
            { yield return Routes(); Finish(); yield break; }
            root.CameraController.FocusPosition(Point(440, 610), 35);
            yield return new WaitForSecondsRealtime(2);
            root.CameraController.enabled = false;
            foreach (var component in Camera.main.GetComponents<Behaviour>())
                if (component.GetType().FullName == "Unity.Cinemachine.CinemachineBrain") component.enabled = false;
            foreach (var hour in new[] { 12, 18 })
            {
                root.WeatherPresentation.ShowPreview(root.Weather.GetWeatherForDay(0), hour * TimeSpan.TicksPerHour);
                Pose(Point(430, 600) + new Vector3(0, 120, -35), Point(430, 600));
                yield return new WaitForSecondsRealtime(3); yield return Capture(hour + "-top");
                Pose(Point(420, 585) + new Vector3(30, 35, -40), Point(450, 610));
                yield return new WaitForSecondsRealtime(3); yield return Capture(hour + "-rts");
                Pose(Point(392, 580) + Vector3.up * 1.7f, Point(440, 610) + Vector3.up * 1.7f);
                yield return new WaitForSecondsRealtime(3); yield return Capture(hour + "-eye");
                Pose(Point(350,542)+new Vector3(20,25,-35),Point(350,542));
                yield return new WaitForSecondsRealtime(3); yield return Capture(hour+"-crossing");
            }
            root.WeatherPresentation.ShowPreview(root.Weather.GetWeatherForDay(0), 12 * TimeSpan.TicksPerHour);
            foreach (var distance in new[] {50f,60f,135f,145f})
            {
                var focus=Point(375,615);
                Pose(focus+new Vector3(0,.5f,-.8660254f)*distance,focus);
                yield return new WaitForSecondsRealtime(2);yield return Capture("lod-"+distance);
            }
            Pose(Point(420, 585) + new Vector3(30, 35, -40), Point(450, 610));
            root.Clock.SetSpeed(Game.Simulation.Time.GameSpeed.Normal); root.Clock.Resume();
            // Initial autonomous orders settle before the matched three measurement intervals.
            const int warmupSeconds=120;
            File.AppendAllText(output+"/session.txt","\nwarmupSeconds="+warmupSeconds);
            yield return new WaitForSecondsRealtime(warmupSeconds);
            for (var run = 1; run <= 3; run++) yield return Measure(run);
            Finish();
        }
        private void Pose(Vector3 eye, Vector3 target)
        {
            Camera.main.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
            root.TiledWorld.RefreshNow(new WorldPosition(target.x, target.y, target.z),
                Array.Empty<WorldPosition>(), Array.Empty<Tuple<WorldPosition, WorldPosition>>());
        }
        private void Audit()
        {
            using (var file=new BinaryWriter(File.Create(output+"/terrain-heights.bin")))
            {
                file.Write(root.WorldDefinition.Tiles.Count);
                foreach(var tile in root.WorldDefinition.Tiles)
                {
                    if(!root.TiledWorld.TryGetTerrain(tile,out var terrain))throw new InvalidOperationException("Missing resident Terrain "+tile);
                    var n=terrain.terrainData.heightmapResolution;var values=terrain.terrainData.GetHeights(0,0,n,n);
                    file.Write(tile.X);file.Write(tile.Z);file.Write(n);
                    foreach(var value in values)file.Write(value);
                }
            }
            using (var file = new StreamWriter(output + "/geometry.csv"))
            {
                file.WriteLine("d1x,d1z,height,slope,water,buildable,waterBoundary");
                for (var z = 475; z <= 705; z += 5) for (var x = 325; x <= 535; x += 5)
                {
                    var p = Point(x, z); var h = root.WorldHeight;
                    file.WriteLine(FormattableString.Invariant($"{x},{z},{p.y:R},{h.SampleSlopeDegrees(p.x,p.z):R},{h.IsWater(p.x,p.z)},{h.IsBuildable(p.x,p.z,8)},{h.WaterBoundaryDistance(p.x,p.z,out _):R}"));
                }
            }
            File.WriteAllLines(output + "/vegetation.csv", root.TiledWorld.VegetationPlan.Instances.Select(v =>
                FormattableString.Invariant($"{v.StableId},{v.Kind},{v.ClassId},{v.Position.X:R},{v.Position.Y:R},{v.Position.Z:R}")));
            var points = new[] { new Vector2(365,555), new Vector2(430,600), new Vector2(500,620), new Vector2(520,650),
                new Vector2(350,525), new Vector2(350,560), new Vector2(495,600), new Vector2(505,600),
                new Vector2(495,630), new Vector2(505,630), new Vector2(495,660), new Vector2(505,660) };
            var lines = new List<string> { "Endpoint audit only; complete NavMesh paths are not physical traversal proof." };
            foreach (var p in points)
            {
                var v = Point(p.x,p.y); var hit = NavMesh.SamplePosition(v,out var n,2,NavMesh.AllAreas);
                lines.Add($"D1 {p}: water={root.WorldHeight.IsWater(v.x,v.z)} navWithin2m={hit} hit={n.position}");
            }
            File.WriteAllLines(output + "/navigation.txt", lines);
            File.WriteAllLines(output + "/crossings.txt",root.TiledWorld.RoadNetworkPlan.Crossings.Select(c =>
                $"{c.Definition.Id}: {c.Link.Start} -> {c.Link.End}"));
        }
        private IEnumerator Measure(int run)
        {
            using var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1);
            using var render = ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Render Thread",1);
            using var gpu = ProfilerRecorder.StartNew(ProfilerCategory.Render,"GPU Frame Time",1);
            using var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
            using var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render,"Batches Count",1);
            using var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count",1);
            using var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1);
            using var advance = Scope("Serenity.Tier1.Advance");
            using var scheduler = Scope("Serenity.Tier1.Scheduler");
            using var callbacks = Scope("Serenity.Tier1.SimulationCallbacks");
            using var movement = Scope("Serenity.Tier1.MovementPresentation");
            var flora=FindAnyObjectByType<WorldMapVegetationRenderer>();
            var rows = new List<(float frame, double main, double render, double gpu, long gc, long batches, long triangles, long draws,
                double height, long queries, double advance, double scheduler, double callbacks, double movement,
                int vegetationBatches,double vegetationMs)>(20000);
            root.WorldHeight.MeasureHeightQueries=true;
            var heightTicks=root.WorldHeight.HeightQueryTicks;var heightCount=root.WorldHeight.HeightQueryCount;
            var timing = new FrameTiming[1];
            var end = Time.realtimeSinceStartup + 60;
            while (Time.realtimeSinceStartup < end)
            {
                FrameTimingManager.CaptureFrameTimings(); yield return null;
                var count = FrameTimingManager.GetLatestTimings(1,timing);
                rows.Add((Time.unscaledDeltaTime*1000, main.Valid?main.LastValue/1e6:-1,
                    count>0&&timing[0].cpuRenderThreadFrameTime>0?timing[0].cpuRenderThreadFrameTime:render.Valid?render.LastValue/1e6:-1,
                    count>0&&timing[0].gpuFrameTime>0?timing[0].gpuFrameTime:gpu.Valid?gpu.LastValue/1e6:-1,
                    gc.Valid?gc.LastValue:-1, batches.Valid?batches.LastValue:-1, triangles.Valid?triangles.LastValue:-1, draws.Valid?draws.LastValue:-1,
                    (root.WorldHeight.HeightQueryTicks-heightTicks)*1000.0/System.Diagnostics.Stopwatch.Frequency,
                    root.WorldHeight.HeightQueryCount-heightCount,
                    advance.Valid?advance.LastValue/1e6:-1,scheduler.Valid?scheduler.LastValue/1e6:-1,
                    callbacks.Valid?callbacks.LastValue/1e6:-1,movement.Valid?movement.LastValue/1e6:-1,
                    flora!=null?flora.LastSubmittedDrawBatches:-1,flora!=null?flora.LastSubmissionMilliseconds:-1));
                heightTicks=root.WorldHeight.HeightQueryTicks;heightCount=root.WorldHeight.HeightQueryCount;
            }
            root.WorldHeight.MeasureHeightQueries=false;
            File.AppendAllText(output + "/memory.txt", $"run={run} allocated={Profiler.GetTotalAllocatedMemoryLong()} reserved={Profiler.GetTotalReservedMemoryLong()} managed={GC.GetTotalMemory(false)}\n");
            File.WriteAllLines(output + "/frames-" + run + ".csv", new[] {"frame_ms,main_ms,render_ms,gpu_ms,gc_bytes,batches,triangles,draws,height_ms,height_queries,advance_ms,scheduler_ms,callbacks_ms,movement_ms,vegetation_submitted_draw_batches,vegetation_submission_ms"}.Concat(rows.Select(r =>
                FormattableString.Invariant($"{r.frame:R},{r.main},{r.render},{r.gpu},{r.gc},{r.batches},{r.triangles},{r.draws},{r.height},{r.queries},{r.advance},{r.scheduler},{r.callbacks},{r.movement},{r.vegetationBatches},{r.vegetationMs}"))));
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); texture.Apply();
            File.WriteAllBytes(output + "/" + name + ".png", texture.EncodeToPNG()); Destroy(texture);
        }
        private static ProfilerRecorder Scope(string name)=>ProfilerRecorder.StartNew(ProfilerCategory.Scripts,name,1,
            ProfilerRecorderOptions.Default|ProfilerRecorderOptions.SumAllSamplesInFrame);
    }
}
