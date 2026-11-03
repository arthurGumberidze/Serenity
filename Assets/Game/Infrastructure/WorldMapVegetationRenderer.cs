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
        private readonly Dictionary<(Material, bool, bool), Material> materials = new();
        private readonly Plane[] planes = new Plane[6];
        public int InstanceCount { get; private set; }
        public int LastDrawnInstances { get; private set; }
        public double LastSubmissionMilliseconds { get; private set; }
        public bool DiagnosticDisableProbes { get; set; }
        private Bounds[] groundClearings = System.Array.Empty<Bounds>();
        private readonly HashSet<StableEntityId> hidden = new HashSet<StableEntityId>();

        public void SetHidden(IEnumerable<StableEntityId> ids)
        {
            var next = new HashSet<StableEntityId>(ids);
            if (hidden.SetEquals(next)) return;
            hidden.Clear(); hidden.UnionWith(next);
            RefreshVisibleMatrices();
        }

        public void SetGroundClearings(IEnumerable<Bounds> bounds)
        {
            var next = bounds.ToArray();
            if (groundClearings.SequenceEqual(next)) return;
            groundClearings = next;
            RefreshVisibleMatrices();
        }

        private void RefreshVisibleMatrices()
        {
            foreach (var batch in batches)
                batch.Matrices = batch.OriginalMatrices.Where((matrix, index) =>
                {
                    if (hidden.Contains(batch.Ids[index])) return false;
                    if (!batch.Ground) return true;
                    var position = matrix.GetColumn(3);
                    return !groundClearings.Any(bounds => position.x >= bounds.min.x && position.x <= bounds.max.x &&
                        position.z >= bounds.min.z && position.z <= bounds.max.z);
                }).ToArray();
        }

        public void Initialize(WorldMapVegetationPlan plan, StarterNatureCatalog catalog, System.Func<float,float,float> groundHeight = null)
        {
            var worldShader = Resources.Load<Material>("World/WorldMapVegetation")?.shader;
            if (worldShader == null) throw new System.InvalidOperationException("World vegetation shader is missing.");
            // Small spatial cells give the renderer useful culling bounds without rebuilding matrices.
            foreach (var group in plan.Instances.GroupBy(v => new
                     { X = Mathf.FloorToInt(v.Position.X / 64f), Z = Mathf.FloorToInt(v.Position.Z / 64f),
                       Prefab = Prefab(v, catalog), Ground = v.Kind == WorldMapVegetationKind.GroundCover,
                       Flowering = IsTree(v) && v.ClassId != "dense_conifer" && v.ClassId != "marsh_vegetation" }))
            {
                var prefab = group.Key.Prefab;
                var instances = group.ToArray();
                // Ground-cover records intentionally omit Y. GPU instances must project it explicitly;
                // unlike the old Terrain detail renderer, DrawMeshInstanced never follows the terrain.
                var positions = instances.Select(v => new Vector3(v.Position.X,
                    group.Key.Ground && groundHeight != null ? groundHeight(v.Position.X,v.Position.Z) : v.Position.Y, v.Position.Z)).ToArray();
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
                    var matrices = instances.Select((v,index) => Matrix4x4.TRS(
                        positions[index],
                        Quaternion.Euler(0f, v.RotationDegrees, 0f),
                        prefab.transform.localScale * v.UniformScale) * local).ToArray();
                    for (var sub = 0; sub < mesh.subMeshCount && sub < renderer.sharedMaterials.Length; sub++)
                    {
                        var source = renderer.sharedMaterials[sub];
                        if (source == null) continue;
                        var key = (source, group.Key.Flowering, group.Key.Ground);
                        if (!materials.TryGetValue(key, out var material))
                        {
                            material = new Material(source) { enableInstancing = true };
                            var foliage = source.HasProperty("_LeafTex");
                            var atlas = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") :
                                source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
                            var keepAuthored = prefab.name.StartsWith("Late_");
                            if (!keepAuthored) material.shader = worldShader;
                            if (!keepAuthored && !foliage)
                            {
                                material.SetTexture("_LeafTex", atlas); material.SetTexture("_TunkTex", atlas);
                                material.SetColor("_LeafBaseColour", Color.black);
                                material.SetColor("_LeafNoiseColour", Color.black);
                                material.SetColor("_TrunkBaseColour", Color.black);
                                material.SetColor("_TrunkNoiseColour", Color.black);
                                material.SetFloat("_AtlasMode", 1f);
                            }
                            if (!keepAuthored)
                            {
                                // Grass meshes do not carry the tree shader's blue leaf/bark mask.
                                // Sampling their absent bark texture produced opaque white polygons.
                                material.SetFloat("_ForceFoliage", group.Key.Ground && foliage ? 1f : 0f);
                                material.SetFloat("_Flowering", group.Key.Flowering ? 1f : 0f);
                                material.SetFloat("_BiomeStrength", group.Key.Ground ? .38f : .72f);
                            }
                            materials.Add(key, material);
                        }
                        for (var offset = 0; offset < matrices.Length; offset += 1023)
                            batches.Add(new Batch { Mesh = mesh, Material = material, Submesh = sub,
                                Lod = level, Ground = group.Key.Ground,
                                Center = new Vector3(group.Key.X * 64f + 32f,
                                    positions.Average(v => v.y), group.Key.Z * 64f + 32f),
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
            var late = catalog.RuntimeLate;
            if (late != null)
            {
                var map = catalog.RuntimeWorldToDesign; var snow = catalog.RuntimeSnowRegion;
                var point = new Vector2(value.Position.X*map.z+map.x,value.Position.Z*map.w+map.y);
                var distance = new Vector2((point.x-snow.x)/snow.z,(point.y-snow.y)/snow.w).magnitude;
                if (IsTree(value) && late.SnowTree != null && distance < .78f) return late.SnowTree;
                if (value.Kind == WorldMapVegetationKind.GroundCover && distance > 1f && Index(value.StableId,100)<18 &&
                    Mathf.PerlinNoise(value.Position.X * .055f + 17f, value.Position.Z * .055f) > .57f)
                {
                    var pool = value.ClassId == "marsh_vegetation" ? late.MarshPlants : late.Flowers;
                    if (pool != null && pool.Length>0) return pool[Index(value.StableId,pool.Length)];
                }
            }
            if (IsTree(value))
            {
                var regional = value.ClassId == "dense_conifer" ? catalog.WorldMapConifers :
                    value.ClassId == "marsh_vegetation" ? catalog.WorldMapWetlandTrees : null;
                // A small admixture of broadleaf trees keeps the forest edge from being a hard species line.
                if (regional != null && regional.Length > 0 && Index(value.StableId, 100) < 85)
                    return regional[Index(value.StableId, regional.Length)];
            }
            if (value.Kind == WorldMapVegetationKind.GroundCover && catalog.WorldMapFlowers?.Length > 0 &&
                (value.ClassId == "meadow" || value.ClassId == "open_woodland" || value.ClassId == "deciduous_grove"))
            {
                var patch = Mathf.PerlinNoise(value.Position.X * .035f, value.Position.Z * .035f);
                if (patch > .62f && Index(value.StableId, 100) < 28)
                    return catalog.WorldMapFlowers[Index(value.StableId, catalog.WorldMapFlowers.Length)];
            }
            // Small non-colliding stones and low shrubs provide a readable meadow edge.
            // They share the ground-cover batch/culling budget, not a MonoBehaviour per prop.
            if (value.Kind == WorldMapVegetationKind.GroundCover)
            {
                var detail = Index(value.StableId, 100);
                var patch = Mathf.PerlinNoise(value.Position.X * .055f + 91f, value.Position.Z * .055f);
                if (detail < 4 && catalog.WorldMapSmallProps?.Length > 0)
                    return catalog.WorldMapSmallProps[Index(value.StableId, catalog.WorldMapSmallProps.Length)];
                if (detail >= 4 && detail < 10 && patch > .60f && catalog.WorldMapShrubs?.Length > 0)
                    return catalog.WorldMapShrubs[Index(value.StableId, catalog.WorldMapShrubs.Length)];
            }
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
