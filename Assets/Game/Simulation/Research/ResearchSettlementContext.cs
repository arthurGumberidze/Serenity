using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Research;
using Game.Domain.Resources;

namespace Game.Simulation.Research
{
    public interface IResearchSettlementContext
    {
        long Fact(string key);
        bool CanPay(ResearchTechnologyProfile profile);
        bool TryPay(ResearchTechnologyProfile profile);
    }

    /// <summary>Reads canonical settlement facts and pays from the existing authorised funding sources.</summary>
    public sealed class ResearchSettlementContext : IResearchSettlementContext
    {
        private readonly ResourceInventoryRegistry inventories;
        private readonly ResourceCatalog resources;
        private readonly Func<IEnumerable<InventoryOwner>> sources;
        private readonly Func<string,long> settlementFact;
        public ResearchSettlementContext(ResourceInventoryRegistry inventories, ResourceCatalog resources,
            Func<IEnumerable<InventoryOwner>> sources, Func<string,long> settlementFact)
        { this.inventories=inventories; this.resources=resources; this.sources=sources; this.settlementFact=settlementFact; }
        private ResourceInventory[] Stocks() => sources().Distinct().Where(x=>inventories.TryGet(x,out _))
            .Select(x=>inventories.Get(x)).Where(x=>!x.IssuanceLocked).ToArray();
        public long Fact(string key)
        {
            var resource=key=="hides"?"hide":key=="herbs"?"medicinal_plants":null;
            if(resource!=null) return Stocks().Sum(x=>x.GetAmount(new ResourceId(resource)).Units);
            if(key=="food") return Stocks().Sum(x=>x.CaptureState().Amounts
                .Where(a=>resources.Get(a.ResourceId).Category==ResourceCategory.Food).Sum(a=>a.Quantity.Units));
            return settlementFact(key);
        }
        public bool CanPay(ResearchTechnologyProfile p) => p==null ||
            Stocks().Sum(x=>x.GetAmount(new ResourceId("wood_log")).Units)>=p.WoodCost &&
            Stocks().Sum(x=>x.GetAmount(new ResourceId("stone")).Units)>=p.StoneCost;
        public bool TryPay(ResearchTechnologyProfile p)
        {
            if(!CanPay(p))return false;
            if(p==null)return true;
            // Synchronous simulation-thread transaction: validate both resources before removing either.
            foreach(var pair in new[]{Tuple.Create("wood_log",p.WoodCost),Tuple.Create("stone",p.StoneCost)})
            {
                long remaining=pair.Item2;var id=new ResourceId(pair.Item1);
                foreach(var stock in Stocks())
                {
                    var take=Math.Min(remaining,stock.GetAmount(id).Units);if(take<=0)continue;
                    if(!stock.TryRemove(id,new ResourceQuantity(take)))throw new InvalidOperationException("Research funding changed during transaction.");
                    remaining-=take;if(remaining==0)break;
                }
            }
            return true;
        }
    }
}
