using System;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Health;
using Game.Presentation.Interaction;
using Game.Presentation.Localization;
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
        private BattleResolutionService battles;
        private Func<long> calendarTick;
        private Action advanceAbstract;
        private string message = "Начните демонстрационный бой или нажмите ПКМ на враждебном персонаже";

        public void Initialize(CombatService combatService, CharacterRegistry characterRegistry,
            HealthAndFamilyService healthService, SelectionProbe selectionProbe,
            WorldPointerRaycaster worldRaycaster, Action startDemoBattle,
            BattleResolutionService battleResolutionService = null, Func<long> calendarTickProvider = null,
            Action advanceAbstractBattle = null)
        {
            combat = combatService ?? throw new ArgumentNullException(nameof(combatService));
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            health = healthService ?? throw new ArgumentNullException(nameof(healthService));
            selection = selectionProbe ?? throw new ArgumentNullException(nameof(selectionProbe));
            startDemo = startDemoBattle;
            battles = battleResolutionService;
            calendarTick = calendarTickProvider;
            advanceAbstract = advanceAbstractBattle;
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
            GUILayout.Label("Тактический бой (отладка U20)");
            var encounter = combat.Encounters.OrderByDescending(x => x.StartCalendarTick).FirstOrDefault();
            if (encounter == null)
            {
                GUILayout.Label("Сражение: нет");
            }
            else
            {
                GUILayout.Label($"Сражение {Short(encounter.BattleId)} · {RussianTextCatalog.Enum(encounter.Mode)} · {RussianTextCatalog.Enum(encounter.Status)} · {RussianTextCatalog.Enum(encounter.Outcome)}");
                GUILayout.Label($"Такт {encounter.LastProcessedCalendarTick} · отряды {Short(encounter.SideAUnitId)} против {Short(encounter.SideBUnitId)}");
                GUILayout.Label($"{encounter.SideA}: {Counts(encounter, encounter.SideA)}");
                GUILayout.Label($"{encounter.SideB}: {Counts(encounter, encounter.SideB)}");
                GUILayout.Label($"Атаки: {encounter.AttackHistory.Count} · хеш {combat.ComputeStateHash():X16}");
            }

            if (selection.SelectedCharacterId.HasValue &&
                characters.TryGet(selection.SelectedCharacterId.Value, out var selected) &&
                combat.TryGetLatestCombatant(selected.Id, out var combatant))
            {
                var stats = combat.GetStats(selected.Id, combatant.WeaponId);
                var conditions = health.Conditions.Where(x => x.TargetId == selected.Id &&
                    x.State != HealthConditionState.Recovered).Select(x => x.DefinitionId.ToString()).ToArray();
                GUILayout.Space(4f);
                GUILayout.Label($"Выбран: {selected.Name.DisplayName} · {combatant.SideId} · {RussianTextCatalog.Enum(combatant.State)}");
                GUILayout.Label("Цель: " + (combatant.TargetId.HasValue ? Short(combatant.TargetId.Value) : "нет"));
                GUILayout.Label($"Оружие {RussianTextCatalog.Content("weapon", combatant.WeaponId.ToString())} · атака {stats.AttackPower} · защита {stats.Defense} · дальность {stats.RangeMetres:0.0} м");
                GUILayout.Label($"Боевой дух {combatant.Morale}/1000 · атак {combatant.AttackSequence} · перезарядка до такта {combatant.NextAttackTick}");
                GUILayout.Label("Недавняя травма: " + (string.IsNullOrEmpty(combatant.RecentInjury) ? "нет" : combatant.RecentInjury));
                GUILayout.Label("Здоровье: " + (conditions.Length == 0 ? "здоров" : string.Join(", ", conditions.Select(x => RussianTextCatalog.Content("condition", x)))));
            }
            else GUILayout.Label("Выбранный боец: нет");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Создать демонстрационный бой"))
            {
                try { startDemo?.Invoke(); message = "Демонстрационный бой начат"; }
                catch (Exception exception) { Debug.LogException(exception); message = RussianTextCatalog.Get("ui.status.internal_error"); }
            }
            if (GUILayout.Button("Остановить активный бой"))
            {
                var active = combat.Encounters.FirstOrDefault(x => x.Status == CombatEncounterStatus.Active);
                message = active != null && combat.CancelEncounter(active.EncounterId) ? "Сражение отменено" : "Активного сражения нет";
            }
            GUILayout.EndHorizontal();
            if (battles != null && encounter != null && encounter.Status == CombatEncounterStatus.Active)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("В автоматический режим")) Transition(encounter.BattleId, BattleMode.Abstract);
                if (GUILayout.Button("На местность")) Transition(encounter.BattleId, BattleMode.Physical);
                if (encounter.Mode == BattleMode.Abstract && GUILayout.Button("Продвинуть")) advanceAbstract?.Invoke();
                GUILayout.EndHorizontal();
            }
            GUILayout.Label(message);
            GUILayout.EndArea();
        }

        private void Transition(StableEntityId battleId, BattleMode mode)
        {
            var result = battles.Transition(battleId, mode, calendarTick == null ? 0 : calendarTick());
            message = RussianTextCatalog.Enum(result.Status);
        }

        private static string Counts(CombatEncounterState encounter, CombatSideId side)
        {
            var values = encounter.Combatants.Where(x => x.SideId == side).ToArray();
            return $"всего {values.Length}, в бою {values.Count(x => x.State == CombatantState.Fighting || x.State == CombatantState.Approaching || x.State == CombatantState.Attacking)}, " +
                   $"недееспособны {values.Count(x => x.State == CombatantState.Incapacitated)}, погибли {values.Count(x => x.State == CombatantState.Dead)}, " +
                   $"отступили {values.Count(x => x.State == CombatantState.Retreating || x.State == CombatantState.Disengaged)}";
        }

        private static Rect GetPanelRect() => new Rect(Mathf.Max(850f, Screen.width - 750f), 246f, PanelWidth, PanelHeight);
        private static string Short(StableEntityId value)
        {
            var text = value.ToString();
            return text.Length <= 8 ? text : text.Substring(0, 8);
        }

        private static string Short(Game.Domain.Military.MilitaryUnitId? value) =>
            value.HasValue ? Short(value.Value.Value) : "нет";
    }
}
