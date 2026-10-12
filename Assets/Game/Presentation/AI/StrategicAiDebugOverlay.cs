using System;
using System.Linq;
using Game.Domain.Resources;
using Game.Presentation.Interaction;
using Game.Presentation.Localization;
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
            GUILayout.Label("Стратегический ИИ (отладка U26)");
            foreach (var faction in scheduler.Factions)
            {
                var state = faction.Strategic;
                var unit = state.KnownMilitaryUnits.FirstOrDefault();
                GUILayout.Label($"Государство/владелец: {state.Id} / {state.OwnerId}");
                GUILayout.Label($"Правитель: {state.RulerCharacterId}");
                GUILayout.Label($"Поселения: {state.ControlledSettlements.Count} · столица {state.HomeSettlementId}");
                GUILayout.Label($"Еда/материалы: {faction.Production.GetCanonicalTotal(new ResourceId("berries"))} / " +
                    $"{faction.Production.GetCanonicalTotal(new ResourceId("wood_log"))}");
                GUILayout.Label("Исследование: " + (faction.Research.ActiveProject == null ? "нет" :
                    RussianTextCatalog.Content("technology", faction.Research.ActiveProject.TechnologyId.ToString())));
                var strength = unit.IsValid ? scheduler.GetUnitSummary(unit).CombatCapable : 0;
                var battle = unit.IsValid ? scheduler.GetUnit(unit).ActiveEncounterId : null;
                GUILayout.Label($"Армия: {state.KnownMilitaryUnits.Count} · боеспособны {strength} · первый {unit}");
                GUILayout.Label($"Цель/место: {RussianTextCatalog.Enum(state.Goal)} / {(state.TargetLocationId.HasValue ? RussianTextCatalog.Content("location", state.TargetLocationId.Value.Value) : "нет")}");
                GUILayout.Label($"Переходы: {state.PendingOrders.Count} · последний такт {state.LastDecisionTick}");
                GUILayout.Label("Сражение: " + (battle.HasValue ? battle.Value.ToString() : "нет"));
                GUILayout.Label($"Побеждён: {(state.IsDefeated ? "да" : "нет")} {RussianTextCatalog.Enum(state.DefeatReason)}");
                if (GUILayout.Button("Продвинуть время мира")) advance?.Invoke();
            }
            GUILayout.EndArea();
        }
    }
}
