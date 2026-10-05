using Game.Domain.Resources;
using Game.Presentation.Interaction;
using Game.Simulation.AI;
using UnityEngine;

namespace Game.Presentation.AI
{
    public sealed class Tier1AiDebugOverlay : MonoBehaviour
    {
        private SelectionProbe selection;
        private Game.Domain.AI.Tier1AiAgentRegistry agents;
        private ResourceInventoryRegistry inventories;

        public void Initialize(SelectionProbe selectionProbe, Game.Domain.AI.Tier1AiAgentRegistry agentRegistry,
            ResourceInventoryRegistry inventoryRegistry)
        {
            selection = selectionProbe;
            agents = agentRegistry;
            inventories = inventoryRegistry;
        }

        private void OnGUI()
        {
            if (!Debug.isDebugBuild || selection == null || agents == null || !selection.SelectedCharacterId.HasValue) return;
            var id = selection.SelectedCharacterId.Value;
            if (!agents.TryGet(id, out var agent)) return;
            var inventory = inventories.Get(new InventoryOwner(InventoryOwnerKind.Character, id));
            GUI.Box(new Rect(12f, 158f, 360f, 116f), "Tier 1 Utility AI");
            GUI.Label(new Rect(24f, 182f, 336f, 22f), "ID: " + id);
            GUI.Label(new Rect(24f, 204f, 336f, 22f), $"Action: {agent.CurrentAction} / {agent.Phase}");
            GUI.Label(new Rect(24f, 226f, 336f, 22f), $"Hunger: {agent.Needs.Hunger:0.00}  Energy: {agent.Needs.Energy:0.00}");
            GUI.Label(new Rect(24f, 248f, 336f, 22f), $"Inventory: {inventory.TotalUnits}/{inventory.Capacity}");
        }
    }
}
