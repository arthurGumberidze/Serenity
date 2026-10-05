using System;
using Game.Domain.Resources;
using UnityEngine;

namespace Game.Presentation.Resources
{
    public readonly struct ResourceLocationTotals
    {
        public ResourceLocationTotals(long worldPiles, long characters, long storage)
        {
            WorldPiles = worldPiles;
            Characters = characters;
            Storage = storage;
        }

        public long WorldPiles { get; }
        public long Characters { get; }
        public long Storage { get; }
        public long Total => checked(WorldPiles + Characters + Storage);
    }

    // Development-only read model. It owns no resource quantity and performs no mutations.
    public sealed class ResourceDebugOverlay : MonoBehaviour
    {
        private ResourceInventoryRegistry inventories;
        private ResourceCatalog resources;

        public void Initialize(ResourceInventoryRegistry registry, ResourceCatalog catalog)
        {
            inventories = registry ?? throw new ArgumentNullException(nameof(registry));
            resources = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public ResourceLocationTotals GetCanonicalTotals(ResourceId id)
        {
            if (inventories == null || resources == null) throw new InvalidOperationException("Overlay is not initialized.");
            resources.Get(id);
            long piles = 0;
            long characters = 0;
            long storage = 0;
            foreach (var inventory in inventories.All)
            {
                var units = inventory.GetAmount(id).Units;
                switch (inventory.Owner.Kind)
                {
                    case InventoryOwnerKind.WorldPile: piles = checked(piles + units); break;
                    case InventoryOwnerKind.Character: characters = checked(characters + units); break;
                    case InventoryOwnerKind.BuildingStorage: storage = checked(storage + units); break;
                }
            }
            return new ResourceLocationTotals(piles, characters, storage);
        }

        private void OnGUI()
        {
            if (inventories == null || resources == null || (!Application.isEditor && !Debug.isDebugBuild)) return;
            GUILayout.BeginArea(new Rect(12, 12, 430, 48 + resources.All.Count * 22), GUI.skin.box);
            GUILayout.Label("Canonical resources: pile / NPC / storage = total");
            foreach (var definition in resources.All)
            {
                var totals = GetCanonicalTotals(definition.Id);
                GUILayout.Label($"{definition.DisplayName}: {totals.WorldPiles} / {totals.Characters} / " +
                    $"{totals.Storage} = {totals.Total}");
            }
            GUILayout.EndArea();
        }
    }
}
