using System;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Military;
using Game.Presentation.Interaction;
using Game.Simulation.Military;
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
        private SelectionProbe selection;
        private WorldPointerRaycaster raycaster;
        private Func<long> calendarTick;
        private MilitaryUnitId? selectedUnitId;
        private string message = "Select characters to create a unit";

        public MilitaryUnitId? SelectedUnitId => selectedUnitId;

        public void Initialize(MilitaryUnitService unitService,
            MilitaryUnitRepresentationService representationService,
            MilitaryUnitCommandService commandService, MilitaryUnitCombatService combatService,
            SelectionProbe selectionProbe, WorldPointerRaycaster worldRaycaster, Func<long> calendarTickProvider)
        {
            units = unitService ?? throw new ArgumentNullException(nameof(unitService));
            representations = representationService ?? throw new ArgumentNullException(nameof(representationService));
            commands = commandService ?? throw new ArgumentNullException(nameof(commandService));
            combat = combatService ?? throw new ArgumentNullException(nameof(combatService));
            selection = selectionProbe ?? throw new ArgumentNullException(nameof(selectionProbe));
            calendarTick = calendarTickProvider ?? throw new ArgumentNullException(nameof(calendarTickProvider));
            raycaster?.UnregisterUiBlocker(this);
            raycaster = worldRaycaster ?? throw new ArgumentNullException(nameof(worldRaycaster));
            raycaster.RegisterUiBlocker(this);
        }

        public MilitaryUnitState CreateFromSelection(MilitarySideId sideId)
        {
            var selected = selection.SelectedCharacterIds;
            if (selected.Count == 0) throw new InvalidOperationException("Select at least one eligible character.");
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
            GUILayout.Label("Military Units (U21 debug)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Create player")) TryUi(() => CreateFromSelection(new MilitarySideId("player")));
            if (GUILayout.Button("Create enemy")) TryUi(() => CreateFromSelection(new MilitarySideId("enemy")));
            GUILayout.EndHorizontal();

            foreach (var unit in units.Units)
            {
                var summary = units.GetRosterSummary(unit.Id);
                if (GUILayout.Button((selectedUnitId == unit.Id ? "> " : string.Empty) +
                    $"{Short(unit.Id.Value)} {unit.OwnerSideId} {unit.Representation} {unit.Status}"))
                    selectedUnitId = unit.Id;
                GUILayout.Label($"capt {Short(unit.CaptainId)} | roster {summary.Active}/{summary.Historical} | " +
                    $"alive {summary.Alive} fight {summary.CombatCapable} inj {summary.Injured} incap {summary.Incapacitated}");
                GUILayout.Label($"order {unit.Order.Kind} | encounter {Short(unit.ActiveEncounterId)} | " +
                    $"morale {unit.MoraleSummary} cohesion {unit.Cohesion}");
            }

            if (selectedUnitId.HasValue)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Captain = selected")) TryUi(() =>
                {
                    if (!selection.SelectedCharacterId.HasValue) throw new InvalidOperationException("Select a roster member.");
                    message = units.AssignCaptain(selectedUnitId.Value, selection.SelectedCharacterId.Value).ToString();
                });
                if (GUILayout.Button("Remove selected")) TryUi(() =>
                {
                    if (!selection.SelectedCharacterId.HasValue) throw new InvalidOperationException("Select a roster member.");
                    units.RemoveMember(selectedUnitId.Value, selection.SelectedCharacterId.Value);
                });
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Detailed")) TryUi(() => representations.Transition(selectedUnitId.Value,
                    MilitaryUnitRepresentationMode.Detailed));
                if (GUILayout.Button("Aggregate")) TryUi(() => representations.Transition(selectedUnitId.Value,
                    MilitaryUnitRepresentationMode.Aggregate));
                if (GUILayout.Button("Hold")) TryUi(() => commands.Hold(selectedUnitId.Value));
                if (GUILayout.Button("Move origin")) TryUi(() => commands.Move(selectedUnitId.Value,
                    new WorldPosition(0f, 0f, 0f)));
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Start first hostile unit battle")) TryUi(StartFirstHostileBattle);
            GUILayout.Label(message);
            GUILayout.EndArea();
        }

        private void StartFirstHostileBattle()
        {
            var values = units.Units;
            var pair = (from first in values from second in values
                where first.Id.CompareTo(second.Id) < 0 && first.OwnerSideId != second.OwnerSideId
                select new { first, second }).FirstOrDefault();
            if (pair == null) throw new InvalidOperationException("Create two hostile detailed units first.");
            combat.Start(pair.first.Id, pair.second.Id, StableEntityId.NewId(), calendarTick());
            selectedUnitId = pair.first.Id;
            message = "Physical encounter started through U20";
        }

        private void TryUi(Action action)
        {
            try { action(); message = "OK"; }
            catch (Exception exception) { message = exception.Message; }
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
