using System;
using System.Collections.Generic;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Military;
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
        private readonly Func<StableEntityId, MilitaryRank?> militaryRank;
        private readonly HashSet<StableEntityId> detailedUmaCharacters = new HashSet<StableEntityId>();
        private readonly Dictionary<StableEntityId, GameObject> residentStyles = new();

        private GameObject ResidentStyle(Character character)
        {
            var rank = militaryRank?.Invoke(character.Id);
            if (rank.HasValue && catalog.MilitaryUniforms != null)
            {
                var uniform = catalog.MilitaryUniforms.Resolve(rank.Value, character.Sex);
                if (uniform != null) return uniform;
            }
            return isRuler != null && !isRuler(character)
            ? appearances?.GetOrCreate(character).ClothingId == StoneAgeAppearanceProfiles.MilitaryClothing
                ? catalog.ResolveTrainedResident(character.Sex) ?? catalog.ResolveResident(character.Sex,false)
                : catalog.ResolveResident(character.Sex, isChild?.Invoke(character) == true) : null;
        }

        public bool RequiresStyleRefresh(Character character) => character != null &&
            residentStyles.TryGetValue(character.Id, out var previous) && previous != ResidentStyle(character);

        public CharacterPresentationSpawner(CharacterPresentationCatalog catalog,
            CharacterPresentationRegistry registry, CharacterAppearanceService appearanceService = null,
            Transform parent = null, Func<Vector3, Vector3> presentationPositionProjector = null,
            Func<Character, bool> rulerSelector = null, Func<Character, bool> childSelector = null,
            Func<StableEntityId, MilitaryRank?> militaryRankSelector = null)
        {
            this.catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            appearances = appearanceService;
            this.parent = parent;
            positionProjector = presentationPositionProjector;
            isRuler = rulerSelector;
            isChild = childSelector;
            militaryRank = militaryRankSelector;
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
                    ProjectRoleVisual(instance, presenter, resident, requested);
                }
                else if (catalog.UmaAvatarPrefab != null && detailedUmaCharacters.Count < MaximumDetailedUmaProjections)
                {
                    detailedUmaCharacters.Add(character.Id);
                    instance.AddComponent<UmaAppearanceProjection>().Configure(catalog.UmaAvatarPrefab, requested);
                }
                else if (catalog.UmaAvatarPrefab == null &&
                         catalog.TryResolveRoleVisual(character.Sex, requested.ClothingId, out var roleVisual))
                {
                    ProjectRoleVisual(instance, presenter, roleVisual, requested);
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

        private static void ProjectRoleVisual(GameObject instance, CharacterPresenter presenter, GameObject rolePrefab, AppearanceState appearance)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            var visual = UnityEngine.Object.Instantiate(rolePrefab, instance.transform, false);
            visual.name = "RoleVisual";
            ConfigureMilitaryArmorVariant(visual, rolePrefab);
            // Stable saved DNA, no additional avatar/texture generation or per-resident Update.
            var height = Mathf.Lerp(.92f, 1.08f, appearance.Parameter(StoneAgeAppearanceProfiles.Height) / 1000f);
            var build = Mathf.Lerp(.91f, 1.10f, appearance.Parameter(StoneAgeAppearanceProfiles.Build) / 1000f);
            visual.transform.localScale = Vector3.Scale(visual.transform.localScale, new Vector3(build, height, build));
            if (rolePrefab.name.IndexOf("Peasant", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                uint seed = 2166136261;
                foreach (var ch in appearance.CharacterId.ToString()) seed = (seed ^ ch) * 16777619;
                var tint = Color.Lerp(new Color(.78f,.80f,.76f), new Color(1f,.94f,.84f), (seed % 101) / 100f);
                var palette = new MaterialPropertyBlock();
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
                {
                    renderer.GetPropertyBlock(palette);
                    palette.SetColor("_BaseColor", tint); palette.SetColor("_Color", tint);
                    renderer.SetPropertyBlock(palette);
                }
            }
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

        private static void ConfigureMilitaryArmorVariant(GameObject visual, GameObject source)
        {
            if (source == null || source.name.IndexOf("Lowpoly_Armors", StringComparison.OrdinalIgnoreCase) < 0)
                return;
            var palette = new MaterialPropertyBlock();
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var part = renderer.gameObject.name;
                // Use one complete authored suit. The vendor showcase prefab enables several
                // mutually exclusive bodies at once, so projecting it without filtering looked
                // like ordinary/overlapping clothing instead of a soldier in armour.
                renderer.enabled = part.IndexOf("_Armor_01_A_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   part.EndsWith("_Armor_head_01", StringComparison.OrdinalIgnoreCase);
                if (!renderer.enabled) continue;

                // The free vendor showcase material serializes its tint slots with transparent,
                // HDR-white defaults. In URP Player builds that turns the selected suit into a
                // featureless white silhouette. Keep the shared vendor material immutable and
                // project a restrained Stone-Age military palette per renderer instead.
                renderer.GetPropertyBlock(palette);
                SetPalette(palette, "_Color", new Color(.42f, .45f, .43f, 1f));
                SetPalette(palette, "_BaseColor", new Color(.42f, .45f, .43f, 1f));
                SetPalette(palette, "_METAL1COLOR", new Color(.28f, .31f, .32f, 1f));
                SetPalette(palette, "_METAL2COLOR", new Color(.46f, .50f, .51f, 1f));
                SetPalette(palette, "_METAL3COLOR", new Color(.16f, .18f, .19f, 1f));
                SetPalette(palette, "_LEATHER1COLOR", new Color(.24f, .12f, .055f, 1f));
                SetPalette(palette, "_LEATHER2COLOR", new Color(.39f, .22f, .09f, 1f));
                SetPalette(palette, "_LEATHER3COLOR", new Color(.13f, .065f, .025f, 1f));
                SetPalette(palette, "_CLOTH1COLOR", new Color(.075f, .14f, .16f, 1f));
                SetPalette(palette, "_CLOTH2COLOR", new Color(.27f, .055f, .045f, 1f));
                SetPalette(palette, "_CLOTH3COLOR", new Color(.34f, .27f, .16f, 1f));
                SetPalette(palette, "_SKINCOLOR", new Color(.57f, .34f, .22f, 1f));
                SetPalette(palette, "_HAIRCOLOR", new Color(.09f, .045f, .02f, 1f));
                SetPalette(palette, "_EYESCOLOR", new Color(.08f, .13f, .12f, 1f));
                renderer.SetPropertyBlock(palette);
            }
        }

        private static void SetPalette(MaterialPropertyBlock block, string property, Color value)
        {
            block.SetColor(Shader.PropertyToID(property), value);
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
