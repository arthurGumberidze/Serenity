using System;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Resources;
using UnityEngine;

namespace Game.Presentation.Buildings
{
    public sealed class BuildingPresenter : MonoBehaviour
    {
        private Building building;
        private BuildingPresentationRegistry registry;
        private ResourceInventory storageInventory;

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
        }

        public void Unbind()
        {
            if (!IsBound) return;
            registry?.Unregister(building.Id, this);
            building = null;
            registry = null;
            storageInventory = null;
        }

        private void OnDestroy() => Unbind();
    }
}
