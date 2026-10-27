using System;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Presentation.Interaction;
using System.Linq;
using UnityEngine;

namespace Game.Presentation.Buildings
{
    public sealed class BuildingPresenter : MonoBehaviour
    {
        private static Material sharedFlameMaterial;
        private Building building;
        private BuildingPresentationRegistry registry;
        private ResourceInventory storageInventory;
        private ParticleSystem fireParticles;
        private ParticleSystem campfireParticles;
        private Light campfireLight;
        private MaterialPropertyBlock propertyBlock;
        private Renderer[] originalRenderers;
        private bool[] originalVisibility;
        private GameObject scaffold;
        private Material scaffoldMaterial;
        private BuildingConstructionStageVisual[] constructionStages;
        private readonly System.Collections.Generic.Dictionary<Transform,Quaternion> gateClosedRotations = new();

        public bool IsBound => building != null;
        public Building Building => building;
        public StableEntityId BuildingId => building != null ? building.Id : throw new InvalidOperationException("Presenter is not bound.");
        public ConstructionState ConstructionState => building != null ? building.ConstructionState : throw new InvalidOperationException("Presenter is not bound.");
        public ResourceInventory StorageInventory => storageInventory;

        public void Bind(Building source, BuildingPresentationRegistry presentationRegistry,
            ResourceInventory inventory = null, float gridSize = 1f)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (presentationRegistry == null) throw new ArgumentNullException(nameof(presentationRegistry));
            if (IsBound) throw new InvalidOperationException("Presenter is already bound.");
            presentationRegistry.Register(source.Id, this);
            building = source;
            registry = presentationRegistry;
            storageInventory = inventory;
            GetComponent<BuildingZoneVisual>()?.Render(source.PlacementCells, gridSize);
            originalRenderers = GetComponentsInChildren<Renderer>(true);
            originalVisibility = Array.ConvertAll(originalRenderers, x => x.enabled);
            constructionStages = GetComponentsInChildren<BuildingConstructionStageVisual>(true);
            gameObject.name = $"Building {source.DefinitionId} [{source.Id}]";
            var hitProxy = GetComponent<StableWorldHitProxy>() ?? gameObject.AddComponent<StableWorldHitProxy>();
            hitProxy.Bind(source.Id, ContextTargetKind.Building);
            RefreshStructuralPresentation();
            if(source.DefinitionId.ToString()=="gate") SetGateOpen(false);
        }

        public void SetGateOpen(bool open)
        {
            if(building?.DefinitionId.ToString()!="gate")return;
            var collider=GetComponent<BoxCollider>();if(collider!=null)collider.enabled=!open;
            var obstacle=GetComponent<UnityEngine.AI.NavMeshObstacle>() ?? gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape=UnityEngine.AI.NavMeshObstacleShape.Box;
            if(collider!=null){obstacle.center=collider.center;obstacle.size=collider.size;}
            obstacle.carving=true;obstacle.carveOnlyStationary=false;obstacle.enabled=!open;
            if(transform.Find("Gate selection")==null && collider!=null)
            {
                var selection=new GameObject("Gate selection");selection.transform.SetParent(transform,false);
                var hit=selection.AddComponent<BoxCollider>();hit.center=collider.center;hit.size=collider.size;hit.isTrigger=true;
                var leaves=GetComponentsInChildren<MeshRenderer>(true).Where(x=>x.name=="DoorL" || x.name=="DoorR").ToArray();
                if(leaves.Length==2)
                {
                    var opening=new Bounds();var initialized=false;
                    foreach(var leaf in leaves)
                    {
                        var b=leaf.localBounds;
                        foreach(var x in new[]{b.min.x,b.max.x})foreach(var y in new[]{b.min.y,b.max.y})foreach(var z in new[]{b.min.z,b.max.z})
                        {
                            var point=transform.InverseTransformPoint(leaf.transform.TransformPoint(new Vector3(x,y,z)));
                            if(!initialized){opening=new Bounds(point,Vector3.zero);initialized=true;}else opening.Encapsulate(point);
                        }
                    }
                    var left=collider.center.x-collider.size.x*.5f;var right=collider.center.x+collider.size.x*.5f;
                    AddGatePost("Left gate frame",left,opening.min.x,collider);
                    AddGatePost("Right gate frame",opening.max.x,right,collider);
                }
            }
            foreach(var visual in GetComponentsInChildren<Transform>(true))
                if(visual.name.EndsWith("Gate_Wood_01_left",StringComparison.Ordinal) ||
                    visual.name.EndsWith("Gate_Wood_01_right",StringComparison.Ordinal) || visual.name=="DoorL" || visual.name=="DoorR")
                {
                    if(!gateClosedRotations.ContainsKey(visual))gateClosedRotations.Add(visual,visual.localRotation);
                    visual.localRotation=gateClosedRotations[visual]*Quaternion.Euler(0,open?(visual.name.EndsWith("_left") || visual.name=="DoorL"?-90:90):0,0);
                }
        }

        private void AddGatePost(string name,float from,float to,BoxCollider gate)
        {
            if(to-from<.01f)return;
            var post=new GameObject(name);post.transform.SetParent(transform,false);
            var hit=post.AddComponent<BoxCollider>();hit.center=new Vector3((from+to)*.5f,gate.center.y,gate.center.z);
            hit.size=new Vector3(to-from,gate.size.y,gate.size.z);
            var blocker=post.AddComponent<UnityEngine.AI.NavMeshObstacle>();blocker.shape=UnityEngine.AI.NavMeshObstacleShape.Box;
            blocker.center=hit.center;blocker.size=hit.size;blocker.carving=true;blocker.carveOnlyStationary=false;
        }

        public void Unbind()
        {
            if (!IsBound) return;
            registry?.Unregister(building.Id, this);
            building = null;
            registry = null;
            storageInventory = null;
        }

        public void RefreshStructuralPresentation()
        {
            if (building == null) return;
            RefreshConstruction();
            GetComponent<Game.Presentation.Production.CropPlotPresentation>()?.Refresh();
            RefreshCampfire();
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            var hasTint = building.IsDestroyed || building.OwnerSideId.ToString() == "enemy";
            var color = building.IsDestroyed ? new Color(0.18f, 0.16f, 0.15f) :
                building.OwnerSideId.ToString() == "enemy" ? new Color(0.65f, 0.24f, 0.22f) : Color.white;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (fireParticles != null && renderer.transform.IsChildOf(fireParticles.transform)) continue;
                if (scaffold != null && renderer.transform.IsChildOf(scaffold.transform)) continue;
                if (!hasTint)
                {
                    renderer.SetPropertyBlock(null);
                    continue;
                }
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(propertyBlock);
            }
            if (building.FireStatus == BuildingFireStatus.Burning && !building.IsDestroyed)
            {
                if (fireParticles == null) fireParticles = CreateFireParticles();
                fireParticles.gameObject.SetActive(true);
                if (!fireParticles.isPlaying) fireParticles.Play();
            }
            else if (fireParticles != null)
            {
                fireParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                fireParticles.gameObject.SetActive(false);
            }
        }

        private ParticleSystem CreateFireParticles()
        {
            var fireObject = Game.Presentation.Environment.WorldPropCatalog.Create("fire", transform);
            fireObject.transform.localPosition = Vector3.up * .3f;
            return fireObject.GetComponent<ParticleSystem>();
        }

        private void RefreshCampfire()
        {
            var torch = building.DefinitionId.ToString() == "ground_torch";
            var shouldBurn = (building.DefinitionId == new BuildingDefinitionId("campfire") || torch) &&
                building.IsOperational && !building.IsDestroyed;
            if (shouldBurn)
            {
                if (campfireLight == null)
                {
                    var glow = new GameObject("Campfire illumination");
                    glow.transform.SetParent(transform, false);
                    glow.transform.localPosition = Vector3.up * (torch ? 1.65f : 1.1f);
                    campfireLight = glow.AddComponent<Light>();
                    campfireLight.type = LightType.Point;
                    campfireLight.color = new Color(1f, .53f, .22f);
                    campfireLight.range = torch ? 12f : 18f;
                    // Keep the falloff radius stable while increasing the requested emitted light.
                    campfireLight.intensity = torch ? 12f : 45f;
                    campfireLight.shadows = LightShadows.None;
                }
                campfireLight.enabled = true;
                if (campfireParticles == null)
                {
                    var flame = Game.Presentation.Environment.WorldPropCatalog.Create("fire", transform);
                    flame.transform.localPosition = Vector3.up * (torch ? 1.5f : .25f);
                    flame.transform.localScale = Vector3.one * (torch ? .22f : .55f);
                    campfireParticles = flame.GetComponent<ParticleSystem>();
                }
                campfireParticles.gameObject.SetActive(true);
                if (!campfireParticles.isPlaying) campfireParticles.Play();
            }
            else if (campfireParticles != null)
            {
                if (campfireLight != null) campfireLight.enabled = false;
                campfireParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                campfireParticles.gameObject.SetActive(false);
            }
        }

        private static void ConfigureFlameRenderer(ParticleSystem particles)
        {
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = FlameMaterial;
        }

        private static Material FlameMaterial
        {
            get
            {
                if (sharedFlameMaterial != null) return sharedFlameMaterial;
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                    Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                sharedFlameMaterial = new Material(shader) { name = "Serenity Runtime Flame" };
                var flame = new Color(1f, 0.42f, 0.035f, 0.92f);
                if (sharedFlameMaterial.HasProperty("_BaseColor")) sharedFlameMaterial.SetColor("_BaseColor", flame);
                if (sharedFlameMaterial.HasProperty("_Color")) sharedFlameMaterial.SetColor("_Color", flame);
                return sharedFlameMaterial;
            }
        }

        private void RefreshConstruction()
        {
            if (originalRenderers == null) return;
            var finished = building.ConstructionState == ConstructionState.Completed;
            if (!finished && scaffold == null)
            {
                scaffold = new GameObject("Construction frame");
                scaffold.transform.SetParent(transform, false);
                scaffoldMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                scaffoldMaterial.color = new Color(0.48f, 0.3f, 0.13f);
                var bounds = new Bounds(transform.position, Vector3.one * 2f);
                foreach (var renderer in originalRenderers) bounds.Encapsulate(renderer.bounds);
                var width = Mathf.Max(2f, bounds.size.x);
                var depth = Mathf.Max(2f, bounds.size.z);
                var height = Mathf.Clamp(bounds.size.y, 1.5f, 5f);
                for (var i = 0; i < 4; i++)
                {
                    var x = (i % 2 == 0 ? -1f : 1f) * width * 0.5f;
                    var z = (i < 2 ? -1f : 1f) * depth * 0.5f;
                    CreateBeam(new Vector3(x, height * 0.5f, z), new Vector3(0.12f, height, 0.12f));
                }
                foreach (var z in new[] { -depth * 0.5f, depth * 0.5f })
                    CreateBeam(new Vector3(0f, height * 0.75f, z), new Vector3(width, 0.12f, 0.12f));
            }
            if (scaffold != null) scaffold.SetActive(!finished && !building.IsDestroyed);
            if (constructionStages != null)
            {
                var finalStage = constructionStages.Length == 0 ? 0 : constructionStages.Max(x => x.RequiredStage);
                foreach (var stage in constructionStages)
                    if (stage != null) stage.gameObject.SetActive(finished ||
                        building.ConstructionStageIndex >= stage.RequiredStage);
                if (finished)
                    foreach (var stage in constructionStages)
                        if (stage != null) stage.gameObject.SetActive(stage.RequiredStage == finalStage);
            }
            var stagedProgress = building.ConstructionStageCount <= 0 ? building.ConstructionProgress :
                Mathf.Clamp01((building.ConstructionStageIndex + (float)building.ConstructionProgress) /
                    (building.ConstructionStageCount + 1f));
            for (var i = 0; i < originalRenderers.Length; i++)
                if (originalRenderers[i] != null)
                    originalRenderers[i].enabled = originalVisibility[i] &&
                        (originalRenderers[i].GetComponentInParent<BuildingConstructionStageVisual>() != null ||
                         finished || stagedProgress >= (i + 1f) / (originalRenderers.Length + 1f));
        }

        private void CreateBeam(Vector3 position, Vector3 scale)
        {
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.transform.SetParent(scaffold.transform, false);
            beam.transform.localPosition = position;
            beam.transform.localScale = scale;
            beam.GetComponent<Collider>().enabled = false;
            if (Application.isPlaying) Destroy(beam.GetComponent<Collider>());
            else DestroyImmediate(beam.GetComponent<Collider>());
            beam.GetComponent<Renderer>().sharedMaterial = scaffoldMaterial;
        }

        private void OnDestroy()
        {
            Unbind();
            if (scaffoldMaterial != null)
            {
                if (Application.isPlaying) Destroy(scaffoldMaterial);
                else DestroyImmediate(scaffoldMaterial);
            }
        }
    }
}
