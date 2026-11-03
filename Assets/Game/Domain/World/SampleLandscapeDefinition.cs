using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.AI;

namespace Game.Domain.World
{
    /// <summary>SR02 immutable authoring input in Unity metres; never mutable save state.</summary>
    [Serializable]
    public sealed class SampleLandscapeDefinition
    {
        public string revision;
        public float minX, maxX, minZ, maxZ, edgeBlend;
        public float clearingX, clearingZ, clearingHalfX, clearingHalfZ, clearingShoulder, clearingHeight;
        public float groundScale, treeScale;
        public int maxTreeProxies;
        public Grove[] groves;
        [Serializable] public sealed class Grove { public float x, z, radiusX, radiusZ; }

        public float Weight(TiledWorldDefinition world, float x, float z)
        {
            var edge = Math.Min(Math.Min(x-minX,maxX-x),Math.Min(z-minZ,maxZ-z));
            if (edge <= 0) return 0;
            var weight = Smooth(edge / edgeBlend);
            foreach (var poi in world.MapData.PointsOfInterest)
            {
                var p = world.MapData.CoordinateTransform.ToUnity(poi.Position);
                var distance = Distance(x,z,p.X,p.Z)-poi.ProtectionRadiusMeters;
                if (distance <= 0) return 0;
                weight *= Smooth(distance/8f);
            }
            return weight;
        }
        public bool Contains(float x,float z) => x>=minX&&x<=maxX&&z>=minZ&&z<=maxZ;
        public WorldPosition ProjectGround(TiledWorldHeightSampler sampler,WorldPosition position)
        {
            if(Weight(sampler.Definition,position.X,position.Z)<=0 || sampler.IsWater(position.X,position.Z))return position;
            return new WorldPosition(position.X,sampler.SampleHeight(position.X,position.Z),position.Z);
        }
        public float Clearing(float x,float z)
        {
            var dx = Math.Max(0,Math.Abs(x-clearingX)-clearingHalfX);
            var dz = Math.Max(0,Math.Abs(z-clearingZ)-clearingHalfZ);
            return 1-Smooth((float)Math.Sqrt(dx*dx+dz*dz)/clearingShoulder);
        }
        public float Forest(float x,float z)
        {
            var result=0f;
            foreach(var grove in groves)
            {
                var dx=(x-grove.x)/grove.radiusX; var dz=(z-grove.z)/grove.radiusZ;
                var r=(float)Math.Sqrt(dx*dx+dz*dz);
                result=Math.Max(result,1-Smooth((r-.65f)/.7f));
            }
            return result*(1-Clearing(x,z));
        }
        public float PathDistance(float x,float z)
        {
            // Shore approach, clearing, eastern seam. Unity coordinates, independent of tile indices.
            return Math.Min(Segment(x,z,-195,455,-140,480),Math.Min(Segment(x,z,-140,480,-70,500),
                Segment(x,z,-70,500,-50,530)));
        }
        public float Height(TiledWorldDefinition world,TiledWorldHeightSampler sampler,float x,float z,float original)
        {
            var weight=Weight(world,x,z);
            if(weight<=0) return original;
            // Both shore and clearing blends are constant beyond 28 m; distant water is irrelevant.
            var distance=sampler.WaterBoundaryDistance(x,z,out var water,28f);
            if(distance<=0) return original; // Canonical water interior and levels remain exact.
            var shoulder=(1-Smooth(distance/28f))*Smooth(distance/3f)*weight;
            var bank=water+.12f+distance*.24f;
            var height=original+(bank-original)*shoulder*.7f;
            var clearing=Clearing(x,z)*weight*Smooth((distance-8)/12f);
            var plane=clearingHeight+(z-clearingZ)*.012f;
            return height+(plane-height)*clearing;
        }
        public void Paint(float[] weights,TiledWorldDefinition world,TiledWorldHeightSampler sampler,float x,float z)
        {
            var influence=Weight(world,x,z); if(influence<=0) return;
            var d=sampler.WaterBoundaryDistance(x,z,out _);
            var forest=Forest(x,z); var path=1-Smooth((PathDistance(x,z)-1.2f)/3f);
            var patch=.5f+.25f*(float)Math.Sin(x*.113+Math.Sin(z*.047))+.25f*(float)Math.Sin(z*.079+x*.021);
            var shore=1-Smooth((d-2)/8f); var wet=1-Smooth(d/3f);
            var rock=Smooth((sampler.SampleSlopeDegrees(x,z,12)-17)/15f);
            var target=new[]{ .58f*(1-forest*.8f)*(1-shore), .08f+forest*.7f,
                .02f+rock*.65f+shore*.12f, shore*.7f*(1-wet*.6f),
                .10f+path*.8f+patch*.13f+wet*.25f, .03f+patch*.04f, wet*.48f };
            var total=target.Sum();
            for(var i=0;i<weights.Length;i++) weights[i]+= (target[i]/total-weights[i])*influence;
        }
        public WorldMapVegetationPlan Compose(WorldMapVegetationPlan original,TiledWorldDefinition world,TiledWorldHeightSampler heights)
        {
            var result=new List<WorldMapVegetationInstance>(original.Instances.Count);
            var placed=new List<WorldPosition>();
            foreach(var v in original.Instances)
            {
                var x=v.Position.X; var z=v.Position.Z; var w=Weight(world,x,z);
                if(w<=0) { result.Add(v); continue; }
                var unit=Unit(v.StableId,17);
                var ground=v.Kind==WorldMapVegetationKind.GroundCover;
                if(ground)
                {
                    // Sparse, low ground detail; no new resource or selectable proxy.
                    var density=.16f+Forest(x,z)*.66f;
                    if(Clearing(x,z)>.98f || PathDistance(x,z)<3.5f) density=.025f;
                    if(unit>w*density+(1-w)) continue;
                }
                else if(unit<w && (Forest(x,z)<.38f || Clearing(x,z)>.1f || PathDistance(x,z)<5))
                {
                    var moved=false;
                    for(var attempt=0;attempt<1024;attempt++)
                    {
                        var grove=groves[(int)(Unit(v.StableId,attempt*7+29)*groves.Length)%groves.Length];
                        var angle=Unit(v.StableId,attempt*7+31)*(float)Math.PI*2;
                        var radius=(float)Math.Sqrt(Unit(v.StableId,attempt*7+37));
                        var nx=grove.x+(float)Math.Cos(angle)*grove.radiusX*radius;
                        var nz=grove.z+(float)Math.Sin(angle)*grove.radiusZ*radius;
                        if(Weight(world,nx,nz)<.999f||Clearing(nx,nz)>.1f||PathDistance(nx,nz)<5||heights.IsWater(nx,nz))continue;
                        if(placed.Any(p=>Distance(nx,nz,p.X,p.Z)<2.4f))continue;
                        x=nx;z=nz;moved=true;break;
                    }
                    if(!moved) throw new InvalidOperationException("SR02 grove capacity cannot preserve all source identities.");
                }
                var p=new WorldPosition(x,ground?0:heights.SampleHeight(x,z),z);
                if(!ground)placed.Add(p);
                world.TryGetTile(x,z,out var tile);
                result.Add(new WorldMapVegetationInstance(v.StableId,v.Kind,v.ZoneId,v.ClassId,tile,p,v.RotationDegrees,
                    v.UniformScale*(1+w*((ground?groundScale:treeScale)-1))));
            }
            return new WorldMapVegetationPlan(result);
        }
        private static float Unit(StableEntityId id,int salt)
        {
            uint hash=2166136261; foreach(var c in id.ToString())hash=unchecked((hash^c)*16777619);
            hash=unchecked((hash^(uint)salt)*16777619);hash^=hash>>16;hash=unchecked(hash*2246822519);hash^=hash>>13;
            return (hash&0xffffff)/16777216f;
        }
        private static float Segment(float x,float z,float ax,float az,float bx,float bz)
        {
            var dx=bx-ax;var dz=bz-az;var t=Math.Max(0,Math.Min(1,((x-ax)*dx+(z-az)*dz)/(dx*dx+dz*dz)));
            return Distance(x,z,ax+dx*t,az+dz*t);
        }
        private static float Distance(float x,float z,float a,float b)=>(float)Math.Sqrt((x-a)*(x-a)+(z-b)*(z-b));
        private static float Smooth(float v) {v=Math.Max(0,Math.Min(1,v));return v*v*(3-2*v);}
    }
}
