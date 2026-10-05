using System;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Resources;
using Game.Simulation.Resources;

namespace Game.Simulation.AI
{
    public readonly struct HaulJobCandidate
    {
        public HaulJobCandidate(InventoryOwner source, InventoryOwner destination, ResourceId resourceId,
            ResourceQuantity quantity, WorldPosition sourcePosition, WorldPosition destinationPosition)
        {
            Source = source;
            Destination = destination;
            ResourceId = resourceId;
            Quantity = quantity;
            SourcePosition = sourcePosition;
            DestinationPosition = destinationPosition;
        }

        public InventoryOwner Source { get; }
        public InventoryOwner Destination { get; }
        public ResourceId ResourceId { get; }
        public ResourceQuantity Quantity { get; }
        public WorldPosition SourcePosition { get; }
        public WorldPosition DestinationPosition { get; }
    }

    public sealed class HaulWorldQuery
    {
        private readonly ResourceInventoryRegistry inventories;
        private readonly WorldPileService piles;
        private readonly BuildingRegistry buildings;
        private readonly HaulClaimRegistry claims;
        private readonly float gridSize;
        private readonly WorldPosition gridOrigin;

        public HaulWorldQuery(ResourceInventoryRegistry inventories, WorldPileService piles, BuildingRegistry buildings,
            HaulClaimRegistry claims, float gridSize = 1f, WorldPosition gridOrigin = default)
        {
            this.inventories = inventories ?? throw new ArgumentNullException(nameof(inventories));
            this.piles = piles ?? throw new ArgumentNullException(nameof(piles));
            this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
            if (gridSize <= 0f || float.IsNaN(gridSize) || float.IsInfinity(gridSize))
                throw new ArgumentOutOfRangeException(nameof(gridSize));
            this.gridSize = gridSize;
            this.gridOrigin = gridOrigin;
        }

        public bool TryFindJob(Tier1AiAgentState agent, long maxUnits, out HaulJobCandidate candidate)
        {
            if (agent == null) throw new ArgumentNullException(nameof(agent));
            candidate = default;
            if (maxUnits <= 0) return false;
            var characterOwner = new InventoryOwner(InventoryOwnerKind.Character, agent.CharacterId);
            if (!inventories.TryGet(characterOwner, out var characterInventory)) return false;
            var characterCapacity = characterInventory.Capacity - characterInventory.TotalUnits;
            if (characterCapacity <= 0) return false;

            var found = false;
            double bestSourceDistance = 0d;
            double bestDestinationDistance = 0d;
            foreach (var pile in piles.All)
            {
                if (!inventories.TryGet(pile.InventoryOwner, out var sourceInventory)) continue;
                var availableAtSource = sourceInventory.GetAmount(pile.ResourceId).Units -
                    claims.ReservedAtSource(pile.InventoryOwner, pile.ResourceId);
                if (availableAtSource <= 0) continue;
                var sourcePosition = ToWorld(pile.Coordinate);

                foreach (var destinationInventory in inventories.All)
                {
                    if (destinationInventory.Owner.Kind != InventoryOwnerKind.BuildingStorage) continue;
                    if (!buildings.TryGet(destinationInventory.Owner.Id, out var building) ||
                        building.ConstructionState != ConstructionState.Completed) continue;
                    var rawDestinationCapacity = destinationInventory.Capacity - destinationInventory.TotalUnits;
                    var destinationCapacity = rawDestinationCapacity - claims.ReservedAtDestination(destinationInventory.Owner);
                    if (destinationCapacity <= 0 || !destinationInventory.CanAdd(pile.ResourceId, new ResourceQuantity(1))) continue;
                    var units = Math.Min(maxUnits, Math.Min(characterCapacity, Math.Min(availableAtSource, destinationCapacity)));
                    if (units <= 0) continue;
                    var destinationPosition = ToWorld(building.Coordinate);
                    var sourceDistance = agent.Position.DistanceSquared(sourcePosition);
                    var destinationDistance = sourcePosition.DistanceSquared(destinationPosition);
                    if (!found || IsBetter(sourceDistance, destinationDistance, pile.InventoryOwner,
                        destinationInventory.Owner, bestSourceDistance, bestDestinationDistance,
                        candidate.Source, candidate.Destination))
                    {
                        found = true;
                        bestSourceDistance = sourceDistance;
                        bestDestinationDistance = destinationDistance;
                        candidate = new HaulJobCandidate(pile.InventoryOwner, destinationInventory.Owner, pile.ResourceId,
                            new ResourceQuantity(units), sourcePosition, destinationPosition);
                    }
                }
            }
            return found;
        }

        public bool TryClaim(Tier1AiAgentState agent, long maxUnits, out HaulJobCandidate candidate, out HaulClaim claim)
        {
            claim = null;
            if (!TryFindJob(agent, maxUnits, out candidate)) return false;
            var source = inventories.Get(candidate.Source);
            var destination = inventories.Get(candidate.Destination);
            return claims.TryClaim(agent.CharacterId, candidate.Source, candidate.Destination, candidate.ResourceId,
                candidate.Quantity, source.GetAmount(candidate.ResourceId).Units,
                destination.Capacity - destination.TotalUnits, out claim);
        }

        public bool TryClaimSpecified(Tier1AiAgentState agent, InventoryOwner sourceOwner,
            InventoryOwner destinationOwner, ResourceId resourceId, ResourceQuantity quantity,
            out HaulJobCandidate candidate, out HaulClaim claim)
        {
            if (agent == null) throw new ArgumentNullException(nameof(agent));
            candidate = default;
            claim = null;
            if (sourceOwner.Kind != InventoryOwnerKind.WorldPile ||
                destinationOwner.Kind != InventoryOwnerKind.BuildingStorage ||
                !resourceId.IsValid || !quantity.IsPositive) return false;
            if (!piles.TryGet(sourceOwner.Id, out var pile) || pile.ResourceId != resourceId ||
                !buildings.TryGet(destinationOwner.Id, out var building) ||
                building.ConstructionState != ConstructionState.Completed ||
                !inventories.TryGet(sourceOwner, out var source) ||
                !inventories.TryGet(destinationOwner, out var destination)) return false;
            var characterOwner = new InventoryOwner(InventoryOwnerKind.Character, agent.CharacterId);
            if (!inventories.TryGet(characterOwner, out var characterInventory) ||
                characterInventory.Capacity - characterInventory.TotalUnits < quantity.Units ||
                !source.CanRemove(resourceId, quantity) || !destination.CanAdd(resourceId, quantity)) return false;
            if (!claims.TryClaim(agent.CharacterId, sourceOwner, destinationOwner, resourceId, quantity,
                    source.GetAmount(resourceId).Units, destination.Capacity - destination.TotalUnits, out claim))
                return false;
            candidate = new HaulJobCandidate(sourceOwner, destinationOwner, resourceId, quantity,
                ToWorld(pile.Coordinate), ToWorld(building.Coordinate));
            return true;
        }

        public bool TryFindDestinationForCarried(Tier1AiAgentState agent, ResourceId resourceId, long units,
            HaulClaim existingClaim, out InventoryOwner destination, out WorldPosition position)
        {
            destination = default;
            position = default;
            var found = false;
            var bestDistance = 0d;
            foreach (var inventory in inventories.All)
            {
                if (inventory.Owner.Kind != InventoryOwnerKind.BuildingStorage) continue;
                if (!buildings.TryGet(inventory.Owner.Id, out var building) || building.ConstructionState != ConstructionState.Completed)
                    continue;
                var reserved = claims.ReservedAtDestination(inventory.Owner);
                if (existingClaim != null && existingClaim.Destination == inventory.Owner) reserved -= existingClaim.Quantity.Units;
                var capacity = inventory.Capacity - inventory.TotalUnits - reserved;
                if (capacity < units || !inventory.CanAdd(resourceId, new ResourceQuantity(units))) continue;
                var candidatePosition = ToWorld(building.Coordinate);
                var distance = agent.Position.DistanceSquared(candidatePosition);
                if (!found || distance < bestDistance ||
                    (distance.Equals(bestDistance) && string.CompareOrdinal(inventory.Owner.Id.ToString(), destination.Id.ToString()) < 0))
                {
                    found = true;
                    bestDistance = distance;
                    destination = inventory.Owner;
                    position = candidatePosition;
                }
            }
            return found;
        }

        public bool InventoryExists(InventoryOwner owner, out ResourceInventory inventory) => inventories.TryGet(owner, out inventory);
        public bool BuildingStorageExists(InventoryOwner owner) => owner.Kind == InventoryOwnerKind.BuildingStorage &&
            inventories.TryGet(owner, out _) && buildings.TryGet(owner.Id, out var building) &&
            building.ConstructionState == ConstructionState.Completed;

        private WorldPosition ToWorld(GridCoordinate coordinate) => new WorldPosition(
            gridOrigin.X + coordinate.X * gridSize,
            gridOrigin.Y + coordinate.Level * gridSize,
            gridOrigin.Z + coordinate.Z * gridSize);

        private static bool IsBetter(double sourceDistance, double destinationDistance, InventoryOwner source,
            InventoryOwner destination, double bestSourceDistance, double bestDestinationDistance,
            InventoryOwner bestSource, InventoryOwner bestDestination)
        {
            if (sourceDistance < bestSourceDistance) return true;
            if (sourceDistance > bestSourceDistance) return false;
            if (destinationDistance < bestDestinationDistance) return true;
            if (destinationDistance > bestDestinationDistance) return false;
            var sourceOrder = string.CompareOrdinal(source.Id.ToString(), bestSource.Id.ToString());
            if (sourceOrder != 0) return sourceOrder < 0;
            return string.CompareOrdinal(destination.Id.ToString(), bestDestination.Id.ToString()) < 0;
        }
    }

    public sealed class Tier1AiWorld
    {
        public Tier1AiWorld(HaulWorldQuery haulJobs, HaulClaimRegistry claims, ResourceTransferService transfers)
        {
            HaulJobs = haulJobs ?? throw new ArgumentNullException(nameof(haulJobs));
            Claims = claims ?? throw new ArgumentNullException(nameof(claims));
            Transfers = transfers ?? throw new ArgumentNullException(nameof(transfers));
        }
        public HaulWorldQuery HaulJobs { get; }
        public HaulClaimRegistry Claims { get; }
        public ResourceTransferService Transfers { get; }
    }
}
