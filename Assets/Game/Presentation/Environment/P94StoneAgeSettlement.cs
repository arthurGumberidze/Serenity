using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game.Domain.Work;
using Game.Presentation.Buildings;
using Game.Simulation.Work;
using UnityEngine;

namespace Game.Presentation.Environment
{
    /// <summary>
    /// Disposable P9.4 presentation metadata for the authored settlement and its approved project-owned assets.
    /// Canonical buildings, resources and work orders remain in their existing Domain/Simulation services.
    /// </summary>
    public sealed class P94StoneAgeSettlement : MonoBehaviour
    {
        public enum WorksiteKind
        {
            Knapping = 0,
            HideWorking = 1,
            WoodWorking = 2,
            Drying = 3,
            Hauling = 4,
            Construction = 5
        }

        [Serializable]
        public sealed class Worksite
        {
            [SerializeField] private string id;
            [SerializeField] private WorksiteKind kind;
            [SerializeField] private GameObject visualRoot;

            public string Id => id;
            public WorksiteKind Kind => kind;
            public GameObject VisualRoot => visualRoot;

            public Worksite Configure(string value, WorksiteKind valueKind, GameObject root)
            {
                id = value;
                kind = valueKind;
                visualRoot = root;
                return this;
            }
        }

        [SerializeField] private P93PopulatedBiomeWorld biomeWorld;
        [SerializeField] private GameObject shelterPrefab;
        [SerializeField] private GameObject storagePrefab;
        [SerializeField] private GameObject cropPlotPrefab;
        [SerializeField] private GameObject workIndicatorPrefab;
        [SerializeField] private Worksite[] worksites = Array.Empty<Worksite>();
        [SerializeField] private Transform[] supplyPresentations = Array.Empty<Transform>();
        [SerializeField] private GameObject[] characterPrefabs = Array.Empty<GameObject>();
        [SerializeField] private Material[] settlementMaterials = Array.Empty<Material>();
        [SerializeField] private string layoutHash;
        [SerializeField] private string sourceAsset =
            "Unity URP Terrain Sample 1.0.3 + UMA 3 + Serenity project-owned P9.4 derivatives";

        public P93PopulatedBiomeWorld BiomeWorld => biomeWorld;
        public GameObject ShelterPrefab => shelterPrefab;
        public GameObject StoragePrefab => storagePrefab;
        public GameObject CropPlotPrefab => cropPlotPrefab;
        public GameObject WorkIndicatorPrefab => workIndicatorPrefab;
        public IReadOnlyList<Worksite> Worksites => worksites;
        public IReadOnlyList<Transform> SupplyPresentations => supplyPresentations;
        public IReadOnlyList<GameObject> CharacterPrefabs => characterPrefabs;
        public IReadOnlyList<Material> SettlementMaterials => settlementMaterials;
        public string LayoutHash => layoutHash;
        public string SourceAsset => sourceAsset;

        public void Configure(P93PopulatedBiomeWorld world, GameObject shelter, GameObject storage,
            GameObject cropPlot, GameObject indicator, IEnumerable<Worksite> authoredWorksites,
            IEnumerable<Transform> supplies, IEnumerable<GameObject> characters,
            IEnumerable<Material> materials)
        {
            biomeWorld = world;
            shelterPrefab = shelter;
            storagePrefab = storage;
            cropPlotPrefab = cropPlot;
            workIndicatorPrefab = indicator;
            worksites = (authoredWorksites ?? Enumerable.Empty<Worksite>()).Where(x => x != null)
                .OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
            supplyPresentations = (supplies ?? Enumerable.Empty<Transform>()).Where(x => x != null).ToArray();
            characterPrefabs = (characters ?? Enumerable.Empty<GameObject>()).Where(x => x != null).ToArray();
            settlementMaterials = (materials ?? Enumerable.Empty<Material>()).Where(x => x != null).Distinct().ToArray();
            layoutHash = ComputeLayoutHash();
        }

        public string ComputeLayoutHash()
        {
            var hash = 14695981039346656037UL;
            foreach (var site in worksites.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                Mix(ref hash, site.Id);
                Mix(ref hash, (int)site.Kind);
                if (site.VisualRoot != null)
                {
                    Mix(ref hash, site.VisualRoot.transform.position);
                    Mix(ref hash, site.VisualRoot.transform.eulerAngles.y);
                }
            }
            foreach (var supply in supplyPresentations.Where(x => x != null)
                         .OrderBy(x => x.position.x).ThenBy(x => x.position.z))
            {
                Mix(ref hash, supply.position);
            }
            foreach (var prefab in new[] { shelterPrefab, storagePrefab, cropPlotPrefab, workIndicatorPrefab })
            {
                Mix(ref hash, prefab != null ? prefab.name : string.Empty);
                if (prefab != null)
                    foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true)
                                 .Where(x => x.sharedMesh != null).OrderBy(x => x.name, StringComparer.Ordinal))
                        Mix(ref hash, filter.sharedMesh.name);
            }
            return hash.ToString("X16", CultureInfo.InvariantCulture);
        }

        public void ValidateConfiguration()
        {
            if (biomeWorld == null) throw new InvalidOperationException("P9.4 requires the completed P9.3 world.");
            biomeWorld.ValidateConfiguration();
            ValidateBuildingPrefab(shelterPrefab, "shelter", 18);
            ValidateBuildingPrefab(storagePrefab, "storage", 14);
            ValidateBuildingPrefab(cropPlotPrefab, "crop plot", 12);
            if (workIndicatorPrefab == null || workIndicatorPrefab.GetComponentsInChildren<Renderer>(true).Length < 2)
                throw new InvalidOperationException("P9.4 requires a readable world work-priority indicator.");
            if (worksites.Length < 6 || Enum.GetValues(typeof(WorksiteKind)).Cast<WorksiteKind>()
                    .Any(kind => worksites.All(x => x.Kind != kind)) ||
                worksites.Any(x => string.IsNullOrWhiteSpace(x.Id) || x.VisualRoot == null) ||
                worksites.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != worksites.Length)
                throw new InvalidOperationException("P9.4 requires six distinct Stone Age worksite affordances.");
            if (supplyPresentations.Length != 4 || supplyPresentations.Any(x =>
                    x.GetComponentsInChildren<Renderer>(true).Length < 3 ||
                    x.GetComponentInChildren<Collider>(true) == null))
                throw new InvalidOperationException("P9.4 requires four detailed, clickable supply presentations.");
            if (characterPrefabs.Any(x => x == null))
                throw new InvalidOperationException("P9.4 optional character prefab references contain a missing asset.");
            if (settlementMaterials.Length < 7 || settlementMaterials.Any(x => x == null || x.shader == null ||
                    string.Equals(x.shader.name, "Hidden/InternalErrorShader", StringComparison.Ordinal)))
                throw new InvalidOperationException("P9.4 settlement materials are missing or invalid.");
            if (string.IsNullOrWhiteSpace(layoutHash) ||
                !string.Equals(layoutHash, ComputeLayoutHash(), StringComparison.Ordinal))
                throw new InvalidOperationException("P9.4 authored settlement layout hash is stale.");
        }

        private static void ValidateBuildingPrefab(GameObject prefab, string label, int minimumRenderers)
        {
            if (prefab == null || prefab.GetComponent<BuildingPresenter>() == null ||
                prefab.GetComponent<Collider>() == null)
                throw new InvalidOperationException("P9.4 " + label + " prefab is not interaction-ready.");
            var filters = prefab.GetComponentsInChildren<MeshFilter>(true).Where(x => x.sharedMesh != null).ToArray();
            if (filters.Length < minimumRenderers || prefab.GetComponentsInChildren<Renderer>(true).Length < minimumRenderers)
                throw new InvalidOperationException("P9.4 " + label + " remains below the authored-detail floor.");
            if (filters.Any(x => !x.sharedMesh.name.StartsWith("P94", StringComparison.Ordinal)))
                throw new InvalidOperationException("P9.4 " + label + " still exposes a primitive/non-P9.4 mesh.");
        }

        private static void Mix(ref ulong hash, string value)
        {
            foreach (var character in value ?? string.Empty) Mix(ref hash, character);
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

    /// <summary>One bounded presentation loop for all open work orders; no per-NPC update is introduced.</summary>
    public sealed class P94WorkOrderIndicatorSystem : MonoBehaviour
    {
        private readonly Dictionary<JobId, GameObject> indicators = new Dictionary<JobId, GameObject>();
        private WorkManager work;
        private GameObject indicatorPrefab;
        private Terrain terrain;
        private float nextRefresh;

        public int VisibleIndicatorCount => indicators.Count;

        public void Initialize(WorkManager manager, P94StoneAgeSettlement settlement)
        {
            work = manager ?? throw new ArgumentNullException(nameof(manager));
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            indicatorPrefab = settlement.WorkIndicatorPrefab != null
                ? settlement.WorkIndicatorPrefab
                : throw new InvalidOperationException("P9.4 work indicator prefab is missing.");
            terrain = settlement.BiomeWorld.BaseRegion.Terrain;
            RefreshNow();
        }

        public bool TryGetIndicator(JobId id, out GameObject indicator) => indicators.TryGetValue(id, out indicator);

        public void RefreshNow()
        {
            if (work == null || indicatorPrefab == null) return;
            var open = work.Jobs.Where(x => !x.IsTerminal).Take(24).ToArray();
            var openIds = new HashSet<JobId>(open.Select(x => x.Id));
            foreach (var stale in indicators.Keys.Where(x => !openIds.Contains(x)).ToArray())
            {
                if (indicators[stale] != null) Destroy(indicators[stale]);
                indicators.Remove(stale);
            }
            foreach (var order in open)
            {
                if (!indicators.TryGetValue(order.Id, out var indicator) || indicator == null)
                {
                    indicator = Instantiate(indicatorPrefab, transform);
                    indicators[order.Id] = indicator;
                }
                indicator.name = $"Work {order.Id} {order.Type} {order.Priority}";
                var position = new Vector3(order.Target.Position.X, order.Target.Position.Y, order.Target.Position.Z);
                if (terrain != null && terrain.terrainData != null)
                    position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
                indicator.transform.position = position + Vector3.up * 0.04f;
                indicator.transform.localScale = order.Status == WorkJobStatus.Active
                    ? Vector3.one * 1.16f : Vector3.one;
                ApplyPriority(indicator, order.Priority, order.Status);
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;
            RefreshNow();
        }

        private static void ApplyPriority(GameObject indicator, WorkPriority priority, WorkJobStatus status)
        {
            var color = priority switch
            {
                WorkPriority.Low => new Color(0.42f, 0.46f, 0.40f),
                WorkPriority.Normal => new Color(0.76f, 0.62f, 0.35f),
                WorkPriority.High => new Color(0.96f, 0.62f, 0.12f),
                WorkPriority.Urgent => new Color(0.92f, 0.28f, 0.08f),
                WorkPriority.Immediate => new Color(0.78f, 0.05f, 0.04f),
                _ => Color.white
            };
            if (status == WorkJobStatus.Active) color = Color.Lerp(color, Color.white, 0.24f);
            var block = new MaterialPropertyBlock();
            foreach (var renderer in indicator.GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                renderer.SetPropertyBlock(block);
            }
        }

        private void OnDestroy()
        {
            indicators.Clear();
            work = null;
        }
    }
}
