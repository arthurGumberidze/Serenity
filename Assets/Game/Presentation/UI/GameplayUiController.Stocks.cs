using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Simulation.Production;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.UI
{
    public sealed partial class GameplayUiController
    {
        private string resourceGroup = "food", stockGroup = "food", drawnResourceGroup;
        private bool resourceHover, resourcePinned;
        private StockPlanningPolicy stockDraft;
        private StockPlan stockPreview;
        private Label stockSummary;
        private VisualElement stockPreviewView;
        private StockPlanningConfig StockConfig => context.Production.StockConfig;
        private static readonly List<string> StockPriorities = new List<string> { "Стоп", "Низкий", "Обычный", "Высокий", "Срочный" };

        private void BuildResourceGroups()
        {
            resourceStrip.style.flexWrap = Wrap.Wrap;
            resourceStrip.style.overflow = Overflow.Visible;
            resourceStrip.style.flexGrow = 1;
            resourceStrip.style.minHeight = 48;
            if (StockConfig == null) return;
            foreach (var group in StockConfig.groups)
            {
                var id = group.id;
                var button = ActionButton(string.Empty, () => { resourcePinned = true; OpenResourceGroup(id); }, "resource-group-" + id);
                button.AddToClassList("serenity-stock-group");
                button.Add(SerenityIconLibrary.Image(GroupIcon(id), group.name));
                var text = new Label(group.name) { name = "resource-group-value-" + id };
                text.style.whiteSpace = WhiteSpace.Normal;
                button.Add(text);
                button.tooltip = group.name + " · навести или нажать для всех ресурсов";
                button.RegisterCallback<PointerEnterEvent>(_ => { resourceHover = true; if (!resourcePinned) OpenResourceGroup(id); });
                button.RegisterCallback<PointerLeaveEvent>(_ => { resourceHover = false; ScheduleResourceClose(); });
                button.RegisterCallback<FocusInEvent>(_ => OpenResourceGroup(id));
                resourceStrip.Add(button);
            }
        }
        private static string GroupIcon(string id) => id switch
        { "food" => "food", "materials" => "stone", "craft" => "work", "equipment" => "military", "health" => "domestic", _ => "production" };
        private static string StockCompact(long n) => n < 10000 ? n.ToString("N0") : n < 1000000 ? (n / 1000d).ToString("0.#") + " тыс." : (n / 1000000d).ToString("0.#") + " млн";
        private void RefreshResourceGroups()
        {
            if (StockConfig == null) return;
            foreach (var group in StockConfig.groups)
            {
                var total = StockConfig.resources.Where(x => x.group == group.id).Sum(x => TotalResource(new ResourceId(x.id)));
                var label = resourceStrip.Q<Label>("resource-group-value-" + group.id);
                if (label != null) label.text = group.name + "  " + StockCompact(total);
            }
        }
        private void OpenResourceGroup(string id)
        {
            resourceGroup = id; resourcesExpanded = true; UpdateResourceDrawer();
            resourceDrawer.BringToFront();
        }
        private void CloseResourceDrawer()
        { resourcePinned = false; resourcesExpanded = false; if (resourceDrawer != null) resourceDrawer.style.display = DisplayStyle.None; }
        private void ScheduleResourceClose() => root.schedule.Execute(() =>
        { if (!resourceHover && !resourcePinned) CloseResourceDrawer(); }).StartingIn(250);

        private void UpdateResourceDrawer()
        {
            if (resourceDrawer == null || StockConfig == null) return;
            resourceDrawer.style.display = resourcesExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            if (!resourcesExpanded) return;
            if (drawnResourceGroup != resourceGroup || resourceDrawer.childCount == 0)
            {
                drawnResourceGroup = resourceGroup; resourceDrawer.Clear();
                var header = Row();
                var title = new Label(StockConfig.groups.First(x => x.id == resourceGroup).name);
                title.style.flexGrow = 1; title.style.fontSize = 22; header.Add(title);
                header.Add(ActionButton("×", CloseResourceDrawer, "resource-drawer-close")); resourceDrawer.Add(header);
                var scroll = new ScrollView { name = "resource-breakdown" };
                scroll.style.maxHeight = 330;
                foreach (var entry in StockConfig.resources.Where(x => x.group == resourceGroup))
                {
                    var row = new VisualElement(); row.AddToClassList("serenity-stock-breakdown-row");
                    var name = new Label(entry.name); name.style.flexGrow = 1; row.Add(name);
                    var label = new Label { name = "stock-breakdown-" + entry.id }; label.style.minWidth = 130; row.Add(label);
                    row.tooltip = entry.consumer + ". " + entry.rule; scroll.Add(row);
                }
                resourceDrawer.Add(scroll);
                resourceDrawer.Add(ActionButton("Открыть запасы и цели", () =>
                { stockGroup = resourceGroup; CloseResourceDrawer(); SetPanel(GameplayPanel.Economy); }, "resource-open-stocks"));
            }
            foreach (var entry in StockConfig.resources.Where(x => x.group == resourceGroup))
            {
                var id = new ResourceId(entry.id);
                var total = TotalResource(id);
                var stored = context.StoredResources.GetStoredTotal(id).Units;
                var label = resourceDrawer.Q<Label>("stock-breakdown-" + entry.id);
                label.text = total.ToString("N0"); label.tooltip = "На складах: " + stored.ToString("N0") + " · всего: " + total.ToString("N0");
            }
        }

        private void DrawStockWorkspace()
        {
            if (StockConfig == null) { AddLine(mainPanel, "Подготовка ресурсов…"); return; }
            stockDraft = context.Production.StockPolicy;
            foreach (var entry in StockConfig.resources)
                if (!stockDraft.Resources.Any(x => x.Resource.ToString() == entry.id))
                    stockDraft = stockDraft.WithResource(new StockResourcePolicy(new ResourceId(entry.id)));
            stockPreview = null;
            var workspace = new VisualElement { name = "stock-workspace" }; mainPanel.Add(workspace);
            AddTitle(workspace, "Запасы поселения");
            stockSummary = new Label { name = "stock-summary" }; workspace.Add(stockSummary);
            AddMuted(workspace, "Ручные цели сохраняются. Расчёт меняет только строки, включённые в авторежим.");
            workspace.Add(StockCalculationStatus("stock-calculation-status"));
            var tabs = Row();
            foreach (var group in StockConfig.groups)
            {
                var id = group.id;
                var button = ActionButton(group.name, () => { stockGroup = id; RebuildStockRows(); }, "stock-tab-" + id);
                tabs.Add(button);
            }
            workspace.Add(tabs);
            workspace.Add(new VisualElement { name = "stock-table" });
            RebuildStockRows();
            var planner = SerenityUiDesign.Card(); planner.name = "stock-planner";
            AddSection(planner, "Автоматический расчёт");
            AddMuted(planner, "Месяц: 30 дней · 3 порции на жителя в день · один открытый вид блюда на день.");
            var options = Row();
            var days = new IntegerField("Запас на дней") { value = stockDraft.StockDays, name = "stock-days", isDelayed = true };
            var reserve = new IntegerField("Надбавка, %") { value = stockDraft.ReservePercent, name = "stock-reserve", isDelayed = true };
            var automatic = new Toggle("Обновлять автоматически") { value = stockDraft.AutoUpdate, name = "stock-auto-update" };
            automatic.AddToClassList("serenity-stock-auto");
            StyleStockField(days, 250); StyleStockField(reserve, 250); StyleStockField(automatic, 300);
            void OptionsChanged()
            {
                stockDraft = new StockPlanningPolicy(Math.Max(1, Math.Min(30, days.value)), Math.Max(0, Math.Min(200, reserve.value)),
                    automatic.value, stockDraft.FirstDay, stockDraft.Resources, stockDraft.Menu);
                days.SetValueWithoutNotify(stockDraft.StockDays); reserve.SetValueWithoutNotify(stockDraft.ReservePercent);
                InvalidateStockPreview();
            }
            days.RegisterValueChangedCallback(_ => OptionsChanged()); reserve.RegisterValueChangedCallback(_ => OptionsChanged());
            automatic.RegisterValueChangedCallback(_ => OptionsChanged());
            options.Add(days); options.Add(reserve); options.Add(automatic); planner.Add(options);
            var actions = Row();
            actions.Add(ActionButton("Включить авто для всех ресурсов", () =>
            {
                foreach (var entry in StockConfig.resources)
                { var rule = stockDraft.For(new ResourceId(entry.id)); stockDraft = stockDraft.WithResource(new StockResourcePolicy(rule.Resource, true, rule.Priority, rule.ReservePercent)); }
                InvalidateStockPreview(); RebuildStockRows();
            }, "stock-enable-auto"));
            actions.Add(ActionButton("Рассчитать автоматически", CalculateStockPreview, "stock-calculate"));
            var apply = ActionButton("Применить автоцели", ApplyStockPreview, "stock-apply"); apply.SetEnabled(false); actions.Add(apply);
            planner.Add(actions);
            planner.Add(StockCalculationStatus("stock-planner-status"));
            AddMuted(planner, "Обновление — каждый игровой час. Нормы: лук и 20 стрел охотнику, копьё и щит военному, одежда жителям. " +
                "Лекарства: 2 на текущий случай и 1 на 10 жителей. Это нормы запаса; фактическая выдача и лечение работают по своим правилам.");
            AddMuted(planner, "Месячный спрос включает сырьё и топливо. На склад применяется ближайший горизонт меню, а не весь месяц сразу.");
            stockPreviewView = new VisualElement { name = "stock-preview" }; planner.Add(stockPreviewView); workspace.Add(planner);
            UpdateStockNumbers();
        }

        private void StyleStockField(VisualElement field, float width)
        {
            SerenityUiDesign.StyleField(field); field.style.width = width; field.style.minWidth = width;
            field.style.flexShrink = 0; field.style.marginRight = 8;
        }
        private static Label StockCalculationStatus(string name)
        {
            var label = new Label { name = name };
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.color = new Color(0.93f, 0.8f, 0.52f);
            label.style.display = DisplayStyle.None;
            return label;
        }
        private void UpdateStockCalculationStatus()
        {
            var blocked = stockPreview != null && !stockPreview.CanApply;
            var reason = blocked ? string.Join("\n", stockPreview.Warnings) : string.Empty;
            foreach (var name in new[] { "stock-calculation-status", "stock-planner-status" })
            {
                var label = mainPanel.Q<Label>(name);
                if (label == null) continue;
                label.text = reason;
                label.style.display = blocked ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
        private void RebuildStockRows()
        {
            var table = mainPanel.Q<VisualElement>("stock-table"); if (table == null) return;
            table.Clear();
            foreach (var entry in StockConfig.resources.Where(x => x.group == stockGroup))
            {
                var id = new ResourceId(entry.id); var rule = stockDraft.For(id);
                var row = new VisualElement { name = "stock-row-" + entry.id }; row.AddToClassList("serenity-stock-row");
                var identity = new VisualElement(); identity.style.width = 260; identity.style.flexGrow = 1;
                var title = new Label(entry.name); title.style.fontSize = 17; identity.Add(title);
                var amount = new Label { name = "stock-amount-" + entry.id }; amount.style.fontSize = 12; identity.Add(amount);
                identity.tooltip = entry.consumer + ". " + entry.rule; row.Add(identity);
                var target = new LongField("Цель") { value = context.Production.GetManualTarget(id), isDelayed = true, name = "stock-target-" + entry.id };
                var mode = new Toggle("Авто") { value = rule.Automatic, name = "stock-mode-" + entry.id };
                mode.AddToClassList("serenity-stock-auto");
                var priority = new PopupField<string>(StockPriorities, rule.Priority) { name = "stock-priority-" + entry.id };
                var buffer = new TextField("Запас %") { value = rule.ReservePercent < 0 ? "" : rule.ReservePercent.ToString(), isDelayed = true, name = "stock-buffer-" + entry.id };
                buffer.textEdition.placeholder = "Общий";
                buffer.tooltip = "Пусто — общая надбавка конечных товаров, для сырья 0%. Или собственный процент от 0 до 200.";
                StyleStockField(target, 150); StyleStockField(mode, 100); StyleStockField(priority, 175); StyleStockField(buffer, 180);
                target.RegisterValueChangedCallback(evt =>
                {
                    context.Production.SetTarget(id, Math.Max(0, evt.newValue));
                    target.SetValueWithoutNotify(context.Production.GetManualTarget(id));
                    var r = stockDraft.For(id); stockDraft = stockDraft.WithResource(new StockResourcePolicy(id, false, r.Priority, r.ReservePercent));
                    mode.SetValueWithoutNotify(false); InvalidateStockPreview();
                });
                void RuleChanged()
                {
                    var percent = int.TryParse(buffer.value, out var typed) ? Math.Max(0, Math.Min(200, typed)) : -1;
                    buffer.SetValueWithoutNotify(percent < 0 ? "" : percent.ToString());
                    stockDraft = stockDraft.WithResource(new StockResourcePolicy(id, mode.value, priority.index, percent));
                    InvalidateStockPreview();
                }
                mode.RegisterValueChangedCallback(_ => RuleChanged()); priority.RegisterValueChangedCallback(_ =>
                {
                    RuleChanged();
                    var current = context.Production.StockPolicy.For(id);
                    context.Production.SetStockPolicy(context.Production.StockPolicy.WithResource(
                        new StockResourcePolicy(id, current.Automatic, priority.index, current.ReservePercent)));
                });
                buffer.RegisterValueChangedCallback(_ => RuleChanged());
                row.Add(target); row.Add(mode); row.Add(priority); row.Add(buffer);
                var calculation = new Label { name = "stock-calculated-" + entry.id }; calculation.style.minWidth = 130; row.Add(calculation);
                table.Add(row);
            }
            UpdateStockNumbers();
        }
        private void UpdateStockNumbers()
        {
            if (StockConfig == null || stockSummary == null) return;
            var storage = context.Inventories.All.Where(x => x.Owner.Kind == InventoryOwnerKind.BuildingStorage).ToArray();
            var residents = PlayerAliveCount();
            var food = StockPlanner.DishOrder.Sum(id => TotalResource(new ResourceId(id)));
            stockSummary.text = "Жителей: " + residents + " · готовой еды: " + food.ToString("N0") +
                " · рацион на " + (residents == 0 ? "0" : (food / (residents * 3d)).ToString("0.#")) +
                " дн. · склады: " + storage.Sum(x => x.TotalUnits).ToString("N0") + " / " + storage.Sum(x => x.Capacity).ToString("N0");
            foreach (var entry in StockConfig.resources.Where(x => x.group == stockGroup))
            {
                var id = new ResourceId(entry.id); var label = mainPanel.Q<Label>("stock-amount-" + entry.id);
                if (label == null) continue;
                label.text = "Склад " + context.StoredResources.GetStoredTotal(id).Units.ToString("N0") + " · всего " + TotalResource(id).ToString("N0");
                var construction = context.Production.ConstructionTarget?.Invoke(id) ?? 0;
                if (construction > 0) label.text += " · на стройки " + construction.ToString("N0");
                var calculation = mainPanel.Q<Label>("stock-calculated-" + entry.id);
                calculation.text = stockPreview == null ? "" : !stockPreview.CanApply ? "Расчёт: —" :
                    "Расчёт: " + StockPlanner.Get(stockPreview.Targets, id).ToString("N0");
                calculation.tooltip = stockPreview == null ? "" : !stockPreview.CanApply ? string.Join("\n", stockPreview.Warnings) :
                    StockPlanner.Get(stockPreview.Targets, id) == 0 ?
                    "На ближайшие " + stockDraft.StockDays + " дн. дополнительный запас не требуется: ресурс не нужен выбранному меню и текущим задачам либо потребность покрыта имеющимися продуктами." :
                    "Предлагаемая цель запаса. Для автоматических строк применяется кнопкой «Применить автоцели».";
            }
        }
        private void InvalidateStockPreview()
        {
            stockPreview = null; mainPanel.Q<Button>("stock-apply")?.SetEnabled(false);
            stockPreviewView?.Clear(); UpdateStockCalculationStatus(); UpdateStockNumbers();
        }
        private void CalculateStockPreview()
        {
            if (stockDraft.Menu.Count == 0)
                stockDraft = new StockPlanningPolicy(stockDraft.StockDays, stockDraft.ReservePercent, stockDraft.AutoUpdate,
                    context.Clock().State.CalendarTicks / TimeSpan.TicksPerDay, stockDraft.Resources);
            stockPreview = context.Production.CalculateStockPlan(stockDraft);
            DrawStockPreview(); UpdateStockCalculationStatus(); UpdateStockNumbers();
            mainPanel.Q<Button>("stock-apply").SetEnabled(stockPreview.CanApply);
        }
        private void DrawStockPreview()
        {
            stockPreviewView.Clear();
            if (stockPreview.CanApply)
            {
                AddSection(stockPreviewView, "Меню на месяц · " + stockPreview.Portions.ToString("N0") + " порций");
                var grid = Row(); grid.style.flexWrap = Wrap.Wrap;
                var available = StockPlanner.DishOrder.Where(id => context.Production.Recipes.All.Any(r =>
                    r.Outputs.Any(x => x.ResourceId.ToString() == id) && context.Research.IsProductionRecipeUnlocked(r.Id))).ToArray();
                var names = available.Select(id => StockConfig.resources.First(x => x.id == id).name).ToList();
                for (var day = 0; day < 30; day++)
                {
                    var index = day;
                    var box = new VisualElement(); box.style.width = 280; box.style.marginRight = 8; box.style.marginBottom = 8;
                    box.Add(new Label("День " + (day + 1) + " · " + (stockPreview.Residents * 3) + " порций"));
                    var dish = new PopupField<string>(names, Array.IndexOf(available, stockPreview.Menu[day])) { name = "stock-menu-day-" + day };
                    SerenityUiDesign.StyleField(dish);
                    dish.RegisterValueChangedCallback(_ =>
                    {
                        var menu = stockPreview.Menu.ToArray(); menu[index] = available[dish.index];
                        stockDraft = new StockPlanningPolicy(stockDraft.StockDays, stockDraft.ReservePercent, stockDraft.AutoUpdate,
                            stockDraft.FirstDay, stockDraft.Resources, menu);
                        CalculateStockPreview();
                    });
                    box.Add(dish); grid.Add(box);
                }
                stockPreviewView.Add(grid);
                var totals = new Foldout { text = "Месячная потребность: блюда, сырьё и топливо", value = true };
                foreach (var item in stockPreview.Monthly.Where(x => x.Value > 0).OrderBy(x => x.Key))
                    AddLine(totals, StockName(item.Key) + ": " + item.Value.ToString("N0"));
                stockPreviewView.Add(totals);
                var changes = new Foldout { text = "Предлагаемые цели на " + stockDraft.StockDays + " дн.", value = true };
                foreach (var entry in StockConfig.resources.Where(x => stockDraft.For(new ResourceId(x.id)).Automatic)
                    .OrderByDescending(x => stockDraft.For(new ResourceId(x.id)).Priority))
                {
                    var id = new ResourceId(entry.id); var target = StockPlanner.Get(stockPreview.Targets, id);
                    AddLine(changes, entry.name + ": " + context.Production.GetManualTarget(id).ToString("N0") + " → " + target.ToString("N0"));
                }
                stockPreviewView.Add(changes);
            }
            foreach (var warning in stockPreview.Warnings)
            {
                var text = warning;
                foreach (var entry in StockConfig.resources.OrderByDescending(x => x.id.Length)) text = text.Replace(entry.id + ":", entry.name + ":");
                AddMuted(stockPreviewView, text);
            }
        }
        private string StockName(ResourceId id) => StockConfig.resources.FirstOrDefault(x => x.id == id.ToString())?.name ?? id.ToString();
        private void ApplyStockPreview()
        {
            if (stockPreview == null || !stockPreview.CanApply) return;
            // Recalculate from current canonical facts before applying; never apply a stale cached total.
            if (!context.Production.ApplyStockPlan(stockDraft)) { CalculateStockPreview(); return; }
            stockDraft = context.Production.StockPolicy; RebuildStockRows(); CalculateStockPreview();
            SetStatus("Автоматические цели применены. Ручные цели сохранены.");
        }
    }
}
