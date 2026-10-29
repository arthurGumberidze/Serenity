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
            Verdict = !CanPlace ? WorldMapPlacementVerdict.Red : ClearableSources.Count > 0
                ? WorldMapPlacementVerdict.Amber : WorldMapPlacementVerdict.Green;
        }

        public bool CanPlace { get; }
        public WorldMapPlacementVerdict Verdict { get; }
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
        private WorldMapBuildabilityEvaluator worldMapBuildability;
        [SerializeField] private float maximumFoundationHeightDifference = 0.75f;

        private sealed class StaticCell { public string Blocked; public float Low, High; }
        private readonly Dictionary<(GridCoordinate cell,float size,Vector3 origin,bool shore,bool adaptive),StaticCell> staticCells = new();

        public void ImportShoreCells(IReadOnlyDictionary<GridCoordinate,ShorePlacementIndex.Cell> cells,float gridSize,Vector3 origin)
        {
            foreach(var pair in cells)
                staticCells[(pair.Key,gridSize,origin,true,false)]=new StaticCell
                { Blocked=pair.Value.Blocked==null?null:AuthoredBlockedReason(pair.Value.Blocked),Low=pair.Value.Low,High=pair.Value.High };
        }

        // Terrain and authored masks are immutable for this world; physical blockers are always checked again.
        public bool PrepareFootprint(IReadOnlyList<GridCoordinate> cells, float gridSize, Vector3 origin,
            bool shorelineFacility, double budgetMilliseconds = 2, bool terrainAdaptive = false)
        {
            if (tiledWorld == null) return true;
            if (staticCells.Count + cells.Count >= 65536) staticCells.Clear();
            var timer = System.Diagnostics.Stopwatch.StartNew(); var computed = 0;
            foreach (var cell in cells)
            {
                var key = (cell,gridSize,origin,shorelineFacility,terrainAdaptive);
                if (staticCells.TryGetValue(key,out var existing)) { if (existing.Blocked != null) return true; continue; }
                if (computed > 0 && budgetMilliseconds > 0 && timer.Elapsed.TotalMilliseconds >= budgetMilliseconds) return false;
                var center = origin + new Vector3(cell.X*gridSize,0,cell.Z*gridSize);
                var value = new StaticCell { Low = float.PositiveInfinity, High = float.NegativeInfinity };
                if (worldMapBuildability != null)
                {
                    var policy = worldMapBuildability.EvaluateFootprint(new[] { new WorldMapFootprintCell(center.x,center.z,gridSize*.5f) },shorelineFacility);
                    if (!policy.CanPlace && !(terrainAdaptive && policy.Reason == WorldMapBuildabilityEvaluator.ExcessiveSlope))
                        value.Blocked = AuthoredBlockedReason(policy.Reason);
                }
                if (value.Blocked == null)
                for (var z=-1;z<=1;z++)
                for (var x=-1;x<=1;x++)
                {
                    var point = center + new Vector3(x*gridSize*.5f,0,z*gridSize*.5f);
                    if (worldMapBuildability == null && !IsSuitable(point) && !terrainAdaptive)
                        value.Blocked = "Слишком крутой склон, вода или неподходящий грунт.";
                    var height = tiledWorld.SampleHeight(point.x,point.z);
                    value.Low = Mathf.Min(value.Low,height); value.High = Mathf.Max(value.High,height);
                }
                staticCells[key] = value; computed++;
                if (value.Blocked != null) return true;
            }
            return true;
        }

        public bool AllowsFootprint(IReadOnlyList<GridCoordinate> cells, float gridSize, Vector3 origin)
            => EvaluateFootprint(cells, gridSize, origin).CanPlace;

        public PlacementSurfaceEvaluation EvaluateFootprint(IReadOnlyList<GridCoordinate> cells, float gridSize,
            Vector3 origin, float? foundationHeightLimit = null, bool replaceOwnedPalisade = false,
            bool shorelineFacility = false, bool terrainAdaptive = false)
        {
            if (tiledWorld == null || cells == null || cells.Count == 0)
                return new PlacementSurfaceEvaluation(false, null, "Поверхность мира недоступна.");
            var low = float.PositiveInfinity;
            var high = float.NegativeInfinity;
            var clearable = new HashSet<StableEntityId>();
            if (!PrepareFootprint(cells,gridSize,origin,shorelineFacility,shorelineFacility ? 2 : 0,terrainAdaptive))
                return new PlacementSurfaceEvaluation(false,null,"Подготавливаем береговую площадку…");
            foreach (var cell in cells)
            {
                var cached = staticCells[(cell,gridSize,origin,shorelineFacility,terrainAdaptive)];
                if (cached.Blocked != null) return new PlacementSurfaceEvaluation(false,null,cached.Blocked);
                low = Mathf.Min(low,cached.Low); high = Mathf.Max(high,cached.High);
            }
            var protectedPoi = false;
            var permanentObstacle = false;
            Physics.SyncTransforms();
            foreach (var cell in cells)
            {
                var center = origin + new Vector3(cell.X * gridSize, 0, cell.Z * gridSize);
                center.y = tiledWorld.SampleHeight(center.x, center.z) + 1.1f;
                foreach (var blocker in Physics.OverlapBox(center, new Vector3(gridSize * 0.49f, 1f, gridSize * 0.49f),
                             Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                {
                    if (blocker is TerrainCollider || blocker.GetComponentInParent<BuildableGround>() != null ||
                        blocker.GetComponentInParent<CharacterPresenter>() != null ||
                        blocker.GetComponentInParent<AnimalHealthPresentation>() != null) continue;
                    var wall=blocker.GetComponentInParent<Game.Presentation.Buildings.BuildingPresenter>();
                    if(replaceOwnedPalisade && wall!=null && wall.IsBound && wall.Building.IsOperational &&
                        wall.Building.OwnerSideId.Value=="player" && wall.Building.DefinitionId.FamilyId.ToString()=="palisade")continue;
                    var poi = blocker.GetComponentInParent<WorldMapPoiProtectionVolume>();
                    if (poi != null)
                    {
                        if (!poi.OverlapsHorizontalBox(center, gridSize * 0.49f)) continue;
                        protectedPoi = true;
                        continue;
                    }
                    var source = blocker.GetComponentInParent<ProductionSourcePresenter>();
                    if (source != null && source.IsBound && source.Site.IsDepleted) continue;
                    if (source != null && source.IsBound && IsClearable(source.Site))
                    {
                        clearable.Add(source.Site.Id);
                        continue;
                    }
                    permanentObstacle = true;
                }
            }
            if (protectedPoi)
                return new PlacementSurfaceEvaluation(false, clearable,
                    "Уникальная точка интереса защищена от строительства и автоматической расчистки.");
            if (permanentObstacle)
                return new PlacementSurfaceEvaluation(false, clearable,
                    "Участок занят постоянным или неразрушаемым препятствием.");
            if (high - low > (foundationHeightLimit ?? maximumFoundationHeightDifference))
                return new PlacementSurfaceEvaluation(false, clearable,
                    "Перепад высоты площадки слишком велик.");
            return new PlacementSurfaceEvaluation(true, clearable);
        }

        private bool IsClearable(ProductionSite site)
        {
            if (site == null || site.Kind != ProductionSiteKind.NaturalSource || site.IsDepleted) return false;
            var recipe = site.RecipeId.ToString();
            if (worldMapBuildability != null)
                return worldMapBuildability.Definition.IsClearableRecipe(recipe);
            return recipe != "fish_water" && recipe != "hunt_animal";
        }
        public void Configure(Terrain value, float water, float maximumSlope)
        {
            staticCells.Clear();
            terrain = value;
            tiledWorld = null;
            worldMapBuildability = null;
            waterLevel = water;
            maximumSlopeDegrees = maximumSlope;
        }
        public void Configure(TiledWorldHeightSampler value, float maximumSlope = 32f)
        {
            staticCells.Clear();
            tiledWorld = value;
            terrain = null;
            waterLevel = TiledWorldHeightSampler.WaterLevelMeters;
            maximumSlopeDegrees = maximumSlope;
            worldMapBuildability = value?.Definition.MapData?.Buildability == null
                ? null : new WorldMapBuildabilityEvaluator(value.Definition, value);
        }

        private static string AuthoredBlockedReason(string reason)
        {
            if (reason.StartsWith(WorldMapBuildabilityEvaluator.ProtectedPoi, System.StringComparison.Ordinal))
                return "Уникальная точка интереса защищена от строительства и автоматической расчистки.";
            if (reason.StartsWith(WorldMapBuildabilityEvaluator.RestrictedMask, System.StringComparison.Ordinal))
                return "Эта зона защищена или требует специальной технологии.";
            if (reason == WorldMapBuildabilityEvaluator.Water)
                return "Строительство на воде или затопленном грунте запрещено.";
            if (reason == WorldMapBuildabilityEvaluator.ExcessiveSlope)
                return "Склон превышает лимит строительной маски.";
            return "Площадка находится вне допустимой поверхности мира.";
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
