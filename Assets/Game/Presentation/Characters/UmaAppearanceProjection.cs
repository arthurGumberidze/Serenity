using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Game.Domain.Characters;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Presentation.Characters
{
    /// <summary>Replaceable UMA 3 projection of canonical AppearanceState. It never owns gameplay state.</summary>
    [DisallowMultipleComponent]
    public sealed class UmaAppearanceProjection : MonoBehaviour
    {
        private static readonly Dictionary<string, string> HairRecipes = new Dictionary<string, string>
        {
            { "hair_very_short", "Hair_VeryShort_Recipe" }, { "hair_bun", "Hair_Bun_Recipe" },
            { "hair_messy", "Hair_MessyRightPart_Recipe" }
        };
        private static readonly Dictionary<string, string> BeardRecipes = new Dictionary<string, string>
        {
            { "beard_trimmed", "Beard_Trimmed_Recipe" }, { "beard_goatee", "Beard_Goatee_Recipe" },
            { "beard_neckbeard", "Beard_Neckbeard_Recipe" }
        };
        private static readonly Dictionary<string, Color> SkinColors = new Dictionary<string, Color>
        {
            { "skin_light", new Color(0.86f, 0.68f, 0.55f) }, { "skin_warm", new Color(0.64f, 0.42f, 0.29f) },
            { "skin_deep", new Color(0.32f, 0.19f, 0.13f) }
        };
        private static readonly Dictionary<string, Color> HairColors = new Dictionary<string, Color>
        {
            { "hair_black", new Color(0.05f, 0.04f, 0.035f) }, { "hair_brown", new Color(0.20f, 0.10f, 0.05f) },
            { "hair_auburn", new Color(0.38f, 0.13f, 0.06f) }, { "hair_blond", new Color(0.72f, 0.58f, 0.30f) },
            { "hair_gray", new Color(0.48f, 0.47f, 0.44f) }
        };
        private static readonly Dictionary<string, Color> EyeColors = new Dictionary<string, Color>
        {
            { "eye_brown", new Color(0.18f, 0.09f, 0.04f) }, { "eye_hazel", new Color(0.34f, 0.27f, 0.09f) },
            { "eye_green", new Color(0.12f, 0.32f, 0.16f) }, { "eye_blue", new Color(0.12f, 0.30f, 0.48f) },
            { "eye_gray", new Color(0.34f, 0.38f, 0.40f) }
        };
        private static readonly Dictionary<AppearanceParameterId, string> DnaNames =
            new Dictionary<AppearanceParameterId, string>
            {
                { StoneAgeAppearanceProfiles.Head, "headSize" }, { StoneAgeAppearanceProfiles.Jaw, "jawsSize" },
                { StoneAgeAppearanceProfiles.Chin, "chinSize" }, { StoneAgeAppearanceProfiles.Nose, "noseSize" },
                { StoneAgeAppearanceProfiles.Eyes, "eyeSize" }, { StoneAgeAppearanceProfiles.Brows, "BrowPosition" },
                { StoneAgeAppearanceProfiles.Lips, "lipsSize" }, { StoneAgeAppearanceProfiles.Height, "height" }
            };

        private GameObject umaPrefab;
        private GameObject umaInstance;
        private MonoBehaviour avatar;
        private Renderer[] fallbackRenderers = Array.Empty<Renderer>();
        private Coroutine generation;
        private AppearanceState appearance;
        private string expectedWardrobeRecipe;
        public bool IsGenerating { get; private set; }
        public string GenerationFailure { get; private set; }

        public AppearanceState Appearance => appearance;
        public bool UsesUma { get; private set; }
        public bool StoneAgeWardrobeApplied { get; private set; }
        public bool WardrobeFallbackUsed { get; private set; }
        public bool HasMissingScripts => umaInstance != null &&
            umaInstance.GetComponentsInChildren<Transform>(true).Any(item =>
                item.GetComponents<Component>().Any(component => component == null));
        public int GenerationCount { get; private set; }
        public long LastGenerationMilliseconds { get; private set; }

        public void Configure(GameObject prefab, AppearanceState state)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            umaPrefab = prefab;
            Apply(state);
        }

        public void Apply(AppearanceState state)
        {
            appearance = state ?? throw new ArgumentNullException(nameof(state));
            if (umaPrefab == null) return;
            if (umaInstance == null)
            {
                fallbackRenderers = GetComponentsInChildren<Renderer>(true);
                umaInstance = Instantiate(umaPrefab, transform);
                umaInstance.name = "UMA_Appearance_" + state.CharacterId;
                umaInstance.transform.localPosition = Vector3.zero;
                umaInstance.transform.localRotation = Quaternion.identity;
                SetLayerRecursively(umaInstance, gameObject.layer);
                foreach (var collider in umaInstance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var body in umaInstance.GetComponentsInChildren<Rigidbody>(true))
                {
                    body.isKinematic = true;
                    body.detectCollisions = false;
                }
                avatar = umaInstance.GetComponents<MonoBehaviour>().FirstOrDefault(component => component != null &&
                    component.GetType().FullName == "UMA.CharacterSystem.DynamicCharacterAvatar");
            }
            if (generation != null) StopCoroutine(generation);
            generation = StartCoroutine(Generate());
        }

        private IEnumerator Generate()
        {
            IsGenerating = true;
            UsesUma = false;
            StoneAgeWardrobeApplied = false;
            WardrobeFallbackUsed = false;
            GenerationFailure = null;
            SetFallbackVisible(false);
            if (avatar == null)
            {
                GenerationFailure = "Не найден компонент модели персонажа";
                IsGenerating = false;
                yield break;
            }
            var watch = Stopwatch.StartNew();
            Exception failure = null;
            try { ProjectState(); }
            catch (Exception exception) { failure = exception is TargetInvocationException tie ? tie.InnerException ?? tie : exception; }
            GenerationCount++;
            var deadline = Time.realtimeSinceStartup + 30f;
            while (failure == null && Time.realtimeSinceStartup < deadline)
            {
                RestoreGeneratedMaterialBindings();
                var generated = umaInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(renderer => renderer.sharedMesh != null && renderer.sharedMesh.vertexCount > 0).ToArray();
                var materialsReady = generated.Length > 0 && generated.All(renderer =>
                    renderer.sharedMaterials.Select((material, index) => new { material, index }).All(slot =>
                        slot.index >= renderer.sharedMesh.subMeshCount || renderer.sharedMesh.GetIndexCount(slot.index) == 0 ||
                        PrepareGeneratedMaterial(slot.material)));
                foreach (var renderer in generated) renderer.enabled = materialsReady;
                if (materialsReady)
                {
                    // UMA creates renderer children after the prefab is instantiated. Keep those late-created
                    // objects on the presenter's layer so isolated preview cameras can render the generated body.
                    SetLayerRecursively(umaInstance, gameObject.layer);
                    StoneAgeWardrobeApplied = string.Equals(
                        Invoke("GetWardrobeItemName", new[] { typeof(string) }, "Chest") as string,
                        expectedWardrobeRecipe, StringComparison.Ordinal);
                    UsesUma = true;
                    var generatedAnimator = umaInstance.GetComponentInChildren<Animator>(true);
                    var owner = GetComponent<CharacterPresenter>();
                    SetFallbackVisible(false);
                    if (generatedAnimator != null && owner != null && owner.Animator != null)
                    {
                        generatedAnimator.runtimeAnimatorController = owner.Animator.runtimeAnimatorController;
                        owner.SetVisualAnimator(generatedAnimator);
                    }
                    break;
                }
                yield return null;
            }
            watch.Stop();
            LastGenerationMilliseconds = watch.ElapsedMilliseconds;
            if (!UsesUma)
            {
                umaInstance.SetActive(false);
                GenerationFailure = failure?.Message ?? "Не удалось подготовить модель персонажа";
                SetFallbackVisible(false);
                Debug.LogWarning("P84_UMA_GENERATION_FAILED " + GenerationFailure);
            }
            generation = null;
            IsGenerating = false;
        }

        private static bool PrepareGeneratedMaterial(Material material)
        {
            if (material == null || material.shader == null || !material.shader.isSupported) return false;
            if (!material.HasProperty("_BaseMap") || !material.HasProperty("_BaseColor")) return true;
            // Some admitted UMA recipes write the generated atlas to the legacy texture property.
            if (material.GetTexture("_BaseMap") == null && material.HasProperty("_MainTex") &&
                material.GetTexture("_MainTex") != null)
            {
                material.SetTexture("_BaseMap", material.GetTexture("_MainTex"));
                material.SetTextureScale("_BaseMap", material.GetTextureScale("_MainTex"));
                material.SetTextureOffset("_BaseMap", material.GetTextureOffset("_MainTex"));
            }
            var color = material.GetColor("_BaseColor");
            return material.GetTexture("_BaseMap") != null || color.r < .92f || color.g < .92f || color.b < .92f;
        }

        private static object Member(object owner, string name)
        {
            if (owner == null) return null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            return owner.GetType().GetField(name, flags)?.GetValue(owner) ??
                owner.GetType().GetProperty(name, flags)?.GetValue(owner);
        }

        private void RestoreGeneratedMaterialBindings()
        {
            // Work with UMA's actual generated atlas/channel associations; never borrow another
            // character's texture or replace the user's body with the role-preview mesh.
            var data = umaInstance.GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(value => value != null && value.GetType().FullName == "UMA.UMAData");
            var generated = Member(Member(data, "generatedMaterials"), "materials") as IEnumerable;
            if (generated == null) return;
            foreach (var entry in generated)
            {
                var material = Member(entry, "material") as Material;
                if (material == null || !material.HasProperty("_BaseMap")) continue;
                var definition = Member(entry, "umaMaterial");
                var channels = (Member(definition, "channels") as IEnumerable)?.Cast<object>().ToArray();
                var atlases = Member(entry, "resultingAtlasList") as Texture[];
                if (channels != null && atlases != null)
                    for (var i = 0; i < Math.Min(channels.Length, atlases.Length); i++)
                    {
                        var property = Member(channels[i], "materialPropertyName") as string;
                        if ((property == "_BaseMap" || property == "_MainTex") && atlases[i] != null &&
                            material.GetTexture("_BaseMap") == null)
                            material.SetTexture("_BaseMap", atlases[i]);
                    }
                // The admitted alpha/hair definition still names Standard's _Color. URP Lit
                // reads _BaseColor, so an untextured color-only hair slot otherwise stays white.
                var parameters = Member(definition, "shaderParms") as IEnumerable;
                if (parameters == null || !material.HasProperty("_BaseColor")) continue;
                foreach (var parameter in parameters)
                {
                    if ((Member(parameter, "ParameterName") as string) != "_Color") continue;
                    var name = Member(parameter, "ColorName") as string;
                    if (name == "Hair") material.SetColor("_BaseColor", HairColors[appearance.HairColorId.Value]);
                    else if (name == "Skin") material.SetColor("_BaseColor", SkinColors[appearance.SkinId.Value]);
                    else if (name == "Eyes") material.SetColor("_BaseColor", EyeColors[appearance.EyeColorId.Value]);
                }
            }
        }

        private void ProjectState()
        {
            umaInstance.SetActive(true);
            Invoke("ChangeRace", new[] { typeof(string), typeof(bool) },
                appearance.ProfileId == StoneAgeAppearanceProfiles.UmaFemale ? "Human Female 3.0" : "Human Male 3.0", true);
            foreach (var slot in new[] { "Hair", "Beard", "Chest", "Legs", "Face" })
                Invoke("ClearSlot", new[] { typeof(string), typeof(bool) }, slot, true);
            if (HairRecipes.TryGetValue(appearance.HairId.Value, out var hair)) SetSlot(hair);
            if (BeardRecipes.TryGetValue(appearance.BeardId.Value, out var beard)) SetSlot(beard);
            if (appearance.AgeId.Value == "age_mature") SetSlot("Aged_U_Wardrobe");
            else if (appearance.AgeId.Value == "age_aged") SetSlot("Aged_U2_Wardrobe");
            var female = appearance.ProfileId == StoneAgeAppearanceProfiles.UmaFemale;
            // Project-owned recipes reuse the admitted UMA Human 3.0 mesh with its leather-only overlay.
            // Wrapped trousers plus bare feet avoid modern demo clothes and the metallic medieval greaves.
            expectedWardrobeRecipe = female ? "F_StoneAgeHideTunic_Recipe" : "M_StoneAgeHideTunic_Recipe";
            if (appearance.ClothingId != StoneAgeAppearanceProfiles.CivilianClothing)
            {
                WardrobeFallbackUsed = true;
                Debug.LogWarning("P105_WARDROBE_FALLBACK clothing=" + appearance.ClothingId +
                                 "; character=" + appearance.CharacterId +
                                 "; admitted visual content is not available; canonical clothing ID is preserved.");
            }
            SetSlot(expectedWardrobeRecipe);
            SetSlot(female ? "F_Wrapped Pants_Recipe" : "M_Wrapped Pants_Recipe");
            SetColor("Skin", SkinColors[appearance.SkinId.Value]);
            SetColor("Hair", HairColors[appearance.HairColorId.Value]);
            SetColor("Eyes", EyeColors[appearance.EyeColorId.Value]);
            foreach (var parameter in appearance.Parameters)
                if (DnaNames.TryGetValue(parameter.Id, out var dna)) SetDna(dna, parameter.Normalized);
            var build = appearance.Parameter(StoneAgeAppearanceProfiles.Build) / 1000f;
            SetDna("upperMuscle", build); SetDna("lowerMuscle", build);
            SetDna("upperWeight", Mathf.Lerp(0.25f, 0.65f, build)); SetDna("lowerWeight", Mathf.Lerp(0.25f, 0.65f, build));
            Invoke("GenerateNow", Type.EmptyTypes);
        }

        private void SetSlot(string recipe) => Invoke("SetSlot", new[] { typeof(string) }, recipe);
        private void SetDna(string name, float value) => Invoke("SetDNA", new[] { typeof(string), typeof(float), typeof(bool) }, name, value, false);
        private void SetColor(string name, Color color) => Invoke("SetColor",
            new[] { typeof(string), typeof(Color), typeof(Color), typeof(float), typeof(bool) },
            name, color, new Color(), 0f, false);
        private object Invoke(string name, Type[] signature, params object[] args)
        {
            var method = avatar.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, signature, null);
            if (method == null) throw new MissingMethodException(avatar.GetType().FullName, name);
            return method.Invoke(avatar, args);
        }
        private void SetFallbackVisible(bool visible)
        {
            foreach (var renderer in fallbackRenderers) if (renderer != null) renderer.enabled = visible;
        }
        private static void SetLayerRecursively(GameObject value, int layer)
        {
            value.layer = layer;
            for (var i = 0; i < value.transform.childCount; i++)
                SetLayerRecursively(value.transform.GetChild(i).gameObject, layer);
        }
    }
}
