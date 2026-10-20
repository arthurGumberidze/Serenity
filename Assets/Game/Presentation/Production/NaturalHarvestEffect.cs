using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Presentation.Production
{
    /// <summary>Short-lived mesh-only effect. Never carries colliders, sources, inventory or IDs.</summary>
    public sealed class NaturalHarvestEffect : MonoBehaviour
    {
        public static void Play(GameObject source, bool tree)
        {
            var root = new GameObject(tree ? "Falling harvested tree" : "Breaking harvested rock");
            root.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            var renderers = new HashSet<Renderer>(source.GetComponentsInChildren<MeshRenderer>(true));
            foreach (var group in source.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = group.GetLODs();
                foreach (var lod in lods)
                foreach (var renderer in lod.renderers) renderers.Remove(renderer);
                if (lods.Length > 0)
                    foreach (var renderer in lods[0].renderers) if (renderer != null) renderers.Add(renderer);
            }
            foreach (var renderer in renderers.Where(x => x != null && x.enabled))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var copy = new GameObject("Harvest visual");
                copy.transform.SetParent(root.transform, false);
                copy.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                copy.transform.localScale = renderer.transform.lossyScale;
                copy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                copy.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            }
            root.AddComponent<NaturalHarvestEffect>().StartCoroutine(Animate(root, tree));
        }

        private static IEnumerator Animate(GameObject root, bool tree)
        {
            var rotation = root.transform.rotation;
            var origin = root.transform.position;
            var elapsed = 0f;
            const float duration = 2.8f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                if (tree)
                    root.transform.rotation = Quaternion.AngleAxis(86f * t * t, Vector3.right) * rotation;
                else
                {
                    root.transform.localScale = Vector3.one * (1f - t * 0.8f);
                    root.transform.position = origin - Vector3.up * t * 0.6f;
                }
                yield return null;
            }
            // Hold the fallen silhouette briefly so completion is readable, then clear the disposable view.
            yield return new WaitForSeconds(4f);
            Destroy(root);
        }
    }
}
