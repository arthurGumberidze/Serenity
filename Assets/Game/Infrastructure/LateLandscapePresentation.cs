using System.Collections.Generic;
using Game.Presentation.Environment;
using UnityEngine;

namespace Game.Infrastructure
{
    // Bounded scenery, created once during loading. Gameplay trees continue using canonical vegetation IDs.
    public sealed class LateLandscapePresentation : MonoBehaviour
    {
        private readonly List<(GameObject view,bool night)> effects = new();
        private float nextRefresh;
        public int SnowSceneryCount { get; private set; }
        public void Initialize(TiledTerrainWorld world,LateLandscapeCatalog art)
        {
            if(art==null) return;
            var random=new System.Random(unchecked((int)(world.HeightSampler.WorldSeed ^ 25102026)));
            var map=world.ArtWorldToDesign;
            Vector3 Ground(float x,float z)
            {
                var p=new Vector3((x-map.x)/map.z,0,(z-map.y)/map.w);
                p.y=world.HeightSampler.SampleHeight(p.x,p.z); return p;
            }
            var snow=world.ArtDirection.regions[2];
            for(var i=0;i<100;i++)
            {
                var x=snow.center.x+((float)random.NextDouble()*2-1)*snow.radius.x*.7f;
                var z=snow.center.y+((float)random.NextDouble()*2-1)*snow.radius.y*.7f;
                var p=Ground(x,z);
                if(world.HeightSampler.IsWater(p.x,p.z) || world.HeightSampler.IsBuildable(p.x,p.z,30f)) continue;
                var view=WorldMapScenery.Instantiate(art.SnowRocks[i%art.SnowRocks.Length],transform);
                view.transform.position=p-Vector3.up*.25f;
                view.transform.rotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);
                view.transform.localScale*=.7f+(float)random.NextDouble()*.65f; SnowSceneryCount++;
            }
            foreach(var lake in world.Definition.MapData.Hydrology.Lakes)
            {
                if(lake.Id!="lake_mountain") continue;
                var center=world.Definition.MapData.CoordinateTransform.ToUnity(lake.Center);
                for(var i=0;i<7;i++)
                {
                    // A loose cluster of broad, flat floes, not identical dots in a geometric ring.
                    var p=new Vector3(center.X+12f+(float)random.NextDouble()*18f,0,
                        center.Z-20f+(float)random.NextDouble()*36f);
                    if(!world.HeightSampler.TrySampleWaterSurface(p.x,p.z,out var y))continue;
                    var view=WorldMapScenery.Instantiate(art.IceFloe,transform);
                    view.transform.position=new Vector3(p.x,y-.2f,p.z);view.transform.rotation=Quaternion.Euler(0,i*67,0);
                    var width=3f+(float)random.NextDouble()*3f;
                    view.transform.localScale=Vector3.Scale(view.transform.localScale,new Vector3(width,1f,width*.8f));
                    SnowSceneryCount++;
                }
            }
            var grove=world.ArtDirection.blossomGroves[0];
            for(var i=0;i<10;i++)
            {
                var angle=i*Mathf.PI*2/10f;
                AddEffect(art.FallingLeaves,Ground(grove.x+Mathf.Cos(angle)*grove.z*.48f,grove.y+Mathf.Sin(angle)*grove.w*.48f)+Vector3.up*2.5f,false);
            }
            var marsh=world.ArtDirection.regions[7];
            for(var i=0;i<8;i++)
                AddEffect(art.Fireflies,Ground(marsh.center.x+(i%4-1.5f)*25f,marsh.center.y+(i/4-.5f)*40f)+Vector3.up*.8f,true);
        }
        private void AddEffect(GameObject prefab,Vector3 point,bool night)
        {
            if(prefab==null)return;
            var view=WorldMapScenery.Instantiate(prefab,transform);view.transform.position=point;
            effects.Add((view,night));
        }
        private void Update()
        {
            if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.5f;
            var camera=Camera.main;if(camera==null)return;
            var sun=RenderSettings.sun;var dark=sun==null || sun.intensity<.45f;
            foreach(var entry in effects)
                entry.view.SetActive((entry.view.transform.position-camera.transform.position).sqrMagnitude<180f*180f && (!entry.night || dark));
        }
    }
}
