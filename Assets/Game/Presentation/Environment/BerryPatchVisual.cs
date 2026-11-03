using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.Environment
{
    /// <summary>Shared low-poly fruit clusters; resource amount/depletion remains on the source.</summary>
    public static class BerryPatchVisual
    {
        private static Mesh fruitMesh;
        private static Material[] materials;
        public static void Decorate(GameObject bush, int variant)
        {
            var renderers = bush.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var scale = Mathf.Clamp(2.3f / Mathf.Max(.1f, bounds.size.x, bounds.size.z), .8f, 4f);
            bush.transform.localScale *= scale;
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            EnsureAssets();
            var fruit = new GameObject("Ripe berry clusters");
            fruit.transform.SetParent(bush.transform, false);
            fruit.transform.position = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * .58f, bounds.center.z);
            var parentScale = bush.transform.lossyScale;
            fruit.transform.localScale = new Vector3(bounds.size.x / 2.3f / parentScale.x,
                Mathf.Max(.8f,bounds.size.y) / parentScale.y, bounds.size.z / 2.3f / parentScale.z);
            fruit.AddComponent<MeshFilter>().sharedMesh = fruitMesh;
            var meshRenderer = fruit.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = materials[Mathf.Abs(variant) % materials.Length];
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void EnsureAssets()
        {
            if (fruitMesh != null) return;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            var corners = new[] { Vector3.up, Vector3.right, Vector3.forward, Vector3.left, Vector3.back, Vector3.down };
            var faces = new[] { 0,2,1, 0,3,2, 0,4,3, 0,1,4, 5,1,2, 5,2,3, 5,3,4, 5,4,1 };
            for (var i = 0; i < 28; i++)
            {
                var angle = i * 137.5f * Mathf.Deg2Rad;
                var radius = .48f + (i % 4) * .13f;
                var center = new Vector3(Mathf.Cos(angle) * radius, (i % 5) * .085f, Mathf.Sin(angle) * radius);
                var first = vertices.Count;
                foreach (var corner in corners) vertices.Add(center + corner * (.055f + (i % 3) * .012f));
                foreach (var index in faces) triangles.Add(first + index);
            }
            fruitMesh = new Mesh { name = "Shared ripe berries" };
            fruitMesh.SetVertices(vertices); fruitMesh.SetTriangles(triangles, 0); fruitMesh.RecalculateNormals();
            fruitMesh.UploadMeshData(true);
            var colors = new[] { new Color(.8f,.055f,.10f), new Color(.22f,.12f,.48f), new Color(.93f,.39f,.06f) };
            materials = new Material[colors.Length];
            for (var i = 0; i < colors.Length; i++)
            {
                materials[i] = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Berry variety " + i, enableInstancing = true };
                materials[i].SetColor("_BaseColor", colors[i]); materials[i].SetFloat("_Smoothness", .25f);
            }
        }
    }
}
