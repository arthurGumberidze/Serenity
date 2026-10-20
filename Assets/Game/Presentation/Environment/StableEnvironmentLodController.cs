using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Presentation.Environment
{
    /// <summary>One scene-level mesh-only LOD policy. It never rotates or updates individual trees.</summary>
    [DisallowMultipleComponent]
    public sealed class StableEnvironmentLodController : MonoBehaviour
    {
        public const float MinimumLastLodHeight = 0.003f;
        private GameObject[] trees = Array.Empty<GameObject>();
        private GameObject[] scenery = Array.Empty<GameObject>();
        private readonly Dictionary<Material, Material> stableMaterials = new Dictionary<Material, Material>();

        public int TreeCount => trees.Length;
        public int LodGroupCount { get; private set; }
        public int ReplacedBillboardLodCount { get; private set; }

        public void Initialize(IEnumerable<GameObject> values, Func<Vector3, float> groundHeight = null,
            IEnumerable<GameObject> groundScenery = null)
        {
            trees = (values ?? throw new ArgumentNullException(nameof(values))).Where(x => x != null).Distinct().ToArray();
            scenery = (groundScenery ?? Array.Empty<GameObject>()).Where(x => x != null).Except(trees).Distinct().ToArray();
            LodGroupCount = 0;
            ReplacedBillboardLodCount = 0;
            foreach (var tree in trees.Concat(scenery))
            {
                // Authored trees belonged to the retired 512 m Terrain. Keep their X/Z and
                // visual rotation, but anchor the root to the active tiled-world height.
                if (groundHeight != null)
                {
                    var position = tree.transform.position;
                    position.y = groundHeight(position);
                    tree.transform.position = position;
                }
                if (Array.IndexOf(trees, tree) >= 0) ConfigureTree(tree);
            }
        }

        public void RefreshVisibility(Func<Vector3, bool> isGroundRendered)
        {
            if (isGroundRendered == null) throw new ArgumentNullException(nameof(isGroundRendered));
            foreach (var tree in trees.Concat(scenery))
            {
                if (tree == null) continue;
                var source = tree.GetComponent<Game.Presentation.Production.ProductionSourcePresenter>();
                var visible = isGroundRendered(tree.transform.position) &&
                    (source == null || !source.IsBound || !source.Site.IsDepleted);
                if (tree.activeSelf != visible) tree.SetActive(visible);
            }
        }

        public void ConfigureTree(GameObject tree)
        {
            foreach (var group in tree.GetComponentsInChildren<LODGroup>(true)) Configure(group);
            // Imported SpeedTree vertex animation depends on wind/geometry metadata. A static
            // Lit projection keeps the approved meshes/textures without camera-facing or wind deformation.
            foreach (var renderer in tree.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (source == null || !source.shader.name.Contains("SpeedTree")) continue;
                    if (!stableMaterials.TryGetValue(source, out var stable))
                    {
                        stable = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                            { name = "Stable tree " + source.name, enableInstancing = true, renderQueue = 2450 };
                        var texture = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : source.mainTexture;
                        stable.SetTexture("_BaseMap", texture);
                        stable.SetColor("_BaseColor", source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
                        stable.SetFloat("_AlphaClip", 1f);
                        stable.SetFloat("_Cutoff", source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : 0.4f);
                        stable.SetFloat("_Cull", 0f);
                        stable.SetFloat("_Smoothness", 0.15f);
                        stable.EnableKeyword("_ALPHATEST_ON");
                        stableMaterials.Add(source, stable);
                    }
                    materials[i] = stable;
                }
                renderer.sharedMaterials = materials;
            }
        }

        private void OnDestroy()
        {
            foreach (var material in stableMaterials.Values) if (material != null) Destroy(material);
        }

        private void Configure(LODGroup group)
        {
            LodGroupCount++;
            group.fadeMode = LODFadeMode.CrossFade;
            group.animateCrossFading = true;
            var lods = group.GetLODs();
            var meshLods = new List<LOD>();
            for (var index = 0; index < lods.Length; index++)
            {
                lods[index].fadeTransitionWidth = 0.35f;
                var meshRenderers = lods[index].renderers.Where(renderer => renderer != null &&
                    !IsBillboard(renderer)).ToArray();
                foreach (var renderer in lods[index].renderers)
                    if (renderer != null && IsBillboard(renderer)) renderer.enabled = false;
                if (meshRenderers.Length == 0) ReplacedBillboardLodCount++;
                else
                {
                    lods[index].renderers = meshRenderers;
                    meshLods.Add(lods[index]);
                }
            }
            // Extend the last real mesh, without sharing it between two cross-fading LODs.
            if (meshLods.Count > 0)
            {
                var last = meshLods[meshLods.Count - 1];
                last.screenRelativeTransitionHeight = MinimumLastLodHeight;
                meshLods[meshLods.Count - 1] = last;
            }
            group.SetLODs(meshLods.ToArray());
            group.RecalculateBounds();
            foreach (var billboard in group.GetComponentsInChildren<BillboardRenderer>(true)) billboard.enabled = false;
        }

        public static bool IsBillboard(Renderer renderer) => renderer is BillboardRenderer ||
            renderer.sharedMaterials.Any(material => material != null &&
                (material.IsKeywordEnabled("EFFECT_BILLBOARD") ||
                 (material.HasProperty("EFFECT_BILLBOARD") && material.GetFloat("EFFECT_BILLBOARD") > 0.5f)));
    }
}
