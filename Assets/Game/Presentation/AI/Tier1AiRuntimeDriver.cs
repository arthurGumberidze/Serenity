using System;
using System.Collections.Generic;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Presentation.Characters;
using Game.Simulation.AI;
using Game.Simulation.Time;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Presentation.AI
{
    /// <summary>Single scene-level bridge. NPC decision logic never owns a MonoBehaviour Update.</summary>
    [DisallowMultipleComponent]
    public sealed class Tier1AiRuntimeDriver : MonoBehaviour
    {
        private const float PresentationRefreshIntervalSeconds = 0.5f;
        private const int PresentationRefreshSlicesAtTargetRate = 30;
        private const float AnimationRefreshIntervalSeconds = 0.1f;
        private const int MaximumAnimationRefreshesPerFrame = 8;
        public const int HighDetailCrowdBudget = 8;
        private const float AvailabilitySweepIntervalSeconds = 1f;
        private static readonly ProfilerMarker AdvanceMarker = new ProfilerMarker("Serenity.Tier1.Advance");
        private static readonly ProfilerMarker SchedulerMarker = new ProfilerMarker("Serenity.Tier1.Scheduler");
        private static readonly ProfilerMarker SimulationCallbacksMarker =
            new ProfilerMarker("Serenity.Tier1.SimulationCallbacks");
        private static readonly ProfilerMarker MovementPresentationMarker =
            new ProfilerMarker("Serenity.Tier1.MovementPresentation");
        private static readonly ProfilerMarker LightweightMovementMarker =
            new ProfilerMarker("Serenity.Tier1.LightweightMovement");
        private static readonly ProfilerMarker AnimationMarker =
            new ProfilerMarker("Serenity.Tier1.AnimationPresentation");
        private static readonly ProfilerMarker LightweightRenderingMarker =
            new ProfilerMarker("Serenity.Tier1.LightweightRendering");
        private readonly List<NavMeshMovementDriver> movements = new List<NavMeshMovementDriver>();
        private readonly Matrix4x4[] lightweightMatrices = new Matrix4x4[1023];
        private GameClock clock;
        private Tier1AiScheduler scheduler;
        private Func<int> movementModifierProvider;
        private double presentationRefreshBudget;
        private int presentationRefreshCursor;
        private double animationRefreshBudget;
        private int animationRefreshCursor;
        private float nextAvailabilitySweep;
        private Mesh lightweightMesh;
        private Material lightweightMaterial;

        public GameClock Clock => clock;
        public Tier1AiScheduler Scheduler => scheduler;
        public IReadOnlyList<NavMeshMovementDriver> Movements => movements;
        public int PerAgentAiUpdateCount => 0;
        public bool AllocationDiagnosticsEnabled { get; set; }
        public long AllocationDiagnosticSamples { get; private set; }
        public long SchedulerAllocatedBytes { get; private set; }
        public long SimulationCallbackAllocatedBytes { get; private set; }
        public long MovementPresentationAllocatedBytes { get; private set; }
        public int LastLightweightDrawCount { get; private set; }
        public event Action<GameTimeAdvance> SimulationAdvanced;

        public void Initialize(GameClock gameClock, Tier1AiScheduler aiScheduler,
            Func<int> weatherMovementModifierProvider = null)
        {
            if (clock != null || scheduler != null) throw new InvalidOperationException("Tier 1 AI runtime is already initialized.");
            clock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
            scheduler = aiScheduler ?? throw new ArgumentNullException(nameof(aiScheduler));
            movementModifierProvider = weatherMovementModifierProvider;
        }

        public Tier1AiAgentState Register(Character character, CharacterPresenter presenter, Tier1Needs needs = null)
        {
            if (clock == null || scheduler == null) throw new InvalidOperationException("Tier 1 AI runtime is not initialized.");
            var staleRegistrationRemoved = false;
            for (var i = movements.Count - 1; i >= 0; i--)
            {
                if (movements[i].CharacterId != character.Id) continue;
                if (movements[i].HasBoundPresenter)
                    throw new InvalidOperationException("Tier 1 movement is already registered for this character.");
                movements.RemoveAt(i);
                staleRegistrationRemoved = true;
            }
            if (staleRegistrationRemoved) scheduler.Unregister(character.Id);
            var movement = new NavMeshMovementDriver(presenter, scheduler.Settings.MovementSpeed);
            var position = movement.CurrentPosition;
            var state = new Tier1AiAgentState(character.Id, position, needs);
            scheduler.Register(character, state, movement);
            movements.Add(movement);
            RefreshCrowdDetailLevels();
            return state;
        }

        public bool Unregister(StableEntityId characterId)
        {
            var removed = false;
            for (var i = movements.Count - 1; i >= 0; i--)
            {
                if (movements[i].CharacterId != characterId) continue;
                movements[i].Stop();
                movements.RemoveAt(i);
                removed = true;
            }
            if (removed) RefreshCrowdDetailLevels();
            return scheduler != null && scheduler.Unregister(characterId) || removed;
        }

        public bool TryGetMovement(StableEntityId characterId, out NavMeshMovementDriver movement)
        {
            movement = movements.Find(candidate => candidate.CharacterId == characterId);
            return movement != null;
        }

        public void SetPaused(bool paused)
        {
            if (clock == null) return;
            if (paused) clock.Pause();
            else clock.Resume();
            RefreshMovementPresentation();
        }

        public void SetSpeed(GameSpeed speed)
        {
            if (clock == null) throw new InvalidOperationException("Tier 1 AI runtime is not initialized.");
            clock.SetSpeed(speed);
            RefreshMovementPresentation();
        }

        public void RebindClock(GameClock replacement)
        {
            clock = replacement ?? throw new ArgumentNullException(nameof(replacement));
            RefreshMovementPresentation();
        }

        public GameTimeAdvance AdvanceForDebug(TimeSpan realDelta)
        {
            return Advance(realDelta, movements.Count);
        }

        private GameTimeAdvance Advance(TimeSpan realDelta, int movementPresentationUpdates)
        {
            if (clock == null || scheduler == null)
                throw new InvalidOperationException("Tier 1 AI runtime is not initialized.");
            if (realDelta < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(realDelta));
            var cursor = AllocationDiagnosticsEnabled ? GC.GetAllocatedBytesForCurrentThread() : 0L;
            var advance = clock.Advance(realDelta);
            using (SchedulerMarker.Auto()) scheduler.Advance(advance);
            if (AllocationDiagnosticsEnabled)
            {
                var current = GC.GetAllocatedBytesForCurrentThread();
                SchedulerAllocatedBytes += Math.Max(0L, current - cursor);
                cursor = current;
            }
            using (SimulationCallbacksMarker.Auto()) SimulationAdvanced?.Invoke(advance);
            if (AllocationDiagnosticsEnabled)
            {
                var current = GC.GetAllocatedBytesForCurrentThread();
                SimulationCallbackAllocatedBytes += Math.Max(0L, current - cursor);
                cursor = current;
            }
            if (movementPresentationUpdates > 0)
                using (MovementPresentationMarker.Auto())
                    RefreshMovementPresentation(movementPresentationUpdates);
            if (AllocationDiagnosticsEnabled)
            {
                var current = GC.GetAllocatedBytesForCurrentThread();
                MovementPresentationAllocatedBytes += Math.Max(0L, current - cursor);
                AllocationDiagnosticSamples++;
            }
            return advance;
        }

        public void ResetAllocationDiagnostics()
        {
            AllocationDiagnosticSamples = 0;
            SchedulerAllocatedBytes = 0;
            SimulationCallbackAllocatedBytes = 0;
            MovementPresentationAllocatedBytes = 0;
        }

        private void Update()
        {
            if (clock == null || scheduler == null) return;
            var now = Time.unscaledTime;
            if (now >= nextAvailabilitySweep)
            {
                nextAvailabilitySweep = now + AvailabilitySweepIntervalSeconds;
                for (var i = movements.Count - 1; i >= 0; i--)
                {
                    if (movements[i].IsAvailable) continue;
                    scheduler.Unregister(movements[i].CharacterId);
                    movements.RemoveAt(i);
                }
            }
            var deltaSeconds = Time.unscaledDeltaTime;
            using (LightweightMovementMarker.Auto())
                for (var index = 0; index < movements.Count; index++)
                    movements[index].AdvanceLightweight(deltaSeconds, clock.IsPaused);
            using (AdvanceMarker.Auto())
                Advance(TimeSpan.FromSeconds(deltaSeconds), TakeMovementPresentationBudget(deltaSeconds));
            using (AnimationMarker.Auto())
                RefreshAnimationPresentation(TakeAnimationPresentationBudget(deltaSeconds), now);
            using (LightweightRenderingMarker.Auto()) DrawLightweightPresenters();
        }

        private void OnDestroy()
        {
            scheduler?.Reset();
            movements.Clear();
            if (lightweightMesh != null) Destroy(lightweightMesh);
            if (lightweightMaterial != null) Destroy(lightweightMaterial);
            SimulationAdvanced = null;
        }

        private void RefreshMovementPresentation()
        {
            presentationRefreshBudget = 0d;
            presentationRefreshCursor = 0;
            RefreshMovementPresentation(movements.Count);
        }

        private int TakeMovementPresentationBudget(float deltaSeconds)
        {
            if (movements.Count == 0 || deltaSeconds <= 0f) return 0;
            var maximumPerFrame = Math.Max(1,
                (movements.Count + PresentationRefreshSlicesAtTargetRate - 1) / PresentationRefreshSlicesAtTargetRate);
            presentationRefreshBudget = Math.Min(maximumPerFrame,
                presentationRefreshBudget + movements.Count * deltaSeconds / PresentationRefreshIntervalSeconds);
            var result = Math.Min(maximumPerFrame, (int)presentationRefreshBudget);
            presentationRefreshBudget -= result;
            return result;
        }

        private int TakeAnimationPresentationBudget(float deltaSeconds)
        {
            if (movements.Count == 0 || deltaSeconds <= 0f) return 0;
            animationRefreshBudget = Math.Min(MaximumAnimationRefreshesPerFrame,
                animationRefreshBudget + movements.Count * deltaSeconds / AnimationRefreshIntervalSeconds);
            var result = Math.Min(MaximumAnimationRefreshesPerFrame, (int)animationRefreshBudget);
            animationRefreshBudget -= result;
            return result;
        }

        private void RefreshMovementPresentation(int maximumUpdates)
        {
            if (clock == null || movements.Count == 0 || maximumUpdates <= 0) return;
            var movementModifier = movementModifierProvider == null ? 1000 : movementModifierProvider();
            if (movementModifier < 0 || movementModifier > 2000)
                throw new InvalidOperationException("Movement modifier provider returned an invalid value.");
            var updates = Math.Min(maximumUpdates, movements.Count);
            for (var i = 0; i < updates; i++)
            {
                if (presentationRefreshCursor >= movements.Count) presentationRefreshCursor = 0;
                movements[presentationRefreshCursor].UpdatePresentation(clock.IsPaused, (int)clock.Speed, movementModifier);
                presentationRefreshCursor++;
            }
        }

        private void RefreshAnimationPresentation(int maximumUpdates, float unscaledTime)
        {
            if (movements.Count == 0 || maximumUpdates <= 0) return;
            var updates = Math.Min(maximumUpdates, movements.Count);
            for (var i = 0; i < updates; i++)
            {
                if (animationRefreshCursor >= movements.Count) animationRefreshCursor = 0;
                movements[animationRefreshCursor].RefreshAnimation(unscaledTime);
                animationRefreshCursor++;
            }
        }

        private void RefreshCrowdDetailLevels()
        {
            for (var index = 0; index < movements.Count; index++)
                movements[index].SetCrowdDetail(index < HighDetailCrowdBudget);
        }

        private void DrawLightweightPresenters()
        {
            LastLightweightDrawCount = 0;
            for (var index = 0; index < movements.Count; index++)
            {
                var presenter = movements[index].Presenter;
                if (presenter == null || !presenter.UsesLightweightVisual) continue;
                EnsureLightweightRenderResources(presenter);
                if (lightweightMesh == null || lightweightMaterial == null) continue;
                var transform = presenter.transform;
                lightweightMatrices[LastLightweightDrawCount++] = Matrix4x4.TRS(
                    transform.position, transform.rotation, Vector3.one);
            }
            if (LastLightweightDrawCount == 0 || lightweightMesh == null || lightweightMaterial == null) return;
            Graphics.DrawMeshInstanced(lightweightMesh, 0, lightweightMaterial, lightweightMatrices,
                LastLightweightDrawCount, null, ShadowCastingMode.Off, false, gameObject.layer);
        }

        private void EnsureLightweightRenderResources(CharacterPresenter presenter)
        {
            if (lightweightMesh == null)
                lightweightMesh = BuildLightweightHumanoidMesh();
            if (lightweightMaterial == null && presenter.LightweightMaterial != null)
            {
                lightweightMaterial = new Material(presenter.LightweightMaterial)
                {
                    name = "Tier1 Stone Age Humanoid LOD Material"
                };
                if (lightweightMaterial.HasProperty("_BaseMap"))
                    lightweightMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);
                if (lightweightMaterial.HasProperty("_MainTex"))
                    lightweightMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);
                var hide = new Color(0.28f, 0.16f, 0.075f, 1f);
                if (lightweightMaterial.HasProperty("_BaseColor")) lightweightMaterial.SetColor("_BaseColor", hide);
                if (lightweightMaterial.HasProperty("_Color")) lightweightMaterial.SetColor("_Color", hide);
                if (lightweightMaterial.HasProperty("_Metallic")) lightweightMaterial.SetFloat("_Metallic", 0f);
                if (lightweightMaterial.HasProperty("_Smoothness")) lightweightMaterial.SetFloat("_Smoothness", 0.16f);
                lightweightMaterial.enableInstancing = true;
            }
        }

        public static Mesh BuildLightweightHumanoidMesh()
        {
            var vertices = new List<Vector3>(192);
            var normals = new List<Vector3>(192);
            var uv = new List<Vector2>(192);
            var triangles = new List<int>(384);
            AddTaperedPart(vertices, normals, uv, triangles, new Vector3(0f, 1.18f, 0f),
                0.23f, 0.29f, 0.58f, 8); // torso
            AddTaperedPart(vertices, normals, uv, triangles, new Vector3(0f, 0.78f, 0f),
                0.28f, 0.35f, 0.34f, 8); // hide tunic
            AddTaperedPart(vertices, normals, uv, triangles, new Vector3(-0.13f, 0.34f, 0f),
                0.085f, 0.075f, 0.62f, 7); // left leg
            AddTaperedPart(vertices, normals, uv, triangles, new Vector3(0.13f, 0.34f, 0f),
                0.085f, 0.075f, 0.62f, 7); // right leg
            AddTaperedPart(vertices, normals, uv, triangles, new Vector3(-0.31f, 1.05f, 0f),
                0.075f, 0.06f, 0.64f, 7); // left arm
            AddTaperedPart(vertices, normals, uv, triangles, new Vector3(0.31f, 1.05f, 0f),
                0.075f, 0.06f, 0.64f, 7); // right arm
            AddTaperedPart(vertices, normals, uv, triangles, new Vector3(0f, 1.62f, 0f),
                0.145f, 0.135f, 0.27f, 10); // head
            var mesh = new Mesh { name = "Tier1 Stone Age Humanoid LOD" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        private static void AddTaperedPart(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv,
            List<int> triangles, Vector3 center, float topRadius, float bottomRadius, float height, int sides)
        {
            var start = vertices.Count;
            var bottom = center.y - height * 0.5f;
            var top = center.y + height * 0.5f;
            for (var side = 0; side < sides; side++)
            {
                var angle = side * Mathf.PI * 2f / sides;
                var direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                vertices.Add(new Vector3(center.x, bottom, center.z) + direction * bottomRadius);
                normals.Add(direction);
                uv.Add(new Vector2(side / (float)sides, 0f));
                vertices.Add(new Vector3(center.x, top, center.z) + direction * topRadius);
                normals.Add(direction);
                uv.Add(new Vector2(side / (float)sides, 1f));
            }
            for (var side = 0; side < sides; side++)
            {
                var next = (side + 1) % sides;
                var a = start + side * 2;
                var b = start + next * 2;
                triangles.Add(a); triangles.Add(a + 1); triangles.Add(b + 1);
                triangles.Add(a); triangles.Add(b + 1); triangles.Add(b);
            }
            var bottomCenter = vertices.Count;
            vertices.Add(new Vector3(center.x, bottom, center.z)); normals.Add(Vector3.down); uv.Add(Vector2.zero);
            var topCenter = vertices.Count;
            vertices.Add(new Vector3(center.x, top, center.z)); normals.Add(Vector3.up); uv.Add(Vector2.one);
            for (var side = 0; side < sides; side++)
            {
                var next = (side + 1) % sides;
                triangles.Add(bottomCenter); triangles.Add(start + next * 2); triangles.Add(start + side * 2);
                triangles.Add(topCenter); triangles.Add(start + side * 2 + 1); triangles.Add(start + next * 2 + 1);
            }
        }
    }
}
