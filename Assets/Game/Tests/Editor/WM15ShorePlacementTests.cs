using System;
using System.Linq;
using System.Collections.Generic;
using Game.Domain.World;
using Game.Domain.Buildings;
using Game.Infrastructure;
using Game.Presentation.Buildings;
using Game.Presentation.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Editor
{
    public sealed class WM15ShorePlacementTests
    {
        [Test] public void PreparedIndexOffersSixIndependentPlacesPerLakeWithoutTerrainRecalculation()
        {
            var world=TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var heights=new TiledWorldHeightSampler(world,1380062136);
            var origin=new TiledWorldPopulationPlanner(world,heights,1380062136).Create().StartTerritory.Center;
            var definition=AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>("Assets/Game/Art/Config/BuildingPresentationCatalog.asset").ResolveDefinition(new BuildingDefinitionId("fishing_camp"));
            var index=System.Threading.Tasks.Task.Run(()=>ShorePlacementIndex.Create(world,1380062136,definition.Footprint,1,origin.X,origin.Z)).GetAwaiter().GetResult();
            TestContext.WriteLine("Prepared candidates="+index.Candidates.Count+" cached cells="+index.Cells.Count);
            Assert.That(index.Cells.Count,Is.LessThan(65000));
            var host=new GameObject("Prepared shore validation");var surface=host.AddComponent<P102WorldPlacementSurface>();surface.Configure(heights);
            var unityOrigin=new Vector3(origin.X,0,origin.Z);surface.ImportShoreCells(index.Cells,1,unityOrigin);
            try
            {
                foreach(var lake in world.MapData.Hydrology.Lakes)
                {
                    var fixedPlaces=index.Candidates.Where(x=>x.WaterBodyId==lake.Id).ToArray();
                    Assert.That(fixedPlaces.Length,Is.EqualTo(6),lake.Id);
                    var occupied=new HashSet<GridCoordinate>();
                    foreach(var place in fixedPlaces)
                        foreach(var cell in definition.Footprint.EnumerateCells(place.Anchor,place.Facing))
                            Assert.That(occupied.Add(cell),Is.True,"Overlapping fishing places on "+lake.Id);
                    var found=0;
                    foreach(var point in lake.Shoreline.Points.Where((_,i)=>i%8==0))
                    {
                        var bank=world.MapData.CoordinateTransform.ToUnity(point);
                        var nearby=index.Candidates.Where(x=>(x.X-bank.X)*(x.X-bank.X)+(x.Z-bank.Z)*(x.Z-bank.Z)<60*60).ToArray();
                        if(nearby.Length==0)continue;
                        var candidate=nearby[0];var cells=definition.Footprint.EnumerateCells(candidate.Anchor,candidate.Facing).ToArray();
                        Assert.That(surface.PrepareFootprint(cells,1,unityOrigin,true,.000001),Is.True,"All terrain cells must already be ready");
                        Assert.That(surface.EvaluateFootprint(cells,1,unityOrigin,4.5f,false,true).CanPlace,Is.True);
                        found++;
                    }
                    TestContext.WriteLine(lake.Id+" nearby shore choices="+found);
                    Assert.That(found,Is.GreaterThanOrEqualTo(4),lake.Id);
                }
                Assert.That(index.Candidates.Any(x=>x.WaterBodyId==world.MapData.Hydrology.MainRiver.Id),Is.True);
            }
            finally{UnityEngine.Object.DestroyImmediate(host);}
        }

        [Test] public void EnlargedCampHasAtLeastFourDrySitesPerLake()
        {
            var world=TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var heights=new TiledWorldHeightSampler(world,1380062136);
            var plan=new TiledWorldPopulationPlanner(world,heights,1380062136).Create();
            var origin=plan.StartTerritory.Center;
            var definition=AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>("Assets/Game/Art/Config/BuildingPresentationCatalog.asset").ResolveDefinition(new BuildingDefinitionId("fishing_camp"));
            var surfaceHost=new GameObject("Shore validation");var surface=surfaceHost.AddComponent<P102WorldPlacementSurface>();surface.Configure(heights);
            try
            {
                foreach(var lake in world.MapData.Hydrology.Lakes)
                {
                    var center=world.MapData.CoordinateTransform.ToUnity(lake.Center);
                    var valid=new HashSet<GridCoordinate>();var failures=new Dictionary<string,int>();
                    foreach(var point in lake.Shoreline.Points)
                    {
                        var p=world.MapData.CoordinateTransform.ToUnity(point);
                        var bank=new Vector3(p.X,0,p.Z);var outward=(bank-new Vector3(center.X,0,center.Z)).normalized;
                        for(var distance=5;distance<=22;distance+=2)
                        for(var f=0;f<4;f++)
                        {
                            var facing=(BuildingOrientation)f;
                            if(Vector3.Dot(Quaternion.Euler(0,facing.Degrees(),0)*Vector3.forward,-outward)<.6f)continue;
                            var c=bank+outward*distance;var size=definition.Footprint.Rotate(facing);
                            var anchor=new GridCoordinate(Mathf.RoundToInt(c.x-origin.X-(size.Width-1)*.5f),Mathf.RoundToInt(c.z-origin.Z-(size.Depth-1)*.5f));
                            if(!ShoreBuildingPlacement.IsValid(heights,anchor,definition.Footprint,facing,1,origin.X,origin.Z))continue;
                            var cells=definition.Footprint.EnumerateCells(anchor,facing).ToArray();
                            surface.PrepareFootprint(cells,1,new Vector3(origin.X,0,origin.Z),true,0);
                            var evaluation=surface.EvaluateFootprint(cells,1,new Vector3(origin.X,0,origin.Z),4.5f,false,true);
                            if(evaluation.CanPlace)valid.Add(anchor);
                            else {failures.TryGetValue(evaluation.BlockedReason,out var count);failures[evaluation.BlockedReason]=count+1;}
                        }
                    }
                    TestContext.WriteLine(lake.Id+" valid="+valid.Count+" blocked="+string.Join(";",failures.Select(x=>x.Key+":"+x.Value)));
                    Assert.That(valid.Count,Is.GreaterThanOrEqualTo(4),lake.Id+" "+string.Join(";",failures.Select(x=>x.Key+":"+x.Value)));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(surfaceHost); }
        }

        [Test] public void CompactAdaptivePierOffersManyDryPlacesPerLakeAcrossSteepBanks()
        {
            var world=TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var heights=new TiledWorldHeightSampler(world,1380062136);
            var origin=new TiledWorldPopulationPlanner(world,heights,1380062136).Create().StartTerritory.Center;
            var definition=AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>("Assets/Game/Art/Config/BuildingPresentationCatalog.asset")
                .ResolveDefinition(new BuildingDefinitionId("fishing_pier"));
            Assert.That(definition.Footprint.Width,Is.EqualTo(4));
            Assert.That(definition.Footprint.Depth,Is.EqualTo(4));
            var index=ShorePlacementIndex.Create(world,1380062136,definition.Footprint,1,origin.X,origin.Z,true);
            foreach(var lake in world.MapData.Hydrology.Lakes)
            {
                var candidates=index.Candidates.Where(x=>x.WaterBodyId==lake.Id).ToArray();
                Assert.That(candidates.Length,Is.GreaterThanOrEqualTo(18),lake.Id);
                foreach(var candidate in candidates)
                    Assert.That(ShoreBuildingPlacement.IsValid(heights,candidate.Anchor,definition.Footprint,
                        candidate.Facing,1,origin.X,origin.Z,true),Is.True,lake.Id);
            }
        }
    }
}
