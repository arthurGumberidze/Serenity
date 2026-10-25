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
        // Match the maximum detailed simulation tier, not the eight animated views per frame.
        // Limiting avatar identity to the animation budget made residents 9+ permanently use toon bodies.
        public const int MaximumDetailedUmaProjections = 32;
        private readonly CharacterPresentationCatalog catalog;
        private readonly CharacterPresentationRegistry registry;
        private readonly CharacterAppearanceService appearances;
        private readonly Transform parent;
        private readonly Func<Vector3, Vector3> positionProjector;
        private readonly Func<Character, bool> isRuler;
        private readonly Func<Character, bool> isChild;
        private readonly HashSet<StableEntityId> detailedUmaCharacters = new HashSet<StableEntityId>();
        private readonly Dictionary<StableEntityId, GameObject> residentStyles = new();

        private GameObject ResidentStyle(Character character) => isRuler != null && !isRuler(character)
            ? catalog.ResolveResident(character.Sex, isChild?.Invoke(character) == true) : null;

        public bool RequiresStyleRefresh(Character character) => character != null &&
            residentStyles.TryGetValue(character.Id, out var previous) && previous != ResidentStyle(character);

        public CharacterPresentationSpawner(CharacterPresentationCatalog catalog,
            CharacterPresentationRegistry registry, CharacterAppearanceService appearanceService = null,
            Transform parent = null, Func<Vector3, Vector3> presentationPositionProjector = null,
            Func<Character, bool> rulerSelector = null, Func<Character, bool> childSelector = null)
        {
            this.catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            appearances = appearanceService;
            this.parent = parent;
            positionProjector = presentationPositionProjector;
            isRuler = rulerSelector;
            isChild = childSelector;
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
                // User art direction: editable UMA rulers, imported Polytope residents of every profession.
                var resident = ResidentStyle(character);
                residentStyles[character.Id] = resident;
                if (resident != null)
                {
                    ProjectRoleVisual(instance, presenter, resident);
                }
                else if (catalog.UmaAvatarPrefab != null && detailedUmaCharacters.Count < MaximumDetailedUmaProjections)
                {
                    detailedUmaCharacters.Add(character.Id);
                    instance.AddComponent<UmaAppearanceProjection>().Configure(catalog.UmaAvatarPrefab, requested);
                }
                else if (catalog.UmaAvatarPrefab == null &&
                         catalog.TryResolveRoleVisual(character.Sex, requested.ClothingId, out var roleVisual))
                {
                    ProjectRoleVisual(instance, presenter, roleVisual);
                }
                presenter.Bind(character, registry, requested);
                if (resident != null) presenter.PreserveAuthoredVisualAtDistance();
                presenter.ConfigureWorkTools(catalog.WorkAxe, catalog.WorkPickaxe, catalog.WorkHammer, catalog.HuntingBow);
                return presenter;
            }
            catch
            {
                detailedUmaCharacters.Remove(character.Id);
                residentStyles.Remove(character.Id);
                if (presenter != null && presenter.IsBound) presenter.Unbind();
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
            {
                roleAnimator.runtimeAnimatorController = presenter.Animator.runtimeAnimatorController;
                presenter.Animator.enabled = false;
            }
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
            residentStyles.Remove(presenter.CharacterId);
            presenter.Unbind();
            UnityEngine.Object.Destroy(presenter.gameObject);
        }
    }
}
