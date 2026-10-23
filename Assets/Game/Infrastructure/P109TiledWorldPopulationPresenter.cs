using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Campaign;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Time;
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
        private CompactWorldArtCatalog art;
        private WorldMapResourceArtCatalog worldMapResourceArt;
        private sealed class AnimalView
        {
            public StableEntityId SiteId;
            public Transform Transform;
            public Vector3 Origin;
            public float Phase;
            public TextMesh HealthLabel;
            public Animator Animator;
            public AudioSource Audio;
            public StableEntityId? AudibleTaskId;
            public long LastAudibleWorkTicks;
            public float NextHuntSoundTime;
        }
        private readonly List<AnimalView> animals = new List<AnimalView>();
        private LocalSceneCompositionRoot session;
        private float animalTime;
        private AudioSource forestAmbience;

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
            session = FindAnyObjectByType<LocalSceneCompositionRoot>();
            art = Resources.Load<CompactWorldArtCatalog>("World/CompactWorldArt");
            worldMapResourceArt = Resources.Load<WorldMapResourceArtCatalog>("World/WorldMapResourceArt");
            CreateForestAmbience();
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
            animals.RemoveAll(x => x.Transform == null);
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

        public StableEntityId ResourceId(TiledWorldResourceNode node) =>
            node.CreateStableId(seed, terrainWorld.Definition.GenerationId);

        public StableEntityId IndustrialId(TiledWorldIndustrialNode node) => DeterministicStableId.Create(seed,
            "p109_world_industry:" + terrainWorld.Definition.GenerationId, node.StableIndex);

        private void RegisterCanonicalPopulation()
        {
            foreach (var node in Plan.Resources)
            {
                // Hidden deposits already have deterministic plan identities, but are deliberately absent from
                // ProductionService and presentation until a later technology-discovery workflow reveals them.
                if (node.Hidden) continue;
                var id = ResourceId(node);
                if (!production.TryGetSite(id, out var site))
                {
                    site = ProductionSite.Restore(new ProductionSiteState(id, new ProductionRecipeId(node.RecipeId),
                        ProductionSiteKind.NaturalSource, node.Position, node.Capacity, node.Capacity,
                        CropPlotPhase.Unplanted, 0, 0));
                    production.AddSite(site);
                }
                ecology.Register(new NaturalNodeRegistration(site, node.Biome,
                    node.RecipeId == "fish_water" ? new[] { node.Position } : RespawnCandidates(node.Position),
                    node.RenewalPolicy != "none"));
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
            foreach (var node in Plan.VisibleResources.Where(value => value.Tile.Equals(tile)))
                CreateResourceView(node, root.transform);
            foreach (var node in Plan.Industry.Where(value => value.Tile.Equals(tile))) CreateIndustrialView(node, root.transform);
            CreateSettlementAndFieldViews(tile, root.transform);
            CreateWoodlandDetails(tile, root.transform);
            return root;
        }

        private void CreateResourceView(TiledWorldResourceNode node, Transform parent)
        {
            if (!production.TryGetSite(ResourceId(node), out var site)) return;
            if (node.RecipeId == "fish_water")
            {
                var school = new GameObject("Fishing school " + node.SourceZoneId + " " + node.StableIndex);
                school.transform.SetParent(parent, false);
                school.transform.position = new Vector3(node.VisualPosition.X, node.VisualPosition.Y, node.VisualPosition.Z);
                var lake = terrainWorld.Definition.Zones.Single(x => x.Id == node.SourceZoneId);
                var radius = Mathf.Min(2.8f, Mathf.Min(lake.RadiusX, lake.RadiusZ) * 0.17f);
                GetComponent<WorksitePresentation>().CreateFish(school.transform, school.transform.position, radius);
                var hit = school.AddComponent<BoxCollider>();
                hit.isTrigger = true;
                hit.size = new Vector3(radius * 2.6f, 1.6f, radius * 2.6f);
                school.AddComponent<ProductionSourcePresenter>().Bind(site, Color.white, true);
                return;
            }
            GameObject view;
            var reviewed = worldMapResourceArt?.PrefabFor(node.ResourceTypeId) ??
                (node.RecipeId == "hunt_animal" ? art?.HuntingDeer :
                node.RecipeId == "gather_stone" ? art?.StoneOutcrop : null);
            if (terrainWorld.Definition.MapData != null && reviewed == null)
            {
                Debug.LogError("No audited World Map V2 resource prefab for " + node.ResourceTypeId);
                return;
            }
            if (reviewed != null)
            {
                view = Instantiate(reviewed, parent);
                var collider = view.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.center = Vector3.up * 0.8f;
                collider.size = new Vector3(2.2f, 1.8f, 2.2f);
            }
            else if (templates.TryGetValue(node.RecipeId, out var template) && template != null)
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
            presenter.Bind(site, ResourceColor(node.RecipeId), reviewed != null || templates.ContainsKey(node.RecipeId));
            if (node.RecipeId == "hunt_animal") RegisterAnimalView(view, site, node.StableIndex);
        }

        private void RegisterAnimalView(GameObject view, ProductionSite site, int stableIndex)
        {
            var labelObject = new GameObject("Animal HP");
            labelObject.transform.SetParent(view.transform, false);
            labelObject.transform.localPosition = Vector3.up * 1.65f;
            var label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.08f;
            label.fontSize = 32;
            label.color = new Color(0.95f, 0.22f, 0.16f);
            label.text = "HP 120/120";
            animals.Add(new AnimalView
            {
                SiteId = site.Id,
                Transform = view.transform,
                Origin = view.transform.position,
                Phase = stableIndex * 0.73f,
                Animator = view.GetComponentInChildren<Animator>(true),
                HealthLabel = label,
                Audio = ConfigureHuntingAudio(view)
            });
        }

        private void Update()
        {
            var delta = session?.Clock?.State.IsPaused == true ? 0f : Time.deltaTime;
            animalTime += delta;
            var now = animalTime;
            var camera = Camera.main;
            RefreshForestAmbience();
            for (var index = animals.Count - 1; index >= 0; index--)
            {
                var animal = animals[index];
                if (animal.Transform == null) { animals.RemoveAt(index); continue; }
                var offset = new Vector3(Mathf.Sin(now * 0.32f + animal.Phase), 0f,
                    Mathf.Cos(now * 0.27f + animal.Phase)) * 2.4f;
                var previous = animal.Transform.position;
                var next = Vector3.MoveTowards(previous, animal.Origin + offset, delta * .85f);
                if (terrainWorld.HeightSampler.IsBuildable(next.x, next.z))
                    next.y = terrainWorld.HeightSampler.SampleHeight(next.x, next.z) + .1f;
                else next = previous;
                var direction = next - previous; direction.y = 0f;
                animal.Transform.position = next;
                if (direction.sqrMagnitude > .000001f)
                    animal.Transform.rotation = Quaternion.RotateTowards(animal.Transform.rotation,
                        Quaternion.LookRotation(direction), delta * 150f);
                if (animal.Animator != null)
                {
                    var speed = delta > 0f ? direction.magnitude / delta : 0f;
                    animal.Animator.SetBool("Moving", speed > .03f);
                    animal.Animator.speed = delta <= 0f ? 0f : speed > .03f ? Mathf.Clamp(speed / .85f, .2f, 1.2f) : 1f;
                }
                var hp = 120;
                var task = production.ActiveTasks.Where(x => x.SiteId == animal.SiteId &&
                        x.RecipeId.ToString() == "hunt_animal")
                    .OrderByDescending(x => x.AccumulatedWorkTicks).FirstOrDefault();
                if (task != null && task.RequiredWorkTicks > 0)
                    hp = Mathf.Clamp(Mathf.CeilToInt(120f * (1f -
                        task.AccumulatedWorkTicks / (float)task.RequiredWorkTicks)), 0, 120);
                if (task == null)
                {
                    animal.AudibleTaskId = null;
                    animal.LastAudibleWorkTicks = 0;
                }
                else if (!animal.AudibleTaskId.HasValue || animal.AudibleTaskId.Value != task.Id)
                {
                    animal.AudibleTaskId = task.Id;
                    animal.LastAudibleWorkTicks = 0;
                }
                if (task != null && task.AccumulatedWorkTicks > animal.LastAudibleWorkTicks &&
                    Time.unscaledTime >= animal.NextHuntSoundTime)
                {
                    var clip = animal.LastAudibleWorkTicks == 0 ? art?.HuntingBow : art?.HuntingArrow;
                    if (clip != null && animal.Audio != null) animal.Audio.PlayOneShot(clip, 0.58f);
                    animal.LastAudibleWorkTicks = task.AccumulatedWorkTicks;
                    animal.NextHuntSoundTime = Time.unscaledTime + 1.15f;
                }
                if (animal.HealthLabel != null)
                {
                    animal.HealthLabel.text = $"HP {hp}/120";
                    if (camera != null) animal.HealthLabel.transform.rotation = camera.transform.rotation;
                }
            }
        }

        private void CreateForestAmbience()
        {
            if (art?.ForestAmbientDay == null) return;
            forestAmbience = gameObject.AddComponent<AudioSource>();
            forestAmbience.loop = true;
            forestAmbience.playOnAwake = false;
            forestAmbience.spatialBlend = 0f;
            forestAmbience.volume = 0.11f;
            forestAmbience.clip = art.ForestAmbientDay;
            forestAmbience.Play();
        }

        private void RefreshForestAmbience()
        {
            if (forestAmbience == null || session?.Clock == null || art == null) return;
            var hour = GameCalendar.HourOfDay(session.Clock.State.CalendarTicks);
            var desired = hour >= 21 || hour < 5 ? art.ForestAmbientNight : art.ForestAmbientDay;
            if (desired == null || forestAmbience.clip == desired) return;
            forestAmbience.Stop();
            forestAmbience.clip = desired;
            forestAmbience.Play();
        }

        private AudioSource ConfigureHuntingAudio(GameObject view)
        {
            if (art?.HuntingBow == null && art?.HuntingArrow == null) return null;
            var source = view.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 3f;
            source.maxDistance = 34f;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }

        private void CreateWoodlandDetails(WorldTileId tile, Transform parent)
        {
            var details = art?.WoodlandDetails;
            if (details == null || details.Length == 0) return;
            foreach (var zone in terrainWorld.Definition.Zones.Where(value =>
                         value.Uses.HasFlag(WorldZoneUse.Forest)))
            {
                if (!terrainWorld.Definition.TryGetTile(zone.Center.X, zone.Center.Z, out var centerTile) ||
                    !centerTile.Equals(tile)) continue;
                for (var index = 0; index < 7; index++)
                {
                    var prefab = details[(StableDetailHash(zone.Id) + index * 17) % details.Length];
                    if (prefab == null) continue;
                    var angle = (index * 137f + tile.X * 19f + tile.Z * 31f) * Mathf.Deg2Rad;
                    var radius = Mathf.Lerp(0.38f, 0.78f, (index + 1f) / 8f);
                    var x = zone.Center.X + Mathf.Cos(angle) * zone.RadiusX * radius;
                    var z = zone.Center.Z + Mathf.Sin(angle) * zone.RadiusZ * radius;
                    if (!terrainWorld.HeightSampler.IsBuildable(x, z)) continue;
                    var detail = Instantiate(prefab, parent);
                    detail.name = "Woodland detail " + zone.Id + " " + index;
                    detail.transform.position = new Vector3(x, terrainWorld.HeightSampler.SampleHeight(x, z), z);
                    detail.transform.rotation = Quaternion.Euler(0f, index * 73f, 0f);
                }
            }
        }

        private static int StableDetailHash(string value)
        {
            var hash = 17;
            foreach (var character in value) hash = unchecked(hash * 31 + character);
            return hash & int.MaxValue;
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
                    if (zone.Id == "starter_settlement") continue; // The real camp already occupies this zone.
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
                if (zone.Uses.HasFlag(WorldZoneUse.Agriculture) && art?.FieldFence != null)
                {
                    // Small boundary markers indicate allowed farmland; no giant brown slab
                    // pretends that unplanted ground is already a functioning cultivated field.
                    for (var i = 0; i < 4; i++)
                    {
                        var x = zone.Center.X + (i % 2 == 0 ? -1 : 1) * zone.RadiusX * 0.65f;
                        var z = zone.Center.Z + (i / 2 == 0 ? -1 : 1) * zone.RadiusZ * 0.65f;
                        if (!terrainWorld.HeightSampler.IsBuildable(x, z)) continue;
                        var field = Instantiate(art.FieldFence, parent);
                        field.name = "Farmland boundary " + zone.Id;
                        field.transform.position = new Vector3(x, terrainWorld.HeightSampler.SampleHeight(x, z), z);
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
            if (id == "gather_mushrooms" || id == "mushrooms") return new Color(0.56f, 0.32f, 0.2f);
            if (id == "gather_medicinal_plants" || id == "medicinal_plants") return new Color(0.42f, 0.62f, 0.3f);
            if (id == "gather_branches" || id == "branches") return new Color(0.35f, 0.24f, 0.12f);
            if (id == "gather_clay" || id == "clay") return new Color(0.52f, 0.31f, 0.2f);
            if (id == "gather_flint" || id == "flint") return new Color(0.31f, 0.34f, 0.36f);
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
            animals.Clear();
        }
    }
}
