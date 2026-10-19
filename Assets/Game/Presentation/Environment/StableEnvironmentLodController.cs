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

        public int TreeCount => trees.Length;
        public int LodGroupCount { get; private set; }
        public int ReplacedBillboardLodCount { get; private set; }

        public void Initialize(IEnumerable<GameObject> values, Func<Vector3, float> groundHeight = null)
        {
            trees = (values ?? throw new ArgumentNullException(nameof(values))).Where(x => x != null).Distinct().ToArray();
            LodGroupCount = 0;
            ReplacedBillboardLodCount = 0;
            foreach (var tree in trees)
            {
                // Authored trees belonged to the retired 512 m Terrain. Keep their X/Z and
                // visual rotation, but anchor the root to the active tiled-world height.
                if (groundHeight != null)
                {
                    var position = tree.transform.position;
                    position.y = groundHeight(position);
                    tree.transform.position = position;
                }
                foreach (var group in tree.GetComponentsInChildren<LODGroup>(true)) Configure(group);
            }
        }

        public void RefreshVisibility(Func<Vector3, bool> isGroundRendered)
        {
            if (isGroundRendered == null) throw new ArgumentNullException(nameof(isGroundRendered));
            foreach (var tree in trees)
            {
                if (tree == null) continue;
                var visible = isGroundRendered(tree.transform.position);
                if (tree.activeSelf != visible) tree.SetActive(visible);
            }
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
