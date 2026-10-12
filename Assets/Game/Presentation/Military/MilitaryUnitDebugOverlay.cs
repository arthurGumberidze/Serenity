using System;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Military;
using Game.Presentation.Interaction;
using Game.Presentation.Localization;
using Game.Simulation.Military;
using Game.Simulation.Combat;
using UnityEngine;

namespace Game.Presentation.Military
{
    /// <summary>Disposable U21 inspection/command surface. U27 owns final army-management UI.</summary>
    public sealed class MilitaryUnitDebugOverlay : MonoBehaviour, IWorldPointerUiBlocker
    {
        private const float PanelWidth = 370f;
        private const float PanelHeight = 340f;
        private MilitaryUnitService units;
        private MilitaryUnitRepresentationService representations;
        private MilitaryUnitCommandService commands;
        private MilitaryUnitCombatService combat;
        private BattleResolutionService battles;
        private SelectionProbe selection;
        private WorldPointerRaycaster raycaster;
        private Func<long> calendarTick;
        private MilitaryUnitId? selectedUnitId;
        private string message = "Выберите персонажей, чтобы создать отряд";

        public MilitaryUnitId? SelectedUnitId => selectedUnitId;

        public void Initialize(MilitaryUnitService unitService,
            MilitaryUnitRepresentationService representationService,
            MilitaryUnitCommandService commandService, MilitaryUnitCombatService combatService,
            SelectionProbe selectionProbe, WorldPointerRaycaster worldRaycaster, Func<long> calendarTickProvider,
            BattleResolutionService battleResolutionService = null)
        {
            units = unitService ?? throw new ArgumentNullException(nameof(unitService));
            representations = representationService ?? throw new ArgumentNullException(nameof(representationService));
            commands = commandService ?? throw new ArgumentNullException(nameof(commandService));
            combat = combatService ?? throw new ArgumentNullException(nameof(combatService));
            battles = battleResolutionService;
            selection = selectionProbe ?? throw new ArgumentNullException(nameof(selectionProbe));
            calendarTick = calendarTickProvider ?? throw new ArgumentNullException(nameof(calendarTickProvider));
            raycaster?.UnregisterUiBlocker(this);
            raycaster = worldRaycaster ?? throw new ArgumentNullException(nameof(worldRaycaster));
            raycaster.RegisterUiBlocker(this);
        }

        public MilitaryUnitState CreateFromSelection(MilitarySideId sideId)
        {
            var selected = selection.SelectedCharacterIds;
            if (selected.Count == 0) throw new InvalidOperationException("Выберите хотя бы одного подходящего персонажа.");
            var unit = units.Create(sideId, selected, selected[0]);
            selectedUnitId = unit.Id;
            return unit;
        }

        public bool SelectUnit(MilitaryUnitId id)
        {
            if (!units.TryGet(id, out _)) return false;
            selectedUnitId = id;
            return true;
        }

        public bool TryGetUiBlockingRect(out Rect guiRect)
        {
            guiRect = GetPanelRect();
            return units != null && (Application.isEditor || Debug.isDebugBuild);
        }

        private void OnEnable() => raycaster?.RegisterUiBlocker(this);
        private void OnDisable() => raycaster?.UnregisterUiBlocker(this);
        private void OnDestroy() => raycaster?.UnregisterUiBlocker(this);

        private void OnGUI()
        {
            if (units == null || (!Application.isEditor && !Debug.isDebugBuild)) return;
            GUILayout.BeginArea(GetPanelRect(), GUI.skin.box);
            GUILayout.Label("Военные отряды (отладка U21)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Создать отряд игрока")) TryUi(() => CreateFromSelection(new MilitarySideId("player")));
            if (GUILayout.Button("Создать отряд соперника")) TryUi(() => CreateFromSelection(new MilitarySideId("enemy")));
            GUILayout.EndHorizontal();

            foreach (var unit in units.Units)
            {
                var summary = units.GetRosterSummary(unit.Id);
                if (GUILayout.Button((selectedUnitId == unit.Id ? "> " : string.Empty) +
                    $"{Short(unit.Id.Value)} · {(unit.OwnerSideId.Value == "player" ? "игрок" : "соперник")} · {RussianTextCatalog.Enum(unit.Representation)} · {RussianTextCatalog.Enum(unit.Status)}"))
                    selectedUnitId = unit.Id;
                GUILayout.Label($"капитан {Short(unit.CaptainId)} · состав {summary.Active}/{summary.Historical} · " +
                    $"живы {summary.Alive} · боеспособны {summary.CombatCapable} · ранены {summary.Injured} · недееспособны {summary.Incapacitated}");
                GUILayout.Label($"приказ {RussianTextCatalog.Enum(unit.Order.Kind)} · сражение {Short(unit.ActiveEncounterId)} · " +
                    $"боевой дух {unit.MoraleSummary} · сплочённость {unit.Cohesion}");
            }

            if (selectedUnitId.HasValue)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Выбранный — капитан")) TryUi(() =>
                {
                    if (!selection.SelectedCharacterId.HasValue) throw new InvalidOperationException("Выберите бойца из состава.");
                    message = RussianTextCatalog.Enum(units.AssignCaptain(selectedUnitId.Value, selection.SelectedCharacterId.Value));
                });
                if (GUILayout.Button("Убрать выбранного")) TryUi(() =>
                {
                    if (!selection.SelectedCharacterId.HasValue) throw new InvalidOperationException("Выберите бойца из состава.");
                    units.RemoveMember(selectedUnitId.Value, selection.SelectedCharacterId.Value);
                });
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Подробный")) TryUi(() => representations.Transition(selectedUnitId.Value,
                    MilitaryUnitRepresentationMode.Detailed));
                if (GUILayout.Button("Сводный")) TryUi(() => representations.Transition(selectedUnitId.Value,
                    MilitaryUnitRepresentationMode.Aggregate));
                if (GUILayout.Button("Удерживать")) TryUi(() => commands.Hold(selectedUnitId.Value));
                if (GUILayout.Button("Идти к началу координат")) TryUi(() => commands.Move(selectedUnitId.Value,
                    new WorldPosition(0f, 0f, 0f)));
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Начать бой на местности")) TryUi(StartFirstHostileBattle);
            if (GUILayout.Button("Начать автоматический бой")) TryUi(StartFirstHostileAbstractBattle);
            GUILayout.EndHorizontal();
            GUILayout.Label(message);
            GUILayout.EndArea();
        }

        private void StartFirstHostileBattle()
        {
            var pair = FirstHostilePair();
            combat.Start(pair.Item1.Id, pair.Item2.Id, StableEntityId.NewId(), calendarTick());
            selectedUnitId = pair.Item1.Id;
            message = "Сражение на местности начато через U20";
        }

        private void StartFirstHostileAbstractBattle()
        {
            if (battles == null) throw new InvalidOperationException("Сервис сражений U23 недоступен.");
            var pair = FirstHostilePair();
            representations.Transition(pair.Item1.Id, MilitaryUnitRepresentationMode.Aggregate);
            representations.Transition(pair.Item2.Id, MilitaryUnitRepresentationMode.Aggregate);
            battles.StartAbstractBattle(pair.Item1.Id, pair.Item2.Id, StableEntityId.NewId(), calendarTick());
            selectedUnitId = pair.Item1.Id;
            message = "Автоматическое сражение начато без физических бойцов";
        }

        private Tuple<MilitaryUnitState, MilitaryUnitState> FirstHostilePair()
        {
            var pair = (from first in units.Units from second in units.Units
                where first.Id.CompareTo(second.Id) < 0 && first.OwnerSideId != second.OwnerSideId
                select Tuple.Create(first, second)).FirstOrDefault();
            if (pair == null) throw new InvalidOperationException("Create two hostile units first.");
            return pair;
        }

        private void TryUi(Action action)
        {
            try { action(); message = "Готово"; }
            catch (Exception exception) { Debug.LogException(exception); message = RussianTextCatalog.Get("ui.status.internal_error"); }
        }

        private static Rect GetPanelRect() => new Rect(Mathf.Max(1220f, Screen.width - PanelWidth - 10f),
            246f, PanelWidth, PanelHeight);
        private static string Short(MilitaryUnitId? id) => id.HasValue ? Short(id.Value.Value) : "none";
        private static string Short(StableEntityId? id) => id.HasValue ? Short(id.Value) : "none";
        private static string Short(StableEntityId id)
        {
            var text = id.ToString();
            return text.Length <= 8 ? text : text.Substring(text.Length - 8);
        }
    }
}
