using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Resources
{
    public sealed class ResourceInventoryState
    {
        private readonly ResourceAmount[] amounts;
        private readonly ResourceCategory[] acceptedCategories;

        public ResourceInventoryState(InventoryOwner owner, long capacity, IEnumerable<ResourceCategory> acceptedCategories,
            IEnumerable<ResourceAmount> amounts)
        {
            if (!owner.Id.IsValid) throw new ArgumentException("Owner required.", nameof(owner));
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Owner = owner;
            Capacity = capacity;
            AcceptsAllCategories = acceptedCategories == null;
            this.acceptedCategories = (acceptedCategories ?? Array.Empty<ResourceCategory>()).Distinct().OrderBy(x => x).ToArray();
            if (this.acceptedCategories.Any(x => !Enum.IsDefined(typeof(ResourceCategory), x)))
                throw new ArgumentOutOfRangeException(nameof(acceptedCategories));
            this.amounts = (amounts ?? throw new ArgumentNullException(nameof(amounts))).ToArray();
        }

        public InventoryOwner Owner { get; }
        public long Capacity { get; }
        public bool AcceptsAllCategories { get; }
        public IReadOnlyList<ResourceCategory> AcceptedCategories => Array.AsReadOnly(acceptedCategories);
        public IReadOnlyList<ResourceAmount> Amounts => Array.AsReadOnly(amounts);
    }

    public sealed class ResourceInventory
    {
        private readonly ResourceCatalog catalog;
        private readonly Dictionary<ResourceId, long> amounts = new Dictionary<ResourceId, long>();
        private readonly HashSet<ResourceCategory> acceptedCategories;
        private long totalUnits;

        public ResourceInventory(InventoryOwner owner, ResourceCatalog catalog, long capacity,
            IEnumerable<ResourceCategory> acceptedCategories = null)
        {
            if (!owner.Id.IsValid) throw new ArgumentException("Owner required.", nameof(owner));
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Owner = owner;
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Capacity = capacity;
            this.acceptedCategories = acceptedCategories == null ? null : new HashSet<ResourceCategory>(acceptedCategories);
            if (this.acceptedCategories != null && this.acceptedCategories.Any(x => !Enum.IsDefined(typeof(ResourceCategory), x)))
                throw new ArgumentOutOfRangeException(nameof(acceptedCategories));
        }

        public InventoryOwner Owner { get; }
        public long Capacity { get; }
        public long TotalUnits => totalUnits;
        public ResourceQuantity GetAmount(ResourceId id)
        {
            catalog.Get(id);
            return new ResourceQuantity(amounts.TryGetValue(id, out var count) ? count : 0);
        }

        public bool CanAdd(ResourceId id, ResourceQuantity quantity)
        {
            var definition = catalog.Get(id);
            if (!quantity.IsPositive) return false;
            if (acceptedCategories != null && !acceptedCategories.Contains(definition.Category)) return false;
            return quantity.Units <= Capacity - totalUnits &&
                (!amounts.TryGetValue(id, out var old) || quantity.Units <= long.MaxValue - old);
        }

        public bool CanRemove(ResourceId id, ResourceQuantity quantity)
        {
            catalog.Get(id);
            return quantity.IsPositive && amounts.TryGetValue(id, out var old) && old >= quantity.Units;
        }

        public bool TryAdd(ResourceId id, ResourceQuantity quantity)
        {
            if (!CanAdd(id, quantity)) return false;
            amounts.TryGetValue(id, out var old);
            amounts[id] = old + quantity.Units;
            totalUnits += quantity.Units;
            return true;
        }

        public bool TryRemove(ResourceId id, ResourceQuantity quantity)
        {
            if (!CanRemove(id, quantity)) return false;
            var remaining = amounts[id] - quantity.Units;
            if (remaining == 0) amounts.Remove(id);
            else amounts[id] = remaining;
            totalUnits -= quantity.Units;
            return true;
        }

        public ResourceInventoryState CaptureState() => new ResourceInventoryState(Owner, Capacity,
            acceptedCategories,
            amounts.OrderBy(x => x.Key).Select(x => new ResourceAmount(x.Key, new ResourceQuantity(x.Value))));

        public static ResourceInventory Restore(ResourceInventoryState state, ResourceCatalog catalog)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var inventory = new ResourceInventory(state.Owner, catalog, state.Capacity,
                state.AcceptsAllCategories ? null : state.AcceptedCategories);
            var seen = new HashSet<ResourceId>();
            foreach (var amount in state.Amounts)
                if (!seen.Add(amount.ResourceId) || !inventory.TryAdd(amount.ResourceId, amount.Quantity))
                    throw new ArgumentException("Inventory snapshot contains invalid, duplicate or over-capacity resource amounts.", nameof(state));
            return inventory;
        }
    }
}
