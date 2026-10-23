using UnityEngine;
using Game.Domain;
using Game.Domain.Production;
using Game.Domain.World;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Buildings;
using Game.Presentation.Characters;
using Game.Presentation.Interaction;
using Game.Presentation.Production;

namespace Game.Presentation.Environment
{
    public readonly struct PlacementSurfaceEvaluation
    {
        public PlacementSurfaceEvaluation(bool canPlace, IEnumerable<StableEntityId> clearableSources,
            string blockedReason = null)
        {
            CanPlace = canPlace;
            ClearableSources = (clearableSources ?? Enumerable.Empty<StableEntityId>()).Distinct().OrderBy(x => x).ToArray();
            BlockedReason = blockedReason ?? string.Empty;
        }

        public bool CanPlace { get; }
        public bool RequiresClearance => CanPlace && ClearableSources.Count > 0;
        public IReadOnlyList<StableEntityId> ClearableSources { get; }
        public string BlockedReason { get; }
    }

    [DisallowMultipleComponent]
    public sealed class P102WorldPlacementSurface : MonoBehaviour
    {
        [SerializeField] private Terrain terrain;
        [SerializeField] private float waterLevel = 0.15f;
        [SerializeField] private float maximumSlopeDegrees = 32f;
        private TiledWorldHeightSampler tiledWorld;
        [SerializeField] private float maximumFoundationHeightDifference = 0.75f;

        public bool AllowsFootprint(IReadOnlyList<GridCoordinate> cells, float gridSize, Vector3 origin)
            => EvaluateFootprint(cells, gridSize, origin).CanPlace;

        public PlacementSurfaceEvaluation EvaluateFootprint(IReadOnlyList<GridCoordinate> cells, float gridSize,
            Vector3 origin)
        {
            if (tiledWorld == null || cells == null || cells.Count == 0)
                return new PlacementSurfaceEvaluation(false, null, "Поверхность мира недоступна.");
            var low = float.PositiveInfinity;
            var high = float.NegativeInfinity;
            var clearable = new HashSet<StableEntityId>();
            Physics.SyncTransforms();
            foreach (var cell in cells)
            {
                var center = origin + new Vector3(cell.X * gridSize, 0, cell.Z * gridSize);
                for (var z = -1; z <= 1; z++)
                for (var x = -1; x <= 1; x++)
                {
                    var point = center + new Vector3(x * gridSize * 0.5f, 0, z * gridSize * 0.5f);
                    if (!IsSuitable(point))
                        return new PlacementSurfaceEvaluation(false, clearable,
                            "Слишком крутой склон, вода или неподходящий грунт.");
                    var height = tiledWorld.SampleHeight(point.x, point.z);
                    low = Mathf.Min(low, height); high = Mathf.Max(high, height);
                }
                center.y = tiledWorld.SampleHeight(center.x, center.z) + 1.1f;
                foreach (var blocker in Physics.OverlapBox(center, new Vector3(gridSize * 0.49f, 1f, gridSize * 0.49f),
                             Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                {
                    if (blocker is TerrainCollider || blocker.GetComponentInParent<BuildableGround>() != null ||
                        blocker.GetComponentInParent<CharacterPresenter>() != null) continue;
                    var poi = blocker.GetComponentInParent<WorldMapPoiProtectionVolume>();
                    if (poi != null)
                    {
                        if (!poi.OverlapsHorizontalBox(center, gridSize * 0.49f)) continue;
                        return new PlacementSurfaceEvaluation(false, clearable,
                            "Уникальная точка интереса защищена от строительства и автоматической расчистки.");
                    }
                    var source = blocker.GetComponentInParent<ProductionSourcePresenter>();
                    if (source != null && source.IsBound && IsClearable(source.Site))
                    {
                        clearable.Add(source.Site.Id);
                        continue;
                    }
                    return new PlacementSurfaceEvaluation(false, clearable,
                        "Участок занят постоянным или неразрушаемым препятствием.");
                }
            }
            if (high - low > maximumFoundationHeightDifference)
                return new PlacementSurfaceEvaluation(false, clearable,
                    "Перепад высоты площадки слишком велик.");
            return new PlacementSurfaceEvaluation(true, clearable);
        }

        private static bool IsClearable(ProductionSite site)
        {
            if (site == null || site.Kind != ProductionSiteKind.NaturalSource || site.IsDepleted) return false;
            var recipe = site.RecipeId.ToString();
            return recipe != "fish_water" && recipe != "hunt_animal";
        }
        public void Configure(Terrain value, float water, float maximumSlope)
        { terrain = value; tiledWorld = null; waterLevel = water; maximumSlopeDegrees = maximumSlope; }
        public void Configure(TiledWorldHeightSampler value, float maximumSlope = 32f)
        {
            tiledWorld = value;
            terrain = null;
            waterLevel = TiledWorldHeightSampler.WaterLevelMeters;
            maximumSlopeDegrees = maximumSlope;
        }
        public bool IsSuitable(Vector3 worldPoint)
        {
            if (tiledWorld != null) return tiledWorld.IsBuildable(worldPoint.x, worldPoint.z, maximumSlopeDegrees);
            if (terrain == null || terrain.terrainData == null) return false;
            var local = worldPoint - terrain.transform.position;
            var u = local.x / terrain.terrainData.size.x;
            var v = local.z / terrain.terrainData.size.z;
            if (u < 0f || u > 1f || v < 0f || v > 1f) return false;
            var height = terrain.SampleHeight(worldPoint) + terrain.transform.position.y;
            return height > waterLevel + 0.25f && terrain.terrainData.GetSteepness(u, v) <= maximumSlopeDegrees;
        }
    }
}
