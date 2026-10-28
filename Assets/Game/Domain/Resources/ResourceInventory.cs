using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Resources
{
    public sealed class ResourceInventoryState
    {
        private readonly ResourceAmount[] amounts;
        private readonly ResourceCategory[] acceptedCategories;
        private readonly ResourceId[] acceptedResources;

        public ResourceInventoryState(InventoryOwner owner, long capacity, IEnumerable<ResourceCategory> acceptedCategories,
            IEnumerable<ResourceAmount> amounts, IEnumerable<ResourceId> acceptedResources = null,
            bool issuanceLocked = false)
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
            AcceptsAllResources = acceptedResources == null;
            this.acceptedResources = (acceptedResources ?? Array.Empty<ResourceId>()).Distinct().OrderBy(x => x).ToArray();
            if (this.acceptedResources.Any(x => !x.IsValid))
                throw new ArgumentException("Accepted resource IDs must be valid.", nameof(acceptedResources));
            IssuanceLocked = issuanceLocked;
        }

        public InventoryOwner Owner { get; }
        public long Capacity { get; }
        public bool AcceptsAllCategories { get; }
        public IReadOnlyList<ResourceCategory> AcceptedCategories => Array.AsReadOnly(acceptedCategories);
        public bool AcceptsAllResources { get; }
        public IReadOnlyList<ResourceId> AcceptedResources => Array.AsReadOnly(acceptedResources);
        public bool IssuanceLocked { get; }
        public IReadOnlyList<ResourceAmount> Amounts => Array.AsReadOnly(amounts);
    }

    public sealed class ResourceInventory
    {
        private readonly ResourceCatalog catalog;
        private readonly Dictionary<ResourceId, long> amounts = new Dictionary<ResourceId, long>();
        private HashSet<ResourceCategory> acceptedCategories;
        private HashSet<ResourceId> acceptedResources;
        private long totalUnits;

        public ResourceInventory(InventoryOwner owner, ResourceCatalog catalog, long capacity,
            IEnumerable<ResourceCategory> acceptedCategories = null,
            IEnumerable<ResourceId> acceptedResources = null, bool issuanceLocked = false)
        {
            if (!owner.Id.IsValid) throw new ArgumentException("Owner required.", nameof(owner));
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Owner = owner;
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Capacity = capacity;
            this.acceptedCategories = acceptedCategories == null ? null : new HashSet<ResourceCategory>(acceptedCategories);
            if (this.acceptedCategories != null && this.acceptedCategories.Any(x => !Enum.IsDefined(typeof(ResourceCategory), x)))
                throw new ArgumentOutOfRangeException(nameof(acceptedCategories));
            this.acceptedResources = acceptedResources == null ? null : new HashSet<ResourceId>(acceptedResources);
            if (this.acceptedResources != null)
                foreach (var resourceId in this.acceptedResources) catalog.Get(resourceId);
            IssuanceLocked = issuanceLocked;
        }

        public InventoryOwner Owner { get; }
        public long Capacity { get; private set; }
        public long TotalUnits => totalUnits;
        public bool AcceptsAllCategories => acceptedCategories == null;
        public bool AcceptsAllResources => acceptedResources == null;
        public IReadOnlyCollection<ResourceCategory> AcceptedCategories => acceptedCategories == null
            ? Array.Empty<ResourceCategory>() : acceptedCategories.OrderBy(x => x).ToArray();
        public IReadOnlyCollection<ResourceId> AcceptedResources => acceptedResources == null
            ? Array.Empty<ResourceId>() : acceptedResources.OrderBy(x => x).ToArray();
        public bool IssuanceLocked { get; private set; }

        public bool Accepts(ResourceId id)
        {
            var definition = catalog.Get(id);
            return (acceptedCategories == null || acceptedCategories.Contains(definition.Category)) &&
                (acceptedResources == null || acceptedResources.Contains(id));
        }

        public void ConfigureAcceptance(IEnumerable<ResourceCategory> categories,
            IEnumerable<ResourceId> resources)
        {
            var categorySet = categories == null ? null : new HashSet<ResourceCategory>(categories);
            if (categorySet != null && categorySet.Any(x => !Enum.IsDefined(typeof(ResourceCategory), x)))
                throw new ArgumentOutOfRangeException(nameof(categories));
            var resourceSet = resources == null ? null : new HashSet<ResourceId>(resources);
            if (resourceSet != null)
                foreach (var resourceId in resourceSet) catalog.Get(resourceId);
            acceptedCategories = categorySet;
            acceptedResources = resourceSet;
        }

        public void SetIssuanceLocked(bool locked) => IssuanceLocked = locked;

        public void EnsureCapacity(long minimumCapacity)
        {
            if (minimumCapacity < 0) throw new ArgumentOutOfRangeException(nameof(minimumCapacity));
            if (minimumCapacity > Capacity) Capacity = minimumCapacity;
        }

        public void RestoreState(ResourceInventoryState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Owner != Owner) throw new ArgumentException("Inventory owner cannot change.", nameof(state));
            var restored = Restore(state, catalog);
            Capacity = restored.Capacity;
            acceptedCategories = restored.acceptedCategories == null
                ? null : new HashSet<ResourceCategory>(restored.acceptedCategories);
            acceptedResources = restored.acceptedResources == null
                ? null : new HashSet<ResourceId>(restored.acceptedResources);
            IssuanceLocked = restored.IssuanceLocked;
            amounts.Clear();
            foreach (var amount in restored.amounts) amounts.Add(amount.Key, amount.Value);
            totalUnits = restored.totalUnits;
        }
        public ResourceQuantity GetAmount(ResourceId id)
        {
            catalog.Get(id);
            return new ResourceQuantity(amounts.TryGetValue(id, out var count) ? count : 0);
        }

        public bool CanAdd(ResourceId id, ResourceQuantity quantity)
        {
            var definition = catalog.Get(id);
            if (!quantity.IsPositive) return false;
            if (!Accepts(id)) return false;
            return quantity.Units <= Capacity - totalUnits &&
                (!amounts.TryGetValue(id, out var old) || quantity.Units <= long.MaxValue - old);
        }

        public bool CanRemove(ResourceId id, ResourceQuantity quantity, bool ignoreIssuanceLock = false)
        {
            catalog.Get(id);
            return (ignoreIssuanceLock || !IssuanceLocked) && quantity.IsPositive &&
                amounts.TryGetValue(id, out var old) && old >= quantity.Units;
        }

        public bool TryAdd(ResourceId id, ResourceQuantity quantity, bool ignoreAcceptance = false)
        {
            if (ignoreAcceptance)
            {
                catalog.Get(id);
                if (!quantity.IsPositive || quantity.Units > Capacity - totalUnits ||
                    amounts.TryGetValue(id, out var current) && quantity.Units > long.MaxValue - current) return false;
            }
            else if (!CanAdd(id, quantity)) return false;
            amounts.TryGetValue(id, out var old);
            amounts[id] = old + quantity.Units;
            totalUnits += quantity.Units;
            return true;
        }

        public bool TryRemove(ResourceId id, ResourceQuantity quantity, bool ignoreIssuanceLock = false)
        {
            if (!CanRemove(id, quantity, ignoreIssuanceLock)) return false;
            var remaining = amounts[id] - quantity.Units;
            if (remaining == 0) amounts.Remove(id);
            else amounts[id] = remaining;
            totalUnits -= quantity.Units;
            return true;
        }

        public ResourceInventoryState CaptureState() => new ResourceInventoryState(Owner, Capacity,
            acceptedCategories,
            amounts.OrderBy(x => x.Key).Select(x => new ResourceAmount(x.Key, new ResourceQuantity(x.Value))),
            acceptedResources, IssuanceLocked);

        public static ResourceInventory Restore(ResourceInventoryState state, ResourceCatalog catalog)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var inventory = new ResourceInventory(state.Owner, catalog, state.Capacity,
                state.AcceptsAllCategories ? null : state.AcceptedCategories,
                state.AcceptsAllResources ? null : state.AcceptedResources, state.IssuanceLocked);
            var seen = new HashSet<ResourceId>();
            foreach (var amount in state.Amounts)
            {
                catalog.Get(amount.ResourceId);
                if (!amount.Quantity.IsPositive || !seen.Add(amount.ResourceId) ||
                    amount.Quantity.Units > state.Capacity - inventory.totalUnits)
                    throw new ArgumentException("Inventory snapshot contains invalid, duplicate or over-capacity resource amounts.", nameof(state));
                inventory.amounts.Add(amount.ResourceId, amount.Quantity.Units);
                inventory.totalUnits += amount.Quantity.Units;
            }
            return inventory;
        }
    }
}
