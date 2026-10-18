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

        public void Initialize(IEnumerable<GameObject> values)
        {
            trees = (values ?? throw new ArgumentNullException(nameof(values))).Where(x => x != null).Distinct().ToArray();
            LodGroupCount = 0;
            ReplacedBillboardLodCount = 0;
            foreach (var tree in trees)
                foreach (var group in tree.GetComponentsInChildren<LODGroup>(true)) Configure(group);
        }

        private void Configure(LODGroup group)
        {
            LodGroupCount++;
            group.fadeMode = LODFadeMode.CrossFade;
            group.animateCrossFading = true;
            var lods = group.GetLODs();
            Renderer[] previousMeshRenderers = Array.Empty<Renderer>();
            for (var index = 0; index < lods.Length; index++)
            {
                lods[index].fadeTransitionWidth = 0.35f;
                var meshRenderers = lods[index].renderers.Where(renderer => renderer != null &&
                    !(renderer is BillboardRenderer)).ToArray();
                if (meshRenderers.Length == 0 && previousMeshRenderers.Length > 0)
                {
                    lods[index].renderers = previousMeshRenderers;
                    ReplacedBillboardLodCount++;
                }
                else if (meshRenderers.Length > 0)
                {
                    lods[index].renderers = meshRenderers;
                    previousMeshRenderers = meshRenderers;
                }
            }
            if (lods.Length > 0 && lods[lods.Length - 1].screenRelativeTransitionHeight < MinimumLastLodHeight)
                lods[lods.Length - 1].screenRelativeTransitionHeight = MinimumLastLodHeight;
            group.SetLODs(lods);
            group.RecalculateBounds();
            foreach (var billboard in group.GetComponentsInChildren<BillboardRenderer>(true)) billboard.enabled = false;
        }
    }
}
