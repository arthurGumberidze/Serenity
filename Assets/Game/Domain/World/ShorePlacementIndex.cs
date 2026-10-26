using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Buildings;

namespace Game.Domain.World
{
    // Built once from immutable world data on a worker thread. No Unity objects or mutable simulation state.
    public sealed class ShorePlacementIndex
    {
        public readonly struct Cell
        {
            public Cell(string blocked, float low, float high) { Blocked=blocked; Low=low; High=high; }
            public string Blocked { get; }
            public float Low { get; }
            public float High { get; }
        }
        public readonly struct Candidate
        {
            public Candidate(GridCoordinate anchor, BuildingOrientation facing, float x, float y, float z)
            { Anchor=anchor; Facing=facing; X=x; Y=y; Z=z; }
            public GridCoordinate Anchor { get; }
            public BuildingOrientation Facing { get; }
            public float X { get; }
            public float Y { get; }
            public float Z { get; }
        }
        private readonly Dictionary<GridCoordinate,Cell> cells=new();
        private readonly List<Candidate> candidates=new();
        public IReadOnlyDictionary<GridCoordinate,Cell> Cells=>cells;
        public IReadOnlyList<Candidate> Candidates=>candidates;

        public static ShorePlacementIndex Create(TiledWorldDefinition world,long seed,BuildingFootprint footprint,
            float gridSize,float originX,float originZ)
        {
            var result=new ShorePlacementIndex();
            var heights=new TiledWorldHeightSampler(world,seed);
            var policy=new WorldMapBuildabilityEvaluator(world,heights);
            var seen=new HashSet<(GridCoordinate,BuildingOrientation)>();
            foreach(var bank in Banks(world))
            for(var distance=5;distance<=22;distance+=2)
            for(var f=0;f<4;f++)
            {
                var facing=(BuildingOrientation)f; var angle=facing.Degrees()*Math.PI/180;
                if(Math.Sin(angle)*-bank.dx+Math.Cos(angle)*-bank.dz<.6)continue;
                var rotated=footprint.Rotate(facing);
                var anchor=new GridCoordinate((int)Math.Round((bank.x+bank.dx*distance-originX)/gridSize-(rotated.Width-1)*.5f),
                    (int)Math.Round((bank.z+bank.dz*distance-originZ)/gridSize-(rotated.Depth-1)*.5f));
                if(!seen.Add((anchor,facing)) || !ShoreBuildingPlacement.IsValid(heights,anchor,footprint,facing,gridSize,originX,originZ))continue;
                var low=float.MaxValue;var high=float.MinValue;var blocked=false;
                foreach(var cell in footprint.EnumerateCells(anchor,facing))
                {
                    if(!result.cells.TryGetValue(cell,out var cached))
                    {
                        var x=originX+cell.X*gridSize;var z=originZ+cell.Z*gridSize;
                        var verdict=policy.EvaluateFootprint(new[]{new WorldMapFootprintCell(x,z,gridSize*.5f)},true);
                        var cellLow=float.MaxValue;var cellHigh=float.MinValue;
                        if(verdict.CanPlace)
                            for(var dz=-1;dz<=1;dz++)for(var dx=-1;dx<=1;dx++)
                            {var h=heights.SampleHeight(x+dx*gridSize*.5f,z+dz*gridSize*.5f);cellLow=Math.Min(cellLow,h);cellHigh=Math.Max(cellHigh,h);}
                        cached=new Cell(verdict.CanPlace?null:verdict.Reason,cellLow,cellHigh);result.cells.Add(cell,cached);
                    }
                    if(cached.Blocked!=null){blocked=true;break;}
                    low=Math.Min(low,cached.Low);high=Math.Max(high,cached.High);
                }
                if(blocked || high-low>4.5f)continue;
                var cx=originX+(anchor.X+(rotated.Width-1)*.5f)*gridSize;
                var cz=originZ+(anchor.Z+(rotated.Depth-1)*.5f)*gridSize;
                result.candidates.Add(new Candidate(anchor,facing,cx,heights.SampleHeight(cx,cz),cz));
            }
            return result;
        }
        private static IEnumerable<(float x,float z,float dx,float dz)> Banks(TiledWorldDefinition world)
        {
            var water=world.MapData.Hydrology;var transform=world.MapData.CoordinateTransform;
            foreach(var lake in water.Lakes)
            {
                var center=transform.ToUnity(lake.Center);
                foreach(var point in lake.Shoreline.Points)
                {
                    var p=transform.ToUnity(point);var dx=p.X-center.X;var dz=p.Z-center.Z;
                    var length=(float)Math.Sqrt(dx*dx+dz*dz);
                    if(length>0)yield return(p.X,p.Z,dx/length,dz/length);
                }
            }
            foreach(var river in new[]{water.MainRiver}.Concat(water.Tributaries))
            for(var i=1;i<river.Spline.Points.Count;i++)
            {
                var a=transform.ToUnity(river.Spline.Points[i-1].Position);var b=transform.ToUnity(river.Spline.Points[i].Position);
                var dx=b.X-a.X;var dz=b.Z-a.Z;var length=(float)Math.Sqrt(dx*dx+dz*dz);if(length<=0)continue;
                var nx=-dz/length;var nz=dx/length;var count=Math.Max(1,(int)Math.Ceiling(length/16));
                for(var j=0;j<count;j++)for(var sign=-1;sign<=1;sign+=2)
                {
                    var t=(float)j/count;var width=river.Spline.Points[i-1].WidthMeters*(1-t)+river.Spline.Points[i].WidthMeters*t;
                    yield return(a.X+dx*t+nx*sign*width*.5f,a.Z+dz*t+nz*sign*width*.5f,nx*sign,nz*sign);
                }
            }
        }
    }
}
