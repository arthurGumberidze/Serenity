using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.World;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Infrastructure
{
    /// <summary>
    /// Disposable Unity projection of the versioned tiled-world authority. Only the camera ring is rendered;
    /// actor rings and active command corridors retain collision/navigation without becoming extra visuals.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TiledTerrainWorld : MonoBehaviour
    {
        public const int HeightmapResolution = 65;
        private const float RefreshIntervalSeconds = 0.5f;
        private readonly Dictionary<WorldTileId, LoadedTile> loaded = new Dictionary<WorldTileId, LoadedTile>();
        private readonly HashSet<WorldTileId> visual = new HashSet<WorldTileId>();
        private readonly List<GameObject> linkObjects = new List<GameObject>();
        private TiledWorldStreamingPlanner planner;
        private TerrainLayer[] sourceLayers = Array.Empty<TerrainLayer>();
        private Material sourceTerrainMaterial;
        private Func<WorldPosition> cameraFocusProvider;
        private Func<IEnumerable<WorldPosition>> physicalPositionProvider;
        private Func<IEnumerable<Tuple<WorldPosition, WorldPosition>>> navigationRouteProvider;
        private float nextRefresh;
        private bool navigationEnabled;
        private GameObject strategicOverview;
        private Material overviewGroundMaterial;
        private Material overviewWaterMaterial;
        private Material localWaterMaterial;
        private bool strategicMode;

        private sealed class LoadedTile
        {
            public WorldTileId Id;
            public GameObject Root;
            public Terrain Terrain;
            public TerrainData Data;
            public TerrainCollider Collider;
            public NavMeshSurface Navigation;
            public GameObject WaterRoot;
        }

        public event Action VisualTilesChanged;

        public TiledWorldDefinition Definition { get; private set; }
        public TiledWorldHeightSampler HeightSampler { get; private set; }
        public int LoadedCollisionTileCount => loaded.Count;
        public int LoadedVisualTileCount => visual.Count;
        public IReadOnlyList<WorldTileId> LoadedTiles => loaded.Keys.OrderBy(value => value).ToArray();
        public IReadOnlyList<WorldTileId> VisualTiles => visual.OrderBy(value => value).ToArray();
        public bool StrategicOverviewVisible => strategicOverview != null && strategicOverview.activeSelf;
        public NavMeshSurface PrimaryNavigationSurface
        {
            get
            {
                if (Definition == null || !Definition.TryGetTile(Definition.PlayerStart.X,
                        Definition.PlayerStart.Z, out var start) || !loaded.TryGetValue(start, out var tile)) return null;
                return tile.Navigation;
            }
        }

        public void Initialize(TiledWorldDefinition definition, TiledWorldHeightSampler heightSampler,
            Terrain legacyTerrain)
        {
            if (Definition != null) throw new InvalidOperationException("The tiled world is already initialized.");
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            HeightSampler = heightSampler ?? throw new ArgumentNullException(nameof(heightSampler));
            if (!ReferenceEquals(definition, heightSampler.Definition))
                throw new ArgumentException("Height sampler must use the active world definition.", nameof(heightSampler));
            planner = new TiledWorldStreamingPlanner(definition);
            if (legacyTerrain != null && legacyTerrain.terrainData != null)
            {
                sourceLayers = legacyTerrain.terrainData.terrainLayers ?? Array.Empty<TerrainLayer>();
                sourceTerrainMaterial = legacyTerrain.materialTemplate;
                legacyTerrain.drawHeightmap = false;
                legacyTerrain.drawTreesAndFoliage = false;
                legacyTerrain.enabled = false;
                var legacyCollider = legacyTerrain.GetComponent<TerrainCollider>();
                if (legacyCollider != null) legacyCollider.enabled = false;
            }
            BuildStrategicOverview();
            RefreshNow(definition.PlayerStart, Array.Empty<WorldPosition>(),
                Array.Empty<Tuple<WorldPosition, WorldPosition>>());
        }

        public void ConfigureStreaming(Func<WorldPosition> cameraFocus,
            Func<IEnumerable<WorldPosition>> physicalPositions,
            Func<IEnumerable<Tuple<WorldPosition, WorldPosition>>> navigationRoutes)
        {
            cameraFocusProvider = cameraFocus ?? throw new ArgumentNullException(nameof(cameraFocus));
            physicalPositionProvider = physicalPositions ?? throw new ArgumentNullException(nameof(physicalPositions));
            navigationRouteProvider = navigationRoutes ?? throw new ArgumentNullException(nameof(navigationRoutes));
            RefreshFromProviders();
        }

        public void RefreshNow(WorldPosition cameraFocus, IEnumerable<WorldPosition> physicalPositions,
            IEnumerable<Tuple<WorldPosition, WorldPosition>> navigationRoutes)
        {
            if (Definition == null) throw new InvalidOperationException("The tiled world is not initialized.");
            var plan = planner.CreatePlan(cameraFocus, physicalPositions ?? Array.Empty<WorldPosition>(),
                navigationRoutes ?? Array.Empty<Tuple<WorldPosition, WorldPosition>>());
            ApplyPlan(plan);
        }

        public Vector3 ProjectToGround(Vector3 worldPosition, float offset = 0f)
        {
            if (HeightSampler == null || !Definition.ContainsClosed(worldPosition.x, worldPosition.z))
                return worldPosition;
            worldPosition.y = HeightSampler.SampleHeight(worldPosition.x, worldPosition.z) + offset;
            return worldPosition;
        }

        public bool TryGetTerrain(WorldTileId id, out Terrain terrain)
        {
            terrain = null;
            if (!loaded.TryGetValue(id, out var tile)) return false;
            terrain = tile.Terrain;
            return terrain != null;
        }

        public void BuildNavigation()
        {
            navigationEnabled = true;
            foreach (var tile in loaded.Values.OrderBy(value => value.Id)) EnsureNavigation(tile);
            RebuildLinks();
        }

        public void SetStrategicMode(bool active)
        {
            strategicMode = active;
            if (strategicOverview != null) strategicOverview.SetActive(active);
            foreach (var pair in loaded)
                pair.Value.Terrain.drawHeightmap = !active && visual.Contains(pair.Key);
            if (!active && cameraFocusProvider != null) RefreshFromProviders();
        }

        private void Update()
        {
            if (Definition == null || strategicMode || cameraFocusProvider == null ||
                Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + RefreshIntervalSeconds;
            RefreshFromProviders();
        }

        private void RefreshFromProviders()
        {
            RefreshNow(cameraFocusProvider(), physicalPositionProvider() ?? Array.Empty<WorldPosition>(),
                navigationRouteProvider() ?? Array.Empty<Tuple<WorldPosition, WorldPosition>>());
        }

        private void ApplyPlan(TiledWorldStreamingPlan plan)
        {
            var required = new HashSet<WorldTileId>(plan.CollisionTiles);
            var added = false;
            foreach (var id in plan.CollisionTiles)
            {
                if (loaded.ContainsKey(id)) continue;
                var tile = CreateTile(id);
                loaded.Add(id, tile);
                if (navigationEnabled) EnsureNavigation(tile);
                added = true;
            }
            foreach (var id in loaded.Keys.Where(value => !required.Contains(value)).ToArray())
            {
                var tile = loaded[id];
                loaded.Remove(id);
                DestroyTile(tile);
                added = true;
            }
            visual.Clear();
            foreach (var id in plan.VisualTiles) visual.Add(id);
            foreach (var pair in loaded)
            {
                var visible = !strategicMode && visual.Contains(pair.Key);
                pair.Value.Terrain.drawHeightmap = visible;
                pair.Value.Terrain.drawTreesAndFoliage = visible;
                if (pair.Value.WaterRoot != null) pair.Value.WaterRoot.SetActive(visible);
                pair.Value.Collider.enabled = true;
            }
            ConnectTerrainNeighbours();
            if (navigationEnabled && added) RebuildLinks();
            VisualTilesChanged?.Invoke();
        }

        private LoadedTile CreateTile(WorldTileId id)
        {
            var data = new TerrainData
            {
                name = "P10.9 TerrainData " + id,
                heightmapResolution = HeightmapResolution,
                size = new Vector3(Definition.TileSizeMeters, TiledWorldHeightSampler.TerrainHeightMeters,
                    Definition.TileSizeMeters),
                alphamapResolution = 16,
                baseMapResolution = 16,
                terrainLayers = sourceLayers
            };
            data.SetHeights(0, 0, HeightSampler.CreateNormalizedHeights(id, HeightmapResolution));
            if (sourceLayers.Length > 0) data.SetAlphamaps(0, 0, CreateTerrainPaint(id, data));
            var root = Terrain.CreateTerrainGameObject(data);
            root.name = "P10.9 Terrain " + id;
            root.transform.SetParent(transform, true);
            var origin = Definition.TileOrigin(id);
            root.transform.position = new Vector3(origin.X, 0f, origin.Z);
            var terrain = root.GetComponent<Terrain>();
            terrain.allowAutoConnect = false;
            terrain.groupingID = 109;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 12f;
            terrain.basemapDistance = 1400f;
            if (sourceTerrainMaterial != null) terrain.materialTemplate = sourceTerrainMaterial;
            var collider = root.GetComponent<TerrainCollider>();
            if (collider == null) collider = root.AddComponent<TerrainCollider>();
            collider.terrainData = data;
            var water = CreateLocalWater(id, root.transform);
            return new LoadedTile { Id = id, Root = root, Terrain = terrain, Data = data, Collider = collider,
                WaterRoot = water };
        }

        private float[,,] CreateTerrainPaint(WorldTileId id, TerrainData data)
        {
            var map = new float[data.alphamapHeight, data.alphamapWidth, sourceLayers.Length];
            var origin = Definition.TileOrigin(id);
            for (var z = 0; z < data.alphamapHeight; z++)
            for (var x = 0; x < data.alphamapWidth; x++)
            {
                var wx = origin.X + (x + 0.5f) * Definition.TileSizeMeters / data.alphamapWidth;
                var wz = origin.Z + (z + 0.5f) * Definition.TileSizeMeters / data.alphamapHeight;
                var uses = Definition.UsesAt(wx, wz);
                var primary = 0;
                if (sourceLayers.Length > 1 && uses.HasFlag(WorldZoneUse.Forest)) primary = 1;
                if (sourceLayers.Length > 2 && uses.HasFlag(WorldZoneUse.Ore)) primary = 2;
                if (sourceLayers.Length > 3 && (uses.HasFlag(WorldZoneUse.Agriculture) ||
                    uses.HasFlag(WorldZoneUse.Settlement) || HeightSampler.DistanceToRiver(wx, wz) < 95f)) primary = 3;
                map[z, x, primary] = 0.78f;
                map[z, x, 0] += 0.22f;
            }
            return map;
        }

        private GameObject CreateLocalWater(WorldTileId id, Transform parent)
        {
            const int resolution = 25;
            var origin = Definition.TileOrigin(id);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var step = Definition.TileSizeMeters / (float)resolution;
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
            {
                var wx = origin.X + (x + 0.5f) * step;
                var wz = origin.Z + (z + 0.5f) * step;
                if (!HeightSampler.IsWater(wx, wz)) continue;
                var start = vertices.Count;
                var x0 = x * step; var x1 = (x + 1) * step;
                var z0 = z * step; var z1 = (z + 1) * step;
                vertices.Add(new Vector3(x0, TiledWorldHeightSampler.WaterLevelMeters, z0));
                vertices.Add(new Vector3(x1, TiledWorldHeightSampler.WaterLevelMeters, z0));
                vertices.Add(new Vector3(x0, TiledWorldHeightSampler.WaterLevelMeters, z1));
                vertices.Add(new Vector3(x1, TiledWorldHeightSampler.WaterLevelMeters, z1));
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start + 1); triangles.Add(start + 2); triangles.Add(start + 3);
            }
            if (vertices.Count == 0) return null;
            var water = new GameObject("P10.9 Local Water " + id);
            water.transform.SetParent(parent, false);
            var mesh = new Mesh { name = "P10.9 Local Water Mesh " + id };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            water.AddComponent<MeshFilter>().sharedMesh = mesh;
            localWaterMaterial ??= CreateMaterial(new Color(0.08f, 0.48f, 0.72f, 0.86f), "P10.9 Local Water");
            water.AddComponent<MeshRenderer>().sharedMaterial = localWaterMaterial;
            return water;
        }

        private void EnsureNavigation(LoadedTile tile)
        {
            if (tile.Navigation != null) return;
            var surface = tile.Root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = new Vector3(Definition.TileSizeMeters * 0.5f,
                TiledWorldHeightSampler.TerrainHeightMeters * 0.5f, Definition.TileSizeMeters * 0.5f);
            surface.size = new Vector3(Definition.TileSizeMeters + 2f,
                TiledWorldHeightSampler.TerrainHeightMeters + 20f, Definition.TileSizeMeters + 2f);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            tile.Navigation = surface;
        }

        private void RebuildLinks()
        {
            foreach (var value in linkObjects) if (value != null) Destroy(value);
            linkObjects.Clear();
            foreach (var tile in loaded.Values.OrderBy(value => value.Id))
            {
                AddLinks(tile, new WorldTileId(tile.Id.X + 1, tile.Id.Z), true);
                AddLinks(tile, new WorldTileId(tile.Id.X, tile.Id.Z + 1), false);
            }
        }

        private void AddLinks(LoadedTile from, WorldTileId toId, bool eastWest)
        {
            if (from.Navigation == null || !loaded.TryGetValue(toId, out var to) || to.Navigation == null) return;
            var origin = Definition.TileOrigin(from.Id);
            for (var index = 0; index < 10; index++)
            {
                var along = 50f + index * 100f;
                var worldX = eastWest ? origin.X + Definition.TileSizeMeters : origin.X + along;
                var worldZ = eastWest ? origin.Z + along : origin.Z + Definition.TileSizeMeters;
                var ax = worldX + (eastWest ? -2f : 0f);
                var az = worldZ + (eastWest ? 0f : -2f);
                var bx = worldX + (eastWest ? 2f : 0f);
                var bz = worldZ + (eastWest ? 0f : 2f);
                if (!HeightSampler.IsBuildable(ax, az) || !HeightSampler.IsBuildable(bx, bz)) continue;
                var linkObject = new GameObject("P10.9 NavLink " + from.Id + " " + toId + " " + index);
                linkObject.transform.SetParent(from.Root.transform, false);
                var link = linkObject.AddComponent<NavMeshLink>();
                link.startPoint = new Vector3(ax - origin.X, HeightSampler.SampleHeight(ax, az) + 0.1f,
                    az - origin.Z);
                link.endPoint = new Vector3(bx - origin.X, HeightSampler.SampleHeight(bx, bz) + 0.1f,
                    bz - origin.Z);
                link.width = 70f;
                link.bidirectional = true;
                linkObjects.Add(linkObject);
            }
        }

        private void ConnectTerrainNeighbours()
        {
            foreach (var pair in loaded)
            {
                loaded.TryGetValue(new WorldTileId(pair.Key.X - 1, pair.Key.Z), out var left);
                loaded.TryGetValue(new WorldTileId(pair.Key.X + 1, pair.Key.Z), out var right);
                loaded.TryGetValue(new WorldTileId(pair.Key.X, pair.Key.Z + 1), out var top);
                loaded.TryGetValue(new WorldTileId(pair.Key.X, pair.Key.Z - 1), out var bottom);
                pair.Value.Terrain.SetNeighbors(left?.Terrain, top?.Terrain, right?.Terrain, bottom?.Terrain);
            }
        }

        private void BuildStrategicOverview()
        {
            strategicOverview = new GameObject("P10.9 Strategic Geography");
            strategicOverview.transform.SetParent(transform, false);
            var ground = new GameObject("Ground");
            ground.transform.SetParent(strategicOverview.transform, false);
            ground.AddComponent<MeshFilter>().sharedMesh = BuildOverviewGroundMesh();
            overviewGroundMaterial = CreateMaterial(new Color(0.42f, 0.53f, 0.28f, 1f), "P10.9 Overview Ground");
            ground.AddComponent<MeshRenderer>().sharedMaterial = overviewGroundMaterial;
            var water = new GameObject("River And Lakes");
            water.transform.SetParent(strategicOverview.transform, false);
            water.AddComponent<MeshFilter>().sharedMesh = BuildOverviewWaterMesh();
            overviewWaterMaterial = CreateMaterial(new Color(0.08f, 0.48f, 0.74f, 1f), "P10.9 Overview Water");
            water.AddComponent<MeshRenderer>().sharedMaterial = overviewWaterMaterial;
            BuildStrategicZoneOverlays();
            strategicOverview.SetActive(false);
        }

        private void BuildStrategicZoneOverlays()
        {
            foreach (var zone in Definition.Zones.Where(value => !value.Uses.HasFlag(WorldZoneUse.Fishing)))
            {
                var color = zone.Uses.HasFlag(WorldZoneUse.Settlement) ? new Color(0.55f, 0.51f, 0.43f, 1f) :
                    zone.Uses.HasFlag(WorldZoneUse.Ore) ? new Color(0.36f, 0.34f, 0.31f, 1f) :
                    zone.Uses.HasFlag(WorldZoneUse.Forest) ? new Color(0.18f, 0.32f, 0.16f, 1f) :
                    zone.Uses.HasFlag(WorldZoneUse.Agriculture) ? new Color(0.58f, 0.52f, 0.28f, 1f) :
                    zone.Uses.HasFlag(WorldZoneUse.Berries) ? new Color(0.37f, 0.29f, 0.34f, 1f) :
                    new Color(0.43f, 0.38f, 0.25f, 1f);
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "Zone " + zone.Id;
                marker.transform.SetParent(strategicOverview.transform, false);
                marker.transform.position = new Vector3(zone.Center.X,
                    HeightSampler.SampleHeight(zone.Center.X, zone.Center.Z) + 0.45f, zone.Center.Z);
                marker.transform.localScale = new Vector3(zone.RadiusX * 2f, 0.18f, zone.RadiusZ * 2f);
                var collider = marker.GetComponent<Collider>(); if (collider != null) Destroy(collider);
                marker.GetComponent<Renderer>().sharedMaterial = CreateMaterial(color, "P10.9 Zone " + zone.Id);
            }
        }

        private Mesh BuildOverviewGroundMesh()
        {
            const int resolution = 49;
            var vertices = new Vector3[resolution * resolution];
            var triangles = new int[(resolution - 1) * (resolution - 1) * 6];
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
            {
                var wx = Definition.Origin.X + x * Definition.WidthMeters / (float)(resolution - 1);
                var wz = Definition.Origin.Z + z * Definition.DepthMeters / (float)(resolution - 1);
                vertices[z * resolution + x] = new Vector3(wx, HeightSampler.SampleHeight(wx, wz), wz);
            }
            var cursor = 0;
            for (var z = 0; z < resolution - 1; z++)
            for (var x = 0; x < resolution - 1; x++)
            {
                var a = z * resolution + x;
                var b = a + 1;
                var c = a + resolution;
                var d = c + 1;
                triangles[cursor++] = a; triangles[cursor++] = c; triangles[cursor++] = b;
                triangles[cursor++] = b; triangles[cursor++] = c; triangles[cursor++] = d;
            }
            var mesh = new Mesh { name = "P10.9 Strategic Ground" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Mesh BuildOverviewWaterMesh()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var i = 1; i < Definition.River.Count; i++)
            {
                var a = Definition.River[i - 1];
                var b = Definition.River[i];
                var dx = b.X - a.X;
                var dz = b.Z - a.Z;
                var length = Mathf.Max(0.001f, Mathf.Sqrt(dx * dx + dz * dz));
                var nx = -dz / length * TiledWorldHeightSampler.RiverHalfWidthMeters;
                var nz = dx / length * TiledWorldHeightSampler.RiverHalfWidthMeters;
                var start = vertices.Count;
                vertices.Add(new Vector3(a.X + nx, TiledWorldHeightSampler.WaterLevelMeters, a.Z + nz));
                vertices.Add(new Vector3(a.X - nx, TiledWorldHeightSampler.WaterLevelMeters, a.Z - nz));
                vertices.Add(new Vector3(b.X + nx, TiledWorldHeightSampler.WaterLevelMeters, b.Z + nz));
                vertices.Add(new Vector3(b.X - nx, TiledWorldHeightSampler.WaterLevelMeters, b.Z - nz));
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start + 1); triangles.Add(start + 2); triangles.Add(start + 3);
            }
            foreach (var lake in Definition.Zones.Where(value => value.Uses.HasFlag(WorldZoneUse.Fishing)))
            {
                const int segments = 24;
                var center = vertices.Count;
                vertices.Add(new Vector3(lake.Center.X, TiledWorldHeightSampler.WaterLevelMeters, lake.Center.Z));
                for (var i = 0; i < segments; i++)
                {
                    var angle = i * Mathf.PI * 2f / segments;
                    vertices.Add(new Vector3(lake.Center.X + Mathf.Cos(angle) * lake.RadiusX,
                        TiledWorldHeightSampler.WaterLevelMeters, lake.Center.Z + Mathf.Sin(angle) * lake.RadiusZ));
                }
                for (var i = 0; i < segments; i++)
                {
                    triangles.Add(center);
                    triangles.Add(center + 1 + i);
                    triangles.Add(center + 1 + (i + 1) % segments);
                }
            }
            var mesh = new Mesh { name = "P10.9 Strategic Water" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateMaterial(Color color, string name)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("A supported world overview shader is required.");
            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.1f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            material.doubleSidedGI = true;
            return material;
        }

        private void DestroyTile(LoadedTile tile)
        {
            if (tile?.Root != null) Destroy(tile.Root);
            if (tile?.Data != null) Destroy(tile.Data);
        }

        private void OnDestroy()
        {
            foreach (var tile in loaded.Values.ToArray()) DestroyTile(tile);
            loaded.Clear();
            if (overviewGroundMaterial != null) Destroy(overviewGroundMaterial);
            if (overviewWaterMaterial != null) Destroy(overviewWaterMaterial);
            if (localWaterMaterial != null) Destroy(localWaterMaterial);
        }
    }
}
