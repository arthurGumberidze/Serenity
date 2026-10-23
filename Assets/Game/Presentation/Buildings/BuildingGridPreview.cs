using System;
using System.Collections.Generic;
using Game.Domain.Buildings;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Presentation.Buildings
{
    public enum BuildingPlacementVisualState
    {
        Free = 0,
        RequiresClearance = 1,
        Blocked = 2
    }

    /// <summary>One combined disposable mesh for the local footprint grid; never creates one object per cell.</summary>
    [DisallowMultipleComponent]
    public sealed class BuildingGridPreview : MonoBehaviour
    {
        private Mesh mesh;
        private Material material;
        private Func<GridCoordinate, Vector3> cellToWorld;
        private float cellSize;

        public void Initialize(float size, Func<GridCoordinate, Vector3> projector)
        {
            if (size <= 0f) throw new ArgumentOutOfRangeException(nameof(size));
            cellSize = size;
            cellToWorld = projector ?? throw new ArgumentNullException(nameof(projector));
            mesh = new Mesh { name = "Building footprint grid" };
            mesh.MarkDynamic();
            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            material = new Material(shader) { name = "Building footprint grid (runtime)" };
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        public void Render(IReadOnlyList<GridCoordinate> cells, BuildingPlacementVisualState state)
        {
            if (mesh == null || cellToWorld == null || cells == null || cells.Count == 0)
            {
                mesh?.Clear();
                return;
            }
            var color = state == BuildingPlacementVisualState.Free
                ? new Color(0.25f, 0.82f, 0.58f, 0.48f)
                : state == BuildingPlacementVisualState.RequiresClearance
                    ? new Color(0.95f, 0.62f, 0.18f, 0.55f)
                    : new Color(0.88f, 0.24f, 0.28f, 0.52f);
            var vertices = new Vector3[cells.Count * 4];
            var colors = new Color[cells.Count * 4];
            var triangles = new int[cells.Count * 6];
            var half = cellSize * 0.46f;
            for (var index = 0; index < cells.Count; index++)
            {
                var center = cellToWorld(cells[index]);
                center.y += 0.055f + index * 0.00002f;
                var vertex = index * 4;
                vertices[vertex] = center + new Vector3(-half, 0f, -half);
                vertices[vertex + 1] = center + new Vector3(-half, 0f, half);
                vertices[vertex + 2] = center + new Vector3(half, 0f, half);
                vertices[vertex + 3] = center + new Vector3(half, 0f, -half);
                colors[vertex] = colors[vertex + 1] = colors[vertex + 2] = colors[vertex + 3] = color;
                var triangle = index * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 1;
                triangles[triangle + 2] = vertex + 2;
                triangles[triangle + 3] = vertex;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }
    }
}
