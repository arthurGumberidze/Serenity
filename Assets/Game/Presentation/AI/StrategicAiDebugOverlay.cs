using System;
using System.Linq;
using Game.Domain.Resources;
using Game.Presentation.Interaction;
using Game.Simulation.AI;
using UnityEngine;

namespace Game.Presentation.AI
{
    /// <summary>Disposable U26 inspection surface. All commands remain canonical service calls.</summary>
    public sealed class StrategicAiDebugOverlay : MonoBehaviour, IWorldPointerUiBlocker
    {
        private AiStrategicScheduler scheduler;
        private WorldPointerRaycaster pointer;
        private Action advance;
        private Rect panel = new Rect(8, 590, 360, 330);

        public void Initialize(AiStrategicScheduler strategicScheduler, WorldPointerRaycaster raycaster,
            Action advanceCommand)
        {
            scheduler = strategicScheduler ?? throw new ArgumentNullException(nameof(strategicScheduler));
            pointer?.UnregisterUiBlocker(this); pointer = raycaster; advance = advanceCommand;
            pointer?.RegisterUiBlocker(this);
        }

        private void OnEnable() => pointer?.RegisterUiBlocker(this);
        private void OnDisable() => pointer?.UnregisterUiBlocker(this);
        public bool TryGetUiBlockingRect(out Rect guiRect) { guiRect = panel; return scheduler != null; }

        private void OnGUI()
        {
            if (scheduler == null) return;
            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Label("Strategic AI (U26 debug)");
            foreach (var faction in scheduler.Factions)
            {
                var state = faction.Strategic;
                var unit = state.KnownMilitaryUnits.FirstOrDefault();
                GUILayout.Label($"State/owner: {state.Id} / {state.OwnerId}");
                GUILayout.Label($"Ruler: {state.RulerCharacterId}");
                GUILayout.Label($"Settlements: {state.ControlledSettlements.Count}; home {state.HomeSettlementId}");
                GUILayout.Label($"Food/material: {faction.Production.GetCanonicalTotal(new ResourceId("berries"))} / " +
                    $"{faction.Production.GetCanonicalTotal(new ResourceId("wood_log"))}");
                GUILayout.Label("Research: " + (faction.Research.ActiveProject?.TechnologyId.ToString() ?? "none"));
                var strength = unit.IsValid ? scheduler.GetUnitSummary(unit).CombatCapable : 0;
                var battle = unit.IsValid ? scheduler.GetUnit(unit).ActiveEncounterId : null;
                GUILayout.Label($"Army: {state.KnownMilitaryUnits.Count}; combat-capable {strength}; first {unit}");
                GUILayout.Label($"Goal/target: {state.Goal} / {state.TargetLocationId?.ToString() ?? "none"}");
                GUILayout.Label($"Travel: {state.PendingOrders.Count}; last tick {state.LastDecisionTick}");
                GUILayout.Label("Battle: " + (battle.HasValue ? battle.Value.ToString() : "none"));
                GUILayout.Label($"Defeated: {state.IsDefeated} {state.DefeatReason}");
                if (GUILayout.Button("Advance canonical time")) advance?.Invoke();
            }
            GUILayout.EndArea();
        }
    }
}
