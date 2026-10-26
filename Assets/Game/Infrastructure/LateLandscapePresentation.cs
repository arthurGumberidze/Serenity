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
        private Material petalMaterial;
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
            // Snow belongs to the terrain/vegetation palette, not randomly painted rock props.
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
            main.playOnAwake = false; main.loop = false; main.maxParticles = 900;
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
            for (var i = 0; i < 7000 && particles.Count < 900; i++)
            {
                var x = (grove.x + ((float)random.NextDouble() * 2 - 1) * 90 - map.x) / map.z;
                var z = (grove.y + ((float)random.NextDouble() * 2 - 1) * 75 - map.y) / map.w;
                var distance = world.HeightSampler.WaterBoundaryDistance(x,z,out var surface);
                // Dense loose patches near the shore, with open water between them.
                if (distance >= -1f || distance < -23f || Mathf.Sin(x*.11f + z*.13f) < -.15f) continue;
                particles.Add(new ParticleSystem.Particle {
                    position = new Vector3(x,surface+.055f,z), startLifetime = float.MaxValue,
                    remainingLifetime = float.MaxValue, startSize = .35f + (float)random.NextDouble()*.30f,
                    startColor = Color.Lerp(new Color(.98f,.47f,.64f),new Color(1f,.78f,.85f),(float)random.NextDouble()),
                    rotation = (float)random.NextDouble()*360f
                });
            }
            system.Play();
            system.SetParticles(particles.ToArray(),particles.Count);
            system.Simulate(0f,false,false);
            system.Pause();
            FloatingPetalCount = particles.Count;
        }
        private void OnDestroy() { if (petalMaterial != null) Destroy(petalMaterial); }
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
