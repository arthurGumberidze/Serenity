using System;
using System.Collections.Generic;
using System.IO;
using Game.Domain.Buildings;
using Game.Domain.Resources;
using Game.Presentation.Buildings;
using Game.Presentation.Resources;
using UnityEditor;
using UnityEngine;

namespace Game.Infrastructure.Editor
{
    public static class P109BaseBuildingFoundation
    {
        public const string HutPath = "Assets/Game/Art/Prefabs/Buildings/Serenity_StoneAgeHut.prefab";
        public const string CampfirePath = "Assets/Game/Art/Prefabs/Buildings/Serenity_BuildableCampfire.prefab";
        public const string WorkshopPath = "Assets/Game/Art/Prefabs/Buildings/Serenity_StoneToolWorkshop.prefab";
        public const string ButcherPath = "Assets/Game/Art/Prefabs/Buildings/Serenity_ButcherStation.prefab";
        public const string HuntingCampPath = "Assets/Game/Art/Prefabs/Buildings/Serenity_HuntingCamp.prefab";
        public const string FoodStoragePath = "Assets/Game/Art/Prefabs/Buildings/Serenity_FoodStorage.prefab";
        public const string DryingRackPath = "Assets/Game/Art/Prefabs/Buildings/Serenity_DryingRack.prefab";
        public const string HideWorkshopPath = "Assets/Game/Art/Prefabs/Buildings/Serenity_HideWorkshop.prefab";
        public const string ClanHousePath = "Assets/Game/Art/Prefabs/Buildings/Serenity_ClanHouse.prefab";
        public const string HealerHutPath = "Assets/Game/Art/Prefabs/Buildings/Serenity_HealerHut.prefab";
        public const string ShrinePath = "Assets/Game/Art/Prefabs/Buildings/Serenity_Shrine.prefab";
        public const string ChiefHutPath = "Assets/Game/Art/Prefabs/Buildings/Serenity_ChiefHut.prefab";
        public const string MeetingAreaPath = "Assets/Game/Art/Prefabs/Buildings/Serenity_MeetingArea.prefab";
        public const string TrainingGroundPath = "Assets/Game/Art/Prefabs/Buildings/Serenity_TrainingGround.prefab";
        public const string FishingCampPath = "Assets/Game/Art/Prefabs/Buildings/Serenity_FishingCamp.prefab";
        public const string GatheringAreaPath = "Assets/Game/Art/Prefabs/Buildings/Serenity_GatheringArea.prefab";
        public const string PrimitiveGardenPath = "Assets/Game/Art/Prefabs/Buildings/Serenity_PrimitiveGarden.prefab";
        public const string WellPath = "Assets/Game/Art/Prefabs/Buildings/Serenity_Well.prefab";
        public const string PalisadePath = "Assets/Game/Art/Prefabs/Buildings/Serenity_Palisade.prefab";
        public const string GatePath = "Assets/Game/Art/Prefabs/Buildings/Serenity_Gate.prefab";
        public const string WatchtowerPath = "Assets/Game/Art/Prefabs/Buildings/Serenity_Watchtower.prefab";
        public const string PrimitivePathPath = "Assets/Game/Art/Prefabs/Buildings/Serenity_PrimitivePath.prefab";
        private const string CropPath = "Assets/Game/Art/Prefabs/Buildings/Serenity_PrimitiveCropPlot.prefab";
        private const string MaterialFolder = "Assets/Game/Art/Materials/P94";
        private const string HutModelPath = "Assets/SoftLeitner/CityBuilderThree/Models/hut.fbx";
        private const string CityModels = "Assets/SoftLeitner/CityBuilderThree/Models/";
        private const string RtsBuildings = "Assets/RTSToonBuildings/Prefabs/Buildings/";
        private const string SyntyCampfire = "Assets/Synty/PolygonNature/Prefabs/Props/SM_Prop_CampFire_01.prefab";

        [MenuItem("Serenity/P10.9/Build Five Base Buildings")]
        public static void Build()
        {
            EnsureBasePrefabs();
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(U08BuildingFoundation.CatalogPath);
            if (catalog == null) throw new FileNotFoundException("Building catalog is missing.", U08BuildingFoundation.CatalogPath);
            ConfigureCatalog(catalog);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            Debug.Log("P10.9 five base building assets generated successfully.");
        }

        public static void EnsureBasePrefabs()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(HutPath));
            BuildHut();
            BuildCampfire();
            BuildWorkshop();
            BuildButcherStation();
            BuildHuntingCamp();
            BuildFoodStorage();
            BuildDryingRack();
            BuildHideWorkshop();
            BuildExtendedBuildings();
        }

        public static void ConfigureCatalog(BuildingPresentationCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var shelter = Load<GameObject>(U08BuildingFoundation.ShelterPrefabPath);
            var storage = Load<GameObject>(U08BuildingFoundation.StoragePrefabPath);
            var entries = new[]
            {
                Entry("primitive_shelter", "Primitive Shelter", BuildingCategory.Shelter, 4, 3, shelter,
                    "A quick hide shelter for the first residents.", "building_primitive_shelter", 50, 3,
                    new[] { Cost("wood_log", 8), Cost("plant_fiber", 3) },
                    "shelter_construction").ConfigureHousing(2),
                Entry("stone_age_hut", "Stone Age Hut", BuildingCategory.Shelter, 4, 4, Load<GameObject>(HutPath),
                    "A sturdier timber and hide home.", "building_stone_age_hut", 80, 3,
                    new[] { Cost("wood_log", 12), Cost("plant_fiber", 6), Cost("hide", 2) },
                    "shelter_construction").ConfigureHousing(4)
                    .ConfigureMaintenance(24, 8, new[] { Cost("wood_log", 2), Cost("plant_fiber", 1) }, 30)
                    .ConfigureUpgrades(new[] { Upgrade("clan_house", 100, Cost("wood_log", 16), Cost("stone", 8), Cost("cured_hide", 4)) }),
                Entry("campfire", "Campfire", BuildingCategory.Food, 2, 2, Load<GameObject>(CampfirePath),
                    "A controlled fire and the settlement's first cooking place.", "building_campfire", 35, 2,
                    new[] { Cost("wood_log", 3), Cost("stone", 4) }, "controlled_fire")
                    .ConfigureProduction(1, new[] { "cook_meat", "cook_fish", "cook_berries" }, 24, 24),
                Entry("storage_basket", "Storage Basket", BuildingCategory.Storage, 2, 1, storage,
                    "A woven shared store for settlement supplies.", "building_storage_basket", 45, 3,
                    new[] { Cost("wood_log", 3), Cost("plant_fiber", 5) }, "woodworking")
                    .ConfigureEconomy(new[] { Cost("wood_log", 3), Cost("plant_fiber", 5) }, 40,
                        new[] { ResourceCategory.Construction, ResourceCategory.Food, ResourceCategory.Other }),
                Entry("stone_tool_workshop", "Stone Tool Workshop", BuildingCategory.Production, 4, 3,
                    Load<GameObject>(WorkshopPath), "A covered knapping bench for early tools.",
                    "building_stone_tool_workshop", 90, 3,
                    new[] { Cost("wood_log", 10), Cost("stone", 8), Cost("plant_fiber", 4) }, "stone_tools"),
                Entry("butcher_station", "Butcher Station", BuildingCategory.Food, 3, 3,
                    Load<GameObject>(ButcherPath), "A dedicated table turns whole carcasses into meat, hide and bone.",
                    "building_butcher_station", 65, 3,
                    new[] { Cost("wood_log", 7), Cost("stone", 3), Cost("plant_fiber", 3) }, "hunting")
                    .ConfigureProduction(1, new[] { "butcher_carcass" }, 12, 24),
                Entry("hunting_camp", "Hunting Camp", BuildingCategory.Production, 4, 3,
                    Load<GameObject>(HuntingCampPath), "A field camp required to organize nearby animal hunts.",
                    "building_hunting_camp", 75, 3,
                    new[] { Cost("wood_log", 9), Cost("plant_fiber", 5), Cost("hide", 2) }, "hunting")
                    .ConfigureProduction(3, Array.Empty<string>(), 0, 0),
                Entry("food_storage", "Food Storage", BuildingCategory.Storage, 4, 3,
                    Load<GameObject>(FoodStoragePath), "A raised, covered store reserved for food supplies.",
                    "building_food_storage", 70, 3,
                    new[] { Cost("wood_log", 10), Cost("plant_fiber", 7), Cost("hide", 2) }, "food_preservation")
                    .ConfigureEconomy(new[] { Cost("wood_log", 10), Cost("plant_fiber", 7), Cost("hide", 2) },
                        120, new[] { ResourceCategory.Food }),
                Entry("drying_rack", "Drying Rack", BuildingCategory.Food, 3, 2,
                    Load<GameObject>(DryingRackPath), "An open rack preserves butchered meat without fuel.",
                    "building_drying_rack", 55, 3,
                    new[] { Cost("wood_log", 6), Cost("plant_fiber", 5) }, "food_preservation")
                    .ConfigureProduction(1, new[] { "dry_meat" }, 18, 18),
                Entry("hide_workshop", "Hide Workshop", BuildingCategory.Production, 4, 3,
                    Load<GameObject>(HideWorkshopPath), "A covered frame for cleaning and curing hides.",
                    "building_hide_workshop", 80, 3,
                    new[] { Cost("wood_log", 9), Cost("stone", 3), Cost("plant_fiber", 7) }, "food_preservation")
                    .ConfigureProduction(1, new[] { "cure_hide" }, 18, 18),
                Entry("primitive_crop_plot", "Primitive Crop Plot", BuildingCategory.Structure, 3, 3,
                    Load<GameObject>(CropPath), "A small worked plot.", "building_primitive_crop_plot", 35, 2,
                    new[] { Cost("wood_log", 2), Cost("plant_fiber", 2) }, "primitive_agriculture"),
                Entry("military_barracks", "Military Barracks", BuildingCategory.Military, 4, 3, shelter,
                    "A simple training and mustering shelter.", "building_military_barracks", 120, 3,
                    new[] { Cost("wood_log", 12), Cost("stone", 6) }, "organized_labor")
                    .ConfigureStructure(1400, 900),
                Entry("clan_house", "Clan House", BuildingCategory.Shelter, 6, 5, Load<GameObject>(ClanHousePath),
                    "A large shared home with room for an extended clan.", "building_clan_house", 150, 4,
                    new[] { Cost("wood_log", 26), Cost("stone", 12), Cost("cured_hide", 6) }, "organized_labor")
                    .ConfigureHousing(10).ConfigureMaintenance(24, 6,
                        new[] { Cost("wood_log", 3), Cost("plant_fiber", 2) }, 50),
                Entry("healer_hut", "Healer Hut", BuildingCategory.Medical, 4, 4, Load<GameObject>(HealerHutPath),
                    "A sheltered medical workplace for treating injuries and disease.", "building_healer_hut", 100, 3,
                    new[] { Cost("wood_log", 12), Cost("stone", 5), Cost("plant_fiber", 8) }, "organized_labor")
                    .ConfigureHousing(2).ConfigureWorkplace(2).ConfigureMaintenance(24, 7,
                        new[] { Cost("wood_log", 2), Cost("plant_fiber", 2) }, 35),
                Entry("shrine", "Shrine", BuildingCategory.Community, 3, 3, Load<GameObject>(ShrinePath),
                    "A maintained communal place that supports settlement morale.", "building_shrine", 65, 3,
                    new[] { Cost("stone", 10), Cost("wood_log", 4) }, "organized_labor")
                    .ConfigureWorkplace(1).ConfigureMaintenance(24, 5, new[] { Cost("stone", 2) }, 25,
                        new[] { Cost("plant_fiber", 1) }),
                Entry("chief_hut", "Chief Hut", BuildingCategory.Community, 5, 4, Load<GameObject>(ChiefHutPath),
                    "The chief's residence and the settlement governance workplace.", "building_chief_hut", 130, 4,
                    new[] { Cost("wood_log", 20), Cost("stone", 8), Cost("cured_hide", 5) }, "organized_labor")
                    .ConfigureHousing(4).ConfigureWorkplace(1).ConfigureMaintenance(24, 6,
                        new[] { Cost("wood_log", 3), Cost("cured_hide", 1) }, 45),
                Zone("meeting_area", "Meeting Area", BuildingCategory.Community, Load<GameObject>(MeetingAreaPath),
                    "A marked area for settlement gatherings.", 35, Cost("wood_log", 3), Cost("stone", 2))
                    .ConfigureWorkplace(12),
                Zone("training_ground", "Training Ground", BuildingCategory.Military, Load<GameObject>(TrainingGroundPath),
                    "An open practice area for improving combat skill.", 55, Cost("wood_log", 6), Cost("stone", 4))
                    .ConfigureWorkplace(6),
                Entry("fishing_camp", "Fishing Camp", BuildingCategory.Food, 4, 3, Load<GameObject>(FishingCampPath),
                    "A shoreline base required for organized fishing.", "building_fishing_camp", 70, 3,
                    new[] { Cost("wood_log", 8), Cost("plant_fiber", 6) }, "gathering")
                    .ConfigurePlacement(BuildingSpatialForm.Structure, BuildingPlacementConstraint.ReachableApproach |
                        BuildingPlacementConstraint.OwnedTerritory | BuildingPlacementConstraint.Shoreline)
                    .ConfigureWorkplace(4),
                Zone("gathering_area", "Gathering Area", BuildingCategory.Production, Load<GameObject>(GatheringAreaPath),
                    "A managed zone over wild berry and fibre sources.", 30, Cost("wood_log", 2), Cost("plant_fiber", 2))
                    .ConfigureWorkplace(8),
                Zone("primitive_garden", "Primitive Garden", BuildingCategory.Food, Load<GameObject>(PrimitiveGardenPath),
                    "A larger cultivated zone with sowing and harvest cycles.", 60, Cost("wood_log", 4), Cost("plant_fiber", 5))
                    .ConfigureWorkplace(4),
                Entry("well", "Well", BuildingCategory.Infrastructure, 3, 3, Load<GameObject>(WellPath),
                    "A stone-lined groundwater source.", "building_well", 85, 3,
                    new[] { Cost("stone", 12), Cost("wood_log", 6) }, "advanced_stonecraft")
                    .ConfigurePlacement(BuildingSpatialForm.Structure, BuildingPlacementConstraint.ReachableApproach |
                        BuildingPlacementConstraint.OwnedTerritory | BuildingPlacementConstraint.Groundwater)
                    .ConfigureProduction(2, new[] { "draw_water" }, 4, 24),
                Segment("palisade", "Palisade", BuildingCategory.Military, Load<GameObject>(PalisadePath), false,
                    "A connected timber defensive barrier.", 35, Cost("wood_log", 5)),
                Segment("gate", "Gate", BuildingCategory.Military, Load<GameObject>(GatePath), false,
                    "A controllable passage through a palisade.", 55, Cost("wood_log", 8), Cost("stone", 2)),
                Entry("watchtower", "Watchtower", BuildingCategory.Military, 3, 3, Load<GameObject>(WatchtowerPath),
                    "An elevated defensive observation platform.", "building_watchtower", 100, 4,
                    new[] { Cost("wood_log", 14), Cost("stone", 5), Cost("plant_fiber", 4) }, "organized_labor")
                    .ConfigureWorkplace(2).ConfigureStructure(1600, 850),
                Segment("primitive_path", "Primitive Path", BuildingCategory.Infrastructure,
                    Load<GameObject>(PrimitivePathPath), true,
                    "A terrain-following footpath that remains traversable.", 15, Cost("stone", 1))
            };
            catalog.Configure(entries, "primitive_shelter");
            var resources = Load<ResourceCatalogAsset>(U09ResourceFoundation.CatalogPath);
            catalog.ConfigureResources(resources);
        }

        public static void Validate()
        {
            var catalog = Load<BuildingPresentationCatalog>(U08BuildingFoundation.CatalogPath);
            catalog.Validate();
            foreach (var path in new[] { HutPath, CampfirePath, WorkshopPath, ButcherPath, HuntingCampPath,
                         FoodStoragePath, DryingRackPath, HideWorkshopPath, ClanHousePath, HealerHutPath,
                         ShrinePath, ChiefHutPath, MeetingAreaPath, TrainingGroundPath, FishingCampPath,
                         GatheringAreaPath, PrimitiveGardenPath, WellPath, PalisadePath, GatePath,
                         WatchtowerPath, PrimitivePathPath })
            {
                var prefab = Load<GameObject>(path);
                if (prefab.GetComponent<BuildingPresenter>() == null || prefab.GetComponent<Collider>() == null)
                    throw new InvalidDataException("Base building prefab is missing its presenter or root collider: " + path);
                var stages = prefab.GetComponentsInChildren<BuildingConstructionStageVisual>(true);
                if (stages.Length < 2) throw new InvalidDataException("Base building prefab requires visual stages: " + path);
            }
        }

        private static BuildingPresentationCatalog.Entry Entry(string id, string name, BuildingCategory category,
            int width, int depth, GameObject prefab, string description, string icon, int minutes, int stages,
            IEnumerable<BuildingPresentationCatalog.Entry.CostEntry> cost, params string[] technologies)
        {
            return new BuildingPresentationCatalog.Entry().Configure(id, name, category, width, depth, prefab)
                .ConfigureMetadata(BuildingEra.StoneAge, description, icon, technologies)
                .ConfigureConstruction(minutes, stages).ConfigureEconomy(cost)
                .ConfigurePlacement(BuildingSpatialForm.Structure, BuildingPlacementConstraint.ReachableApproach)
                .ConfigureMaintenance(24, 5, new[] { Cost("wood_log", 1) }, 30);
        }

        private static BuildingPresentationCatalog.Entry.CostEntry Cost(string id, long quantity) =>
            new BuildingPresentationCatalog.Entry.CostEntry().Configure(id, quantity);

        private static BuildingPresentationCatalog.Entry.UpgradeEntry Upgrade(string targetId, int minutes,
            params BuildingPresentationCatalog.Entry.CostEntry[] cost) =>
            new BuildingPresentationCatalog.Entry.UpgradeEntry().Configure(targetId, cost, minutes);

        private static BuildingPresentationCatalog.Entry Zone(string id, string name, BuildingCategory category,
            GameObject prefab, string description, int minutes,
            params BuildingPresentationCatalog.Entry.CostEntry[] cost) =>
            Entry(id, name, category, 3, 3, prefab, description, "building_" + id, minutes, 2, cost,
                id == "primitive_garden" ? "primitive_agriculture" : "organized_labor")
                .Configure("" + id, name, category, 3, 3, prefab, BuildingPlacementMode.Grid, true)
                .ConfigurePlacement(BuildingSpatialForm.Zone, BuildingPlacementConstraint.OwnedTerritory);

        private static BuildingPresentationCatalog.Entry Segment(string id, string name, BuildingCategory category,
            GameObject prefab, bool overlap, string description, int minutes,
            params BuildingPresentationCatalog.Entry.CostEntry[] cost) =>
            new BuildingPresentationCatalog.Entry().Configure(id, name, category, 1, 1, prefab,
                    BuildingPlacementMode.Grid, overlap)
                .ConfigureMetadata(BuildingEra.StoneAge, description, "building_" + id,
                    new[] { "organized_labor" }).ConfigureConstruction(minutes, 2).ConfigureEconomy(cost)
                .ConfigurePlacement(BuildingSpatialForm.Segment, BuildingPlacementConstraint.OwnedTerritory)
                .ConfigureMaintenance(24, 5, new[] { Cost("wood_log", 1) }, 20);

        private static void BuildHut()
        {
            var root = Root("Serenity Stone Age Hut", new Vector3(4f, 3.2f, 4f), new Vector3(0f, 1.6f, 0f));
            var foundation = Stage(root.transform, "Stage 1 Foundation", 1);
            for (var x = -1; x <= 1; x += 2)
                for (var z = -1; z <= 1; z += 2)
                    Part(PrimitiveType.Cylinder, "Footing", foundation, new Vector3(x * 1.45f, .12f, z * 1.45f),
                        new Vector3(.5f, .12f, .5f), Stone);
            var frame = Stage(root.transform, "Stage 2 Timber Frame", 2);
            for (var x = -1; x <= 1; x += 2)
                for (var z = -1; z <= 1; z += 2)
                    Part(PrimitiveType.Cylinder, "Post", frame, new Vector3(x * 1.45f, 1.45f, z * 1.45f),
                        new Vector3(.18f, 1.45f, .18f), Bark);
            var finish = Stage(root.transform, "Stage 3 Finished Hut", 3);
            AddFittedModel(Load<GameObject>(HutModelPath), finish, new Vector3(3.8f, 3.1f, 3.8f));
            Save(root, HutPath);
        }

        private static GameObject AddFittedModel(GameObject source, Transform parent, Vector3 maximumSize)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            model.name = "Finished authored model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidDataException("Authored model has no renderers: " + source.name);
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            var scale = Mathf.Min(maximumSize.x / Mathf.Max(.01f, bounds.size.x),
                maximumSize.y / Mathf.Max(.01f, bounds.size.y),
                maximumSize.z / Mathf.Max(.01f, bounds.size.z));
            model.transform.localScale = Vector3.one * scale;
            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            model.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            foreach (var collider in model.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            return model;
        }

        private static void BuildCampfire()
        {
            var root = Root("Serenity Buildable Campfire", new Vector3(2f, .8f, 2f), new Vector3(0f, .4f, 0f));
            var ring = Stage(root.transform, "Stage 1 Stone Ring", 1);
            for (var i = 0; i < 10; i++)
            {
                var angle = i * Mathf.PI * 2f / 10f;
                Part(PrimitiveType.Sphere, "Ring stone", ring,
                    new Vector3(Mathf.Cos(angle) * .72f, .16f, Mathf.Sin(angle) * .72f),
                    new Vector3(.36f, .22f, .3f), Stone, new Vector3(0f, -angle * Mathf.Rad2Deg, 0f));
            }
            var fuel = Stage(root.transform, "Stage 2 Fuel", 2);
            AddFittedModel(Load<GameObject>(SyntyCampfire), fuel, new Vector3(1.75f, .75f, 1.75f));
            Save(root, CampfirePath);
        }

        private static void BuildWorkshop()
        {
            var root = Root("Serenity Stone Tool Workshop", new Vector3(4f, 3f, 3f), new Vector3(0f, 1.5f, 0f));
            var baseStage = Stage(root.transform, "Stage 1 Work Floor", 1);
            Part(PrimitiveType.Cube, "Packed floor", baseStage, new Vector3(0f, .06f, 0f),
                new Vector3(3.5f, .12f, 2.5f), Soil);
            var frame = Stage(root.transform, "Stage 2 Shelter Frame", 2);
            foreach (var x in new[] { -1.5f, 1.5f })
                foreach (var z in new[] { -1f, 1f })
                    Part(PrimitiveType.Cylinder, "Post", frame, new Vector3(x, 1.25f, z),
                        new Vector3(.14f, 1.25f, .14f), Bark);
            Part(PrimitiveType.Cube, "Canopy", frame, new Vector3(0f, 2.45f, 0f),
                new Vector3(3.7f, .12f, 2.7f), Hide);
            var tools = Stage(root.transform, "Stage 3 Workbench", 3);
            Part(PrimitiveType.Cube, "Bench", tools, new Vector3(0f, .72f, .25f),
                new Vector3(2.2f, .18f, .8f), DarkBark);
            Part(PrimitiveType.Cylinder, "Bench support A", tools, new Vector3(-.75f, .37f, .25f),
                new Vector3(.12f, .37f, .12f), Bark);
            Part(PrimitiveType.Cylinder, "Bench support B", tools, new Vector3(.75f, .37f, .25f),
                new Vector3(.12f, .37f, .12f), Bark);
            for (var i = -1; i <= 1; i++)
                Part(PrimitiveType.Sphere, "Knapping stone", tools, new Vector3(i * .35f, .94f, .25f),
                    new Vector3(.22f, .12f, .18f), Stone);
            ClearChildren(tools);
            AddFittedModel(Load<GameObject>(CityModels + "workshop.fbx"), tools, new Vector3(3.8f, 2.9f, 2.8f));
            Save(root, WorkshopPath);
        }

        private static void BuildButcherStation()
        {
            var root = Root("Serenity Butcher Station", new Vector3(3f, 2.4f, 3f), new Vector3(0f, 1.2f, 0f));
            var foundation = Stage(root.transform, "Stage 1 Work Ground", 1);
            Part(PrimitiveType.Cube, "Packed ground", foundation, new Vector3(0f, .05f, 0f),
                new Vector3(2.6f, .1f, 2.4f), Soil);
            var frame = Stage(root.transform, "Stage 2 Rack", 2);
            foreach (var x in new[] { -1f, 1f })
                Part(PrimitiveType.Cylinder, "Rack post", frame, new Vector3(x, 1.1f, .65f),
                    new Vector3(.11f, 1.1f, .11f), Bark);
            Part(PrimitiveType.Cylinder, "Rack beam", frame, new Vector3(0f, 2.08f, .65f),
                new Vector3(.11f, 1.1f, .11f), Bark, new Vector3(0f, 0f, 90f));
            var finish = Stage(root.transform, "Stage 3 Butcher Table", 3);
            Part(PrimitiveType.Cube, "Work slab", finish, new Vector3(0f, .82f, -.35f),
                new Vector3(2.15f, .18f, 1.05f), DarkBark);
            foreach (var x in new[] { -.8f, .8f })
                foreach (var z in new[] { -.68f, -.02f })
                    Part(PrimitiveType.Cylinder, "Table leg", finish, new Vector3(x, .4f, z),
                        new Vector3(.1f, .4f, .1f), Bark);
            ClearChildren(finish);
            AddFittedModel(Load<GameObject>(CityModels + "MasonCamp.fbx"), finish, new Vector3(2.8f, 2.25f, 2.8f));
            Save(root, ButcherPath);
        }

        private static void BuildHuntingCamp()
        {
            var root = Root("Serenity Hunting Camp", new Vector3(4f, 3f, 3f), new Vector3(0f, 1.5f, 0f));
            var ground = Stage(root.transform, "Stage 1 Camp Ground", 1);
            Part(PrimitiveType.Cube, "Cleared ground", ground, new Vector3(0f, .04f, 0f),
                new Vector3(3.6f, .08f, 2.6f), Soil);
            var frame = Stage(root.transform, "Stage 2 Camp Frame", 2);
            foreach (var x in new[] { -1.5f, 1.5f })
                foreach (var z in new[] { -.9f, .9f })
                    Part(PrimitiveType.Cylinder, "Camp pole", frame, new Vector3(x, 1.25f, z),
                        new Vector3(.1f, 1.25f, .1f), Bark);
            var finish = Stage(root.transform, "Stage 3 Hide Canopy", 3);
            Part(PrimitiveType.Cube, "Hide canopy", finish, new Vector3(0f, 2.35f, 0f),
                new Vector3(3.7f, .1f, 2.7f), Hide, new Vector3(0f, 0f, 6f));
            Part(PrimitiveType.Cube, "Gear bench", finish, new Vector3(0f, .55f, .5f),
                new Vector3(2.1f, .16f, .55f), DarkBark);
            ClearChildren(finish);
            AddFittedModel(Load<GameObject>(CityModels + "Hunter.fbx"), finish, new Vector3(3.8f, 2.9f, 2.8f));
            Save(root, HuntingCampPath);
        }

        private static void BuildFoodStorage()
        {
            var root = Root("Serenity Food Storage", new Vector3(4f, 3f, 3f), new Vector3(0f, 1.5f, 0f));
            var baseStage = Stage(root.transform, "Stage 1 Raised Base", 1);
            foreach (var x in new[] { -1.45f, 1.45f })
                foreach (var z in new[] { -.95f, .95f })
                    Part(PrimitiveType.Cylinder, "Foundation post", baseStage, new Vector3(x, .35f, z),
                        new Vector3(.14f, .35f, .14f), Bark);
            Part(PrimitiveType.Cube, "Raised floor", baseStage, new Vector3(0f, .72f, 0f),
                new Vector3(3.5f, .16f, 2.45f), DarkBark);
            var walls = Stage(root.transform, "Stage 2 Woven Store", 2);
            Part(PrimitiveType.Cube, "Store body", walls, new Vector3(0f, 1.55f, 0f),
                new Vector3(3.15f, 1.45f, 2.1f), Hide);
            var roof = Stage(root.transform, "Stage 3 Weather Roof", 3);
            Part(PrimitiveType.Cube, "Sloped roof", roof, new Vector3(0f, 2.55f, 0f),
                new Vector3(3.8f, .16f, 2.75f), DarkBark, new Vector3(0f, 0f, 5f));
            ClearChildren(roof);
            AddFittedModel(Load<GameObject>(CityModels + "silo.fbx"), roof, new Vector3(3.8f, 2.9f, 2.8f));
            Save(root, FoodStoragePath);
        }

        private static void BuildDryingRack()
        {
            var root = Root("Serenity Drying Rack", new Vector3(3f, 2.6f, 2f), new Vector3(0f, 1.3f, 0f));
            var feet = Stage(root.transform, "Stage 1 Rack Feet", 1);
            foreach (var x in new[] { -1.1f, 1.1f })
                Part(PrimitiveType.Cylinder, "Rack foot", feet, new Vector3(x, .55f, 0f),
                    new Vector3(.13f, .55f, .13f), Bark);
            var frame = Stage(root.transform, "Stage 2 Crossbar", 2);
            Part(PrimitiveType.Cylinder, "Crossbar", frame, new Vector3(0f, 1.75f, 0f),
                new Vector3(.12f, 1.25f, .12f), Bark, new Vector3(0f, 0f, 90f));
            var finish = Stage(root.transform, "Stage 3 Drying Lines", 3);
            for (var i = -2; i <= 2; i++)
            {
                Part(PrimitiveType.Cylinder, "Hanging cord", finish, new Vector3(i * .42f, 1.28f, 0f),
                    new Vector3(.025f, .4f, .025f), DarkBark);
                Part(PrimitiveType.Cube, "Drying strip", finish, new Vector3(i * .42f, .92f, 0f),
                    new Vector3(.25f, .5f, .08f), Hide);
            }
            Save(root, DryingRackPath);
        }

        private static void BuildHideWorkshop()
        {
            var root = Root("Serenity Hide Workshop", new Vector3(4f, 3f, 3f), new Vector3(0f, 1.5f, 0f));
            var ground = Stage(root.transform, "Stage 1 Work Ground", 1);
            Part(PrimitiveType.Cube, "Packed ground", ground, new Vector3(0f, .05f, 0f),
                new Vector3(3.6f, .1f, 2.5f), Soil);
            var frame = Stage(root.transform, "Stage 2 Tanning Frame", 2);
            foreach (var x in new[] { -1.25f, 1.25f })
                Part(PrimitiveType.Cylinder, "Frame post", frame, new Vector3(x, 1.25f, .45f),
                    new Vector3(.12f, 1.25f, .12f), Bark);
            Part(PrimitiveType.Cylinder, "Frame top", frame, new Vector3(0f, 2.35f, .45f),
                new Vector3(.12f, 1.35f, .12f), Bark, new Vector3(0f, 0f, 90f));
            var finish = Stage(root.transform, "Stage 3 Stretched Hide", 3);
            Part(PrimitiveType.Cube, "Stretched hide", finish, new Vector3(0f, 1.35f, .42f),
                new Vector3(2.1f, 1.55f, .06f), Hide);
            Part(PrimitiveType.Cube, "Scraping bench", finish, new Vector3(0f, .55f, -.7f),
                new Vector3(2.2f, .16f, .65f), DarkBark);
            ClearChildren(finish);
            AddFittedModel(Load<GameObject>(CityModels + "labourCamp.fbx"), finish, new Vector3(3.8f, 2.9f, 2.8f));
            Save(root, HideWorkshopPath);
        }

        private static void BuildExtendedBuildings()
        {
            BuildFacility(ClanHousePath, "Serenity Clan House", new Vector3(6f, 4f, 5f), FacilityKind.House);
            BuildFacility(HealerHutPath, "Serenity Healer Hut", new Vector3(4f, 3.2f, 4f), FacilityKind.Healer);
            BuildFacility(ShrinePath, "Serenity Shrine", new Vector3(3f, 2.8f, 3f), FacilityKind.Shrine);
            BuildFacility(ChiefHutPath, "Serenity Chief Hut", new Vector3(5f, 3.8f, 4f), FacilityKind.Chief);
            BuildFacility(MeetingAreaPath, "Serenity Meeting Area", new Vector3(3f, .2f, 3f), FacilityKind.Meeting);
            BuildFacility(TrainingGroundPath, "Serenity Training Ground", new Vector3(3f, .2f, 3f), FacilityKind.Training);
            BuildFacility(FishingCampPath, "Serenity Fishing Camp", new Vector3(4f, 2.8f, 3f), FacilityKind.Fishing);
            BuildFacility(GatheringAreaPath, "Serenity Gathering Area", new Vector3(3f, .2f, 3f), FacilityKind.Gathering);
            BuildFacility(PrimitiveGardenPath, "Serenity Primitive Garden", new Vector3(3f, .2f, 3f), FacilityKind.Garden);
            BuildFacility(WellPath, "Serenity Well", new Vector3(3f, 2.5f, 3f), FacilityKind.Well);
            BuildFacility(PalisadePath, "Serenity Palisade", new Vector3(1f, 2.8f, 1f), FacilityKind.Palisade);
            BuildFacility(GatePath, "Serenity Gate", new Vector3(1f, 3.2f, 1f), FacilityKind.Gate);
            BuildFacility(WatchtowerPath, "Serenity Watchtower", new Vector3(3f, 6f, 3f), FacilityKind.Watchtower);
            BuildFacility(PrimitivePathPath, "Serenity Primitive Path", new Vector3(1f, .08f, 1f), FacilityKind.Path);
        }

        private enum FacilityKind { House, Healer, Shrine, Chief, Meeting, Training, Fishing, Gathering, Garden, Well, Palisade, Gate, Watchtower, Path }

        private static void BuildFacility(string path, string name, Vector3 size, FacilityKind kind)
        {
            var root = Root(name, size, new Vector3(0f, size.y * .5f, 0f));
            if (kind == FacilityKind.Meeting || kind == FacilityKind.Gathering || kind == FacilityKind.Training || kind == FacilityKind.Garden ||
                kind == FacilityKind.Path) root.GetComponent<BoxCollider>().isTrigger = true;
            var baseStage = Stage(root.transform, "Stage 1 Foundation", 1);
            Part(PrimitiveType.Cube, "Ground", baseStage, new Vector3(0f, .04f, 0f),
                new Vector3(Mathf.Max(.85f, size.x * .9f), .08f, Mathf.Max(.85f, size.z * .9f)),
                kind == FacilityKind.Path || kind == FacilityKind.Garden ? Soil : Stone);
            var finish = Stage(root.transform, "Stage 2 Finished", 2);
            if (kind == FacilityKind.Garden)
            {
                var template = new GameObject("Garden cell template");
                template.transform.SetParent(finish, false);
                Part(PrimitiveType.Cube, "Cultivated soil", template.transform, new Vector3(0f, .035f, 0f),
                    new Vector3(.9f, .07f, .9f), Soil);
                AddFittedModel(Load<GameObject>(CityModels + "Crop.fbx"), template.transform,
                    new Vector3(.8f, .55f, .8f));
                root.AddComponent<BuildingZoneVisual>().Configure(template, 64);
            }
            else
            {
                AddFittedModel(Load<GameObject>(FacilityModelPath(kind)), finish,
                    new Vector3(Mathf.Max(.8f, size.x * .95f), Mathf.Max(.07f, size.y * .95f),
                        Mathf.Max(.8f, size.z * .95f)));
            }
            Save(root, path);
        }

        private static string FacilityModelPath(FacilityKind kind) => kind switch
        {
            FacilityKind.House => CityModels + "house.fbx",
            FacilityKind.Healer => CityModels + "doctor.fbx",
            FacilityKind.Shrine => CityModels + "ObelisksSite.fbx",
            FacilityKind.Chief => CityModels + "villa.fbx",
            FacilityKind.Meeting => CityModels + "stage.fbx",
            FacilityKind.Training => RtsBuildings + "ArcheryRange.prefab",
            FacilityKind.Fishing => RtsBuildings + "Dock.prefab",
            FacilityKind.Gathering => CityModels + "StorageYard.fbx",
            FacilityKind.Well => CityModels + "well.fbx",
            FacilityKind.Palisade => RtsBuildings + "Wall_01.prefab",
            FacilityKind.Gate => RtsBuildings + "Gate.prefab",
            FacilityKind.Watchtower => RtsBuildings + "WatchTower.prefab",
            FacilityKind.Path => CityModels + "Roads/road0.fbx",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        private static void ClearChildren(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        private static GameObject Root(string name, Vector3 colliderSize, Vector3 colliderCenter)
        {
            var root = new GameObject(name);
            root.AddComponent<BuildingPresenter>();
            var collider = root.AddComponent<BoxCollider>();
            collider.size = colliderSize;
            collider.center = colliderCenter;
            return root;
        }

        private static Transform Stage(Transform parent, string name, int requiredStage)
        {
            var stage = new GameObject(name);
            stage.transform.SetParent(parent, false);
            stage.AddComponent<BuildingConstructionStageVisual>().Configure(requiredStage);
            return stage.transform;
        }

        private static void Part(PrimitiveType primitive, string name, Transform parent, Vector3 position,
            Vector3 scale, Material material, Vector3 rotation = default)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localEulerAngles = rotation;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        }

        private static void Save(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new FileNotFoundException("Required asset is missing.", path);

        private static Material Bark => Load<Material>(MaterialFolder + "/P94_Bark.mat");
        private static Material DarkBark => Load<Material>(MaterialFolder + "/P94_DarkBark.mat");
        private static Material Hide => Load<Material>(MaterialFolder + "/P94_Hide.mat");
        private static Material Stone => Load<Material>(MaterialFolder + "/P94_Stone.mat");
        private static Material Soil => Load<Material>(MaterialFolder + "/P94_Soil.mat");
    }
}
