using System.Collections.Generic;
using System.Linq;
using Game.Presentation.Environment;
using UnityEngine;

namespace Game.Infrastructure
{
    // Bounded scenery, created once during loading. Gameplay trees continue using canonical vegetation IDs.
    public sealed class LateLandscapePresentation : MonoBehaviour
    {
        private readonly List<(GameObject view,bool night)> effects = new();
        private float nextRefresh;
        private Material petalMaterial;
        private Material iceMaterial;
        public int FloatingPetalCount { get; private set; }
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
            AddMountainScenery(world,art,random);
            var grove=world.ArtDirection.blossomGroves[0];
            AddFloatingPetals(world, art.FallingLeaves, grove, random);
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
        private void AddFloatingPetals(TiledTerrainWorld world, GameObject leaves, Vector4 grove, System.Random random)
        {
            var source = leaves != null ? leaves.GetComponentInChildren<ParticleSystemRenderer>(true) : null;
            if (source == null || source.sharedMaterial == null) return;
            petalMaterial = new Material(source.sharedMaterial);
            petalMaterial.renderQueue = 3100; // Draw floating petals after the large transparent water surface.
            var view = new GameObject("Sakura petals on western lake");
            view.transform.SetParent(transform, false);
            var system = view.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false; main.loop = false; main.maxParticles = 1800;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.Pause;
            main.startSpeed = 0; main.gravityModifier = 0;
            var emission = system.emission; emission.enabled = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.sharedMaterial = petalMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var particles = new List<ParticleSystem.Particle>();
            var map = world.ArtWorldToDesign;
            for (var i = 0; i < 14000 && particles.Count < 1800; i++)
            {
                var x = (grove.x + ((float)random.NextDouble() * 2 - 1) * 90 - map.x) / map.z;
                var z = (grove.y + ((float)random.NextDouble() * 2 - 1) * 75 - map.y) / map.w;
                var distance = world.HeightSampler.WaterBoundaryDistance(x,z,out var surface);
                // Dense loose patches near the shore, with open water between them.
                if (distance >= -1f || distance < -23f || Mathf.Sin(x*.11f + z*.13f) < -.15f) continue;
                particles.Add(new ParticleSystem.Particle {
                    position = new Vector3(x,surface+.055f,z), startLifetime = float.MaxValue,
                    remainingLifetime = float.MaxValue, startSize = .45f + (float)random.NextDouble()*.45f,
                    startColor = Color.Lerp(new Color(1f,.16f,.48f),new Color(1f,.48f,.68f),(float)random.NextDouble()),
                    rotation = (float)random.NextDouble()*360f
                });
            }
            system.Play();
            system.SetParticles(particles.ToArray(),particles.Count);
            system.Simulate(0f,false,false);
            system.Pause();
            FloatingPetalCount = particles.Count;
        }
        private void AddMountainScenery(TiledTerrainWorld world,LateLandscapeCatalog art,System.Random random)
        {
            var map=world.ArtWorldToDesign;
            var ridge=world.ArtDirection.regions[2];
            if(art.SnowRocks?.Length>0)
                for(var i=0;i<45;i++)
                {
                    var angle=(float)random.NextDouble()*Mathf.PI*2;
                    var radius=Mathf.Sqrt((float)random.NextDouble())*.78f;
                    var x=(ridge.center.x+Mathf.Cos(angle)*ridge.radius.x*radius-map.x)/map.z;
                    var z=(ridge.center.y+Mathf.Sin(angle)*ridge.radius.y*radius-map.y)/map.w;
                    if(!world.Definition.ContainsClosed(x,z)||world.HeightSampler.IsWater(x,z)||world.HeightSampler.SampleSlopeDegrees(x,z)>32f)continue;
                    var snow=WorldMapScenery.Instantiate(art.SnowRocks[i%art.SnowRocks.Length],transform);
                    snow.name="Wind packed mountain snow";
                    snow.transform.position=new Vector3(x,world.HeightSampler.SampleHeight(x,z)-.5f,z);
                    snow.transform.rotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);
                    var scale=1.3f+(float)random.NextDouble()*1.2f;
                    snow.transform.localScale=Vector3.Scale(snow.transform.localScale,new Vector3(scale,scale*.4f,scale));
                    SnowSceneryCount++;
                }
            var lake=world.Definition.MapData.Hydrology.Lakes.FirstOrDefault(x=>x.Id=="lake_mountain");
            if(lake==null||art.IceFloe==null)return;
            var points=lake.Shoreline.Points.Select(x=>world.Definition.MapData.CoordinateTransform.ToUnity(x)).ToArray();
            iceMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            iceMaterial.SetColor("_BaseColor",new Color(.68f,.83f,.92f));iceMaterial.SetFloat("_Smoothness",.78f);
            iceMaterial.SetFloat("_Metallic",.08f);
            for(var x=points.Min(p=>p.X)+4;x<points.Max(p=>p.X)-4;x+=8)
            for(var z=points.Min(p=>p.Z)+4;z<points.Max(p=>p.Z)-4;z+=8)
            {
                if(random.NextDouble()>.62 || world.HeightSampler.NearestWaterBodyId(x,z)!=lake.Id ||
                    world.HeightSampler.WaterBoundaryDistance(x,z,out var water)>-3)continue;
                var ice=WorldMapScenery.Instantiate(art.IceFloe,transform);ice.name="Mountain lake ice";
                ice.transform.position=new Vector3(x,water+.025f,z);
                ice.transform.rotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);
                var size=1.7f+(float)random.NextDouble()*1.4f;
                ice.transform.localScale=Vector3.Scale(ice.transform.localScale,new Vector3(size,.25f,size));
                foreach(var renderer in ice.GetComponentsInChildren<Renderer>())renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>iceMaterial).ToArray();
            }
        }
        private void OnDestroy() { if (petalMaterial != null) Destroy(petalMaterial);if(iceMaterial!=null)Destroy(iceMaterial); }
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
