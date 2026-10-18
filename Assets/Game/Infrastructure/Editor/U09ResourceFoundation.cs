using System;
using System.IO;
using Game.Domain.Buildings;
using Game.Domain.Resources;
using Game.Presentation.Buildings;
using Game.Presentation.Resources;
using UnityEditor;
using UnityEngine;

namespace Game.Infrastructure.Editor
{
    public static class U09ResourceFoundation
    {
        public const string CatalogPath = "Assets/Game/Art/Config/ResourceCatalog.asset";

        [MenuItem("Serenity/U09/Build Resource Foundation")]
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
            var catalog = AssetDatabase.LoadAssetAtPath<ResourceCatalogAsset>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ResourceCatalogAsset>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Configure(new[]
            {
                new ResourceCatalogAsset.Entry().Configure("wood_log", "Wood Logs", ResourceCategory.Construction),
                new ResourceCatalogAsset.Entry().Configure("stone", "Stone", ResourceCategory.Construction),
                new ResourceCatalogAsset.Entry().Configure("plant_fiber", "Plant Fiber", ResourceCategory.Construction),
                new ResourceCatalogAsset.Entry().Configure("hide", "Hide", ResourceCategory.Other),
                new ResourceCatalogAsset.Entry().Configure("berries", "Berries", ResourceCategory.Food),
                new ResourceCatalogAsset.Entry().Configure("iron_ore", "Iron Ore", ResourceCategory.Construction),
                new ResourceCatalogAsset.Entry().Configure("gold_ore", "Gold Ore", ResourceCategory.Construction),
                new ResourceCatalogAsset.Entry().Configure("meat", "Meat", ResourceCategory.Food),
                new ResourceCatalogAsset.Entry().Configure("fish", "Fish", ResourceCategory.Food)
            });
            EditorUtility.SetDirty(catalog);
            U08BuildingFoundation.RefreshCatalogAndPrefabs();
            Validate();
            Debug.Log("U09 resource foundation generated successfully.");
        }

        public static void ApplyEconomy(BuildingPresentationCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            foreach (var entry in catalog.Entries)
            {
                if (entry.DefinitionId == "primitive_shelter")
                    entry.ConfigureEconomy(new[]
                    {
                        new BuildingPresentationCatalog.Entry.CostEntry().Configure("wood_log", 8),
                        new BuildingPresentationCatalog.Entry.CostEntry().Configure("plant_fiber", 3)
                    }); // BALANCE_TBD
                else if (entry.DefinitionId == "storage_basket")
                    entry.ConfigureEconomy(new[]
                    {
                        new BuildingPresentationCatalog.Entry.CostEntry().Configure("wood_log", 3),
                        new BuildingPresentationCatalog.Entry.CostEntry().Configure("plant_fiber", 5)
                    }, 40, new[] { ResourceCategory.Construction, ResourceCategory.Food, ResourceCategory.Other }); // BALANCE_TBD
                else if (entry.DefinitionId == "primitive_crop_plot")
                    entry.ConfigureEconomy(new[]
                    {
                        new BuildingPresentationCatalog.Entry.CostEntry().Configure("wood_log", 2),
                        new BuildingPresentationCatalog.Entry.CostEntry().Configure("plant_fiber", 2)
                    }); // BALANCE_TBD: U15 placement uses the U08 construction transaction.
                else if (entry.DefinitionId == "military_barracks")
                    entry.ConfigureEconomy(new[]
                    {
                        new BuildingPresentationCatalog.Entry.CostEntry().Configure("wood_log", 12),
                        new BuildingPresentationCatalog.Entry.CostEntry().Configure("stone", 6)
                    });
            }
        }

        public static void Validate()
        {
            var resourceAsset = AssetDatabase.LoadAssetAtPath<ResourceCatalogAsset>(CatalogPath);
            if (resourceAsset == null) throw new FileNotFoundException("U09 resource catalog missing.", CatalogPath);
            var resources = resourceAsset.CreateCatalog();
            var buildings = AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(U08BuildingFoundation.CatalogPath);
            if (buildings == null) throw new FileNotFoundException("U09 building catalog missing.");
            buildings.Validate();
            if (buildings.ResourceCatalog != resourceAsset)
                throw new InvalidDataException("Building catalog is not linked to the U09 resource catalog.");
            foreach (var entry in buildings.Entries)
            {
                var definition = entry.CreateDefinition();
                if (definition.ConstructionCost.Count == 0) throw new InvalidDataException("Building has no construction cost: " + definition.Id);
                foreach (var cost in definition.ConstructionCost) resources.Get(cost.ResourceId);
            }
            var basket = buildings.ResolveDefinition(new BuildingDefinitionId("storage_basket"));
            var shelter = buildings.ResolveDefinition(new BuildingDefinitionId("primitive_shelter"));
            if (basket.StorageCapacity != 40 || shelter.StorageCapacity != 0)
                throw new InvalidDataException("Storage capability differs from U09 authoring data.");
        }
    }
}
