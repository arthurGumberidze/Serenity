using System;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Combat;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Presentation.Interaction;
using Game.Presentation.Localization;
using Game.Simulation.Buildings;
using Game.Simulation.Combat;
using Game.Simulation.Production;
using Game.Simulation.Time;
using UnityEngine;

namespace Game.Presentation.Buildings
{
    public sealed class BuildingDamageDebugOverlay : MonoBehaviour, IWorldPointerUiBlocker
    {
        private const float Width = 430f;
        private const float Height = 330f;
        private BuildingDamageService damage;
        private BuildingRegistry buildings;
        private BuildingPresentationCatalog definitions;
        private ResourceInventoryRegistry inventories;
        private ProductionService production;
        private CombatService combat;
        private GameClock clock;
        private WorldPointerRaycaster raycaster;
        private StableEntityId? selectedId;
        private string message = "Готово";

        public void Initialize(BuildingDamageService service, BuildingRegistry registry,
            BuildingPresentationCatalog catalog, ResourceInventoryRegistry inventoryRegistry,
            ProductionService productionService, CombatService combatService, GameClock gameClock,
            WorldPointerRaycaster worldRaycaster)
        {
            damage = service ?? throw new ArgumentNullException(nameof(service));
            buildings = registry ?? throw new ArgumentNullException(nameof(registry));
            definitions = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            inventories = inventoryRegistry ?? throw new ArgumentNullException(nameof(inventoryRegistry));
            production = productionService ?? throw new ArgumentNullException(nameof(productionService));
            combat = combatService ?? throw new ArgumentNullException(nameof(combatService));
            clock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
            raycaster?.UnregisterUiBlocker(this);
            raycaster = worldRaycaster ?? throw new ArgumentNullException(nameof(worldRaycaster));
            raycaster.RegisterUiBlocker(this);
            selectedId = buildings.All.OrderBy(x => x.Id).Select(x => (StableEntityId?)x.Id).FirstOrDefault();
        }

        private void OnEnable() => raycaster?.RegisterUiBlocker(this);
        private void OnDisable() => raycaster?.UnregisterUiBlocker(this);
        public bool TryGetUiBlockingRect(out Rect guiRect)
        {
            guiRect = PanelRect();
            return damage != null && (Application.isEditor || Debug.isDebugBuild);
        }

        private void OnGUI()
        {
            if (damage == null || (!Application.isEditor && !Debug.isDebugBuild)) return;
            GUILayout.BeginArea(PanelRect(), GUI.skin.box);
            GUILayout.Label("Повреждение, пожар и захват построек (U24)");
            var ordered = buildings.All.OrderBy(x => x.Id).ToArray();
            if (ordered.Length == 0) { GUILayout.Label("Построек нет"); GUILayout.EndArea(); return; }
            var selected = selectedId.HasValue && buildings.TryGet(selectedId.Value, out var found) ? found : ordered[0];
            selectedId = selected.Id;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Предыдущая")) SelectOffset(ordered, selected, -1);
            if (GUILayout.Button("Следующая")) SelectOffset(ordered, selected, 1);
            GUILayout.EndHorizontal();
            GUILayout.Label($"{RussianTextCatalog.Content("building", selected.DefinitionId.ToString())} [{Short(selected.Id)}] · владелец " +
                (selected.OwnerSideId.Value == "player" ? RussianTextCatalog.Get("ui.side.player") : RussianTextCatalog.Get("ui.side.enemy")));
            GUILayout.Label($"Прочность {selected.CurrentIntegrity}/{selected.MaxIntegrity} · {RussianTextCatalog.Enum(selected.StructuralStatus)}");
            GUILayout.Label($"Огонь: {RussianTextCatalog.Enum(selected.FireStatus)} · сила {selected.FireIntensity} · такт {selected.LastFireProcessedTick}");
            GUILayout.Label($"Производство: {(production.IsSiteOperational(selected.Id) ? "работает" : "остановлено")} · можно захватить: {(definitions.ResolveDefinition(selected.DefinitionId).Capturable ? "да" : "нет")}");
            var owner = new InventoryOwner(InventoryOwnerKind.BuildingStorage, selected.Id);
            GUILayout.Label("Склад: " + (inventories.TryGet(owner, out var storage)
                ? string.Join(", ", storage.CaptureState().Amounts.Select(x => RussianTextCatalog.Content("resource", x.ResourceId.ToString()) + "=" + x.Quantity.Units))
                : "нет"));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Повредить на 100")) Apply(selected, 100, BuildingDamageType.Generic);
            if (GUILayout.Button("Боевой урон 100")) Apply(selected, 100, BuildingDamageType.Combat);
            if (GUILayout.Button("Разрушить")) Apply(selected, selected.CurrentIntegrity, BuildingDamageType.Generic);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Поджечь")) message = RussianTextCatalog.Enum(damage.IgniteBuilding(selected.Id, 800, Tick()).Failure);
            if (GUILayout.Button("Потушить")) message = RussianTextCatalog.Enum(damage.ExtinguishBuilding(selected.Id, Tick()).Failure);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Передать победившему сопернику")) Capture(selected);
            GUILayout.Label(message);
            GUILayout.EndArea();
        }

        private void Apply(Building building, int amount, BuildingDamageType type)
        {
            if (amount <= 0) { message = RussianTextCatalog.Enum(BuildingDamageFailure.AlreadyDestroyed); return; }
            var result = damage.ApplyDamage(building.Id, StableEntityId.NewId(), amount, type, Tick());
            message = RussianTextCatalog.Enum(result.Failure) + $" · применено: {result.Applied}";
        }

        private void Capture(Building building)
        {
            var battle = combat.Encounters.Where(x => x.Status == CombatEncounterStatus.Finished)
                .OrderByDescending(x => x.LastProcessedCalendarTick).FirstOrDefault();
            if (battle == null) { message = RussianTextCatalog.Enum(BuildingCaptureFailure.MissingBattle); return; }
            var result = damage.CaptureBuilding(building.Id, new CombatSideId("enemy"), battle.BattleId, Tick());
            message = RussianTextCatalog.Enum(result.Failure);
        }

        private void SelectOffset(Building[] ordered, Building selected, int offset)
        {
            var index = Array.FindIndex(ordered, x => x.Id == selected.Id);
            selectedId = ordered[(index + offset + ordered.Length) % ordered.Length].Id;
        }

        private long Tick() => clock.State.CalendarTicks;
        private static string Short(StableEntityId id) => id.ToString().Substring(0, 8);
        private static Rect PanelRect() => new Rect(12f, 500f, Width, Height);
    }
}
