using System;
using System.Collections.Generic;
using Game.Domain;
using Game.Domain.Characters;
using Game.Simulation.Characters;
using UnityEngine;

namespace Game.Presentation.Characters
{
    public sealed class CharacterPresentationSpawner
    {
        public const int MaximumDetailedUmaProjections = 8;
        private readonly CharacterPresentationCatalog catalog;
        private readonly CharacterPresentationRegistry registry;
        private readonly CharacterAppearanceService appearances;
        private readonly Transform parent;
        private readonly Func<Vector3, Vector3> positionProjector;
        private readonly HashSet<StableEntityId> detailedUmaCharacters = new HashSet<StableEntityId>();

        public CharacterPresentationSpawner(CharacterPresentationCatalog catalog,
            CharacterPresentationRegistry registry, CharacterAppearanceService appearanceService = null,
            Transform parent = null, Func<Vector3, Vector3> presentationPositionProjector = null)
        {
            this.catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            appearances = appearanceService;
            this.parent = parent;
            positionProjector = presentationPositionProjector;
        }

        public CharacterPresenter Spawn(Character character, Vector3 position, Quaternion rotation)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            var requested = appearances != null
                ? appearances.GetOrCreate(character)
                : catalog.CreateDefinitionCatalog().DefaultFor(character.Sex).CreateState(character.Id);
            var prefab = catalog.Resolve(character, requested, out var resolved);
            var presentationPosition = positionProjector != null ? positionProjector(position) : position;
            var instance = UnityEngine.Object.Instantiate(prefab, presentationPosition, rotation, parent);
            var presenter = instance.GetComponent<CharacterPresenter>();
            try
            {
                // UMA generation is bounded to the same eight detailed Tier 1 views used by the crowd renderer.
                // This gives nearby key/family NPCs coherent Human 3.0 art without starting a coroutine per mass NPC.
                if (catalog.UmaAvatarPrefab != null && (requested.Parameters.Count > 0 ||
                    detailedUmaCharacters.Count < MaximumDetailedUmaProjections))
                {
                    detailedUmaCharacters.Add(character.Id);
                    instance.AddComponent<UmaAppearanceProjection>().Configure(catalog.UmaAvatarPrefab, requested);
                }
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
            detailedUmaCharacters.Remove(presenter.CharacterId);
            presenter.Unbind();
            UnityEngine.Object.Destroy(presenter.gameObject);
        }
    }
}
