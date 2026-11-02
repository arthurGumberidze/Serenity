using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Production;
using Game.Domain.Resources;

namespace Game.Simulation.Production
{
    /// <summary>Pure monthly demand/recipe calculation; does not mutate inventory, jobs or manual goals.</summary>
    public static class StockPlanner
    {
        public static readonly string[] DishOrder = { "cooked_fish", "cooked_meat", "cooked_berries", "dried_meat" };

        public static StockPlan Calculate(ProductionCatalog catalog, Func<ProductionRecipeId, bool> unlocked,
            StockPlanningPolicy policy, StockPlanningFacts facts, Func<ResourceId, long> manualTarget)
        {
            if (facts.Residents < 0) throw new ArgumentOutOfRangeException(nameof(facts.Residents));
            var result = new StockPlan(facts.Residents);
            // Only working recipes with real inputs; gathering sources are terminal resources in this graph.
            var recipes = catalog.All.Where(x => x.Enabled && unlocked(x.Id) && x.Inputs.Count > 0)
                .OrderBy(x => x.Id).ToArray();
            var producers = new Dictionary<ResourceId, ProductionRecipe>();
            foreach (var recipe in recipes)
                foreach (var output in recipe.Outputs)
                    if (!producers.ContainsKey(output.ResourceId)) producers.Add(output.ResourceId, recipe);
            // Field dressing is an existing alternative supply, never an additional hunting quota.
            var fieldButchery = recipes.FirstOrDefault(x => x.Id.ToString() == "butcher_field_carcass");
            var dishes = DishOrder.Where(id => producers.ContainsKey(new ResourceId(id))).ToArray();
            if (dishes.Length == 0)
            { result.Warnings.Add("Расчёт недоступен: нет открытых рецептов готовой еды. Изучите «Приготовление пищи» во вкладке «Знания», затем повторите расчёт. Текущие цели не изменены."); return result; }
            for (var day = 0; day < 30; day++)
            {
                var selected = policy.Menu.Count == 30 ? policy.Menu[day] : null;
                result.Menu.Add(dishes.Contains(selected) ? selected : dishes[day % dishes.Length]);
            }
            var monthly = new Dictionary<ResourceId, long>(facts.OneTimeNeeds);
            var near = new Dictionary<ResourceId, long>(facts.OneTimeNeeds);
            foreach (var pair in facts.DailyNeeds)
            { Add(monthly, pair.Key, checked(pair.Value * 30)); Add(near, pair.Key, checked(pair.Value * policy.StockDays)); }
            var offset = (int)(Math.Max(0, facts.Day - policy.FirstDay) % 30);
            for (var day = 0; day < 30; day++)
            {
                Add(monthly, new ResourceId(result.Menu[day]), checked(facts.Residents * 3L));
                if (day < policy.StockDays)
                    Add(near, new ResourceId(result.Menu[(day + offset) % 30]), checked(facts.Residents * 3L));
            }
            Expand(monthly, result.Monthly, producers, new Dictionary<ResourceId, long>(), null, null, null);
            Expand(near, result.Targets, producers, facts.Available, policy, manualTarget, fieldButchery);
            foreach (var need in facts.OneTimeNeeds.Where(x => x.Value > 0 && !producers.ContainsKey(x.Key) &&
                         catalog.All.Any(r => r.Inputs.Count > 0 && r.Outputs.Any(o => o.ResourceId == x.Key))))
                result.Warnings.Add(need.Key + ": нужный рецепт ещё не открыт. Цель сохранится до его открытия.");
            foreach (var pair in result.Targets)
                if (!policy.For(pair.Key).Automatic && manualTarget(pair.Key) < pair.Value)
                    result.Warnings.Add(pair.Key + ": ручная цель " + manualTarget(pair.Key) + " ниже расчёта " + pair.Value + ".");
            if (facts.Capacity > 0 && result.Targets.Sum(x => policy.For(x.Key).Automatic ? x.Value : manualTarget(x.Key)) > facts.Capacity)
                result.Warnings.Add("План превышает вместимость складов. Уменьшите горизонт или расширьте хранение.");
            if (policy.StockDays > 3)
                result.Warnings.Add("Готовая еда и туши портятся: длинный горизонт требует регулярного производства и свободного хранения.");
            return result;
        }

        private static void Expand(Dictionary<ResourceId, long> seeds, Dictionary<ResourceId, long> targets,
            Dictionary<ResourceId, ProductionRecipe> producers, IReadOnlyDictionary<ResourceId, long> stock,
            StockPlanningPolicy policy, Func<ResourceId, long> manualTarget, ProductionRecipe fieldButchery)
        {
            var demand = new Dictionary<ResourceId, long>(seeds);
            if (policy != null)
                foreach (var rule in policy.Resources.Where(x => !x.Automatic && manualTarget(x.Resource) > 0))
                    if (!demand.ContainsKey(rule.Resource)) demand.Add(rule.Resource, 0);
            // Consumer-before-input topological order sums shared inputs before rounding each batch.
            var order = new List<ResourceId>(); var visiting = new HashSet<ResourceId>(); var done = new HashSet<ResourceId>();
            void Visit(ResourceId id)
            {
                if (done.Contains(id)) return;
                if (!visiting.Add(id)) throw new InvalidOperationException("Cyclic stock recipe: " + id);
                if (producers.TryGetValue(id, out var recipe)) foreach (var input in recipe.Inputs) Visit(input.ResourceId);
                visiting.Remove(id); done.Add(id); order.Add(id);
            }
            foreach (var id in demand.Keys.OrderBy(x => x).ToArray()) Visit(id);
            order.Reverse();
            var credits = new Dictionary<ResourceId, long>();
            foreach (var id in order)
            {
                if (id.ToString() == "water" || id.ToString() == "spoiled_food") continue;
                var need = Math.Max(0, Get(demand, id) - Get(credits, id));
                var rule = policy?.For(id);
                var reserve = rule == null ? 0 : rule.ReservePercent >= 0 ? rule.ReservePercent : seeds.ContainsKey(id) ? policy.ReservePercent : 0;
                var target = checked((need * (100 + reserve) + 99) / 100);
                if (rule != null && !rule.Automatic) target = Math.Max(target, manualTarget(id));
                targets[id] = target;
                // A manual target is a warehouse quota, not permission to hide upstream menu needs.
                var deficit = Math.Max(0, target - Get(stock, id));
                if (id.ToString() == "meat" && deficit > 0 && fieldButchery != null)
                {
                    var meat = fieldButchery.Outputs.First(x => x.ResourceId == id).Quantity.Units;
                    var field = fieldButchery.Inputs.First();
                    var fieldBatches = Math.Min((deficit + meat - 1) / meat, Get(stock, field.ResourceId) / field.Quantity.Units);
                    deficit = Math.Max(0, deficit - fieldBatches * meat);
                    foreach (var byproduct in fieldButchery.Outputs.Where(x => x.ResourceId != id))
                        Add(credits, byproduct.ResourceId, checked(byproduct.Quantity.Units * fieldBatches));
                }
                if (deficit == 0 || !producers.TryGetValue(id, out var recipe)) continue;
                var output = recipe.Outputs.First(x => x.ResourceId == id).Quantity.Units;
                var batches = checked((deficit + output - 1) / output);
                foreach (var input in recipe.Inputs) Add(demand, input.ResourceId, checked(input.Quantity.Units * batches));
                foreach (var byproduct in recipe.Outputs.Where(x => x.ResourceId != id))
                    Add(credits, byproduct.ResourceId, checked(byproduct.Quantity.Units * batches));
            }
        }
        public static long Get(IReadOnlyDictionary<ResourceId, long> values, ResourceId id) => values.TryGetValue(id, out var n) ? n : 0;
        public static void Add(IDictionary<ResourceId, long> values, ResourceId id, long amount)
        { values[id] = checked((values.TryGetValue(id, out var n) ? n : 0) + amount); }
    }
}
