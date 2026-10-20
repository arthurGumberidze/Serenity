using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Presentation.Environment
{
    public static class StarterNatureProjection
    {
        public static GameObject[] CreatePondGrove(Transform parent, Vector3 center, Vector2 radii,
            StarterNatureCatalog catalog, out GameObject[] shoreProps)
        {
            var trees = new List<GameObject>();
            var props = new List<GameObject>();
            for (var i = 0; i < 20; i++)
            {
                // Leave the south-west approach from camp open.
                if (i > 10 && i < 19) continue;
                var angle = i * Mathf.PI * 0.1f;
                var tree = new GameObject("Starter pond resource tree " + i);
                tree.transform.SetParent(parent, false);
                tree.transform.position = center + new Vector3(Mathf.Cos(angle) * radii.x * 1.35f, 0,
                    Mathf.Sin(angle) * radii.y * 1.35f);
                tree.transform.rotation = Quaternion.Euler(0, i * 71f, 0);
                trees.Add(tree);
            }
            if (catalog.ShoreProps != null && catalog.ShoreProps.Length > 0)
            for (var i = 0; i < 72; i++)
            {
                var angle = i * Mathf.PI / 36f;
                var factor = 1.035f + (i % 5) * 0.012f;
                var prop = Object.Instantiate(catalog.ShoreProps[i % catalog.ShoreProps.Length], parent);
                prop.name = "Starter shore ground detail " + i;
                prop.transform.position = center + new Vector3(Mathf.Cos(angle) * radii.x * factor, 0,
                    Mathf.Sin(angle) * radii.y * factor);
                prop.transform.rotation = Quaternion.Euler(0, i * 137f, 0);
                var meshes = prop.GetComponentsInChildren<MeshRenderer>();
                var bounds = meshes[0].bounds;
                foreach (var mesh in meshes) bounds.Encapsulate(mesh.bounds);
                prop.transform.localScale *= (0.65f + (i % 4) * 0.12f) / Mathf.Max(0.01f, bounds.size.y);
                props.Add(prop);
            }
            shoreProps = props.ToArray();
            return trees.ToArray();
        }

        public static void Apply(IEnumerable<GameObject> trees, StarterNatureCatalog catalog)
        {
            if (catalog == null || catalog.Trees == null || catalog.Trees.Length == 0) return;
            var index = 0;
            foreach (var tree in trees.Where(x => x != null).Distinct())
            {
                if (tree.transform.Find("Synty playable tree") != null) continue;
                // Retain the authored root/ID. Retire every legacy visual/physics part before
                // adding the replacement: no overlapping crowns or stale canopy colliders.
                foreach (var group in tree.GetComponentsInChildren<LODGroup>(true))
                { group.SetLODs(System.Array.Empty<LOD>()); group.enabled = false; }
                foreach (var renderer in tree.GetComponentsInChildren<Renderer>(true))
                { renderer.enabled = false; renderer.sharedMaterials = System.Array.Empty<Material>(); }
                foreach (var collider in tree.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                var visual = Object.Instantiate(catalog.Trees[index % catalog.Trees.Length], tree.transform);
                visual.name = "Synty playable tree";
                // Historical roots carry arbitrary scaling. Normalize the replacement in world units.
                var parentScale = tree.transform.lossyScale;
                visual.transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
                var renderers = visual.GetComponentsInChildren<MeshRenderer>();
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                var height = Mathf.Lerp(catalog.MinimumTreeHeight, catalog.MaximumTreeHeight, (index % 7) / 6f);
                visual.transform.localScale *= height / Mathf.Max(0.01f, bounds.size.y);
                bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                visual.transform.position += Vector3.up * (tree.transform.position.y - bounds.min.y);
                // Only the trunk obstructs walking. The presenter adds a separate selection trigger.
                var trunk = visual.AddComponent<CapsuleCollider>();
                trunk.radius = 0.45f / Mathf.Abs(visual.transform.lossyScale.x);
                trunk.height = 3f / Mathf.Abs(visual.transform.lossyScale.y);
                trunk.center = Vector3.up * trunk.height * 0.5f;
                index++;
            }
        }
    }
}
