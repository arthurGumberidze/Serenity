using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Presentation.Environment
{
    /// <summary>
    /// Disposable, deterministic biome dressing over the P9.2 Terrain. It owns no resource amount,
    /// location, navigation, construction or save state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class P93PopulatedBiomeWorld : MonoBehaviour
    {
        public enum BiomeKind
        {
            Forest,
            Understory,
            Meadow,
            RockyUpland,
            SoilClearing,
            Shoreline
        }

        [Serializable]
        public sealed class BiomeZone
        {
            [SerializeField] private string id;
            [SerializeField] private BiomeKind kind;
            [SerializeField] private Vector2 center;
            [SerializeField] private Vector2 radius;
            [SerializeField, Range(0f, 1f)] private float canopyDensity;
            [SerializeField, Range(0f, 1f)] private float groundCoverDensity;
            [SerializeField, Range(0f, 1f)] private float rockDensity;

            public string Id => id;
            public BiomeKind Kind => kind;
            public Vector2 Center => center;
            public Vector2 Radius => radius;
            public float CanopyDensity => canopyDensity;
            public float GroundCoverDensity => groundCoverDensity;
            public float RockDensity => rockDensity;

            public BiomeZone Configure(string stableId, BiomeKind biomeKind, Vector2 zoneCenter,
                Vector2 zoneRadius, float canopy, float cover, float rocks)
            {
                id = stableId;
                kind = biomeKind;
                center = zoneCenter;
                radius = zoneRadius;
                canopyDensity = Mathf.Clamp01(canopy);
                groundCoverDensity = Mathf.Clamp01(cover);
                rockDensity = Mathf.Clamp01(rocks);
                return this;
            }
        }

        [Serializable]
        public sealed class ResourceFormation
        {
            [SerializeField, Min(0)] private int stableIndex;
            [SerializeField] private string recipeId;
            [SerializeField] private Transform clickableRoot;
            [SerializeField] private GameObject[] decorativeSatellites = Array.Empty<GameObject>();

            public int StableIndex => stableIndex;
            public string RecipeId => recipeId;
            public Transform ClickableRoot => clickableRoot;
            public IReadOnlyList<GameObject> DecorativeSatellites => decorativeSatellites;

            public ResourceFormation Configure(int index, string recipe, Transform root,
                IEnumerable<GameObject> satellites)
            {
                stableIndex = index;
                recipeId = recipe;
                clickableRoot = root;
                decorativeSatellites = Compact(satellites);
                return this;
            }
        }

        [SerializeField] private P92RealisticGameplayRegion baseRegion;
        [SerializeField] private int scatterSeed = 93031;
        [SerializeField] private BiomeZone[] zones = Array.Empty<BiomeZone>();
        [SerializeField] private GameObject[] canopyTrees = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] majorRocks = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] groundCover = Array.Empty<GameObject>();
        [SerializeField] private ResourceFormation[] resourceFormations = Array.Empty<ResourceFormation>();
        [SerializeField, Min(0)] private int accessibleCoverageSamples;
        [SerializeField, Min(0)] private int populatedCoverageSamples;
        [SerializeField] private string layoutHash;
        [SerializeField] private string sourceAsset =
            "Unity URP Terrain Sample 1.0.3 (Asset Store 213197) + Serenity deterministic composition";
        private Dictionary<GameObject, Vector3> authoredTreePositions;

        public P92RealisticGameplayRegion BaseRegion => baseRegion;
        public int ScatterSeed => scatterSeed;
        public IReadOnlyList<BiomeZone> Zones => zones;
        public IReadOnlyList<GameObject> CanopyTrees => canopyTrees;
        public IReadOnlyList<GameObject> MajorRocks => majorRocks;
        public IReadOnlyList<GameObject> GroundCover => groundCover;
        public IReadOnlyList<ResourceFormation> ResourceFormations => resourceFormations;
        public int AccessibleCoverageSamples => accessibleCoverageSamples;
        public int PopulatedCoverageSamples => populatedCoverageSamples;
        public float CoverageRatio => accessibleCoverageSamples <= 0
            ? 0f : populatedCoverageSamples / (float)accessibleCoverageSamples;
        public string LayoutHash => layoutHash;
        public string SourceAsset => sourceAsset;

        /// <summary>Preserve the authored-layout audit while disposable runtime views follow the tiled terrain.</summary>
        public void CaptureAuthoredTreePositions()
        {
            if (authoredTreePositions != null) return;
            ValidateConfiguration();
            authoredTreePositions = canopyTrees.Concat(majorRocks).Concat(groundCover)
                .Concat(resourceFormations.SelectMany(x => x.DecorativeSatellites))
                .Where(value => value != null).Distinct()
                .ToDictionary(value => value, value => value.transform.position);
        }

        public void Configure(P92RealisticGameplayRegion region, int seed, IEnumerable<BiomeZone> biomeZones,
            IEnumerable<GameObject> trees, IEnumerable<GameObject> rocks, IEnumerable<GameObject> cover,
            IEnumerable<ResourceFormation> formations, int coverageSamples, int coveredSamples)
        {
            baseRegion = region;
            scatterSeed = seed;
            zones = (biomeZones ?? Enumerable.Empty<BiomeZone>()).Where(x => x != null).ToArray();
            canopyTrees = Compact(trees);
            majorRocks = Compact(rocks);
            groundCover = Compact(cover);
            resourceFormations = (formations ?? Enumerable.Empty<ResourceFormation>())
                .Where(x => x != null).OrderBy(x => x.StableIndex).ToArray();
            accessibleCoverageSamples = Mathf.Max(0, coverageSamples);
            populatedCoverageSamples = Mathf.Clamp(coveredSamples, 0, accessibleCoverageSamples);
            layoutHash = ComputeLayoutHash();
        }

        public string ComputeLayoutHash()
        {
            var hash = 14695981039346656037UL;
            Mix(ref hash, scatterSeed);
            foreach (var zone in zones.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                Mix(ref hash, zone.Id);
                Mix(ref hash, (int)zone.Kind);
                Mix(ref hash, zone.Center);
                Mix(ref hash, zone.Radius);
            }
            foreach (var value in canopyTrees.Concat(majorRocks).Concat(groundCover)
                         .Where(x => x != null).OrderBy(x => x.name, StringComparer.Ordinal))
            {
                Mix(ref hash, value.name);
                Mix(ref hash, authoredTreePositions != null &&
                    authoredTreePositions.TryGetValue(value, out var authoredPosition)
                    ? authoredPosition : value.transform.position);
                Mix(ref hash, value.transform.localScale);
                Mix(ref hash, value.transform.eulerAngles.y);
            }
            foreach (var formation in resourceFormations.OrderBy(x => x.StableIndex))
            {
                Mix(ref hash, formation.StableIndex);
                Mix(ref hash, formation.RecipeId);
                foreach (var satellite in formation.DecorativeSatellites.Where(x => x != null)
                             .OrderBy(x => x.name, StringComparer.Ordinal))
                {
                    Mix(ref hash, satellite.name);
                    Mix(ref hash, authoredTreePositions != null &&
                        authoredTreePositions.TryGetValue(satellite, out var authoredSatellite)
                        ? authoredSatellite : satellite.transform.position);
                }
            }
            return hash.ToString("X16", CultureInfo.InvariantCulture);
        }

        public void ValidateConfiguration()
        {
            if (baseRegion == null) throw new InvalidOperationException("P9.3 requires the P9.2 Terrain region.");
            baseRegion.ValidateConfiguration();
            if (scatterSeed != 93031)
                throw new InvalidOperationException("P9.3 deterministic scatter seed changed unexpectedly.");
            var requiredKinds = Enum.GetValues(typeof(BiomeKind)).Cast<BiomeKind>();
            if (zones.Length < 7 || requiredKinds.Any(kind => zones.All(x => x.Kind != kind)) ||
                zones.Any(x => string.IsNullOrWhiteSpace(x.Id) || x.Radius.x <= 0f || x.Radius.y <= 0f) ||
                zones.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != zones.Length)
                throw new InvalidOperationException("P9.3 requires distinct forest, understory, meadow, rock, soil and shore zones.");
            if (baseRegion.Terrain.terrainData.terrainLayers.Length < 7)
                throw new InvalidOperationException("P9.3 requires seven official terrain layers for readable soils and shore.");
            if (canopyTrees.Length < 170 || majorRocks.Length < 90 || groundCover.Length < 900)
                throw new InvalidOperationException("P9.3 biome density is below the accepted populated-world floor.");
            if (canopyTrees.Any(x => x.GetComponentInChildren<LODGroup>(true) == null ||
                                     x.GetComponentInChildren<Collider>(true) == null))
                throw new InvalidOperationException("P9.3 canopy trees require authored LOD and physical trunk collision.");
            if (majorRocks.Any(x => x.GetComponentInChildren<LODGroup>(true) == null ||
                                    x.GetComponentInChildren<Collider>(true) == null))
                throw new InvalidOperationException("P9.3 major rocks require authored LOD and physical collision.");
            if (groundCover.Any(x => x.GetComponentInChildren<Collider>(true) != null))
                throw new InvalidOperationException("P9.3 traversable ground cover must not create hidden navigation obstacles.");
            if (resourceFormations.Length != 24 ||
                !resourceFormations.Select(x => x.StableIndex).SequenceEqual(Enumerable.Range(0, 24)) ||
                resourceFormations.Any(x => x.ClickableRoot == null ||
                    x.ClickableRoot.GetComponentInChildren<Collider>(true) == null ||
                    x.DecorativeSatellites.Count < 4))
                throw new InvalidOperationException("P9.3 requires 24 clickable canonical resource formations.");
            if (accessibleCoverageSamples < 80 || CoverageRatio < 0.92f)
                throw new InvalidOperationException("P9.3 does not populate enough of the accessible land.");
            if (string.IsNullOrWhiteSpace(layoutHash) ||
                !string.Equals(layoutHash, ComputeLayoutHash(), StringComparison.Ordinal))
                throw new InvalidOperationException("P9.3 deterministic layout hash is stale.");
            if (!ValidShaders(canopyTrees.Concat(majorRocks).Concat(groundCover)
                    .Concat(resourceFormations.SelectMany(x => x.DecorativeSatellites))))
                throw new InvalidOperationException("P9.3 contains a missing or error shader.");
        }

        private static GameObject[] Compact(IEnumerable<GameObject> values) =>
            (values ?? Enumerable.Empty<GameObject>()).Where(x => x != null).ToArray();

        private static bool ValidShaders(IEnumerable<GameObject> roots) => roots.Where(x => x != null)
            .SelectMany(x => x.GetComponentsInChildren<Renderer>(true)).All(renderer =>
                renderer.sharedMaterials.Length > 0 && renderer.sharedMaterials.All(material =>
                    material != null && material.shader != null &&
                    !string.Equals(material.shader.name, "Hidden/InternalErrorShader", StringComparison.Ordinal)));

        private static void Mix(ref ulong hash, string value)
        {
            foreach (var character in value ?? string.Empty) Mix(ref hash, character);
        }

        private static void Mix(ref ulong hash, Vector2 value)
        {
            Mix(ref hash, Mathf.RoundToInt(value.x * 1000f));
            Mix(ref hash, Mathf.RoundToInt(value.y * 1000f));
        }

        private static void Mix(ref ulong hash, Vector3 value)
        {
            Mix(ref hash, Mathf.RoundToInt(value.x * 1000f));
            Mix(ref hash, Mathf.RoundToInt(value.y * 1000f));
            Mix(ref hash, Mathf.RoundToInt(value.z * 1000f));
        }

        private static void Mix(ref ulong hash, float value) => Mix(ref hash, Mathf.RoundToInt(value * 1000f));

        private static void Mix(ref ulong hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= 1099511628211UL;
            }
        }
    }
}
