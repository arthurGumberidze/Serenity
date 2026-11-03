using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Work;
using Game.Simulation.Work;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.UI
{
    public sealed partial class GameplayUiController
    {
        private ListView workRoster;
        private readonly List<StableEntityId> workRosterIds = new();
        private string workRosterSearch = string.Empty;
        private int workRosterKnownCount = -1;

        private void DrawWorkPanel()
        {
            AddTitle(mainPanel, "Рабочие группы");
            AddMuted(mainPanel, "Выберите профессию и смену прямо напротив имени. Работу внутри группы жители распределяют сами.");
            var search = new TextField("Поиск жителя") { name = "work-roster-search", value = workRosterSearch };
            StyleTextField(search);
            search.RegisterValueChangedCallback(evt => { workRosterSearch = evt.newValue; RebuildWorkRoster(); });
            mainPanel.Add(search);
            var groups = context.DirectWork.ProfessionGroups.EnsureGroups().ToArray();
            var choices = groups.Select(g => ProfessionGroupService.DisplayName(g.Role)).ToList();
            workRoster = new ListView { name = "work-roster", fixedItemHeight = 112,
                selectionType = SelectionType.None, virtualizationMethod = CollectionVirtualizationMethod.FixedHeight };
            var panelHeight = float.IsNaN(root.resolvedStyle.height) ? 900 : root.resolvedStyle.height;
            workRoster.style.height = Mathf.Clamp(panelHeight - Mathf.Max(130, topBar.layout.yMax + 10) - 320, 180, 700);
            workRoster.style.flexShrink = 0;
            workRoster.makeItem = () =>
            {
                var row = SerenityUiDesign.Card();
                row.style.paddingTop = 4; row.style.paddingBottom = 4;
                var heading = new Label { name = "resident-name" }; row.Add(heading);
                var controls = Row();
                controls.style.flexWrap = Wrap.NoWrap;
                var profession = new DropdownField(choices, 0) { name = "resident-profession" };
                SerenityUiDesign.StyleField(profession);
                profession.style.flexGrow = 1; profession.style.flexShrink = 1;
                profession.style.flexBasis = 0; profession.style.minWidth = 0;
                var shift = new DropdownField(new List<string> { "День", "Ночь" }, 0) {
                    name = "resident-shift", tooltip = "День: 06:00–18:00. Ночь: 18:00–06:00." };
                SerenityUiDesign.StyleField(shift);
                shift.style.width = 155; shift.style.minWidth = 155; shift.style.flexShrink = 0;
                foreach (var field in new[] { profession, shift })
                {
                    var fieldInput = field.Q(className: "unity-base-field__input");
                    if (fieldInput != null) fieldInput.style.minWidth = 0;
                    field.Query<TextElement>().ForEach(text => {
                        text.style.whiteSpace = WhiteSpace.NoWrap;
                        text.style.textOverflow = TextOverflow.Ellipsis;
                    });
                }
                profession.RegisterValueChangedCallback(_ =>
                {
                    if (row.userData is not StableEntityId id || profession.index < 0) return;
                    TryCommand(() => context.DirectWork.ProfessionGroups.Reassign(id, groups[profession.index].Id));
                    BindResidentRow(row, id, groups, choices);
                });
                shift.RegisterValueChangedCallback(_ =>
                {
                    if (row.userData is not StableEntityId id || !context.Work.Groups.TryGetForMember(id, out var group)) return;
                    TryCommand(() => context.Work.SetMemberShift(group.Id, id, shift.index == 0 ? WorkShiftKind.Day : WorkShiftKind.Night));
                    BindResidentRow(row, id, groups, choices);
                });
                controls.Add(profession); controls.Add(shift); row.Add(controls);
                row.Add(new Label { name = "resident-work-status" });
                return row;
            };
            workRoster.bindItem = (row, index) => BindResidentRow(row, workRosterIds[index], groups, choices);
            mainPanel.Add(workRoster);
            AddMuted(mainPanel, "Исследователи требуют построенный Дом знаний. Приоритет задаётся у работы или здания.");
            RebuildWorkRoster();
        }

        private void BindResidentRow(VisualElement row, StableEntityId id, WorkGroup[] groups, List<string> choices)
        {
            row.userData = id;
            row.Q<Label>("resident-name").text = CharacterName(id);
            if (!context.Work.Groups.TryGetForMember(id, out var current)) return;
            var index = System.Array.FindIndex(groups, g => g.Id == current.Id);
            row.Q<DropdownField>("resident-profession").SetValueWithoutNotify(choices[System.Math.Max(0, index)]);
            row.Q<DropdownField>("resident-shift").SetValueWithoutNotify(current.ShiftFor(id) == WorkShiftKind.Day ? "День" : "Ночь");
            var onShift = context.Work.IsOnShift(id, context.Clock().State.CalendarTicks);
            row.Q<Label>("resident-work-status").text = onShift ? "Рабочая смена" : "Время отдыха";
        }

        private void RebuildWorkRoster()
        {
            workRosterIds.Clear();
            workRosterIds.AddRange(context.PlayerCharacterIds().Where(id => context.Work.Groups.TryGetForMember(id, out _) &&
                (string.IsNullOrWhiteSpace(workRosterSearch) || CharacterName(id).IndexOf(workRosterSearch, System.StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(id => CharacterName(id)).ThenBy(id => id));
            workRosterKnownCount = context.Characters.Count;
            workRoster.itemsSource = workRosterIds;
            workRoster.Rebuild();
        }

        private void UpdateWorkRoster()
        {
            if (workRosterKnownCount != context.Characters.Count) RebuildWorkRoster();
            // Updating labels in place preserves scroll, an open dropdown and its keyboard focus.
            workRoster.Query<Label>("resident-work-status").ForEach(label =>
            {
                var row = label.parent;
                if (row.userData is StableEntityId id)
                    label.text = context.Work.IsOnShift(id, context.Clock().State.CalendarTicks) ? "Рабочая смена" : "Время отдыха";
            });
        }
    }
}
