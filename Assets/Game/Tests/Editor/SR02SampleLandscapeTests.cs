using System;
using System.Linq;
using Game.Domain.World;
using Game.Infrastructure;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Editor
{
    public sealed class SR02SampleLandscapeTests
    {
        private TiledWorldDefinition world;
        private TiledWorldHeightSampler current, original;
        [OneTimeSetUp] public void Setup()
        {
            world=TiledWorldDefinitionLoader.LoadActive();
            current=new TiledWorldHeightSampler(world,1380062136);
            original=new TiledWorldHeightSampler(world,1380062136,false);
            Assert.That(current.Landscape,Is.Not.Null);
        }
        [Test] public void OutsideMaskAndProtectedPoiRemainExactlyOriginal()
        {
            var paint=new WorldMapTerrainPaintSampler(world,current);
            var oldPaint=new WorldMapTerrainPaintSampler(world,original);
            for(var z=-120;z<=880;z+=10)for(var x=-570;x<=430;x+=10)
            {
                if(current.Landscape.Weight(world,x,z)>0)continue;
                Assert.That(current.SampleHeight(x,z),Is.EqualTo(original.SampleHeight(x,z)),$"height {x},{z}");
                Assert.That(current.IsWater(x,z),Is.EqualTo(original.IsWater(x,z)));
                Assert.That(paint.SampleWeights(x,z),Is.EqualTo(oldPaint.SampleWeights(x,z)),$"paint {x},{z}");
            }
        }
        [Test] public void ClearingMeetsEightDegreeSlopeAndCanonicalPlacementPolicy()
        {
            var policy=new WorldMapBuildabilityEvaluator(world,current);
            for(var z=475;z<=505;z+=2)for(var x=-140;x<=-100;x+=2)
            {
                Assert.That(current.IsWater(x,z),Is.False);
                Assert.That(current.SampleSlopeDegrees(x,z),Is.LessThanOrEqualTo(8),$"{x},{z}");
                Assert.That(policy.EvaluateFootprint(new[]{new WorldMapFootprintCell(x,z,1)}).CanPlace,
                    Is.True,$"{x},{z}");
            }
        }
        [Test] public void SeamAndFourAdjacentHeightSamplesAreContinuous()
        {
            var west=current.CreateNormalizedHeights(new WorldTileId(1,2),257);
            var east=current.CreateNormalizedHeights(new WorldTileId(2,2),257);
            for(var z=0;z<257;z++) Assert.That(west[z,256],Is.EqualTo(east[z,0]));
            for(var z=480;z<=580;z+=2)for(var offset=-4;offset<=4;offset++)
            {
                var x=-70+offset*250f/256;
                Assert.That(current.SampleHeight(x,z),Is.Not.NaN);
                var p=new WorldMapTerrainPaintSampler(world,current).SampleWeights(x,z);
                Assert.That(p.Sum(),Is.EqualTo(1).Within(.00001));
            }
        }
        [Test] public void VegetationPreservesResourceIdentitiesAndOutsideProjection()
        {
            var before=new WorldMapVegetationPlanner(world,original,1380062136).Create();
            var after=new WorldMapVegetationPlanner(world,current,1380062136).Create();
            var oldResources=before.Instances.Where(v=>v.Kind!=WorldMapVegetationKind.GroundCover).ToArray();
            Assert.That(after.Instances.Where(v=>v.Kind!=WorldMapVegetationKind.GroundCover).Select(v=>v.StableId),
                Is.EqualTo(oldResources.Select(v=>v.StableId)));
            var byId=after.Instances.ToDictionary(v=>v.StableId);
            foreach(var v in before.Instances.Where(v=>current.Landscape.Weight(world,v.Position.X,v.Position.Z)==0))
            {
                Assert.That(byId.ContainsKey(v.StableId),Is.True);
                var other=byId[v.StableId]; Assert.That(other.Position,Is.EqualTo(v.Position));
                Assert.That(other.UniformScale,Is.EqualTo(v.UniformScale));
            }
            var inside=after.Instances.Where(v=>current.Landscape.Contains(v.Position.X,v.Position.Z)).ToArray();
            Assert.That(inside.Count(v=>v.Kind==WorldMapVegetationKind.InteractiveTree),Is.LessThanOrEqualTo(250));
            Assert.That(inside.Count(v=>v.Kind==WorldMapVegetationKind.DecorativeTreeOrShrub),Is.LessThanOrEqualTo(550));
            Assert.That(inside.Count(v=>v.Kind==WorldMapVegetationKind.GroundCover),Is.LessThanOrEqualTo(1500));
            Assert.That(inside.Where(v=>v.Kind!=WorldMapVegetationKind.GroundCover &&
                Math.Abs(v.Position.X+120)<=20&&Math.Abs(v.Position.Z-490)<=15),Is.Empty);
        }
        [Test] public void CanonicalResourceDeclarationsKeepIdsStocksAndXZWithGroundedSampleY()
        {
            var before=new TiledWorldPopulationPlanner(world,original,1380062136).Create();
            var after=new TiledWorldPopulationPlanner(world,current,1380062136).Create();
            Assert.That(after.Resources.Select(v=>v.StableKey),Is.EqualTo(before.Resources.Select(v=>v.StableKey)));
            Assert.That(after.Resources.Select(v=>v.Capacity),Is.EqualTo(before.Resources.Select(v=>v.Capacity)));
            for(var i=0;i<before.Resources.Count;i++)
            {
                var a=before.Resources[i].Position;var b=after.Resources[i].Position;
                Assert.That(b.X,Is.EqualTo(a.X));Assert.That(b.Z,Is.EqualTo(a.Z));
                if(current.Landscape.Weight(world,a.X,a.Z)>0&&!current.IsWater(a.X,a.Z))
                    Assert.That(b.Y,Is.EqualTo(current.SampleHeight(a.X,a.Z)));
                else Assert.That(b,Is.EqualTo(a));
            }
        }
        [Test] public void WildlifeKeepsAllocationIdentityAndXZWhileSampleYFollowsGround()
        {
            var before=new WorldMapWildlifePlanner(world,original,1380062136).Create();
            var after=new WorldMapWildlifePlanner(world,current,1380062136).Create();
            Assert.That(after.Individuals.Select(v=>v.Id),Is.EqualTo(before.Individuals.Select(v=>v.Id)));
            Assert.That(after.Individuals.Select(v=>v.HabitatNodeId),Is.EqualTo(before.Individuals.Select(v=>v.HabitatNodeId)));
            for(var i=0;i<before.Individuals.Count;i++)
            {
                var a=before.Individuals[i].Position;var b=after.Individuals[i].Position;
                Assert.That(b.X,Is.EqualTo(a.X));Assert.That(b.Z,Is.EqualTo(a.Z));
                if(current.Landscape.Weight(world,a.X,a.Z)>0&&!current.IsWater(a.X,a.Z))
                    Assert.That(b.Y,Is.EqualTo(current.SampleHeight(a.X,a.Z)));
                else Assert.That(b,Is.EqualTo(a));
            }
        }
        [Test] public void SampleUsesDisjointSaveAndTerrainCacheWithoutMigration()
        {
            var legacy=TiledWorldDefinitionLoader.LoadProposedWorldMap();
            Assert.That(StartupSaveProfile.ForWorld(legacy),Is.SameAs(StartupSaveProfile.WorldMapV2Proposed));
            Assert.That(StartupSaveProfile.ForWorld(world),Is.SameAs(StartupSaveProfile.SR02Sample));
            Assert.That(StartupSaveProfile.SR02Sample.SaveGenerationId,Is.Not.EqualTo(StartupSaveProfile.WorldMapV2Proposed.SaveGenerationId));
            Assert.That(StartupSaveProfile.SR02Sample.ResolveDirectory("C:/unused"),Is.Not.EqualTo(StartupSaveProfile.WorldMapV2Proposed.ResolveDirectory("C:/unused")));
            var a=new WorldTilePreparationCache("C:/unused",world,current,64,7);
            var b=new WorldTilePreparationCache("C:/unused",world,original,64,7);
            Assert.That(a.DirectoryPath,Is.Not.EqualTo(b.DirectoryPath));
        }
        [Test] public void BoundedShoreQueryMatchesUnboundedDistanceAndWaterLevels()
        {
            for(var z=-120f;z<=880;z+=7.5f)for(var x=-570f;x<=430;x+=7.5f)
            {
                var full=original.WaterBoundaryDistance(x,z,out var fullLevel);
                var bounded=original.WaterBoundaryDistance(x,z,out var boundedLevel,28f);
                Assert.That(bounded,Is.EqualTo(Math.Min(full,28f)), $"distance {x},{z}");
                if(full<28f)Assert.That(boundedLevel,Is.EqualTo(fullLevel),$"water level {x},{z}");
            }
        }
    }
}
