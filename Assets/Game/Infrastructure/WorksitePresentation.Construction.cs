using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Resources;
using Game.Presentation.Environment;
using UnityEngine;

namespace Game.Infrastructure
{
    public sealed partial class WorksitePresentation
    {
        private readonly Dictionary<(StableEntityId,ResourceId),GameObject> constructionMaterials = new();
        private void RefreshConstructionMaterials()
        {
            var visible = new HashSet<(StableEntityId,ResourceId)>();
            foreach(var building in root.Buildings.All.Where(x => !x.IsOperational && !x.IsDestroyed &&
                x.ConstructionPhase != Game.Domain.Buildings.ConstructionPhase.Cancelled))
            {
                var definition = root.BuildingCatalog.ResolveDefinition(building.DefinitionId);
                var center = root.BuildingSpawner.GridToWorld(building.Coordinate,definition,building.Orientation);
                if((center-root.CameraController.transform.position).sqrMagnitude>240f*240f)continue;
                TryInventory(building.ConstructionBufferOwner,out var buffer);
                var index = 0;
                foreach(var material in building.ConstructionMaterials)
                {
                    var units = material.InstalledUnits+(buffer?.GetAmount(material.ResourceId).Units??0);
                    if(units<=0)continue;
                    var key=(building.Id,material.ResourceId); visible.Add(key);
                    if(!constructionMaterials.TryGetValue(key,out var view))
                    {
                        var prefab=WorldPropCatalog.Current.Find("pile_"+material.ResourceId) ?? WorldPropCatalog.Current.Find("sack");
                        view=WorldMapScenery.Instantiate(prefab,transform);
                        view.name="Delivered construction materials "+material.ResourceId+" "+building.Id;
                        constructionMaterials.Add(key,view);
                    }
                    var offset=new Vector3((index%3-1)*.8f,0f,(index/3-1)*.8f);
                    view.transform.position=root.TiledWorld.ProjectToGround(center+offset);
                    view.transform.localScale=Vector3.one*Mathf.Clamp(.55f+units*.018f,.6f,1.3f);
                    view.SetActive(true); index++;
                }
            }
            foreach(var pair in constructionMaterials) if(pair.Value!=null&&!visible.Contains(pair.Key)) pair.Value.SetActive(false);
        }
    }
}
