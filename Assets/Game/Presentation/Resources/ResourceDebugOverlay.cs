using System;
using Game.Domain.Resources;
using Game.Presentation.Interaction;
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
    public sealed class ResourceDebugOverlay : MonoBehaviour, IWorldPointerUiBlocker
    {
        private static readonly Rect BasePanelRect = new Rect(12f, 12f, 430f, 48f);
        private ResourceInventoryRegistry inventories;
        private ResourceCatalog resources;
        private WorldPointerRaycaster raycaster;

        public void Initialize(ResourceInventoryRegistry registry, ResourceCatalog catalog,
            WorldPointerRaycaster worldRaycaster)
        {
            inventories = registry ?? throw new ArgumentNullException(nameof(registry));
            resources = catalog ?? throw new ArgumentNullException(nameof(catalog));
            raycaster?.UnregisterUiBlocker(this);
            raycaster = worldRaycaster ?? throw new ArgumentNullException(nameof(worldRaycaster));
            raycaster.RegisterUiBlocker(this);
        }

        private void OnEnable() => raycaster?.RegisterUiBlocker(this);
        private void OnDisable() => raycaster?.UnregisterUiBlocker(this);

        public bool TryGetUiBlockingRect(out Rect guiRect)
        {
            guiRect = new Rect(BasePanelRect.x, BasePanelRect.y, BasePanelRect.width,
                BasePanelRect.height + (resources?.All.Count ?? 0) * 22f);
            return inventories != null && resources != null && (Application.isEditor || Debug.isDebugBuild);
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
            TryGetUiBlockingRect(out var panelRect);
            GUILayout.BeginArea(panelRect, GUI.skin.box);
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
