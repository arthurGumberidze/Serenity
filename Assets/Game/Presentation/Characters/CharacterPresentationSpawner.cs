using System;
using Game.Domain.Characters;
using UnityEngine;

namespace Game.Presentation.Characters
{
    public sealed class CharacterPresentationSpawner
    {
        private readonly CharacterPresentationCatalog catalog;
        private readonly CharacterPresentationRegistry registry;
        private readonly Transform parent;

        public CharacterPresentationSpawner(CharacterPresentationCatalog catalog,
            CharacterPresentationRegistry registry, Transform parent = null)
        {
            this.catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.parent = parent;
        }

        public CharacterPresenter Spawn(Character character, Vector3 position, Quaternion rotation)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            var prefab = catalog.Resolve(character);
            var instance = UnityEngine.Object.Instantiate(prefab, position, rotation, parent);
            var presenter = instance.GetComponent<CharacterPresenter>();
            try
            {
                presenter.Bind(character, registry);
                return presenter;
            }
            catch
            {
                UnityEngine.Object.Destroy(instance);
                throw;
            }
        }

        public void Despawn(CharacterPresenter presenter)
        {
            if (presenter == null) return;
            presenter.Unbind();
            UnityEngine.Object.Destroy(presenter.gameObject);
        }
    }
}
