using System;
using Game.Domain.Characters;
using Game.Simulation.Characters;
using UnityEngine;

namespace Game.Presentation.Characters
{
    public sealed class CharacterPresentationSpawner
    {
        private readonly CharacterPresentationCatalog catalog;
        private readonly CharacterPresentationRegistry registry;
        private readonly CharacterAppearanceService appearances;
        private readonly Transform parent;

        public CharacterPresentationSpawner(CharacterPresentationCatalog catalog,
            CharacterPresentationRegistry registry, CharacterAppearanceService appearanceService = null,
            Transform parent = null)
        {
            this.catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            appearances = appearanceService;
            this.parent = parent;
        }

        public CharacterPresenter Spawn(Character character, Vector3 position, Quaternion rotation)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            var requested = appearances != null
                ? appearances.GetOrCreate(character)
                : catalog.CreateDefinitionCatalog().DefaultFor(character.Sex).CreateState(character.Id);
            var prefab = catalog.Resolve(character, requested, out var resolved);
            var instance = UnityEngine.Object.Instantiate(prefab, position, rotation, parent);
            var presenter = instance.GetComponent<CharacterPresenter>();
            try
            {
                // Only editor-confirmed/key-character states carry detailed parameters. Aggregate NPC defaults stay
                // on the bounded fallback presentation and never start a mass of UMA generation coroutines.
                if (catalog.UmaAvatarPrefab != null && requested.Parameters.Count > 0)
                    instance.AddComponent<UmaAppearanceProjection>().Configure(catalog.UmaAvatarPrefab, requested);
                presenter.Bind(character, registry, requested);
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
