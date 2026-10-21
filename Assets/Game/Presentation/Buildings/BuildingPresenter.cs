using System;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Presentation.Interaction;
using UnityEngine;

namespace Game.Presentation.Buildings
{
    public sealed class BuildingPresenter : MonoBehaviour
    {
        private Building building;
        private BuildingPresentationRegistry registry;
        private ResourceInventory storageInventory;
        private ParticleSystem fireParticles;
        private MaterialPropertyBlock propertyBlock;
        private Renderer[] originalRenderers;
        private bool[] originalVisibility;
        private GameObject scaffold;
        private Material scaffoldMaterial;

        public bool IsBound => building != null;
        public Building Building => building;
        public StableEntityId BuildingId => building != null ? building.Id : throw new InvalidOperationException("Presenter is not bound.");
        public ConstructionState ConstructionState => building != null ? building.ConstructionState : throw new InvalidOperationException("Presenter is not bound.");
        public ResourceInventory StorageInventory => storageInventory;

        public void Bind(Building source, BuildingPresentationRegistry presentationRegistry, ResourceInventory inventory = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (presentationRegistry == null) throw new ArgumentNullException(nameof(presentationRegistry));
            if (IsBound) throw new InvalidOperationException("Presenter is already bound.");
            presentationRegistry.Register(source.Id, this);
            building = source;
            registry = presentationRegistry;
            storageInventory = inventory;
            originalRenderers = GetComponentsInChildren<Renderer>(true);
            originalVisibility = Array.ConvertAll(originalRenderers, x => x.enabled);
            gameObject.name = $"Building {source.DefinitionId} [{source.Id}]";
            var hitProxy = GetComponent<StableWorldHitProxy>() ?? gameObject.AddComponent<StableWorldHitProxy>();
            hitProxy.Bind(source.Id, ContextTargetKind.Building);
            RefreshStructuralPresentation();
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
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            var color = building.IsDestroyed ? new Color(0.18f, 0.16f, 0.15f) :
                building.StructuralStatus == BuildingStructuralStatus.Critical ? new Color(0.48f, 0.22f, 0.12f) :
                building.StructuralStatus == BuildingStructuralStatus.Damaged ? new Color(0.62f, 0.42f, 0.18f) :
                building.OwnerSideId.ToString() == "enemy" ? new Color(0.65f, 0.24f, 0.22f) : Color.white;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (fireParticles != null && renderer.transform.IsChildOf(fireParticles.transform)) continue;
                if (scaffold != null && renderer.transform.IsChildOf(scaffold.transform)) continue;
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
            var fireObject = new GameObject("Building Fire VFX");
            fireObject.transform.SetParent(transform, false);
            fireObject.transform.localPosition = Vector3.up * 0.75f;
            var particles = fireObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.startLifetime = 0.8f;
            main.startSpeed = 0.7f;
            main.startSize = 0.35f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.22f, 0.02f, 0.85f),
                new Color(1f, 0.75f, 0.05f, 0.75f));
            main.maxParticles = 32;
            var emission = particles.emission;
            emission.rateOverTime = 16f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.2f, 0.1f, 1.2f);
            return particles;
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
            for (var i = 0; i < originalRenderers.Length; i++)
                if (originalRenderers[i] != null)
                    originalRenderers[i].enabled = originalVisibility[i] &&
                        (finished || building.ConstructionProgress >= (i + 1f) / (originalRenderers.Length + 1f));
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
