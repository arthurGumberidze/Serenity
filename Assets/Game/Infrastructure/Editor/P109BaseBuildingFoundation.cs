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
        private const string CropPath = "Assets/Game/Art/Prefabs/Buildings/Serenity_PrimitiveCropPlot.prefab";
        private const string MaterialFolder = "Assets/Game/Art/Materials/P94";
        private const string HutModelPath = "Assets/SoftLeitner/CityBuilderThree/Models/hut.fbx";

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
                    "shelter_construction").ConfigureHousing(4),
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
                Entry("primitive_crop_plot", "Primitive Crop Plot", BuildingCategory.Structure, 3, 3,
                    Load<GameObject>(CropPath), "A small worked plot.", "building_primitive_crop_plot", 35, 2,
                    new[] { Cost("wood_log", 2), Cost("plant_fiber", 2) }, "primitive_agriculture"),
                Entry("military_barracks", "Military Barracks", BuildingCategory.Military, 4, 3, shelter,
                    "A simple training and mustering shelter.", "building_military_barracks", 120, 3,
                    new[] { Cost("wood_log", 12), Cost("stone", 6) }, "organized_labor")
                    .ConfigureStructure(1400, 900)
            };
            catalog.Configure(entries, "primitive_shelter");
            var resources = Load<ResourceCatalogAsset>(U09ResourceFoundation.CatalogPath);
            catalog.ConfigureResources(resources);
        }

        public static void Validate()
        {
            var catalog = Load<BuildingPresentationCatalog>(U08BuildingFoundation.CatalogPath);
            catalog.Validate();
            foreach (var path in new[] { HutPath, CampfirePath, WorkshopPath })
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
            IEnumerable<BuildingPresentationCatalog.Entry.CostEntry> cost, params string[] technologies) =>
            new BuildingPresentationCatalog.Entry().Configure(id, name, category, width, depth, prefab)
                .ConfigureMetadata(BuildingEra.StoneAge, description, icon, technologies)
                .ConfigureConstruction(minutes, stages).ConfigureEconomy(cost)
                .ConfigurePlacement(BuildingSpatialForm.Structure, BuildingPlacementConstraint.ReachableApproach);

        private static BuildingPresentationCatalog.Entry.CostEntry Cost(string id, long quantity) =>
            new BuildingPresentationCatalog.Entry.CostEntry().Configure(id, quantity);

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

        private static void AddFittedModel(GameObject source, Transform parent, Vector3 maximumSize)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            model.name = "Finished hut model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidDataException("Hut model has no renderers: " + HutModelPath);
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            var scale = Mathf.Min(maximumSize.x / Mathf.Max(.01f, bounds.size.x),
                maximumSize.y / Mathf.Max(.01f, bounds.size.y),
                maximumSize.z / Mathf.Max(.01f, bounds.size.z));
            model.transform.localScale = Vector3.one * scale;
            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            model.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
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
            Part(PrimitiveType.Cylinder, "Log A", fuel, new Vector3(0f, .28f, 0f),
                new Vector3(.14f, .72f, .14f), DarkBark, new Vector3(0f, 0f, 90f));
            Part(PrimitiveType.Cylinder, "Log B", fuel, new Vector3(0f, .3f, 0f),
                new Vector3(.14f, .72f, .14f), DarkBark, new Vector3(90f, 0f, 0f));
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
            Save(root, WorkshopPath);
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
