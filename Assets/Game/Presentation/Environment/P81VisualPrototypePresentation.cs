using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Domain;
using Game.Domain.Characters;
using UnityEngine;

namespace Game.Presentation.Environment
{
    /// <summary>Small art-direction benchmark. Canonical appearance remains data-only and UMA is optional.</summary>
    [DisallowMultipleComponent]
    public sealed class P81VisualPrototypePresentation : MonoBehaviour
    {
        [SerializeField] private string stableCharacterId = "81000000000000000000000000000001";
        [SerializeField] private Transform benchmarkCenter;
        [SerializeField] private GameObject umaVisual;
        [SerializeField] private GameObject fallbackVisual;
        [SerializeField] private Renderer[] environmentRenderers = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] trunkRenderers = Array.Empty<Renderer>();

        public Transform BenchmarkCenter => benchmarkCenter;
        public bool UsesUma { get; private set; }
        public bool HasVisibleCharacter => umaVisual != null && umaVisual.activeInHierarchy ||
            fallbackVisual != null && fallbackVisual.activeInHierarchy;
        public IReadOnlyList<Renderer> EnvironmentRenderers => environmentRenderers;
        public IReadOnlyList<Renderer> TrunkRenderers => trunkRenderers;
        public string ActiveProvider => UsesUma ? "uma3" : "project_fallback";

        public AppearanceState CanonicalAppearance => new AppearanceState(
            StableEntityId.Parse(stableCharacterId), new AppearanceProfileId("uma_human_male_30"),
            Part("uma_human_male_30_body"), Part("uma_human_male_30_face"), Part("uma_skin_warm"),
            Part("uma_hair_short"), Part("uma_beard_short"), Part("uma_hair_brown"),
            Part("uma_stone_age_clothing"));

        public void Configure(Transform center, GameObject uma, GameObject fallback,
            IEnumerable<Renderer> environment, IEnumerable<Renderer> trunks)
        {
            benchmarkCenter = center;
            umaVisual = uma;
            fallbackVisual = fallback;
            environmentRenderers = (environment ?? Array.Empty<Renderer>()).Where(x => x != null).ToArray();
            trunkRenderers = (trunks ?? Array.Empty<Renderer>()).Where(x => x != null).ToArray();
        }

        public void ValidateConfiguration()
        {
            _ = CanonicalAppearance;
            if (benchmarkCenter == null) throw new InvalidOperationException("P8.1 benchmark center is missing.");
            if (fallbackVisual == null) throw new InvalidOperationException("P8.1 fallback character is missing.");
            if (environmentRenderers == null || environmentRenderers.Length < 8)
                throw new InvalidOperationException("P8.1 needs a representative natural environment.");
            if (trunkRenderers == null || trunkRenderers.Length < 3)
                throw new InvalidOperationException("P8.1 needs visible brown tree trunks.");
        }

        private void Awake()
        {
            ValidateConfiguration();
            if (fallbackVisual != null) fallbackVisual.SetActive(true);
            if (umaVisual != null) StartCoroutine(ResolveVisualProvider());
        }

        private IEnumerator ResolveVisualProvider()
        {
            yield return new WaitForSecondsRealtime(0.25f);
            var avatar = umaVisual.GetComponents<MonoBehaviour>()
                .FirstOrDefault(x => x != null && x.GetType().FullName ==
                    "UMA.CharacterSystem.DynamicCharacterAvatar");
            var generateNow = avatar?.GetType().GetMethod("GenerateNow", BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            var setSlot = avatar?.GetType().GetMethod("SetSlot", BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(string) }, null);
            if (generateNow != null)
            {
                try
                {
                    foreach (var recipe in new[]
                    {
                        "bb_Male_Military_Hair_Recipe", "Beard_Trimmed_Recipe",
                        "male_tanktop_yellow_Recipe", "male_sweatpants_black_Recipe"
                    }) setSlot?.Invoke(avatar, new object[] { recipe });
                    generateNow.Invoke(avatar, null);
                }
                catch (TargetInvocationException exception)
                {
                    Debug.LogWarning("P81 UMA generation failed; using project fallback. " +
                        (exception.InnerException ?? exception));
                }
            }
            var deadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var generated = umaVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Any(x => x.sharedMesh != null && x.sharedMesh.vertexCount > 0 && x.sharedMaterials.Length > 0);
                if (generated)
                {
                    UsesUma = true;
                    fallbackVisual.SetActive(false);
                    yield break;
                }
                yield return null;
            }
            var diagnostics = umaVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Select(x => x.name + ":" + (x.sharedMesh == null ? "null" : x.sharedMesh.vertexCount.ToString()))
                .ToArray();
            Debug.LogWarning("P81_UMA_FALLBACK " + string.Join(",", diagnostics) + "; " + DescribeUmaState(avatar));
            UsesUma = false;
            umaVisual.SetActive(false);
            fallbackVisual.SetActive(true);
        }

        private static string DescribeUmaState(MonoBehaviour avatar)
        {
            if (avatar == null) return "avatar=null";
            var type = avatar.GetType();
            var recipe = type.GetProperty("umaRecipe", BindingFlags.Public | BindingFlags.Instance)?.GetValue(avatar);
            var slots = recipe?.GetType().GetField("slotDataList", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(recipe) as Array;
            var meshDirty = type.GetField("isMeshDirty", BindingFlags.Public | BindingFlags.Instance)?.GetValue(avatar);
            var textureDirty = type.GetField("isTextureDirty", BindingFlags.Public | BindingFlags.Instance)?.GetValue(avatar);
            var dirty = type.GetField("dirty", BindingFlags.Public | BindingFlags.Instance)?.GetValue(avatar);
            var generatedMaterials = type.GetField("generatedMaterials", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(avatar);
            var materials = generatedMaterials?.GetType().GetField("materials", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(generatedMaterials) as System.Collections.ICollection;
            var rendererAssets = generatedMaterials?.GetType().GetField("rendererAssets", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(generatedMaterials) as System.Collections.ICollection;
            return $"slots={slots?.Length.ToString() ?? "null"}; generatedMaterials={materials?.Count.ToString() ?? "null"}; " +
                $"rendererAssets={rendererAssets?.Count.ToString() ?? "null"}; meshDirty={meshDirty}; textureDirty={textureDirty}; dirty={dirty}";
        }

        private static AppearancePartId Part(string value) => new AppearancePartId(value);
    }
}
