using System;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Health;
using Game.Presentation.Interaction;
using Game.Presentation.Localization;
using Game.Simulation.Dynasties;
using Game.Simulation.Health;
using Game.Simulation.Time;
using UnityEngine;

namespace Game.Presentation.Health
{
    public sealed class HealthFamilyDebugOverlay : MonoBehaviour, IWorldPointerUiBlocker
    {
        private const float PanelWidth = 420f;
        private const float PanelHeight = 330f;
        private HealthAndFamilyService service;
        private CharacterRegistry characters;
        private FamilyTreeQuery family;
        private SelectionProbe selection;
        private GameClock clock;
        private WorldPointerRaycaster raycaster;
        private string message = "Выберите персонажа";

        public void Initialize(HealthAndFamilyService healthFamily, CharacterRegistry registry,
            FamilyTreeQuery familyQuery, SelectionProbe selectionProbe, GameClock gameClock,
            WorldPointerRaycaster worldRaycaster)
        {
            service = healthFamily ?? throw new ArgumentNullException(nameof(healthFamily));
            characters = registry ?? throw new ArgumentNullException(nameof(registry));
            family = familyQuery ?? throw new ArgumentNullException(nameof(familyQuery));
            selection = selectionProbe ?? throw new ArgumentNullException(nameof(selectionProbe));
            clock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
            raycaster?.UnregisterUiBlocker(this);
            raycaster = worldRaycaster ?? throw new ArgumentNullException(nameof(worldRaycaster));
            raycaster.RegisterUiBlocker(this);
        }

        public bool TryGetUiBlockingRect(out Rect guiRect)
        {
            guiRect = GetPanelRect();
            return service != null && (Application.isEditor || Debug.isDebugBuild);
        }

        private void OnEnable() => raycaster?.RegisterUiBlocker(this);
        private void OnDisable() => raycaster?.UnregisterUiBlocker(this);

        private void OnGUI()
        {
            if (service == null || (!Application.isEditor && !Debug.isDebugBuild)) return;
            GUILayout.BeginArea(GetPanelRect(), GUI.skin.box);
            GUILayout.Label("Здоровье и семья персонажа (отладка U18)");
            if (!selection.SelectedCharacterId.HasValue ||
                !characters.TryGet(selection.SelectedCharacterId.Value, out var selected))
            {
                GUILayout.Label(message);
                GUILayout.EndArea();
                return;
            }
            var capabilities = service.GetCapabilities(selected.Id);
            GUILayout.Label($"{selected.Name.DisplayName} · возраст {selected.AgeInCompletedYears(clock.State.BiologicalTicks)} · {RussianTextCatalog.Enum(selected.LifeState)}");
            GUILayout.Label($"Работа {capabilities.WorkPerThousand / 10f:0}% · исследования {capabilities.ResearchPerThousand / 10f:0}% · " +
                            $"движение {capabilities.MovementPerThousand / 10f:0}% · недееспособен: {(capabilities.IsIncapacitated ? "да" : "нет")}");
            var active = service.Conditions.Where(x => x.TargetId == selected.Id &&
                x.State != HealthConditionState.Recovered).ToArray();
            GUILayout.Label("Состояния: " + (active.Length == 0 ? "нет" : string.Join(", ", active.Select(x =>
                RussianTextCatalog.Content("condition", x.DefinitionId.ToString()) + " · " + RussianTextCatalog.Enum(x.Severity) +
                " · шаг " + x.ProgressionSteps))));
            var pregnancy = service.Pregnancies.LastOrDefault(x => x.MotherId == selected.Id && x.Status == PregnancyStatus.Active);
            GUILayout.Label(pregnancy == null ? "Беременность: нет" :
                $"Беременность: ожидаемый биологический такт {pregnancy.DueBiologicalTick} · состояние {RussianTextCatalog.Enum(pregnancy.Status)}");
            var spouse = family.GetSpouse(selected.Id);
            GUILayout.Label("Супруг или супруга: " + (spouse == null ? "нет" : spouse.Name.DisplayName));
            GUILayout.Label("Родители: " + Join(family.GetParents(selected.Id)));
            GUILayout.Label("Дети: " + Join(family.GetChildren(selected.Id)));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Добавить лёгкую травму")) Execute(() => service.ApplyCondition(selected.Id,
                new HealthConditionId("minor_cut"), clock.State.CalendarTicks), "Лёгкая травма добавлена");
            if (GUILayout.Button("Добавить тяжёлую травму")) Execute(() => service.ApplyCondition(selected.Id,
                new HealthConditionId("serious_wound"), clock.State.CalendarTicks), "Тяжёлая травма добавлена");
            if (GUILayout.Button("Добавить болезнь")) Execute(() => service.ApplyCondition(selected.Id,
                new HealthConditionId("infection"), clock.State.CalendarTicks), "Болезнь добавлена");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Вылечить выбранного")) Execute(() => service.RecoverAll(selected.Id), "Лечение завершено");
            if (GUILayout.Button("Начать беременность с супругом") && spouse != null)
            {
                var mother = selected.Sex == CharacterSex.Female ? selected : spouse;
                var father = selected.Sex == CharacterSex.Male ? selected : spouse;
                Execute(() => service.StartPregnancy(mother.Id, father.Id, clock.State.CalendarTicks,
                    clock.State.BiologicalTicks), "Беременность началась; для рождения ребёнка продвиньте игровое время");
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(message);
            GUILayout.EndArea();
        }

        private void Execute(Action action, string success)
        {
            try { action(); message = success; }
            catch (Exception exception) { Debug.LogException(exception); message = RussianTextCatalog.Get("ui.status.internal_error"); }
        }

        private static string Join(System.Collections.Generic.IReadOnlyList<Character> values) => values.Count == 0
            ? "нет" : string.Join(", ", values.Select(x => x.Name.DisplayName));
        private static Rect GetPanelRect() => new Rect(12f, 430f, PanelWidth, PanelHeight);
    }
}
