using System;
using System.Collections.Generic;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Resources;
using Game.Domain.Characters;
using Game.Presentation.Interaction;
using Game.Simulation.AI;
using Game.Simulation.Time;
using Game.Simulation.Tiers;
using UnityEngine;

namespace Game.Presentation.AI
{
    public enum DevelopmentSelectionKind
    {
        WorldResourcePile = 0,
        BuildingStorage = 1,
        Building = 2,
        Character = 3
    }

    public readonly struct DebugResourceAmount
    {
        public DebugResourceAmount(ResourceId resourceId, string displayName, long quantity)
        {
            ResourceId = resourceId;
            DisplayName = displayName;
            Quantity = quantity;
        }

        public ResourceId ResourceId { get; }
        public string DisplayName { get; }
        public long Quantity { get; }
    }

    // Ephemeral read model captured from canonical state on demand. It is never retained or mutated by the UI.
    public sealed class DevelopmentSelectionSnapshot
    {
        internal DevelopmentSelectionSnapshot(DevelopmentSelectionKind kind, StableEntityId stableId,
            DebugResourceAmount[] resources, long capacity, long occupied, UtilityActionKind? action,
            AiActionPhase? phase, double? hunger, double? energy, HaulClaim[] activeClaims, InventoryOwner? target,
            InventoryOwner? source, InventoryOwner? destination)
        {
            Kind = kind;
            StableId = stableId;
            Resources = resources ?? Array.Empty<DebugResourceAmount>();
            Capacity = capacity;
            Occupied = occupied;
            CurrentAction = action;
            ActionPhase = phase;
            Hunger = hunger;
            Energy = energy;
            ActiveClaims = activeClaims ?? Array.Empty<HaulClaim>();
            Target = target;
            Source = source;
            Destination = destination;
        }

        internal DevelopmentSelectionSnapshot WithSimulation(CharacterSimulationTier tier, long lastCalendarTick,
            long lastBiologicalTick, long pendingCalendarTicks, long abstractActivityProgress,
            ulong deterministicStateHash, long worldSeed)
        {
            SimulationTier = tier;
            LastSimulationCalendarTick = lastCalendarTick;
            LastSimulationBiologicalTick = lastBiologicalTick;
            PendingCalendarTicks = pendingCalendarTicks;
            AbstractActivityProgress = abstractActivityProgress;
            DeterministicStateHash = deterministicStateHash;
            WorldSeed = worldSeed;
            return this;
        }

        public DevelopmentSelectionKind Kind { get; }
        public StableEntityId StableId { get; }
        public IReadOnlyList<DebugResourceAmount> Resources { get; }
        public long Capacity { get; }
        public long Occupied { get; }
        public long FreeCapacity => Capacity - Occupied;
        public UtilityActionKind? CurrentAction { get; }
        public AiActionPhase? ActionPhase { get; }
        public double? Hunger { get; }
        public double? Energy { get; }
        public IReadOnlyList<HaulClaim> ActiveClaims { get; }
        public InventoryOwner? Target { get; }
        public InventoryOwner? Source { get; }
        public InventoryOwner? Destination { get; }
        public CharacterSimulationTier? SimulationTier { get; private set; }
        public long? LastSimulationCalendarTick { get; private set; }
        public long? LastSimulationBiologicalTick { get; private set; }
        public long? PendingCalendarTicks { get; private set; }
        public long? AbstractActivityProgress { get; private set; }
        public ulong? DeterministicStateHash { get; private set; }
        public long? WorldSeed { get; private set; }
    }

    public sealed class Tier1AiDebugOverlay : MonoBehaviour, IWorldPointerUiBlocker
    {
        private SelectionProbe selection;
        private Tier1AiAgentRegistry agents;
        private ResourceInventoryRegistry inventories;
        private ResourceCatalog resources;
        private HaulClaimRegistry claims;
        private WorldPointerRaycaster raycaster;
        private TierManager tiers;
        private GameClock clock;
        private OffCameraSimulationService offCamera;

        public void Initialize(SelectionProbe selectionProbe, Tier1AiAgentRegistry agentRegistry,
            ResourceInventoryRegistry inventoryRegistry, ResourceCatalog resourceCatalog,
            HaulClaimRegistry claimRegistry, WorldPointerRaycaster worldRaycaster,
            TierManager tierManager = null, GameClock gameClock = null,
            OffCameraSimulationService offCameraSimulation = null)
        {
            selection = selectionProbe ?? throw new ArgumentNullException(nameof(selectionProbe));
            agents = agentRegistry ?? throw new ArgumentNullException(nameof(agentRegistry));
            inventories = inventoryRegistry ?? throw new ArgumentNullException(nameof(inventoryRegistry));
            resources = resourceCatalog ?? throw new ArgumentNullException(nameof(resourceCatalog));
            claims = claimRegistry ?? throw new ArgumentNullException(nameof(claimRegistry));
            raycaster?.UnregisterUiBlocker(this);
            raycaster = worldRaycaster ?? throw new ArgumentNullException(nameof(worldRaycaster));
            tiers = tierManager;
            clock = gameClock;
            offCamera = offCameraSimulation;
            raycaster.RegisterUiBlocker(this);
        }

        private void OnEnable() => raycaster?.RegisterUiBlocker(this);
        private void OnDisable() => raycaster?.UnregisterUiBlocker(this);

        public bool TryGetUiBlockingRect(out Rect guiRect)
        {
            guiRect = default;
            if ((!Application.isEditor && !Debug.isDebugBuild) || !TryCaptureSelected(out var snapshot)) return false;
            var height = 150f + snapshot.Resources.Count * 20f + snapshot.ActiveClaims.Count * 42f;
            guiRect = new Rect(12f, 160f, 620f, height);
            return true;
        }

        public bool TryCaptureSelected(out DevelopmentSelectionSnapshot snapshot)
        {
            snapshot = null;
            if (selection == null || inventories == null || resources == null || claims == null) return false;

            var pile = selection.SelectedWorldResourcePilePresenter;
            if (pile != null)
            {
                var owner = new InventoryOwner(InventoryOwnerKind.WorldPile, pile.PileId);
                if (!inventories.TryGet(owner, out var inventory)) return false;
                var definition = resources.Get(pile.ResourceId);
                snapshot = new DevelopmentSelectionSnapshot(DevelopmentSelectionKind.WorldResourcePile, pile.PileId,
                    new[] { new DebugResourceAmount(pile.ResourceId, definition.DisplayName,
                        inventory.GetAmount(pile.ResourceId).Units) }, inventory.Capacity, inventory.TotalUnits,
                    null, null, null, null, claims.CaptureForSource(owner), null, owner, null);
                return true;
            }

            var building = selection.SelectedBuildingPresenter;
            if (building != null && building.IsBound)
            {
                var inventory = building.StorageInventory;
                snapshot = new DevelopmentSelectionSnapshot(
                    inventory != null ? DevelopmentSelectionKind.BuildingStorage : DevelopmentSelectionKind.Building,
                    building.BuildingId, CaptureContents(inventory), inventory?.Capacity ?? 0,
                    inventory?.TotalUnits ?? 0, null, null, null, null, Array.Empty<HaulClaim>(), null, null,
                    inventory?.Owner);
                return true;
            }

            var character = selection.SelectedCharacterPresenter;
            if (character == null || !character.IsBound) return false;
            var characterId = character.CharacterId;
            inventories.TryGet(new InventoryOwner(InventoryOwnerKind.Character, characterId), out var characterInventory);
            agents.TryGet(characterId, out var agent);
            HaulClaim activeClaim = null;
            var activeClaims = claims.TryGetForClaimant(characterId, out activeClaim)
                ? new[] { activeClaim }
                : Array.Empty<HaulClaim>();
            InventoryOwner? target = null;
            InventoryOwner? source = null;
            InventoryOwner? destination = null;
            if (activeClaim != null)
            {
                source = activeClaim.Source;
                destination = activeClaim.Destination;
                target = activeClaim.Phase == HaulClaimPhase.Carrying ||
                    agent != null && (agent.Phase == AiActionPhase.MoveToDestination || agent.Phase == AiActionPhase.Dropoff)
                        ? activeClaim.Destination
                        : activeClaim.Source;
            }
            snapshot = new DevelopmentSelectionSnapshot(DevelopmentSelectionKind.Character, characterId,
                CaptureContents(characterInventory), characterInventory?.Capacity ?? 0,
                characterInventory?.TotalUnits ?? 0, agent?.CurrentAction, agent?.Phase,
                agent?.Needs.Hunger, agent?.Needs.Energy, activeClaims,
                target, source, destination);
            if (tiers != null && clock != null && offCamera != null)
            {
                var runtime = tiers.GetRuntimeState(characterId);
                snapshot.WithSimulation(tiers.GetTier(characterId), runtime.LastSimulationCalendarTick,
                    runtime.LastSimulationBiologicalTick,
                    Math.Max(0L, clock.State.CalendarTicks - runtime.LastSimulationCalendarTick),
                    runtime.AbstractActivityProgress, offCamera.ComputeStateHash(new[] { runtime }),
                    offCamera.WorldSeed);
            }
            return true;
        }

        private DebugResourceAmount[] CaptureContents(ResourceInventory inventory)
        {
            if (inventory == null) return Array.Empty<DebugResourceAmount>();
            var result = new DebugResourceAmount[resources.All.Count];
            for (var i = 0; i < result.Length; i++)
            {
                var definition = resources.All[i];
                result[i] = new DebugResourceAmount(definition.Id, definition.DisplayName,
                    inventory.GetAmount(definition.Id).Units);
            }
            return result;
        }

        private void OnGUI()
        {
            if ((!Application.isEditor && !Debug.isDebugBuild) || !TryCaptureSelected(out var snapshot)) return;
            TryGetUiBlockingRect(out var panelRect);
            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.Label(snapshot.SimulationTier.HasValue
                ? $"Canonical selection — {snapshot.Kind} | {snapshot.SimulationTier} | seed {snapshot.WorldSeed} | " +
                    $"last {snapshot.LastSimulationCalendarTick}/{snapshot.LastSimulationBiologicalTick} | " +
                    $"pending {snapshot.PendingCalendarTicks}"
                : "Canonical selection debug — " + snapshot.Kind);
            GUILayout.Label(snapshot.SimulationTier.HasValue
                ? $"StableEntityId: {snapshot.StableId} | abstract {snapshot.AbstractActivityProgress} | " +
                    $"hash {snapshot.DeterministicStateHash:X16}"
                : "StableEntityId: " + snapshot.StableId);
            if (snapshot.CurrentAction.HasValue)
                GUILayout.Label($"AI: {snapshot.CurrentAction} / {snapshot.ActionPhase}");
            if (snapshot.Hunger.HasValue)
                GUILayout.Label($"Hunger: {snapshot.Hunger:0.00}  Energy: {snapshot.Energy:0.00}");
            if (snapshot.Kind == DevelopmentSelectionKind.BuildingStorage || snapshot.Kind == DevelopmentSelectionKind.Character)
                GUILayout.Label($"Capacity: {snapshot.Capacity}  occupied: {snapshot.Occupied}  free: {snapshot.FreeCapacity}");
            foreach (var amount in snapshot.Resources)
                GUILayout.Label($"{amount.ResourceId} / {amount.DisplayName}: {amount.Quantity}");
            if (snapshot.Source.HasValue) GUILayout.Label("Source: " + FormatOwner(snapshot.Source.Value));
            if (snapshot.Destination.HasValue) GUILayout.Label("Destination: " + FormatOwner(snapshot.Destination.Value));
            if (snapshot.Target.HasValue) GUILayout.Label("Current target: " + FormatOwner(snapshot.Target.Value));
            if (snapshot.ActiveClaims.Count == 0)
                GUILayout.Label("Active claim/job: none");
            else
                foreach (var claim in snapshot.ActiveClaims)
                {
                    GUILayout.Label($"Claim #{claim.Id}: {claim.Phase}, {claim.ResourceId} x{claim.Quantity.Units}");
                    GUILayout.Label("Claimant CharacterId: " + claim.Claimant);
                }
            GUILayout.EndArea();
        }

        private static string FormatOwner(InventoryOwner owner) => $"{owner.Kind} [{owner.Id}]";
    }
}
