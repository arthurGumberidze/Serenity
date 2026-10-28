using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Production;

namespace Game.Simulation.Production
{
    public sealed partial class ProductionService
    {
        // Immutable world associations are regenerated on load; quantities stay in saved ProductionSites.
        private readonly Dictionary<string,StableEntityId> fishWaterBodies = new(StringComparer.Ordinal);
        public Func<WorldPosition,string> WaterBodyAt { get; set; }
        public Func<WorldPosition,bool> IsWaterAt { get; set; }
        public void RegisterFishWater(string waterId, StableEntityId siteId) => fishWaterBodies[waterId] = siteId;
        public ProductionSite FishStockAt(WorldPosition position)
        {
            var waterId = WaterBodyAt?.Invoke(position);
            return waterId != null && fishWaterBodies.TryGetValue(waterId,out var id) && sites.TryGetValue(id,out var stock) ? stock : null;
        }
        private ProductionSite FishStockFor(ProductionSite site) => site.RecipeId.ToString()=="fish_water" ? site : FishStockAt(site.Position);
        public void SetFisheryBreeding(StableEntityId buildingId,bool breeding)
        {
            foreach(var site in orderedSites.Where(x=>x.OwnerBuildingId==buildingId))
                if(site.RecipeId.ToString()=="fish_camp" || site.RecipeId.ToString()=="fish_breed")
                {
                    var paused=(site.RecipeId.ToString()=="fish_breed")!=breeding;
                    SetWorkplacePaused(buildingId,site.RecipeId,paused);
                    if(paused) CancelTasksForSite(site.Id);
                }
        }

        public void MaintainFisheryModes()
        {
            foreach (var group in orderedSites.Where(x => x.Kind == ProductionSiteKind.BuildingWorkplace &&
                         (x.RecipeId.ToString() == "fish_camp" || x.RecipeId.ToString() == "fish_breed"))
                         .GroupBy(x => x.OwnerBuildingId).OrderBy(x => x.Key))
            {
                var stock = FishStockFor(group.First());
                if (stock == null) continue;
                var breeding = group.Any(x => x.RecipeId.ToString() == "fish_breed" && !x.Paused);
                if (!breeding && stock.RemainingAmount <= 20) SetFisheryBreeding(group.Key, true);
                else if (breeding && stock.RemainingAmount >= Math.Min(80, stock.SourceCapacity))
                    SetFisheryBreeding(group.Key, false);
            }
        }
    }
}
