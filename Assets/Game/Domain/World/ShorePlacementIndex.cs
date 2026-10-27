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
            public Candidate(GridCoordinate anchor, BuildingOrientation facing, float x, float y, float z, string waterBodyId)
            { Anchor=anchor; Facing=facing; X=x; Y=y; Z=z; WaterBodyId=waterBodyId; }
            public GridCoordinate Anchor { get; }
            public BuildingOrientation Facing { get; }
            public float X { get; }
            public float Y { get; }
            public float Z { get; }
            public string WaterBodyId { get; }
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
                result.candidates.Add(new Candidate(anchor,facing,cx,heights.SampleHeight(cx,cz),cz,bank.id));
            }
            // Lakes get six stable, evenly spaced construction places. Keep all river
            // candidates so the player can choose a river bank freely.
            var all = result.candidates.ToArray();
            result.candidates.Clear();
            var lakeIds = new HashSet<string>(world.MapData.Hydrology.Lakes.Select(x=>x.Id));
            result.candidates.AddRange(all.Where(x => !lakeIds.Contains(x.WaterBodyId)));
            foreach (var lake in world.MapData.Hydrology.Lakes)
            {
                var center = world.MapData.CoordinateTransform.ToUnity(lake.Center);
                var options = all.Where(x => x.WaterBodyId == lake.Id).ToArray();
                var chosen = new HashSet<(GridCoordinate,BuildingOrientation)>();
                var occupied = new HashSet<GridCoordinate>();
                for (var slot=0; slot<6; slot++)
                {
                    var angle = slot * Math.PI / 3d;
                    var target = angle;
                    var candidate = options.Where(x => !chosen.Contains((x.Anchor,x.Facing)))
                        .Where(x => !footprint.EnumerateCells(x.Anchor,x.Facing).Any(occupied.Contains))
                        .OrderBy(x => AngularDistance(Math.Atan2(x.Z-center.Z,x.X-center.X),target))
                        .ThenBy(x => DistanceSquared(x.X,x.Z,center.X,center.Z)).FirstOrDefault();
                    if (candidate.WaterBodyId == null) continue;
                    chosen.Add((candidate.Anchor,candidate.Facing));
                    foreach (var cell in footprint.EnumerateCells(candidate.Anchor,candidate.Facing)) occupied.Add(cell);
                    result.candidates.Add(candidate);
                }
            }
            return result;
        }
        private static double AngularDistance(double a,double b)
        { var d=Math.Abs(a-b)%(Math.PI*2); return Math.Min(d,Math.PI*2-d); }
        private static double DistanceSquared(double x,double z,double cx,double cz)
        { var dx=x-cx;var dz=z-cz;return dx*dx+dz*dz; }
        private static IEnumerable<(float x,float z,float dx,float dz,string id)> Banks(TiledWorldDefinition world)
        {
            var water=world.MapData.Hydrology;var transform=world.MapData.CoordinateTransform;
            foreach(var lake in water.Lakes)
            {
                var center=transform.ToUnity(lake.Center);
                foreach(var point in lake.Shoreline.Points)
                {
                    var p=transform.ToUnity(point);var dx=p.X-center.X;var dz=p.Z-center.Z;
                    var length=(float)Math.Sqrt(dx*dx+dz*dz);
                    if(length>0)yield return(p.X,p.Z,dx/length,dz/length,lake.Id);
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
                    yield return(a.X+dx*t+nx*sign*width*.5f,a.Z+dz*t+nz*sign*width*.5f,nx*sign,nz*sign,river.Id);
                }
            }
        }
    }
}
