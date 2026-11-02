using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Resources;

namespace Game.Domain.Production
{
    [Serializable]
    public sealed class StockPlanningConfig
    {
        public StockGroupDefinition[] groups;
        public StockResourceDefinition[] resources;
        public int arrowsPerHunter = 20;
        public int medicinesPerCase = 2;
        public int residentsPerReserveMedicine = 10;
    }
    [Serializable]
    public sealed class StockGroupDefinition { public string id; public string name; public string icon; }
    [Serializable]
    public sealed class StockResourceDefinition { public string id; public string name; public string group; public string consumer; public string rule; }

    public sealed class StockResourcePolicy
    {
        public StockResourcePolicy(ResourceId resource, bool automatic = false, int priority = 2, int reservePercent = -1)
        {
            if (!resource.IsValid || priority < 0 || priority > 4 || reservePercent < -1 || reservePercent > 200)
                throw new ArgumentOutOfRangeException(nameof(priority));
            Resource = resource; Automatic = automatic; Priority = priority; ReservePercent = reservePercent;
        }
        public ResourceId Resource { get; }
        public bool Automatic { get; }
        public int Priority { get; }
        public int ReservePercent { get; }
    }

    /// <summary>Saved preferences. A calculation is a proposal until explicitly applied.</summary>
    public sealed class StockPlanningPolicy
    {
        private readonly Dictionary<ResourceId, StockResourcePolicy> byResource;
        public StockPlanningPolicy(int stockDays = 3, int reservePercent = 25, bool autoUpdate = false,
            long firstDay = 0, IEnumerable<StockResourcePolicy> resources = null, IEnumerable<string> menu = null)
        {
            if (stockDays < 1 || stockDays > 30 || reservePercent < 0 || reservePercent > 200 || firstDay < 0)
                throw new ArgumentOutOfRangeException(nameof(stockDays));
            StockDays = stockDays; ReservePercent = reservePercent; AutoUpdate = autoUpdate; FirstDay = firstDay;
            Resources = Array.AsReadOnly((resources ?? Array.Empty<StockResourcePolicy>()).OrderBy(x => x.Resource).ToArray());
            if (Resources.Select(x => x.Resource).Distinct().Count() != Resources.Count)
                throw new ArgumentException("Duplicate stock policy.");
            byResource = Resources.ToDictionary(x => x.Resource);
            HasMealRules = Resources.Any(x => x.Resource.ToString() == "cooked_fish" || x.Resource.ToString() == "cooked_meat" ||
                x.Resource.ToString() == "cooked_berries" || x.Resource.ToString() == "dried_meat");
            Menu = Array.AsReadOnly((menu ?? Array.Empty<string>()).ToArray());
            if (Menu.Count != 0 && Menu.Count != 30) throw new ArgumentException("Menu must cover 30 days.");
        }
        public int StockDays { get; }
        public int ReservePercent { get; }
        public bool AutoUpdate { get; }
        public long FirstDay { get; }
        public IReadOnlyList<StockResourcePolicy> Resources { get; }
        public IReadOnlyList<string> Menu { get; }
        public bool HasMealRules { get; }
        public int PriorityFor(ResourceId id) => byResource.TryGetValue(id, out var rule) ? rule.Priority : 2;
        public bool IsAutomatic(ResourceId id) => byResource.TryGetValue(id, out var rule) && rule.Automatic;
        public StockResourcePolicy For(ResourceId id) => byResource.TryGetValue(id, out var rule) ? rule : new StockResourcePolicy(id);
        public StockPlanningPolicy WithResource(StockResourcePolicy value) => new StockPlanningPolicy(StockDays,
            ReservePercent, AutoUpdate, FirstDay, Resources.Where(x => x.Resource != value.Resource).Append(value), Menu);
    }

    public sealed class StockPlanningFacts
    {
        public int Residents { get; set; }
        public long Day { get; set; }
        public long Capacity { get; set; }
        public Dictionary<ResourceId, long> Available { get; } = new Dictionary<ResourceId, long>();
        // Confirmed construction and role equipment requirements; no speculative world resource quotas.
        public Dictionary<ResourceId, long> OneTimeNeeds { get; } = new Dictionary<ResourceId, long>();
        public Dictionary<ResourceId, long> DailyNeeds { get; } = new Dictionary<ResourceId, long>();
    }

    public sealed class StockPlan
    {
        public StockPlan(int residents) { Residents = residents; }
        public List<string> Menu { get; } = new List<string>();
        public Dictionary<ResourceId, long> Monthly { get; } = new Dictionary<ResourceId, long>();
        public Dictionary<ResourceId, long> Targets { get; } = new Dictionary<ResourceId, long>();
        public List<string> Warnings { get; } = new List<string>();
        public int Residents { get; }
        public long Portions => checked((long)Residents * 3 * 30);
        public bool CanApply => Menu.Count == 30;
    }
}
