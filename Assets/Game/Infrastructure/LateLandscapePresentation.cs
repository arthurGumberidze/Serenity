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
            // Snow belongs to the terrain/vegetation palette, not randomly painted rock props.
            var grove=world.ArtDirection.blossomGroves[0];
            for(var i=0;i<10;i++)
            {
                var angle=i*Mathf.PI*2/10f;
                AddEffect(art.FallingLeaves,Ground(grove.x+Mathf.Cos(angle)*grove.z*.48f,grove.y+Mathf.Sin(angle)*grove.w*.48f)+Vector3.up*2.5f,false);
            }
            for(var i=0;i<6;i++)
            {
                var point=Ground(grove.x+(i%3-1)*12f,grove.y+(i/3-.5f)*14f)+Vector3.up*.65f;
                AddEffect(WorldPropCatalog.Current.Find("butterfly"),point,false);
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
