using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Weather;
using UnityEngine;

namespace Game.Presentation.Environment
{
    /// <summary>
    /// Authored, disposable projection of the first inhabitable region. Stable resource state remains
    /// in ProductionService; this component only exposes scene anchors, territory correspondence and
    /// seasonal surface treatment to Infrastructure.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class P5InhabitableRegionPresentation : MonoBehaviour
    {
        [Serializable]
        public sealed class ResourceAnchor
        {
            [SerializeField] private string recipeId;
            [SerializeField, Min(1)] private long amount;
            [SerializeField, Min(0)] private int stableIndex;
            [SerializeField] private Transform visualRoot;
            [SerializeField] private Color activeColor = Color.white;

            public string RecipeId => recipeId;
            public long Amount => amount;
            public int StableIndex => stableIndex;
            public Transform VisualRoot => visualRoot;
            public Color ActiveColor => activeColor;

            public ResourceAnchor Configure(string recipe, long quantity, int index, Transform root, Color color)
            {
                recipeId = recipe;
                amount = quantity;
                stableIndex = index;
                visualRoot = root;
                activeColor = color;
                return this;
            }
        }

        [SerializeField] private Transform playerTerritoryAnchor;
        [SerializeField] private Transform rivalTerritoryAnchor;
        [SerializeField] private Transform[] routeWaypoints = Array.Empty<Transform>();
        [SerializeField] private Transform[] buildableAreaAnchors = Array.Empty<Transform>();
        [SerializeField] private Transform[] obstacleAnchors = Array.Empty<Transform>();
        [SerializeField] private Transform[] pileAnchors = Array.Empty<Transform>();
        [SerializeField] private ResourceAnchor[] resourceAnchors = Array.Empty<ResourceAnchor>();
        [SerializeField] private Renderer[] seasonalGround = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] seasonalFoliage = Array.Empty<Renderer>();
        [SerializeField] private Renderer waterRenderer;
        [SerializeField] private Terrain seasonalTerrain;
        [SerializeField] private Material seasonalTerrainMaterial;
        [SerializeField] private Vector2[] landBoundary = Array.Empty<Vector2>();
        [SerializeField] private Transform[] authoredPlacementAnchors = Array.Empty<Transform>();
        [SerializeField] private Renderer[] trunkRenderers = Array.Empty<Renderer>();
        [SerializeField, Min(0)] private int authoredTreeCount;
        [SerializeField, Min(0)] private int authoredRockCount;
        [SerializeField, Min(0)] private int authoredGrassCount;

        private MaterialPropertyBlock propertyBlock;

        public Transform PlayerTerritoryAnchor => playerTerritoryAnchor;
        public Transform RivalTerritoryAnchor => rivalTerritoryAnchor;
        public Renderer WaterRenderer => waterRenderer;
        public Terrain SeasonalTerrain => seasonalTerrain;
        public IReadOnlyList<Transform> RouteWaypoints => routeWaypoints;
        public IReadOnlyList<Transform> BuildableAreaAnchors => buildableAreaAnchors;
        public IReadOnlyList<Transform> ObstacleAnchors => obstacleAnchors;
        public IReadOnlyList<ResourceAnchor> ResourceAnchors => resourceAnchors;
        public int TreeCount => authoredTreeCount;
        public int RockCount => authoredRockCount;
        public int GrassCount => authoredGrassCount;
        public IReadOnlyList<Vector2> LandBoundary => landBoundary;
        public IReadOnlyList<Transform> AuthoredPlacementAnchors => authoredPlacementAnchors;
        public IReadOnlyList<Renderer> TrunkRenderers => trunkRenderers;
        public Season DisplayedSeason { get; private set; }
        public bool SnowCoverVisible { get; private set; }
        public bool HasCanonicalMapCorrespondence { get; private set; }

        public void Configure(Transform playerTerritory, Transform rivalTerritory,
            IEnumerable<Transform> routes, IEnumerable<Transform> buildableAreas,
            IEnumerable<Transform> obstacles, IEnumerable<Transform> piles,
            IEnumerable<ResourceAnchor> resources, IEnumerable<Renderer> groundRenderers,
            IEnumerable<Renderer> foliageRenderers, Renderer water,
            int treeCount, int rockCount, int grassCount)
        {
            playerTerritoryAnchor = playerTerritory;
            rivalTerritoryAnchor = rivalTerritory;
            routeWaypoints = (routes ?? Enumerable.Empty<Transform>()).Where(x => x != null).ToArray();
            buildableAreaAnchors = (buildableAreas ?? Enumerable.Empty<Transform>()).Where(x => x != null).ToArray();
            obstacleAnchors = (obstacles ?? Enumerable.Empty<Transform>()).Where(x => x != null).ToArray();
            pileAnchors = (piles ?? Enumerable.Empty<Transform>()).Where(x => x != null).ToArray();
            resourceAnchors = (resources ?? Enumerable.Empty<ResourceAnchor>()).Where(x => x != null).ToArray();
            seasonalGround = (groundRenderers ?? Enumerable.Empty<Renderer>()).Where(x => x != null).ToArray();
            seasonalFoliage = (foliageRenderers ?? Enumerable.Empty<Renderer>()).Where(x => x != null).ToArray();
            waterRenderer = water;
            authoredTreeCount = Mathf.Max(0, treeCount);
            authoredRockCount = Mathf.Max(0, rockCount);
            authoredGrassCount = Mathf.Max(0, grassCount);
            ValidateConfiguration();
        }

        public void BindCanonicalMap(IEnumerable<string> locationIds)
        {
            var ids = new HashSet<string>(locationIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            HasCanonicalMapCorrespondence = ids.Contains("starter_settlement") &&
                ids.Contains("enemy_settlement") && ids.Contains("north_forest") &&
                ids.Contains("river_ford") && ids.Contains("east_hills") && ids.Contains("south_field");
        }

        public void ConfigureGeography(IEnumerable<Vector2> boundary, IEnumerable<Transform> placements,
            IEnumerable<Renderer> trunks)
        {
            landBoundary = (boundary ?? Enumerable.Empty<Vector2>()).ToArray();
            authoredPlacementAnchors = (placements ?? Enumerable.Empty<Transform>()).Where(x => x != null).ToArray();
            trunkRenderers = (trunks ?? Enumerable.Empty<Renderer>()).Where(x => x != null).ToArray();
            ValidateGeography();
        }

        public void ConfigureTerrainProjection(Terrain terrain, Material material)
        {
            seasonalTerrain = terrain;
            seasonalTerrainMaterial = material;
        }

        public bool ContainsLandPoint(Vector3 worldPoint)
        {
            if (landBoundary.Length < 3) return true;
            var point = new Vector2(worldPoint.x, worldPoint.z);
            var inside = false;
            for (int i = 0, j = landBoundary.Length - 1; i < landBoundary.Length; j = i++)
            {
                var a = landBoundary[i];
                var b = landBoundary[j];
                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        public void ValidateGeography()
        {
            if (landBoundary.Length < 8)
                throw new InvalidOperationException("P8.2 region requires an authored finite land boundary.");
            if (authoredPlacementAnchors.Any(x => !ContainsLandPoint(x.position)) ||
                resourceAnchors.Any(x => !ContainsLandPoint(x.VisualRoot.position)))
                throw new InvalidOperationException("P8.2 authored objects and resource routes must remain on land.");
            if (trunkRenderers.Length < 8 || trunkRenderers.Any(x => x.sharedMaterial == null))
                throw new InvalidOperationException("P8.2 region requires readable brown-bark tree projections.");
        }

        public Transform GetPileAnchor(int index) => index >= 0 && index < pileAnchors.Length
            ? pileAnchors[index] : null;

        public void ApplySeason(Season season, bool snowing)
        {
            DisplayedSeason = season;
            SnowCoverVisible = snowing || season == Season.Winter;
            var ground = season switch
            {
                Season.Spring => new Color(0.72f, 0.86f, 0.66f),
                Season.Summer => new Color(0.82f, 0.86f, 0.68f),
                Season.Autumn => new Color(0.82f, 0.66f, 0.43f),
                _ => new Color(0.68f, 0.76f, 0.76f)
            };
            var foliage = season switch
            {
                Season.Spring => new Color(0.66f, 0.95f, 0.58f),
                Season.Summer => new Color(0.78f, 0.9f, 0.58f),
                Season.Autumn => new Color(0.95f, 0.55f, 0.22f),
                _ => new Color(0.72f, 0.78f, 0.74f)
            };
            ApplyTint(seasonalGround, ground);
            ApplyTint(seasonalFoliage, foliage);
            if (seasonalTerrain != null && seasonalTerrainMaterial != null)
            {
                var terrainTint = Color.Lerp(Color.white, ground, SnowCoverVisible ? 0.32f : 0.13f);
                if (seasonalTerrainMaterial.HasProperty("_BaseColor"))
                    seasonalTerrainMaterial.SetColor("_BaseColor", terrainTint);
                if (seasonalTerrainMaterial.HasProperty("_Color"))
                    seasonalTerrainMaterial.SetColor("_Color", terrainTint);
                seasonalTerrain.materialTemplate = seasonalTerrainMaterial;
            }
            if (waterRenderer != null)
                ApplyTint(new[] { waterRenderer }, season == Season.Winter
                    ? new Color(0.62f, 0.78f, 0.86f) : new Color(0.72f, 0.9f, 1f));
        }

        public void ValidateConfiguration()
        {
            if (playerTerritoryAnchor == null || rivalTerritoryAnchor == null || waterRenderer == null)
                throw new InvalidOperationException("P5 region requires player/rival territory and water anchors.");
            if (routeWaypoints.Length < 4 || buildableAreaAnchors.Length < 2 || obstacleAnchors.Length < 8)
                throw new InvalidOperationException("P5 region lacks routes, buildable areas or obstacles.");
            if (pileAnchors.Length < 4 || resourceAnchors.Length < 12)
                throw new InvalidOperationException("P5 region lacks authored canonical resource projections.");
            if (resourceAnchors.Any(x => string.IsNullOrWhiteSpace(x.RecipeId) || x.Amount <= 0 ||
                    x.StableIndex < 0 || x.VisualRoot == null) ||
                resourceAnchors.Select(x => x.StableIndex).Distinct().Count() != resourceAnchors.Length)
                throw new InvalidOperationException("P5 resource anchors must be valid and uniquely indexed.");
        }

        private void ApplyTint(IEnumerable<Renderer> renderers, Color tint)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", tint);
                propertyBlock.SetColor("_Color", tint);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
