using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Presentation.Environment
{
    /// <summary>
    /// Read-only scene marker for the P9.1 art benchmark. Gameplay state remains owned by the
    /// existing domain and simulation layers; this component only exposes authored presentation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class P91RealisticWorldBenchmark : MonoBehaviour
    {
        [SerializeField] private Transform benchmarkCenter;
        [SerializeField] private Terrain terrain;
        [SerializeField] private GameObject[] trees = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] rocks = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] foliage = Array.Empty<GameObject>();
        [SerializeField] private Renderer waterRenderer;
        [SerializeField] private string sourceAsset = "Unity URP Terrain Sample 1.0.3 (Asset Store 213197)";

        public Transform BenchmarkCenter => benchmarkCenter;
        public Terrain Terrain => terrain;
        public IReadOnlyList<GameObject> Trees => trees;
        public IReadOnlyList<GameObject> Rocks => rocks;
        public IReadOnlyList<GameObject> Foliage => foliage;
        public Renderer WaterRenderer => waterRenderer;
        public string SourceAsset => sourceAsset;

        public void Configure(Transform center, Terrain terrainComponent, IEnumerable<GameObject> treeObjects,
            IEnumerable<GameObject> rockObjects, IEnumerable<GameObject> foliageObjects, Renderer water)
        {
            benchmarkCenter = center;
            terrain = terrainComponent;
            trees = (treeObjects ?? Array.Empty<GameObject>()).Where(x => x != null).ToArray();
            rocks = (rockObjects ?? Array.Empty<GameObject>()).Where(x => x != null).ToArray();
            foliage = (foliageObjects ?? Array.Empty<GameObject>()).Where(x => x != null).ToArray();
            waterRenderer = water;
        }

        public void ValidateConfiguration()
        {
            if (benchmarkCenter == null) throw new InvalidOperationException("P9.1 benchmark center is missing.");
            if (terrain == null || terrain.terrainData == null)
                throw new InvalidOperationException("P9.1 terrain is missing.");
            if (terrain.terrainData.terrainLayers == null || terrain.terrainData.terrainLayers.Length < 4)
                throw new InvalidOperationException("P9.1 requires at least four PBR terrain layers.");
            if (trees == null || trees.Length < 24) throw new InvalidOperationException("P9.1 forest is incomplete.");
            if (rocks == null || rocks.Length < 12) throw new InvalidOperationException("P9.1 rocks are incomplete.");
            if (foliage == null || foliage.Length < 48)
                throw new InvalidOperationException("P9.1 ground foliage is incomplete.");
            if (waterRenderer == null || waterRenderer.sharedMaterial == null)
                throw new InvalidOperationException("P9.1 water presentation is missing.");
            if (!AllRenderersUseValidShaders(trees.Concat(rocks).Concat(foliage)))
                throw new InvalidOperationException("P9.1 contains a missing or error shader.");
        }

        private static bool AllRenderersUseValidShaders(IEnumerable<GameObject> roots)
        {
            return roots.SelectMany(x => x.GetComponentsInChildren<Renderer>(true)).All(renderer =>
                renderer.sharedMaterials.Length > 0 && renderer.sharedMaterials.All(material =>
                    material != null && material.shader != null &&
                    !string.Equals(material.shader.name, "Hidden/InternalErrorShader", StringComparison.Ordinal)));
        }
    }
}
