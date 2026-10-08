using System;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Health;
using Game.Presentation.Interaction;
using Game.Simulation.Combat;
using Game.Simulation.Health;
using UnityEngine;

namespace Game.Presentation.Combat
{
    public sealed class CombatDebugOverlay : MonoBehaviour, IWorldPointerUiBlocker
    {
        private const float PanelWidth = 330f;
        private const float PanelHeight = 330f;
        private CombatService combat;
        private CharacterRegistry characters;
        private HealthAndFamilyService health;
        private SelectionProbe selection;
        private WorldPointerRaycaster raycaster;
        private Action startDemo;
        private string message = "Start demo battle or RMB a hostile character";

        public void Initialize(CombatService combatService, CharacterRegistry characterRegistry,
            HealthAndFamilyService healthService, SelectionProbe selectionProbe,
            WorldPointerRaycaster worldRaycaster, Action startDemoBattle)
        {
            combat = combatService ?? throw new ArgumentNullException(nameof(combatService));
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            health = healthService ?? throw new ArgumentNullException(nameof(healthService));
            selection = selectionProbe ?? throw new ArgumentNullException(nameof(selectionProbe));
            startDemo = startDemoBattle;
            raycaster?.UnregisterUiBlocker(this);
            raycaster = worldRaycaster ?? throw new ArgumentNullException(nameof(worldRaycaster));
            raycaster.RegisterUiBlocker(this);
        }

        public bool TryGetUiBlockingRect(out Rect guiRect)
        {
            guiRect = GetPanelRect();
            return combat != null && (Application.isEditor || Debug.isDebugBuild);
        }

        private void OnEnable() => raycaster?.RegisterUiBlocker(this);
        private void OnDisable() => raycaster?.UnregisterUiBlocker(this);
        private void OnDestroy() => raycaster?.UnregisterUiBlocker(this);

        private void OnGUI()
        {
            if (combat == null || (!Application.isEditor && !Debug.isDebugBuild)) return;
            GUILayout.BeginArea(GetPanelRect(), GUI.skin.box);
            GUILayout.Label("Tactical Combat (U20 debug)");
            var encounter = combat.Encounters.OrderByDescending(x => x.StartCalendarTick).FirstOrDefault();
            if (encounter == null)
            {
                GUILayout.Label("Encounter: none");
            }
            else
            {
                GUILayout.Label($"Encounter {Short(encounter.EncounterId)} | {encounter.Status} | {encounter.Outcome}");
                GUILayout.Label($"{encounter.SideA}: {Counts(encounter, encounter.SideA)}");
                GUILayout.Label($"{encounter.SideB}: {Counts(encounter, encounter.SideB)}");
                GUILayout.Label($"Attacks: {encounter.AttackHistory.Count} | hash {combat.ComputeStateHash():X16}");
            }

            if (selection.SelectedCharacterId.HasValue &&
                characters.TryGet(selection.SelectedCharacterId.Value, out var selected) &&
                combat.TryGetLatestCombatant(selected.Id, out var combatant))
            {
                var stats = combat.GetStats(selected.Id, combatant.WeaponId);
                var conditions = health.Conditions.Where(x => x.TargetId == selected.Id &&
                    x.State != HealthConditionState.Recovered).Select(x => x.DefinitionId.ToString()).ToArray();
                GUILayout.Space(4f);
                GUILayout.Label($"Selected: {selected.Name.DisplayName} | {combatant.SideId} | {combatant.State}");
                GUILayout.Label("Target: " + (combatant.TargetId.HasValue ? Short(combatant.TargetId.Value) : "none"));
                GUILayout.Label($"Weapon {combatant.WeaponId} | atk {stats.AttackPower} def {stats.Defense} range {stats.RangeMetres:0.0}m");
                GUILayout.Label($"Morale {combatant.Morale}/1000 | attacks {combatant.AttackSequence} | cooldown tick {combatant.NextAttackTick}");
                GUILayout.Label("Recent: " + (string.IsNullOrEmpty(combatant.RecentInjury) ? "none" : combatant.RecentInjury));
                GUILayout.Label("Health: " + (conditions.Length == 0 ? "healthy" : string.Join(", ", conditions)));
            }
            else GUILayout.Label("Selected combatant: none");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Spawn/start demo"))
            {
                try { startDemo?.Invoke(); message = "Demo battle started"; }
                catch (Exception exception) { message = exception.Message; }
            }
            if (GUILayout.Button("Stop active"))
            {
                var active = combat.Encounters.FirstOrDefault(x => x.Status == CombatEncounterStatus.Active);
                message = active != null && combat.CancelEncounter(active.EncounterId) ? "Encounter cancelled" : "No active encounter";
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(message);
            GUILayout.EndArea();
        }

        private static string Counts(CombatEncounterState encounter, CombatSideId side)
        {
            var values = encounter.Combatants.Where(x => x.SideId == side).ToArray();
            return $"{values.Length} total, {values.Count(x => x.State == CombatantState.Fighting || x.State == CombatantState.Approaching || x.State == CombatantState.Attacking)} fighting, " +
                   $"{values.Count(x => x.State == CombatantState.Incapacitated)} incap, {values.Count(x => x.State == CombatantState.Dead)} dead, " +
                   $"{values.Count(x => x.State == CombatantState.Retreating || x.State == CombatantState.Disengaged)} retreated";
        }

        private static Rect GetPanelRect() => new Rect(Mathf.Max(850f, Screen.width - 750f), 246f, PanelWidth, PanelHeight);
        private static string Short(StableEntityId value)
        {
            var text = value.ToString();
            return text.Length <= 8 ? text : text.Substring(0, 8);
        }
    }
}
