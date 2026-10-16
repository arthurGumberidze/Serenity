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
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            var color = building.IsDestroyed ? new Color(0.18f, 0.16f, 0.15f) :
                building.StructuralStatus == BuildingStructuralStatus.Critical ? new Color(0.48f, 0.22f, 0.12f) :
                building.StructuralStatus == BuildingStructuralStatus.Damaged ? new Color(0.62f, 0.42f, 0.18f) :
                building.OwnerSideId.ToString() == "enemy" ? new Color(0.65f, 0.24f, 0.22f) : Color.white;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (fireParticles != null && renderer.transform.IsChildOf(fireParticles.transform)) continue;
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

        private void OnDestroy() => Unbind();
    }
}
