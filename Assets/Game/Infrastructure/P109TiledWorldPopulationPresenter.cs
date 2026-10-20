using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Campaign;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.World;
using Game.Presentation.Environment;
using Game.Presentation.Production;
using Game.Presentation.Resources;
using Game.Simulation.Production;
using Game.Simulation.Resources;
using UnityEngine;

namespace Game.Infrastructure
{
    /// <summary>Streams disposable views for the canonical, deterministic P10.9 world population.</summary>
    [DisallowMultipleComponent]
    public sealed class P109TiledWorldPopulationPresenter : MonoBehaviour
    {
        private readonly Dictionary<WorldTileId, GameObject> tileViews = new Dictionary<WorldTileId, GameObject>();
        private readonly Dictionary<string, GameObject> templates = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private ProductionService production;
        private ResourceEcologyService ecology;
        private IndustrialExtractionService industry;
        private WorldPileService worldPiles;
        private ResourceInventoryRegistry inventories;
        private TiledTerrainWorld terrainWorld;
        private long seed;

        public TiledWorldPopulationPlan Plan { get; private set; }
        public int ActiveTileViewCount => tileViews.Count;
        public int ActiveResourceViewCount => tileViews.Values.Sum(root =>
            root.GetComponentsInChildren<ProductionSourcePresenter>(true).Length);
        public int ActiveIndustrialViewCount => tileViews.Values.Sum(root =>
            root.GetComponentsInChildren<WorldResourcePilePresenter>(true).Length);

        public void Initialize(TiledTerrainWorld world, ProductionService productionService,
            ResourceEcologyService ecologyService, IndustrialExtractionService industryService,
            WorldPileService pileService, ResourceInventoryRegistry inventoryRegistry,
            P93PopulatedBiomeWorld authoredBiome, long worldSeed)
        {
            terrainWorld = world ?? throw new ArgumentNullException(nameof(world));
            production = productionService ?? throw new ArgumentNullException(nameof(productionService));
            ecology = ecologyService ?? throw new ArgumentNullException(nameof(ecologyService));
            industry = industryService ?? throw new ArgumentNullException(nameof(industryService));
            worldPiles = pileService ?? throw new ArgumentNullException(nameof(pileService));
            inventories = inventoryRegistry ?? throw new ArgumentNullException(nameof(inventoryRegistry));
            seed = worldSeed;
            IndexTemplates(authoredBiome);
            Plan = new TiledWorldPopulationPlanner(world.Definition, world.HeightSampler, worldSeed).Create();
            RegisterCanonicalPopulation();
            terrainWorld.VisualTilesChanged += RefreshViews;
            RefreshViews();
        }

        public void RefreshViews()
        {
            if (terrainWorld == null || Plan == null) return;
            var required = new HashSet<WorldTileId>(terrainWorld.VisualTiles);
            foreach (var tile in tileViews.Keys.Where(tile => !required.Contains(tile)).ToArray())
            {
                Destroy(tileViews[tile]);
                tileViews.Remove(tile);
            }
            foreach (var tile in required.OrderBy(value => value))
                if (!tileViews.ContainsKey(tile)) tileViews.Add(tile, CreateTileView(tile));
            foreach (var presenter in tileViews.Values.SelectMany(root =>
                         root.GetComponentsInChildren<ProductionSourcePresenter>(true))) presenter.Refresh();
        }

        public void RebindAfterLoad()
        {
            foreach (var root in tileViews.Values) if (root != null) Destroy(root);
            tileViews.Clear();
            RefreshViews();
        }

        public StableEntityId ResourceId(TiledWorldResourceNode node) => DeterministicStableId.Create(seed,
            "p109_world_source:" + terrainWorld.Definition.GenerationId, node.StableIndex);

        public StableEntityId IndustrialId(TiledWorldIndustrialNode node) => DeterministicStableId.Create(seed,
            "p109_world_industry:" + terrainWorld.Definition.GenerationId, node.StableIndex);

        private void RegisterCanonicalPopulation()
        {
            foreach (var node in Plan.Resources)
            {
                var id = ResourceId(node);
                if (!production.TryGetSite(id, out var site))
                {
                    site = ProductionSite.Restore(new ProductionSiteState(id, new ProductionRecipeId(node.RecipeId),
                        ProductionSiteKind.NaturalSource, node.Position, node.Capacity, node.Capacity,
                        CropPlotPhase.Unplanted, 0, 0));
                    production.AddSite(site);
                }
                ecology.Register(new NaturalNodeRegistration(site, node.Biome, RespawnCandidates(node.Position)));
            }
            foreach (var node in Plan.Industry)
            {
                var id = IndustrialId(node);
                if (!industry.TryGetZone(id, out _)) industry.AddZone(id, node.DefinitionId, node.Position);
                if (!worldPiles.TryGet(id, out _))
                    worldPiles.RegisterExistingInventory(new ResourceId(node.ResourceId),
                        new GridCoordinate(Mathf.RoundToInt(node.Position.X), Mathf.RoundToInt(node.Position.Z)), id);
            }
        }

        private GameObject CreateTileView(WorldTileId tile)
        {
            var root = new GameObject("P10.9 Population " + tile);
            root.transform.SetParent(transform, false);
            foreach (var node in Plan.Resources.Where(value => value.Tile.Equals(tile))) CreateResourceView(node, root.transform);
            foreach (var node in Plan.Industry.Where(value => value.Tile.Equals(tile))) CreateIndustrialView(node, root.transform);
            CreateSettlementAndFieldViews(tile, root.transform);
            return root;
        }

        private void CreateResourceView(TiledWorldResourceNode node, Transform parent)
        {
            if (!production.TryGetSite(ResourceId(node), out var site)) return;
            GameObject view;
            if (templates.TryGetValue(node.RecipeId, out var template) && template != null)
            {
                view = Instantiate(template, parent);
                view.name = "P10.9 " + node.RecipeId + " " + node.StableIndex;
                if (view.transform.Find("Synty playable tree") != null)
                {
                    // This is already a normalized single-tree projection. Expanding/rescaling
                    // the retired formation would reintroduce invisible colliders and giant crowns.
                    view.transform.localScale = template.transform.lossyScale;
                }
                else
                {
                    ExpandAuthoredFormation(view, node.StableIndex);
                    view.transform.localScale = Vector3.one * AuthoredFormationScale(node.RecipeId);
                }
            }
            else
            {
                var primitive = node.RecipeId == "fish_water" ? PrimitiveType.Cylinder :
                    node.RecipeId == "hunt_animal" ? PrimitiveType.Capsule : PrimitiveType.Sphere;
                view = GameObject.CreatePrimitive(primitive);
                view.transform.SetParent(parent, false);
                view.name = "P10.9 " + node.RecipeId + " " + node.StableIndex;
                view.transform.localScale = node.RecipeId == "fish_water"
                    ? new Vector3(2.4f, 0.22f, 1.2f) : new Vector3(1.2f, 1.8f, 1.2f);
            }
            view.SetActive(true);
            if (node.RecipeId == "chop_wood")
                terrainWorld.GetComponent<StableEnvironmentLodController>()?.ConfigureTree(view);
            view.transform.position = new Vector3(site.Position.X,
                site.Position.Y + (node.RecipeId == "fish_water" ? 0.08f : 0.1f), site.Position.Z);
            var presenter = view.GetComponent<ProductionSourcePresenter>() ?? view.AddComponent<ProductionSourcePresenter>();
            presenter.Bind(site, ResourceColor(node.RecipeId), templates.ContainsKey(node.RecipeId));
        }

        private static void ExpandAuthoredFormation(GameObject root, int stableIndex)
        {
            if (root.transform.childCount == 0) return;
            Transform visual = null;
            for (var i = 0; i < root.transform.childCount; i++)
            {
                var candidate = root.transform.GetChild(i);
                if (candidate.GetComponentInChildren<Renderer>(true) == null) continue;
                visual = candidate;
                break;
            }
            if (visual == null) return;
            var baseScale = visual.localScale;
            for (var i = 0; i < 3; i++)
            {
                var satellite = Instantiate(visual.gameObject, root.transform);
                satellite.name = "Formation visual " + (i + 2);
                var angle = (stableIndex * 47f + i * 120f) * Mathf.Deg2Rad;
                var radius = 1.7f + (stableIndex + i * 3) % 4 * 0.32f;
                satellite.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, 0f,
                    Mathf.Sin(angle) * radius);
                satellite.transform.localRotation = Quaternion.Euler(0f, stableIndex * 31f + i * 97f, 0f);
                satellite.transform.localScale = baseScale * (0.72f + i * 0.08f);
                // Each visible satellite is part of this same resource formation and must be hittable.
                if (satellite.GetComponentInChildren<Collider>(true) == null) satellite.AddComponent<BoxCollider>();
            }
        }

        private static float AuthoredFormationScale(string recipeId)
        {
            if (recipeId == "gather_stone" || recipeId == "gather_iron" || recipeId == "gather_gold") return 3.2f;
            if (recipeId == "gather_berries" || recipeId == "gather_fiber") return 2.1f;
            return 1.25f;
        }

        private void CreateIndustrialView(TiledWorldIndustrialNode node, Transform parent)
        {
            var id = IndustrialId(node);
            if (!worldPiles.TryGet(id, out var pile) || !inventories.TryGet(pile.InventoryOwner, out var inventory)) return;
            var marker = GameObject.CreatePrimitive(node.DefinitionId == "lake_fishing" ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            marker.name = "P10.9 Industry " + node.DefinitionId + " " + node.StableIndex;
            marker.transform.SetParent(parent, false);
            marker.transform.position = new Vector3(node.Position.X, node.Position.Y + 0.55f, node.Position.Z);
            marker.transform.localScale = new Vector3(3.4f, 1.1f, 3.4f);
            ApplyColor(marker, ResourceColor(node.ResourceId));
            marker.AddComponent<WorldResourcePilePresenter>().Bind(pile, inventory);
        }

        private void CreateSettlementAndFieldViews(WorldTileId tile, Transform parent)
        {
            var origin = terrainWorld.Definition.TileOrigin(tile);
            foreach (var zone in terrainWorld.Definition.Zones.Where(zone =>
                         zone.Uses.HasFlag(WorldZoneUse.Settlement) || zone.Uses.HasFlag(WorldZoneUse.Agriculture)))
            {
                if (!terrainWorld.Definition.TryGetTile(zone.Center.X, zone.Center.Z, out var centerTile) ||
                    !centerTile.Equals(tile)) continue;
                if (zone.Uses.HasFlag(WorldZoneUse.Settlement))
                {
                    for (var i = 0; i < 7; i++)
                    {
                        var hut = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        hut.name = "P10.9 Settlement Hut " + zone.Id + " " + i;
                        hut.transform.SetParent(parent, false);
                        var angle = i * Mathf.PI * 2f / 7f;
                        var x = zone.Center.X + Mathf.Cos(angle) * zone.RadiusX * 0.45f;
                        var z = zone.Center.Z + Mathf.Sin(angle) * zone.RadiusZ * 0.45f;
                        hut.transform.position = new Vector3(x, terrainWorld.HeightSampler.SampleHeight(x, z) + 1.3f, z);
                        hut.transform.localScale = new Vector3(5f, 2.6f, 4f);
                        ApplyColor(hut, new Color(0.42f, 0.29f, 0.16f));
                    }
                }
                if (zone.Uses.HasFlag(WorldZoneUse.Agriculture))
                {
                    for (var i = 0; i < 5; i++)
                    {
                        var field = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        field.name = "P10.9 Field " + zone.Id + " " + i;
                        field.transform.SetParent(parent, false);
                        var x = zone.Center.X - zone.RadiusX * 0.6f + i * zone.RadiusX * 0.3f;
                        var z = zone.Center.Z;
                        field.transform.position = new Vector3(x, terrainWorld.HeightSampler.SampleHeight(x, z) + 0.08f, z);
                        field.transform.localScale = new Vector3(zone.RadiusX * 0.2f, 0.12f, zone.RadiusZ * 0.9f);
                        ApplyColor(field, new Color(0.48f, 0.39f, 0.17f));
                        var collider = field.GetComponent<Collider>(); if (collider != null) Destroy(collider);
                    }
                }
            }
        }

        private void IndexTemplates(P93PopulatedBiomeWorld authoredBiome)
        {
            if (authoredBiome == null) return;
            foreach (var formation in authoredBiome.ResourceFormations.OrderBy(value => value.StableIndex))
                if (formation.ClickableRoot != null && !templates.ContainsKey(formation.RecipeId))
                    templates.Add(formation.RecipeId, formation.ClickableRoot.gameObject);
        }

        private static WorldPosition[] RespawnCandidates(WorldPosition origin) => new[]
        {
            origin, new WorldPosition(origin.X + 6f, origin.Y, origin.Z),
            new WorldPosition(origin.X - 6f, origin.Y, origin.Z),
            new WorldPosition(origin.X, origin.Y, origin.Z + 6f),
            new WorldPosition(origin.X, origin.Y, origin.Z - 6f)
        };

        private static Color ResourceColor(string id)
        {
            if (id == "chop_wood" || id == "wood_log") return new Color(0.26f, 0.38f, 0.14f);
            if (id == "gather_berries" || id == "berries") return new Color(0.48f, 0.16f, 0.3f);
            if (id == "gather_fiber" || id == "plant_fiber") return new Color(0.36f, 0.58f, 0.18f);
            if (id == "fish_water" || id == "fish") return new Color(0.12f, 0.55f, 0.72f);
            if (id == "hunt_animal" || id == "meat") return new Color(0.48f, 0.25f, 0.13f);
            if (id == "gather_gold" || id == "gold_ore") return new Color(0.82f, 0.62f, 0.16f);
            if (id == "gather_iron" || id == "iron_ore") return new Color(0.34f, 0.29f, 0.26f);
            return new Color(0.46f, 0.47f, 0.48f);
        }

        private static void ApplyColor(GameObject value, Color color)
        {
            var renderer = value.GetComponent<Renderer>(); if (renderer == null) return;
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color); block.SetColor("_Color", color); renderer.SetPropertyBlock(block);
        }

        private void OnDestroy()
        {
            if (terrainWorld != null) terrainWorld.VisualTilesChanged -= RefreshViews;
        }
    }
}
