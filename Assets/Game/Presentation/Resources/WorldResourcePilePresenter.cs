using System;
using Game.Domain;
using Game.Domain.Resources;
using UnityEngine;

namespace Game.Presentation.Resources
{
    public sealed class WorldResourcePilePresenter : MonoBehaviour
    {
        private WorldResourcePile pile;
        private ResourceInventory inventory;

        public StableEntityId PileId => pile != null ? pile.Id : throw new InvalidOperationException("Pile view is not bound.");
        public ResourceId ResourceId => pile != null ? pile.ResourceId : throw new InvalidOperationException("Pile view is not bound.");
        public ResourceQuantity Quantity => inventory != null ? inventory.GetAmount(ResourceId) : throw new InvalidOperationException("Pile view is not bound.");

        public void Bind(WorldResourcePile source, ResourceInventory canonicalInventory)
        {
            pile = source ?? throw new ArgumentNullException(nameof(source));
            inventory = canonicalInventory ?? throw new ArgumentNullException(nameof(canonicalInventory));
            if (inventory.Owner != source.InventoryOwner) throw new ArgumentException("Inventory owner mismatch.", nameof(canonicalInventory));
            name = $"World Pile {ResourceId} [{PileId}]";
        }

        private void OnDestroy() { pile = null; inventory = null; }
    }
}
