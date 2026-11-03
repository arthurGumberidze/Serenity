using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Events;
using Game.Domain.Buildings;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.UI
{
    public sealed partial class GameplayUiController
    {
        public Action<VisualElement, Action> DrawSettingsContent { get; set; }
        private bool pauseSession;
        private bool pausedBeforeMenu;
        private int continuityRevision;

        public void HandleEscape()
        {
            if (context.BuildingPlacement.WasCancelledThisFrame) return;
            if (modal != null && modal.resolvedStyle.display != DisplayStyle.None)
            {
                if (modal.Q("pause-resume") != null) ResumeFromPauseMenu();
                else { CloseModal(); if (pauseSession) OpenPauseMenu(); }
                return;
            }
            if (ContextMenuVisible) { CloseContextMenu(); return; }
            if (resourcesExpanded) { CloseResourceDrawer(); return; }
            if (activePanel != GameplayPanel.None)
            {
                SetPanel(activePanel);
                if (pauseSession) OpenPauseMenu();
                return;
            }
            if (context.Selection.SelectedCharacterIds.Count > 0 ||
                context.Selection.SelectedBuildingPresenter != null ||
                context.Selection.SelectedProductionSourcePresenter != null ||
                context.Selection.SelectedWorldResourcePilePresenter != null || context.Selection.SelectedWildlife != null)
            { context.Selection.ClearSelection(); RefreshNow(); return; }
            OpenPauseMenu();
        }

        public void OpenPauseMenu()
        {
            if (!pauseSession) pausedBeforeMenu = context.Clock().IsPaused;
            pauseSession = true;
            context.SetPaused(true);
            modal.Clear();
            modal.style.display = DisplayStyle.Flex;
            AddTitle(modal, "Пауза");
            modal.Add(ActionButton("Продолжить", ResumeFromPauseMenu, "pause-resume"));
            modal.Add(ActionButton("Сохранить игру", OpenPauseSaves, "pause-save"));
            modal.Add(ActionButton("Загрузить игру", OpenPauseSaves, "pause-load"));
            modal.Add(ActionButton("Настройки", OpenSettings, "pause-settings"));
            modal.Add(ActionButton("В главное меню", RequestReturnToMainMenu, "pause-main-menu"));
            FocusWhenReady("pause-resume", modal);
        }

        private void OpenPauseSaves()
        {
            CloseModal();
            if (activePanel != GameplayPanel.SaveLoad) SetPanel(GameplayPanel.SaveLoad);
        }

        private void ResumeFromPauseMenu()
        {
            CloseModal();
            context.SetPaused(pausedBeforeMenu);
            pauseSession = false;
        }

        private void BlockNavigationDuringDirectControl(NavigationMoveEvent evt)
        {
            if (!input.ThirdPersonActionsEnabled || pauseSession) return;
            evt.StopImmediatePropagation(); evt.PreventDefault();
        }

        private void BlockSubmitDuringDirectControl(NavigationSubmitEvent evt)
        {
            if (!input.ThirdPersonActionsEnabled || pauseSession) return;
            evt.StopImmediatePropagation(); evt.PreventDefault();
        }

        private string EventEffectSummary(EventChoiceDefinition choice)
        {
            var effect = choice.Effects.FirstOrDefault(x => x.Kind != EventEffectKind.None);
            if (effect == null) return "Без изменения показателей; продолжение истории.";
            if (effect.Kind == EventEffectKind.RelationshipEvent)
                return effect.Parameter == "family_support"
                    ? "Отношения +12. У обоих участников работа и исследования +10% на 2 игровых дня."
                    : "Отношения −15. У обоих участников работа и исследования −15% на 2 игровых дня.";
            var condition = context.Health.Catalog.Get(new Game.Domain.Health.HealthConditionId(effect.Parameter));
            return Content("condition", effect.Parameter) + ": работа " + Percent(condition.Effect.WorkPerThousand) +
                " · срок " + condition.Duration.TotalDays.ToString("0.#") + " дн.";
        }

        private string BuildingPurpose(BuildingDefinition definition)
        {
            var purpose = definition.Id.FamilyId.ToString() switch
            {
                "research_house" => "Рабочее место исследователей: изучение знаний после прибытия в здание.",
                "military_barracks" => "Подготовка военных и размещение гарнизона.",
                "campfire" => "Приготовление еды из открытых рецептов. Требует продукты и топливо.",
                "fishing_pier" => "Место для рыбной ловли с доступом к воде.",
                "fishing_camp" => "Рыбацкое рабочее место у берега.",
                "healer_hut" => "Лечение и уход за больными жителями.",
                _ => definition.Category switch
                {
                    BuildingCategory.Shelter => "Жильё для отдыха и ночлега жителей.",
                    BuildingCategory.Storage => "Место хранения и выдачи запасов поселения.",
                    BuildingCategory.Production => "Рабочее место для переработки ресурсов и изготовления изделий.",
                    BuildingCategory.Food => "Производство и подготовка пищи для поселения.",
                    BuildingCategory.Military => "Военная инфраструктура поселения.",
                    BuildingCategory.Medical => "Помощь больным и пострадавшим жителям.",
                    _ => "Обустройство поселения."
                }
            };
            if (definition.Housing != null) purpose += " Мест: " + definition.Housing.ResidentCapacity + ".";
            if (definition.Storage != null) purpose += " Вместимость: " + definition.Storage.Capacity + ".";
            if (definition.Workplace != null) purpose += " Работников: " + definition.Workplace.MaximumWorkers + ".";
            return purpose;
        }

        public static string SkillDisplayName(string id) => id switch
        {
            "gathering" => "Собирательство", "construction" => "Строительство",
            "combat" => "Боевое мастерство", "research" => "Исследования",
            "medicine" => "Медицина", "crafting" => "Ремесло", "cooking" => "Кулинария",
            "farming" => "Земледелие", "hunting" => "Охота", "fishing" => "Рыболовство",
            "hauling" => "Переноска", "mining" => "Горное дело", "woodcutting" => "Рубка леса",
            "leadership" => "Руководство", "scouting" => "Разведка", "social" => "Общение",
            _ => "Общие навыки"
        };

        private sealed class UiContinuity
        {
            public readonly Dictionary<string, bool> Foldouts = new();
            public readonly Dictionary<string, Vector2> ListScroll = new();
            public Vector2 MainScroll, InspectorScroll;
        }

        private UiContinuity CaptureUiContinuity()
        {
            var state = new UiContinuity { MainScroll = mainPanel?.scrollOffset ?? Vector2.zero,
                InspectorScroll = inspector?.scrollOffset ?? Vector2.zero };
            root.Query<Foldout>().ForEach(f => { if (!string.IsNullOrEmpty(f.name)) state.Foldouts[f.name] = f.value; });
            root.Query<ListView>().ForEach(list =>
            {
                var scroll = list.Q<ScrollView>();
                if (!string.IsNullOrEmpty(list.name) && scroll != null)
                    state.ListScroll[list.name] = scroll.scrollOffset;
            });
            return state;
        }

        private void RestoreUiContinuity(UiContinuity state)
        {
            root.Query<Foldout>().ForEach(f =>
            { if (!string.IsNullOrEmpty(f.name) && state.Foldouts.TryGetValue(f.name, out var expanded)) f.SetValueWithoutNotify(expanded); });
            var revision = ++continuityRevision;
            // Clear/rebuild temporarily shrinks the content to zero. Restore after layout,
            // and discard any deferred restoration superseded by a newer refresh.
            root.schedule.Execute(() =>
            {
                if (revision != continuityRevision) return;
                if (mainPanel != null) mainPanel.scrollOffset = state.MainScroll;
                if (inspector != null) inspector.scrollOffset = state.InspectorScroll;
                root.Query<ListView>().ForEach(list =>
                {
                    if (!string.IsNullOrEmpty(list.name) && state.ListScroll.TryGetValue(list.name, out var offset))
                    {
                        var scroll = list.Q<ScrollView>();
                        if (scroll != null) scroll.scrollOffset = offset;
                    }
                });
            }).ExecuteLater(0);
        }
    }
}
