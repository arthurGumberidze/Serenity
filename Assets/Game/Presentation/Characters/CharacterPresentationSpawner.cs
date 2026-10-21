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
                if (catalog.TryResolveRoleVisual(character.Sex, requested.ClothingId, out var roleVisual))
                {
                    ProjectRoleVisual(instance, presenter, roleVisual);
                }
                else if (catalog.UmaAvatarPrefab != null && detailedUmaCharacters.Count < MaximumDetailedUmaProjections)
                {
                    detailedUmaCharacters.Add(character.Id);
                    instance.AddComponent<UmaAppearanceProjection>().Configure(catalog.UmaAvatarPrefab, requested);
                }
                presenter.Bind(character, registry, requested);
                presenter.ConfigureWorkTool(catalog.WorkAxe);
                return presenter;
            }
            catch
            {
                UnityEngine.Object.Destroy(instance);
                throw;
            }
        }

        private static void ProjectRoleVisual(GameObject instance, CharacterPresenter presenter, GameObject rolePrefab)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            var visual = UnityEngine.Object.Instantiate(rolePrefab, instance.transform, false);
            visual.name = "RoleVisual";
            SetLayerRecursively(visual, instance.layer);
            var roleAnimator = visual.GetComponentInChildren<Animator>(true);
            if (roleAnimator == null || !roleAnimator.isHuman)
                throw new InvalidOperationException("Role visual requires a valid humanoid Animator: " + rolePrefab.name);
            if (presenter.Animator != null)
                roleAnimator.runtimeAnimatorController = presenter.Animator.runtimeAnimatorController;
            roleAnimator.applyRootMotion = false;
            presenter.SetVisualAnimator(roleAnimator);
        }

        private static void SetLayerRecursively(GameObject value, int layer)
        {
            value.layer = layer;
            for (var index = 0; index < value.transform.childCount; index++)
                SetLayerRecursively(value.transform.GetChild(index).gameObject, layer);
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
