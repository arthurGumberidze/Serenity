using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.World;
using Game.Presentation.Environment;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Infrastructure
{
    /// <summary>Immutable mesh batches prepared at loading, with camera culling only during play.</summary>
    public sealed class WorldMapVegetationRenderer : MonoBehaviour
    {
        private sealed class Batch
        {
            public Mesh Mesh;
            public Material Material;
            public int Submesh;
            public int Lod;
            public bool Ground;
            public Vector3 Center;
            public Matrix4x4[] Matrices;
            public Matrix4x4[] OriginalMatrices;
            public StableEntityId[] Ids;
        }
        private readonly List<Batch> batches = new List<Batch>();
        private readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();
        private readonly Plane[] planes = new Plane[6];
        public int InstanceCount { get; private set; }
        public int LastDrawnInstances { get; private set; }
        public double LastSubmissionMilliseconds { get; private set; }
        public bool DiagnosticDisableProbes { get; set; }
        private readonly HashSet<StableEntityId> hidden = new HashSet<StableEntityId>();

        public void SetHidden(IEnumerable<StableEntityId> ids)
        {
            var next = new HashSet<StableEntityId>(ids);
            if (hidden.SetEquals(next)) return;
            hidden.Clear(); hidden.UnionWith(next);
            foreach (var batch in batches)
                batch.Matrices = batch.OriginalMatrices.Where((_, index) => !hidden.Contains(batch.Ids[index])).ToArray();
        }

        public void Initialize(WorldMapVegetationPlan plan, StarterNatureCatalog catalog)
        {
            var worldShader = Resources.Load<Material>("World/WorldMapVegetation")?.shader;
            if (worldShader == null) throw new System.InvalidOperationException("World vegetation shader is missing.");
            // Small spatial cells give the renderer useful culling bounds without rebuilding matrices.
            foreach (var group in plan.Instances.GroupBy(v => new
                     { X = Mathf.FloorToInt(v.Position.X / 64f), Z = Mathf.FloorToInt(v.Position.Z / 64f),
                       Prefab = Prefab(v, catalog), Ground = v.Kind == WorldMapVegetationKind.GroundCover }))
            {
                var prefab = group.Key.Prefab;
                var instances = group.ToArray();
                InstanceCount += instances.Length;
                var lodGroup = prefab.GetComponent<LODGroup>();
                var lods = lodGroup != null ? lodGroup.GetLODs() : new[]
                    { new LOD(0f, prefab.GetComponentsInChildren<Renderer>(true)) };
                for (var level = 0; level < 3; level++)
                foreach (var renderer in lods[Mathf.Min(level, lods.Length - 1)].renderers)
                {
                    if (renderer == null) continue;
                    var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) continue;
                    var local = prefab.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                    var matrices = instances.Select(v => Matrix4x4.TRS(
                        new Vector3(v.Position.X, v.Position.Y, v.Position.Z),
                        Quaternion.Euler(0f, v.RotationDegrees, 0f),
                        prefab.transform.localScale * v.UniformScale) * local).ToArray();
                    for (var sub = 0; sub < mesh.subMeshCount && sub < renderer.sharedMaterials.Length; sub++)
                    {
                        var source = renderer.sharedMaterials[sub];
                        if (source == null) continue;
                        if (!materials.TryGetValue(source, out var material))
                        {
                            material = new Material(source) { enableInstancing = true };
                            if (source.shader.name == "SyntyStudios/VegitationShader")
                                material.shader = worldShader;
                            materials.Add(source, material);
                        }
                        for (var offset = 0; offset < matrices.Length; offset += 1023)
                            batches.Add(new Batch { Mesh = mesh, Material = material, Submesh = sub,
                                Lod = level, Ground = group.Key.Ground,
                                Center = new Vector3(group.Key.X * 64f + 32f,
                                    instances.Average(v => v.Position.Y), group.Key.Z * 64f + 32f),
                                Matrices = matrices.Skip(offset).Take(1023).ToArray(),
                                OriginalMatrices = matrices.Skip(offset).Take(1023).ToArray(),
                                Ids = instances.Skip(offset).Take(1023).Select(value => value.StableId).ToArray() });
                    }
                }
            }
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null) return;
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            LastDrawnInstances = 0;
            foreach (var batch in batches)
            {
                if (batch.Matrices.Length == 0) continue;
                var distance = Vector3.Distance(camera.transform.position, batch.Center);
                if (distance > (batch.Ground ? 170f : 1100f)) continue;
                if (!GeometryUtility.TestPlanesAABB(planes, new Bounds(batch.Center, new Vector3(110f, 100f, 110f)))) continue;
                // A 17m tree occupies ~15% of the screen at 100m; avoid close-up meshes across the forest.
                var level = distance < 55f ? 0 : distance < 140f ? 1 : 2;
                if (batch.Lod != level) continue;
                Graphics.DrawMeshInstanced(batch.Mesh, batch.Submesh, batch.Material, batch.Matrices,
                    batch.Matrices.Length, null, batch.Ground || distance > 250f ? ShadowCastingMode.Off : ShadowCastingMode.On,
                    true, gameObject.layer, camera, DiagnosticDisableProbes ? LightProbeUsage.Off : LightProbeUsage.BlendProbes);
                LastDrawnInstances += batch.Matrices.Length;
            }
            LastSubmissionMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) *
                1000d / System.Diagnostics.Stopwatch.Frequency;
        }

        public static bool IsTree(WorldMapVegetationInstance value) =>
            value.Kind == WorldMapVegetationKind.InteractiveTree ||
            (value.Kind == WorldMapVegetationKind.DecorativeTreeOrShrub && Index(value.StableId, 100) < 60);

        public static GameObject Prefab(WorldMapVegetationInstance value, StarterNatureCatalog catalog)
        {
            var prefabs = value.Kind == WorldMapVegetationKind.GroundCover ? catalog.WorldMapGroundCover :
                IsTree(value)
                    ? catalog.WorldMapTrees : catalog.WorldMapShrubs;
            return prefabs[Index(value.StableId, prefabs.Length)];
        }

        private static int Index(StableEntityId id, int count)
        {
            var hash = 17;
            foreach (var value in id.ToGuid().ToByteArray()) hash = unchecked(hash * 31 + value);
            return (hash & int.MaxValue) % count;
        }

        private void OnDestroy()
        {
            foreach (var material in materials.Values) Destroy(material);
        }
    }
}
